using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Convert.Core
{
    public class MkvmergeEngine
    {
        private readonly string _exe;

        public MkvmergeEngine(string exePath)
        {
            _exe = exePath;
        }

        // CHANGEMENT : on renvoie (args, finalOutputPath) au lieu de stocker dans des propriétés
        public (string Arguments, string FinalOutputPath) BuildCommand(string inputPath, string originalFileName)
        {
            var dir = Path.GetDirectoryName(inputPath);
            var name = Path.GetFileNameWithoutExtension(inputPath);
            var finalOutputPath = Path.Combine(dir, $"{name}_remuxed.mkv");

            var args = $"-o \"{finalOutputPath}\" \"{inputPath}\"";
            return (args, finalOutputPath);
        }

        public async Task<int> ExecuteAsync(
            string args,
            Action<string> log,
            Action<double>? onProgress,
            CancellationToken token,
            Action<Process>? onProcessCreated,
            bool dumpDebug,
            string originalFileName)
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
            fullLog.AppendLine("=======================");
            fullLog.AppendLine("=== MKVMERGE COMMAND ===");
            fullLog.AppendLine("=======================");
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
                    Path.GetFileNameWithoutExtension(originalFileName)
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

            var m1 = Regex.Match(line, @"Progression :\s*(\d+)%");
            if (m1.Success)
            {
                double p = double.Parse(m1.Groups[1].Value) / 100.0;
                onProgress(p);
                return;
            }

            var m2 = Regex.Match(line, @"progression :\s*(\d+)%", RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                double p = double.Parse(m2.Groups[1].Value) / 100.0;
                onProgress(p);
            }
        }
    }
}
