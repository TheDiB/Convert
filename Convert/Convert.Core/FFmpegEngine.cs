namespace Convert.Core
{
    using Convert.Models;
    using System.Diagnostics;
    using System.Text;

    public class FFmpegEngine
    {
        private readonly string _ffmpegPath;

        public FFmpegEngine(string ffmpegPath)
        {
            _ffmpegPath = ffmpegPath;
        }

        public async Task<string> BuildCommandAsync(FileAnalysisResult analysis, TranscodeOptions options)
        {
            var sb = new StringBuilder();

            sb.Append($"-i \"{analysis.FilePath}\" ");

            //
            // --- VIDÉO ---
            //
            sb.Append("-map 0:v ");

            if (options.VideoCodec == "copy")
                sb.Append("-c:v copy ");
            else
                sb.Append($"-c:v {options.VideoCodec} ");

            //
            // --- SOUS-TITRES ---
            //
            sb.Append("-map 0:s? ");

            if (options.ConvertMovTextToSrt)
                sb.Append("-c:s srt ");
            else
                sb.Append("-c:s copy ");

            //
            // --- AUDIO ---
            //
            foreach (var audio in analysis.AudioStreams)
            {
                sb.Append($"-map 0:{audio.Index} ");

                bool isDts = audio.Codec.StartsWith("dts", StringComparison.OrdinalIgnoreCase);

                // 1) DTS → EAC3 si activé
                if (options.ConvertDtsToEac3 && isDts)
                {
                    sb.Append($"-c:a:{audio.Index} eac3 -b:a:{audio.Index} {options.AudioBitrateKbps}k ");
                    continue;
                }

                // 2) Codec audio choisi par l'utilisateur
                if (options.AudioCodec != "copy")
                {
                    sb.Append($"-c:a:{audio.Index} {options.AudioCodec} ");

                    // Bitrate uniquement si codec ≠ copy
                    if (options.AudioCodec != "copy")
                        sb.Append($"-b:a:{audio.Index} {options.AudioBitrateKbps}k ");

                    continue;
                }

                // 3) Sinon copie
                sb.Append($"-c:a:{audio.Index} copy ");
            }

            //
            // --- SORTIE ---
            //
            string output = System.IO.Path.ChangeExtension(
                analysis.FilePath,
                $"_reencoded.{options.OutputContainer}"
            );

            sb.Append($"\"{output}\"");

            return sb.ToString();
        }

        public async Task<int> ExecuteAsync(string arguments, Action<string> log, Action<string> onProgressLine, CancellationToken token)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
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
                    log(e.Data);
                    onProgressLine(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    log(e.Data);
                    onProgressLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
    }
}
