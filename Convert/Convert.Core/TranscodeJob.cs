using Convert.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Convert.Core
{
    public class TranscodeJob
    {
        public string InputPath { get; }
        public string OutputPath { get; private set; }
        public double Progress { get; private set; }
        public Process? FFprobeProcess { get; private set; }
        public Process? FFmpegProcess { get; private set; }
        public FileAnalysisResult? Analysis { get; private set; }

        public enum JobMode
        {
            AnalyzeOnly,
            Transcode
        }
        public JobMode Mode { get; set; }
        public event Action ProgressChanged;
        public CancellationTokenSource Cts { get; set; } = new CancellationTokenSource();

        public TranscodeJob(string inputPath)
        {
            InputPath = inputPath;
        }

        public event Action StatusChanged;

        private string _status = "Pending";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                StatusChanged?.Invoke();
            }
        }

        public async Task RunAsync(
            FFprobeService probe,
            FFmpegEngine engine,
            TranscodeOptions options,
            Action<string> log,
            CancellationToken globalToken,
            Func<bool> isStopAllRequested)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken, this.Cts.Token);
            var token = linkedCts.Token;

            try
            {
                Status = "Analyzing";
                Analysis = await probe.AnalyzeAsync(InputPath, token, p => FFprobeProcess = p);

                if (Mode == JobMode.AnalyzeOnly)
                {
                    DumpAnalysisToLog(Analysis, log);
                    Status = "Analyzed";
                    return;
                }

                Status = "Building command";
                string args = await engine.BuildCommandAsync(Analysis, options);

                Status = "Transcoding";
                int code = await engine.ExecuteAsync(args, log, line => ParseProgress(line, Analysis.DurationSeconds), token, p => FFmpegProcess = p);

                Status = code == 0 ? "Done" : (isStopAllRequested() ? "Canceled" : "Failed");
            }
            catch (OperationCanceledException)
            {
                Status = isStopAllRequested() ? "Canceled" : "Failed";
            }
            catch (Exception ex)
            {
                Status = isStopAllRequested() ? "Canceled" : "Error";
                log($"ERROR: {ex.Message}");
            }
        }

        private void ParseProgress(string line, double totalDuration)
        {
            if (!line.Contains("time="))
                return;

            var match = Regex.Match(line, @"time=(\d+):(\d+):(\d+(?:[.,]\d+)?)");
            if (!match.Success)
                return;

            double h = double.Parse(match.Groups[1].Value);
            double m = double.Parse(match.Groups[2].Value);
            double s = double.Parse(match.Groups[3].Value.Replace(',', '.'), CultureInfo.InvariantCulture);

            double current = h * 3600 + m * 60 + s;

            if (totalDuration > 0)
            {
                Progress = current / totalDuration;
                ProgressChanged?.Invoke();
            }
        }

        private void DumpAnalysisToLog(FileAnalysisResult analysis, Action<string> log)
        {
            log($"=== ANALYSE DU FICHIER ===");
            log($"Fichier : {analysis.FilePath}");
            log($"Durée   : {analysis.DurationSeconds:F2} sec");

            log("");
            log("=== PISTES AUDIO ===");
            foreach (var a in analysis.AudioStreams)
                log($"Audio #{a.Index} : {a.Codec}");

            log("");
            log("=== SOUS-TITRES ===");
            foreach (var s in analysis.SubtitleStreams)
                log($"Sub #{s.Index} : {s.Codec}");

            log("==============================");
        }

        public void Stop()
        {
            try
            {
                Cts?.Cancel();
            }
            catch { }
        }

        public void SetPending()
        {
            Status = "Pending";
        }

        public void Cancel()
        {
            try
            {
                Cts?.Cancel();

                FFprobeProcess?.Kill(true);
                FFmpegProcess?.Kill(true);
            }
            catch { }

            Status = "Canceled";
        }

    }
}
