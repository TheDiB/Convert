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
        public Process? MkvmergeProcess { get; private set; }

        public FileAnalysisResult? Analysis { get; private set; }
        public bool AnalysisEventFired { get; private set; }

        public enum JobMode
        {
            AnalyzeOnly,
            Transcode
        }
        public JobMode Mode { get; set; }
        public event Action ProgressChanged;
        public CancellationTokenSource Cts { get; set; } = new CancellationTokenSource();

        public event Action<FileAnalysisResult>? AnalysisCompleted;

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

        public async Task<JobResult> RunAsync(
                FFprobeService probe,
                FFmpegEngine engine,
                MkvmergeEngine mkvmergeEngine,
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

                if (options.DumpDebugFiles)
                {
                    var reportDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Convert",
                        "Reports",
                        Path.GetFileNameWithoutExtension(InputPath)
                    );

                    Directory.CreateDirectory(reportDir);

                    File.WriteAllText(Path.Combine(reportDir, "analysis.json"), Analysis.RawJson);
                }

                if (Mode == JobMode.AnalyzeOnly)
                {
                    DumpAnalysisToLog(Analysis, log);
                    Status = "Analyzed";
                    return JobResult.Success;
                }

                Status = "Building command";

                var (args, ffmpegOutputPath) = await engine.BuildCommandAsync(Analysis, options);

                Status = "Transcoding";

                Action<string> ffmpegLogger = null;

                int code = await engine.ExecuteAsync(
                    args,
                    line =>
                    {
                        if (ffmpegLogger == null && FFmpegProcess != null)
                            ffmpegLogger = WrapLogger(log, FFmpegProcess.Id);

                        ffmpegLogger?.Invoke(line);
                    },
                    line => ParseProgress(line, Analysis.DurationSeconds),
                    token,
                    p => FFmpegProcess = p,
                    options.DumpDebugFiles,
                    InputPath);

                // Chemin de sortie propre à CE job
                OutputPath = ffmpegOutputPath;

                if (this.Cts.IsCancellationRequested || isStopAllRequested())
                {
                    Status = "Canceled";
                    return JobResult.Canceled;
                }

                if (code == 0)
                {
                    if (options.CompatibilityMode)
                    {
                        Status = "Remuxing";

                        Progress = 0;
                        ProgressChanged?.Invoke();

                        // CHANGEMENT : on récupère args + finalOutputPath
                        var (mkvArgs, finalOutputPath) = mkvmergeEngine.BuildCommand(OutputPath, InputPath);

                        Action<string> mkvLogger = null;

                        int mergeCode = await mkvmergeEngine.ExecuteAsync(
                            mkvArgs,
                            line =>
                            {
                                if (mkvLogger == null && MkvmergeProcess != null)
                                    mkvLogger = WrapLogger(log, MkvmergeProcess.Id);

                                mkvLogger?.Invoke(line);
                            },
                            p =>
                            {
                                Progress = p;
                                ProgressChanged?.Invoke();
                            },
                            token,
                            p => MkvmergeProcess = p,
                            options.DumpDebugFiles,
                            InputPath);

                        if (mergeCode != 0)
                        {
                            Status = "Failed";
                            return JobResult.Failed;
                        }

                        string original = OutputPath;       // movie_reencoded.mkv
                        string fixedFile = finalOutputPath; // movie_reencoded_remuxed.mkv

                        if (!File.Exists(fixedFile))
                        {
                            log($"ERROR: MKVMerge output file not found: {fixedFile}");
                            Status = "Failed";
                            return JobResult.Failed;
                        }

                        string backup = original + ".bak";

                        try
                        {
                            if (File.Exists(backup))
                                File.Delete(backup);

                            if (File.Exists(original))
                                File.Move(original, backup);
                        }
                        catch (Exception ex)
                        {
                            log($"ERROR: Unable to backup original file: {ex.Message}");
                            Status = "Failed";
                            return JobResult.Failed;
                        }

                        try
                        {
                            File.Move(fixedFile, original);
                        }
                        catch (Exception ex)
                        {
                            log($"ERROR: Unable to replace original file: {ex.Message}");
                            Status = "Failed";

                            if (File.Exists(backup))
                                File.Move(backup, original);

                            return JobResult.Failed;
                        }

                        try
                        {
                            if (File.Exists(backup))
                                File.Delete(backup);
                        }
                        catch { }

                        OutputPath = original;
                    }

                    Status = "Done";
                    return JobResult.Success;
                }

                if (code < 0)
                {
                    Status = "Canceled";
                    return JobResult.Canceled;
                }

                Status = "Failed";
                return JobResult.Failed;
            }
            catch (OperationCanceledException)
            {
                Status = "Canceled";
                return JobResult.Canceled;
            }
            catch (Exception ex)
            {
                Status = "Error";
                log($"ERROR: {ex.Message}");
                return JobResult.Error;
            }
        }

        public void ResetForRetry()
        {
            Status = "Pending";
            Progress = 0;
            Mode = JobMode.Transcode;

            // Reset du CTS
            Cts.Cancel();
            Cts.Dispose();
            Cts = new CancellationTokenSource();
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
            log("=== ANALYSE DU FICHIER ===");
            log($"Fichier : {analysis.FilePath}");
            log($"Durée   : {analysis.DurationSeconds:F2} sec");
            log("");

            //
            // --- VIDEO ---
            //
            log("=== PISTES VIDEO ===");
            if (analysis.VideoStreams.Count == 0)
            {
                log("Aucune piste vidéo détectée");
            }
            else
            {
                foreach (var v in analysis.VideoStreams)
                {
                    string resolution = (v.Width > 0 && v.Height > 0)
                        ? $"{v.Width}x{v.Height}"
                        : "résolution inconnue";

                    string fps = v.FPS > 0
                        ? $"{v.FPS:0.###} fps"
                        : "fps inconnu";

                    string bitrate = v.Bitrate > 0
                        ? $"{v.Bitrate} kbps"
                        : "bitrate inconnu";

                    log($"Video #{v.Index} : {v.Codec}  {resolution}  {fps}  {bitrate}");
                }
            }

            log("");

            //
            // --- AUDIO ---
            //
            log("=== PISTES AUDIO ===");
            foreach (var a in analysis.AudioStreams)
                log($"Audio #{a.Index} : {a.Codec}");

            log("");

            //
            // --- SOUS-TITRES ---
            //
            log("=== SOUS TITRES ===");
            foreach (var s in analysis.SubtitleStreams)
                log($"Sub #{s.Index} : {s.Codec}");

            log("==============================");
        }

        private Action<string> WrapLogger(Action<string> originalLog, int pid)
        {
            return line =>
            {
                if (string.IsNullOrWhiteSpace(line))
                    return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                originalLog($"[{timestamp}] [PID {pid}] {line}");
            };
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
