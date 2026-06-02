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

            #region VIDEO
            //
            // --- VIDÉO ---
            //

            int outVideoIndex = 0;
            foreach (var video in analysis.VideoStreams)
            {
                // Profil choisi ?
                if (!options.VideoTrackProfiles.TryGetValue(video.Index, out var profile))
                    profile = VideoProfile.Copy;

                // IGNORE → ne pas mapper cette piste
                if (profile == VideoProfile.Ignore)
                    continue;

                // Mapper la piste vidéo
                sb.Append($"-map 0:{video.Index} ");

                // Logique par profil
                switch (profile)
                {
                    case VideoProfile.Copy:
                        sb.Append($"-c:v:{outVideoIndex} copy ");
                        break;

                    case VideoProfile.H265_High:
                        sb.Append($"-c:v:{outVideoIndex} libx265 -preset slow -crf 18 ");
                        break;

                    case VideoProfile.H264_Medium:
                        sb.Append($"-c:v:{outVideoIndex} libx264 -preset medium -crf 20 ");
                        break;

                    case VideoProfile.H264_Low:
                        sb.Append($"-c:v:{outVideoIndex} libx264 -preset fast -crf 23 ");
                        break;

                    case VideoProfile.Ignore:
                        continue;
                }

                outVideoIndex++;
            }

            // Si aucune piste vidéo n’a été mappée → désactiver la vidéo
            if (outVideoIndex == 0)
            {
                sb.Append("-vn ");
            }
            #endregion

            #region SOUS-TITRES
            //
            // --- SOUS-TITRES ---
            //
            int subtitleIndex = 0;
            foreach (var sub in analysis.SubtitleStreams)
            {
                // Récupération du profil choisi par l'utilisateur
                if (!options.SubtitleTrackProfiles.TryGetValue(sub.Index, out var profile))
                    profile = SubtitleProfile.Copy; // fallback par défaut

                switch (profile)
                {
                    case SubtitleProfile.Ignore:
                        // On ne mappe pas cette piste
                        continue;

                    case SubtitleProfile.Copy:
                        sb.Append($"-map 0:{sub.Index} ");
                        sb.Append($"-c:s:{subtitleIndex} copy ");
                        break;

                    case SubtitleProfile.ConvertToSrt:
                        sb.Append($"-map 0:{sub.Index} ");
                        sb.Append($"-c:s:{subtitleIndex} srt ");
                        break;

                    default:
                        // Pour l'instant, on ignore Extract (géré plus tard)
                        continue;
                }

                subtitleIndex++;
            }
            #endregion

            #region AUDIO
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
                bool isDts = audio.Codec.Equals("dts", StringComparison.OrdinalIgnoreCase);
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

                    case AudioProfile.Aac_7_1:
                        {
                            sb.Append($"-c:a:{outAudioIndex} aac ");
                            sb.Append($"-b:a:{outAudioIndex} 896k ");
                            sb.Append($"-ac:{outAudioIndex} 8 ");
                            sb.Append($"-channel_layout:{outAudioIndex} 7.1 ");
                            sb.Append($"-filter:a:{outAudioIndex} \"pan=7.1\" ");
                            WriteStatisticsTags(sb, outAudioIndex, 896, analysis.DurationSeconds);
                            sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} AAC 7.1\" ");
                            break;
                        }

                    case AudioProfile.Eac3_5_1:
                        if (!isEac3)
                        {
                            sb.Append($"-c:a:{outAudioIndex} eac3 ");
                            sb.Append($"-b:a:{outAudioIndex} 1536k ");
                            sb.Append($"-ac:{outAudioIndex} 6 ");
                            sb.Append($"-channel_layout:{outAudioIndex} 5.1 ");
                            WriteStatisticsTags(sb, outAudioIndex, 1536, analysis.DurationSeconds);
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
                            WriteStatisticsTags(sb, outAudioIndex, 640, analysis.DurationSeconds);
                            sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} AC3 5.1\" ");
                        }
                        else
                        {
                            sb.Append($"-c:a:{outAudioIndex} copy ");
                        }
                        break;

                    case AudioProfile.Aac_5_1:
                        {
                            sb.Append($"-c:a:{outAudioIndex} aac ");
                            sb.Append($"-b:a:{outAudioIndex} 512k ");
                            sb.Append($"-ac:{outAudioIndex} 6 ");
                            sb.Append($"-channel_layout:{outAudioIndex} 5.1 ");
                            sb.Append($"-filter:a:{outAudioIndex} \"pan=5.1\" ");
                            WriteStatisticsTags(sb, outAudioIndex, 512, analysis.DurationSeconds);
                            sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} AAC 5.1\" ");
                            break;
                        }


                    case AudioProfile.Ac3_2_0:
                        sb.Append($"-c:a:{outAudioIndex} ac3 ");
                        sb.Append($"-b:a:{outAudioIndex} 192k ");
                        sb.Append($"-ac:{outAudioIndex} 2 ");
                        sb.Append($"-channel_layout:{outAudioIndex} stereo ");
                        sb.Append($"-af:a:{outAudioIndex} pan=stereo ");
                        WriteStatisticsTags(sb, outAudioIndex, 192, analysis.DurationSeconds);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} AC3 2.0\" ");
                        break;

                    case AudioProfile.Mp3_2_0:
                        sb.Append($"-c:a:{outAudioIndex} mp3 ");
                        sb.Append($"-b:a:{outAudioIndex} 192k ");
                        sb.Append($"-ac:{outAudioIndex} 2 ");
                        sb.Append($"-channel_layout:{outAudioIndex} stereo ");
                        WriteStatisticsTags(sb, outAudioIndex, 192, analysis.DurationSeconds);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} MP3 2.0\" ");
                        break;

                    case AudioProfile.Aac_2_0:
                        sb.Append($"-c:a:{outAudioIndex} aac ");
                        sb.Append($"-b:a:{outAudioIndex} 320k ");
                        sb.Append($"-ac:{outAudioIndex} 2 ");
                        sb.Append($"-af:a:{outAudioIndex} pan=stereo ");
                        WriteStatisticsTags(sb, outAudioIndex, 320, analysis.DurationSeconds);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{langName} AAC 2.0\" ");
                        break;
                }

                outAudioIndex++;
            }
            #endregion

            //
            // --- SORTIE ---
            //
            var dir = Path.GetDirectoryName(analysis.FilePath);
            var name = Path.GetFileNameWithoutExtension(analysis.FilePath);
            var ext = !string.IsNullOrEmpty(options.Container) ? '.' + options.Container.ToLower() : Path.GetExtension(analysis.FilePath);
            var id = AppPaths.GenerateShortId();
            string OutputPath = Path.Combine(dir, $"{name}_{id}{ext}");

            sb.Append($"\"{OutputPath}\"");
            return sb.ToString();
        }

        public async Task<int> ExecuteAsync(string arguments, Action<string> log, Action<string> onProgressLine, CancellationToken token, Action<Process>? onProcessCreated, bool dumpDebug, string inputPath)
        {
            var fullLog = new StringBuilder();
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
                    fullLog.AppendLine(e.Data);
                    log(e.Data);
                    onProgressLine(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    fullLog.AppendLine(e.Data);
                    log(e.Data);
                    onProgressLine(e.Data);
                }
            };

            onProcessCreated?.Invoke(process);   // ← LE POINT CLÉ
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (dumpDebug)
            {
                var reportDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Convert",
                    "Reports",
                    Path.GetFileNameWithoutExtension(inputPath)
                );

                Directory.CreateDirectory(reportDir);

                File.WriteAllText(Path.Combine(reportDir, "ffmpeg.log"), fullLog.ToString());
            }

            return process.ExitCode;
        }

        private void ClearStatisticsTags(StringBuilder sb, int index)
        {
            sb.Append($"-metadata:s:a:{index} BPS= ");
            sb.Append($"-metadata:s:a:{index} NUMBER_OF_BYTES= ");
            sb.Append($"-metadata:s:a:{index} NUMBER_OF_FRAMES= ");
            sb.Append($"-metadata:s:a:{index} DURATION= ");
        }
        private void WriteStatisticsTags(StringBuilder sb, int index, int bitrateKbps, double durationSeconds)
        {
            long bytes = (long)((bitrateKbps * 1000.0 / 8.0) * durationSeconds);

            sb.Append($"-metadata:s:a:{index} BPS={bitrateKbps * 1000} ");
            sb.Append($"-metadata:s:a:{index} NUMBER_OF_BYTES={bytes} ");
            sb.Append($"-metadata:s:a:{index} DURATION={durationSeconds} ");
            sb.Append($"-metadata:s:a:{index} NUMBER_OF_FRAMES=0 ");
        }
    }
}
