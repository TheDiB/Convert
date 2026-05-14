using Convert.Models;
using System.Diagnostics;
using System.Text;

namespace Convert.Core
{
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

            //if (options.ConvertMovTextToSrt)
            //    sb.Append("-c:s srt ");
            //else
            sb.Append("-c:s copy ");

            //
            // --- AUDIO ---
            //
            int outAudioIndex = 0;
            foreach (var audio in analysis.AudioStreams)
            {
                // Récupérer le profil choisi pour cette piste
                if (!options.AudioTrackProfiles.TryGetValue(audio.Index, out var profile))
                {
                    // Fallback : copy
                    sb.Append($"-map 0:{audio.Index} ");
                    sb.Append($"-c:a:{outAudioIndex} copy ");
                    outAudioIndex++;
                    continue;
                }

                // Profil IGNORE → ne pas mapper
                if (profile == AudioProfile.Ignore)
                    continue;

                // Mapper la piste
                sb.Append($"-map 0:{audio.Index} ");

                // Détection codecs source
                bool isEac3 = audio.Codec.Equals("eac3", StringComparison.OrdinalIgnoreCase);
                bool isAc3 = audio.Codec.Equals("ac3", StringComparison.OrdinalIgnoreCase);
                bool isDtsX =
                    (audio.Profile?.Contains("X", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (audio.CodecTagString?.Contains("DTSX", StringComparison.OrdinalIgnoreCase) ?? false);

                // Langue
                string langCode = audio.Language?.ToLower() ?? "und";
                string langName = AudioLanguageStreamInfo.LanguageMap.ContainsKey(langCode) ? AudioLanguageStreamInfo.LanguageMap[langCode] : langCode;

                // Canaux + layout
                int channels = audio.Channels;
                string layout = audio.ChannelLayout;

                if (audio.Codec.StartsWith("dts") && layout == "5.1(side)")
                    layout = "5.1";

                if (isDtsX)
                {
                    channels = 8;
                    layout = "7.1";
                }

                // Bitrate dynamique
                int bitrate = channels switch
                {
                    1 => 96,
                    2 => 192,
                    6 => 640,
                    8 => 896,
                    _ => 640
                };

                //
                // --- LOGIQUE PAR PROFIL ---
                //
                switch (profile)
                {
                    case AudioProfile.Copy:
                        sb.Append($"-c:a:{outAudioIndex} copy ");
                        break;

                    case AudioProfile.Eac3_5_1:
                        if (!isEac3)
                        {
                            sb.Append($"-c:a:{outAudioIndex} eac3 ");
                            sb.Append($"-b:a:{outAudioIndex} 640k ");
                            sb.Append($"-ac:{outAudioIndex} 6 ");
                            sb.Append($"-channel_layout:{outAudioIndex} 5.1 ");
                            sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} EAC3 5.1\" ");
                        }
                        else
                        {
                            sb.Append($"-c:a:{outAudioIndex} copy ");
                        }
                        break;

                    case AudioProfile.Ac3_5_1:
                        if (!isAc3)
                        {
                            sb.Append($"-c:a:{outAudioIndex} ac3 ");
                            sb.Append($"-b:a:{outAudioIndex} 640k ");
                            sb.Append($"-ac:{outAudioIndex} 6 ");
                            sb.Append($"-channel_layout:{outAudioIndex} 5.1 ");
                            sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} AC3 5.1\" ");
                        }
                        else
                        {
                            sb.Append($"-c:a:{outAudioIndex} copy ");
                        }
                        break;

                    case AudioProfile.Ac3_2_0:
                        sb.Append($"-c:a:{outAudioIndex} ac3 ");
                        sb.Append($"-b:a:{outAudioIndex} 192k ");
                        sb.Append($"-ac:{outAudioIndex} 2 ");
                        sb.Append($"-channel_layout:{outAudioIndex} stereo ");
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} AC3 2.0\" ");
                        break;

                    case AudioProfile.Mp3_2_0:
                        sb.Append($"-c:a:{outAudioIndex} mp3 ");
                        sb.Append($"-b:a:{outAudioIndex} 320k ");
                        sb.Append($"-ac:{outAudioIndex} 2 ");
                        sb.Append($"-channel_layout:{outAudioIndex} stereo ");
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} MP3 2.0\" ");
                        break;
                }

                outAudioIndex++;
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

        public async Task<int> ExecuteAsync(string arguments, Action<string> log, Action<string> onProgressLine, CancellationToken token, Action<Process>? onProcessCreated = null)
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

            onProcessCreated?.Invoke(process);   // ← LE POINT CLÉ
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
    }
}
