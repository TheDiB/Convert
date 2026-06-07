using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Convert.Core
{
    public class MkvmergeEngine
    {
        private readonly string _exe;

        public string FinalOutputPath { get; private set; }
        public string OriginalFileName { get; private set; }

        public MkvmergeEngine(string exePath)
        {
            _exe = exePath;
        }

        public string BuildCommand(string inputPath, string originalFileName)
        {
            var dir = Path.GetDirectoryName(inputPath);
            var name = Path.GetFileNameWithoutExtension(inputPath);
            FinalOutputPath = Path.Combine(dir, $"{name}_remuxed.mkv");
            OriginalFileName = originalFileName;

            return $"-o \"{FinalOutputPath}\" \"{inputPath}\"";
        }

        public async Task<int> ExecuteAsync(
            string args,
            Action<string> log,
            Action<double>? onProgress,
            CancellationToken token,
            Action<Process>? onProcessCreated,
            bool dumpDebug)
        {
            var fullLog = new StringBuilder();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _exe,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch { }
            });

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    fullLog.AppendLine(e.Data);
                    log(e.Data);
                    TryParseProgress(e.Data, onProgress);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    fullLog.AppendLine(e.Data);
                    log(e.Data);
                    TryParseProgress(e.Data, onProgress);
                }
            };

            // --- Dump de la commande MKVMerge ---
            fullLog.AppendLine("=== MKVMERGE COMMAND ===");
            fullLog.AppendLine($"{_exe} {args}");
            fullLog.AppendLine();

            onProcessCreated?.Invoke(process);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(token);

            if (dumpDebug)
            {
                var reportDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Convert",
                    "Reports",
                    Path.GetFileNameWithoutExtension(OriginalFileName)
                );

                Directory.CreateDirectory(reportDir);

                File.AppendAllText(Path.Combine(reportDir, "ffmpeg.log"), fullLog.ToString());
            }

            return process.ExitCode;
        }

        private void TryParseProgress(string line, Action<double>? onProgress)
        {
            if (onProgress == null)
                return;

            // Format 1 : "Progress: 47%"
            var m1 = Regex.Match(line, @"Progression :\s*(\d+)%");
            if (m1.Success)
            {
                double p = double.Parse(m1.Groups[1].Value) / 100.0;
                onProgress(p);
                return;
            }

            // Format 2 : "Muxing in progress: 34%"
            var m2 = Regex.Match(line, @"progression :\s*(\d+)%", RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                double p = double.Parse(m2.Groups[1].Value) / 100.0;
                onProgress(p);
            }
        }

    }
}
