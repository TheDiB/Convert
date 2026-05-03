using Convert.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Convert.Core
{
    public class TranscodeJob
    {
        public string InputPath { get; }
        public string OutputPath { get; private set; }
        public double Progress { get; private set; }
        public FileAnalysisResult? Analysis { get; private set; }

        public enum JobMode
        {
            AnalyzeOnly,
            Transcode
        }

        public JobMode Mode { get; set; }


        public event Action ProgressChanged;
        public CancellationTokenSource Cts { get; private set; }

        public TranscodeJob(string inputPath)
        {
            InputPath = inputPath;
        }

        public event Action StatusChanged;

        private string _status = "Pending";
        public string Status
        {
            get => _status;
            private set
            {
                _status = value;
                StatusChanged?.Invoke();
            }
        }

        public async Task RunAsync(
            FFprobeService probe,
            FFmpegEngine engine,
            TranscodeOptions options,
            Action<string> log)
        {
            try
            {
                Cts = new CancellationTokenSource();

                Status = "Analyzing";
                Analysis = await probe.AnalyzeAsync(InputPath);

                if (Mode == JobMode.AnalyzeOnly)
                {
                    DumpAnalysisToLog(Analysis, log);
                    Status = "Analyzed";
                    return;
                }

                Status = "Building command";
                string args = await engine.BuildCommandAsync(Analysis, options);

                Status = "Transcoding";
                int code = await engine.ExecuteAsync(args, log, line => ParseProgress(line, Analysis.DurationSeconds), Cts.Token);

                Status = code == 0 ? "Done" : "Failed";
            }
            catch (Exception ex)
            {
                Status = "Error";
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
    }
}
