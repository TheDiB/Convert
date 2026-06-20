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

        public async Task<(string Arguments, string OutputPath)> BuildCommandAsync(
            FileAnalysisResult analysis,
            TranscodeOptions options)
        {
            var sb = new StringBuilder();

            sb.Append($"-fflags +genpts -avoid_negative_ts make_zero ");
            sb.Append($"-i \"{analysis.FilePath}\" ");
            sb.Append($"-map_metadata -1 ");
            sb.Append($"-max_muxing_queue_size 9999 ");

            #region VIDEO
            int outVideoIndex = 0;
            foreach (var video in analysis.VideoStreams)
            {
                if (!options.VideoTrackProfiles.TryGetValue(video.Index, out var profile))
                    profile = VideoProfile.Copy;

                if (profile == VideoProfile.Ignore)
                    continue;

                if (!options.VideoTrackHDRModes.TryGetValue(video.Index, out var dvMode))
                    dvMode = HDRProfile.Keep;

                sb.Append($"-map 0:{video.Index} ");

                bool useNvenc = false;
                if (options.EnableGPUEncoding && IsNvencAvailable())
                    useNvenc = true;

                switch (profile)
                {
                    case VideoProfile.Copy:
                        sb.Append($"-c:v:{outVideoIndex} copy ");
                        break;

                    case VideoProfile.H265_High:
                        if (useNvenc)
                        {
                            sb.Append($"-c:v:{outVideoIndex} hevc_nvenc ");
                            sb.Append($"-preset p4 ");
                            sb.Append($"-profile:v:{outVideoIndex} main10 ");
                            sb.Append($"-rc vbr -cq 19 ");
                        }
                        else
                        {
                            sb.Append($"-c:v:{outVideoIndex} libx265 -preset medium -crf 18 ");
                        }
                        break;

                    case VideoProfile.H264_Medium:
                        if (useNvenc)
                        {
                            sb.Append($"-c:v:{outVideoIndex} h264_nvenc -preset slow -cq 20 ");
                        }
                        else
                        {
                            sb.Append($"-c:v:{outVideoIndex} libx264 -preset medium -crf 20 ");
                        }
                        break;

                    case VideoProfile.H264_Low:
                        if (useNvenc)
                        {
                            sb.Append($"-c:v:{outVideoIndex} h264_nvenc -preset fast -cq 25 ");
                        }
                        else
                        {
                            sb.Append($"-c:v:{outVideoIndex} libx264 -preset fast -crf 23 ");
                        }
                        break;

                    case VideoProfile.Ignore:
                        continue;
                }


                if (dvMode == HDRProfile.StripDv || dvMode == HDRProfile.ForceHdr10)
                {
                    // Supprime les métadonnées Dolby Vision (RPU) du flux HEVC
                    //sb.Append($"-bsf:v:{outVideoIndex} hevc_dovi_rpu=remove ");
                    //NB : depuis 2026, plus de filtre DoVi dans FFMpeg, le réencodage avec suppression des tags suffit à le retirer --> rien à faire
                }

                if (dvMode == HDRProfile.ForceSdr)
                {
                    sb.Append($"-vf:v:{outVideoIndex} \"zscale=t=linear:npl=100,tonemap=hable,zscale=t=bt709:m=bt709:r=tv,format=yuv420p\" ");
                }

                outVideoIndex++;
            }

            if (outVideoIndex == 0)
            {
                sb.Append("-vn ");
            }
            #endregion

            #region SOUS-TITRES
            int subtitleIndex = 0;
            foreach (var sub in analysis.SubtitleStreams)
            {
                if (!options.SubtitleTrackProfiles.TryGetValue(sub.Index, out var profile))
                    profile = SubtitleProfile.Copy;

                if (!options.SubtitleTrackLanguages.TryGetValue(sub.Index, out string langCode))
                    langCode = sub.Language?.ToLower() ?? "und";

                string langName = DicoMaps.SubtitleLanguageMap.ContainsKey(langCode)
                    ? DicoMaps.SubtitleLanguageMap[langCode]
                    : langCode;

                string baseTitle, finalTitle = "";

                switch (profile)
                {
                    case SubtitleProfile.Ignore:
                        continue;

                    case SubtitleProfile.Copy:
                        sb.Append($"-map 0:{sub.Index} ");
                        sb.Append($"-c:s:{subtitleIndex} copy ");
                        sb.Append($"-metadata:s:s:{subtitleIndex} language={langCode} ");

                        baseTitle = $"{langName}";
                        finalTitle = BuildFinalTitle(baseTitle, sub.Title);
                        sb.Append($"-metadata:s:s:{subtitleIndex} title=\"{finalTitle}\" ");
                        break;

                    case SubtitleProfile.ConvertToSrt:
                        sb.Append($"-map 0:{sub.Index} ");
                        sb.Append($"-c:s:{subtitleIndex} srt ");
                        sb.Append($"-metadata:s:s:{subtitleIndex} language={langCode} ");

                        baseTitle = $"{langName} SRT";
                        finalTitle = BuildFinalTitle(baseTitle, sub.Title);
                        sb.Append($"-metadata:s:s:{subtitleIndex} title=\"{finalTitle}\" ");
                        break;

                    default:
                        continue;
                }

                subtitleIndex++;
            }
            #endregion

            #region AUDIO
            int outAudioIndex = 0;
            foreach (var audio in analysis.AudioStreams)
            {
                if (!options.AudioTrackProfiles.TryGetValue(audio.Index, out var profile))
                {
                    sb.Append($"-map 0:{audio.Index} ");
                    sb.Append($"-c:a:{outAudioIndex} copy ");
                    outAudioIndex++;
                    continue;
                }

                if (profile == AudioProfile.Ignore)
                    continue;

                sb.Append($"-map 0:{audio.Index} ");

                bool isEac3 = audio.Codec.Equals("eac3", StringComparison.OrdinalIgnoreCase);
                bool isAc3 = audio.Codec.Equals("ac3", StringComparison.OrdinalIgnoreCase);
                bool isDts = audio.Codec.Equals("dts", StringComparison.OrdinalIgnoreCase);
                bool isDtsX =
                    (audio.Profile?.Contains("X", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (audio.CodecTagString?.Contains("DTSX", StringComparison.OrdinalIgnoreCase) ?? false);

                if (!options.AudioTrackLanguages.TryGetValue(audio.Index, out string langCode))
                    langCode = audio.Language?.ToLower() ?? "und";

                string langName = DicoMaps.AudioLanguageMap.ContainsKey(langCode)
                    ? DicoMaps.AudioLanguageMap[langCode]
                    : langCode;

                int channels = audio.Channels;
                string layout = audio.ChannelLayout;

                if (audio.Codec.StartsWith("dts") && layout == "5.1(side)")
                    layout = "5.1";

                if (isDtsX)
                {
                    channels = 8;
                    layout = "7.1";
                }

                int bitrate = channels switch
                {
                    1 => 96,
                    2 => 192,
                    6 => 640,
                    8 => 896,
                    _ => 640
                };

                string baseTitle, finalTitle = "";

                switch (profile)
                {
                    case AudioProfile.Copy:
                        sb.Append($"-c:a:{outAudioIndex} copy ");
                        sb.Append($"-metadata:s:a:{outAudioIndex} language={langCode} ");

                        baseTitle = audio.Title;
                        finalTitle = BuildFinalTitle(baseTitle, audio.Title);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{finalTitle}\" ");
                        break;

                    case AudioProfile.Flac_auto:
                        channels = audio.Channels;
                        layout = channels switch
                        {
                            1 => "mono",
                            2 => "stereo",
                            3 => "2.1",
                            4 => "4.0",
                            5 => "5.0",
                            6 => "5.1",
                            7 => "6.1",
                            8 => "7.1",
                            _ => $"{channels}c"
                        };

                        sb.Append($"-c:a:{outAudioIndex} flac ");
                        sb.Append($"-compression_level:{outAudioIndex} 5 ");
                        sb.Append($"-ac:{outAudioIndex} {channels} ");
                        sb.Append($"-channel_layout:{outAudioIndex} {layout} ");
                        sb.Append($"-metadata:s:a:{outAudioIndex} language={langCode} ");

                        baseTitle = $"{langName} FLAC {layout.ToUpper()}";
                        finalTitle = BuildFinalTitle(baseTitle, audio.Title);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{finalTitle}\" ");
                        break;

                    case AudioProfile.Aac_7_1:
                        sb.Append($"-c:a:{outAudioIndex} aac ");
                        sb.Append($"-b:a:{outAudioIndex} 896k ");
                        sb.Append($"-ac:{outAudioIndex} 8 ");
                        sb.Append($"-channel_layout:{outAudioIndex} 7.1 ");
                        sb.Append($"-filter:a:{outAudioIndex} \"pan=7.1\" ");
                        sb.Append($"-metadata:s:a:{outAudioIndex} language={langCode} ");

                        baseTitle = $"{langName} AAC 7.1 896 Kbps";
                        finalTitle = BuildFinalTitle(baseTitle, audio.Title);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{finalTitle}\" ");
                        break;

                    case AudioProfile.Eac3_5_1:
                        sb.Append($"-c:a:{outAudioIndex} eac3 ");
                        sb.Append($"-b:a:{outAudioIndex} 640k ");
                        sb.Append($"-ac:{outAudioIndex} 6 ");
                        sb.Append($"-metadata:s:a:{outAudioIndex} language={langCode} ");

                        baseTitle = $"{langName} EAC3 5.1 640 Kbps";
                        finalTitle = BuildFinalTitle(baseTitle, audio.Title);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{finalTitle}\" ");
                        break;

                    case AudioProfile.Ac3_5_1:
                        sb.Append($"-c:a:{outAudioIndex} ac3 ");
                        sb.Append($"-b:a:{outAudioIndex} 640k ");
                        sb.Append($"-ac:{outAudioIndex} 6 ");
                        sb.Append($"-channel_layout:{outAudioIndex} 5.1 ");
                        sb.Append($"-metadata:s:a:{outAudioIndex} language={langCode} ");

                        baseTitle = $"{langName} AC3 5.1 640 Kbps";
                        finalTitle = BuildFinalTitle(baseTitle, audio.Title);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{finalTitle}\" ");
                        break;

                    case AudioProfile.Aac_2_0:
                        sb.Append($"-c:a:{outAudioIndex} aac ");
                        sb.Append($"-b:a:{outAudioIndex} 320k ");
                        sb.Append($"-ac:{outAudioIndex} 2 ");
                        sb.Append($"-af:a:{outAudioIndex} pan=stereo ");
                        sb.Append($"-metadata:s:a:{outAudioIndex} language={langCode} ");

                        baseTitle = $"{langName} AAC 2.0 320 Kbps";
                        finalTitle = BuildFinalTitle(baseTitle, audio.Title);
                        sb.Append($"-metadata:s:a:{outAudioIndex} title=\"{finalTitle}\" ");
                        break;
                }

                outAudioIndex++;
            }
            #endregion

            // --- SORTIE ---
            var dir = Path.GetDirectoryName(analysis.FilePath);
            var name = Path.GetFileNameWithoutExtension(analysis.FilePath);
            var ext = !string.IsNullOrEmpty(options.Container)
                ? '.' + options.Container.ToLower()
                : Path.GetExtension(analysis.FilePath);
            var id = AppPaths.GenerateShortId();
            string outputPath = Path.Combine(dir, $"{name}_{id}{ext}");

            sb.Append($"\"{outputPath}\"");

            return (sb.ToString(), outputPath);
        }

        public async Task<int> ExecuteAsync(
            string arguments,
            Action<string> log,
            Action<string> onProgressLine,
            CancellationToken token,
            Action<Process>? onProcessCreated,
            bool dumpDebug,
            string inputPath)
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

            // --- Dump de la commande FFmpeg ---
            fullLog.AppendLine("=====================");
            fullLog.AppendLine("=== FFMPEG COMMAND ===");
            fullLog.AppendLine("=====================");
            fullLog.AppendLine($"{_ffmpegPath} {arguments}");
            fullLog.AppendLine();

            onProcessCreated?.Invoke(process);
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

        private static List<string> ExtractMarkers(string originalTitle)
        {
            if (string.IsNullOrWhiteSpace(originalTitle))
                return new List<string>();

            string lower = originalTitle.ToLower();
            return DicoMaps.MarkerMap
                .Where(kvp => lower.Contains(kvp.Key))
                .Select(kvp => kvp.Value)
                .Distinct()
                .ToList();
        }

        private static string BuildFinalTitle(string baseTitle, string originalTitle)
        {
            var markers = ExtractMarkers(originalTitle);

            if (markers.Count == 0)
                return baseTitle;

            return $"{baseTitle} [{string.Join(", ", markers)}]";
        }

        private bool IsNvencAvailable()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = "-hide_banner -encoders",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains("hevc_nvenc");
            }
            catch
            {
                return false;
            }
        }

    }
}