using Convert.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Convert.Core
{
    public class FFprobeService
    {
        private readonly string _ffprobePath;

        public FFprobeService(string ffprobePath)
        {
            _ffprobePath = ffprobePath;
        }

        public async Task<FileAnalysisResult> AnalyzeAsync(string filePath, CancellationToken token, Action<Process>? onProcessCreated = null)
        {
            var args = $"-v error -show_streams -show_format -of json \"{filePath}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            onProcessCreated?.Invoke(process);
            process.Start();

            token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); }
                catch { }
            });

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(token);

            token.ThrowIfCancellationRequested();

            // Extraction JSON robuste
            int start = output.IndexOf("{");
            int end = output.LastIndexOf("}");

            if (start < 0 || end < 0 || end <= start)
                throw new Exception("Impossible d'extraire un JSON valide depuis ffprobe.");

            string json = output.Substring(start, end - start + 1);

            using var doc = JsonDocument.Parse(json);

            var result = new FileAnalysisResult { FilePath = filePath };

            if (doc.RootElement.TryGetProperty("streams", out var streamsProp))
            {
                foreach (var stream in streamsProp.EnumerateArray())
                {
                    string codec = stream.TryGetProperty("codec_name", out var codecProp) ? codecProp.GetString() ?? "unknown" : "unknown";
                    int index = stream.TryGetProperty("index", out var indexProp) ? indexProp.GetInt32() : -1;
                    string type = stream.TryGetProperty("codec_type", out var typeProp) ? typeProp.GetString() ?? "unknown" : "unknown";

                    #region AUDIO
                    if (type == "audio")
                    {
                        int? bitRate = ExtractBitrate(stream);
                        var audio = new AudioStreamInfo
                        {
                            Index = index,
                            Codec = codec,
                            Channels = stream.TryGetProperty("channels", out var ch) ? ch.GetInt32() : 0,
                            Bitrate = bitRate.HasValue ? bitRate.Value / 1000 : 0,

                            // Channel layout
                            ChannelLayout = stream.TryGetProperty("channel_layout", out var layoutProp)
                                ? layoutProp.GetString() ?? ""
                                : "",

                            // Profile
                            Profile = stream.TryGetProperty("profile", out var profileProp)
                                ? profileProp.GetString() ?? ""
                                : "",

                            // Codec tag string
                            CodecTagString = stream.TryGetProperty("codec_tag_string", out var tagProp)
                                ? tagProp.GetString() ?? ""
                                : ""
                        };

                        // --- TAGS (language, title) ---
                        if (stream.TryGetProperty("tags", out var tagsProp))
                        {
                            audio.Language = tagsProp.TryGetProperty("language", out var langProp)
                                ? langProp.GetString() ?? "und"
                                : "und";

                            audio.Title = tagsProp.TryGetProperty("title", out var titleProp)
                                ? FixEncoding(titleProp.GetString()) ?? ""
                                : "";
                        }
                        else
                        {
                            audio.Language = "und";
                            audio.Title = "";
                        }

                        // Fallback layout si vide
                        if (string.IsNullOrWhiteSpace(audio.ChannelLayout))
                        {
                            audio.ChannelLayout = audio.Channels switch
                            {
                                1 => "mono",
                                2 => "stereo",
                                6 => "5.1",
                                8 => "7.1",
                                _ => $"{audio.Channels}.0"
                            };
                        }

                        result.AudioStreams.Add(audio);
                    }
                    #endregion

                    #region SOUS-TITRES
                    if (type == "subtitle")
                    {
                        var subtitles = new SubtitleStreamInfo();

                        subtitles.Index = index;
                        subtitles.Codec = codec;

                        // --- TAGS (language, title) ---
                        if (stream.TryGetProperty("tags", out var tagsProp))
                        {
                            subtitles.Language = tagsProp.TryGetProperty("language", out var langProp)
                                ? langProp.GetString() ?? "und"
                                : "und";

                            subtitles.Title = tagsProp.TryGetProperty("title", out var titleProp)
                                ? FixEncoding(titleProp.GetString()) ?? ""
                                : "";
                        }
                        else
                        {
                            subtitles.Language = "und";
                            subtitles.Title = "";
                        }

                        result.SubtitleStreams.Add(subtitles);
                    }
                    #endregion

                    #region VIDEO
                    if (type == "video")
                    {
                        var video = new VideoStreamInfo();

                        // --- HDR / Dolby Vision detection ---

                        // SDR par défaut
                        video.HasHDR10 = false;
                        video.HasHDR10Plus = false;
                        video.HasDolbyVision = false;
                        video.DolbyVisionProfile = 0;

                        // 1) HDR10 (PQ + BT2020)
                        if (stream.TryGetProperty("color_transfer", out var trProp) &&
                            trProp.GetString() == "smpte2084")
                        {
                            video.HasHDR10 = true;
                        }

                        if (stream.TryGetProperty("color_primaries", out var primProp) &&
                            primProp.GetString() == "bt2020")
                        {
                            video.HasHDR10 = true;
                        }

                        // 2) HDR10+ (metadata dynamiques)
                        if (stream.TryGetProperty("side_data_list", out var sideDataList))
                        {
                            foreach (var side in sideDataList.EnumerateArray())
                            {
                                if (side.TryGetProperty("side_data_type", out var sdtProp))
                                {
                                    string sdt = sdtProp.GetString() ?? "";

                                    if (sdt.Contains("HDR10+", StringComparison.OrdinalIgnoreCase) ||
                                        sdt.Contains("HDR10+ Metadata", StringComparison.OrdinalIgnoreCase))
                                    {
                                        video.HasHDR10Plus = true;
                                    }

                                    // 3) Dolby Vision
                                    if (sdt.Contains("DOVI", StringComparison.OrdinalIgnoreCase) ||
                                        sdt.Contains("DOVI configuration record", StringComparison.OrdinalIgnoreCase))
                                    {
                                        video.HasDolbyVision = true;

                                        if (side.TryGetProperty("dv_profile", out var dvProp))
                                            video.DolbyVisionProfile = dvProp.GetInt32();
                                    }
                                }
                            }
                        }

                        video.Index = index;
                        video.Codec = codec;
                        video.Width = stream.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                        video.Height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : 0;

                        // Profile
                        video.Profile = stream.TryGetProperty("profile", out var profileProp)
                              ? profileProp.GetString() ?? ""
                              : "";

                        // Codec tag string
                        video.CodecTagString = stream.TryGetProperty("codec_tag_string", out var tagProp)
                              ? tagProp.GetString() ?? ""
                              : "";

                        // --- TAGS (language, title) ---
                        if (stream.TryGetProperty("tags", out var tagsProp))
                        {
                            video.Language = tagsProp.TryGetProperty("language", out var langProp)
                                ? langProp.GetString() ?? "und"
                                : "und";

                            video.Title = tagsProp.TryGetProperty("title", out var titleProp)
                                ? FixEncoding(titleProp.GetString()) ?? ""
                                : "";
                        }
                        else
                        {
                            video.Language = "und";
                            video.Title = "";
                        }

                        if (stream.TryGetProperty("avg_frame_rate", out var afr) && afr.ToString() != "0/0")
                        {
                            var parts = afr.ToString().Split('/');
                            if (parts.Length == 2 &&
                                double.TryParse(parts[0], out double num) &&
                                double.TryParse(parts[1], out double den) &&
                                den > 0)
                            {
                                video.FPS = Math.Truncate((num / den) * 1000) / 1000;
                            }
                        }

                        if (stream.TryGetProperty("tags", out var tags) && tags.ToString() != string.Empty)
                        {
                            if (tags.TryGetProperty("BPS", out var bpsProp) && long.TryParse(bpsProp.GetString(), out long bps))
                            {
                                video.Bitrate = (int)(bps / 1000);
                            }
                            else if (tags.TryGetProperty("NUMBER_OF_BYTES", out var bytesProp) &&
                                     long.TryParse(bytesProp.GetString(), out long bytes) &&
                                     tags.TryGetProperty("DURATION", out var durProp) &&
                                     TimeSpan.TryParse(durProp.GetString(), out var duration) &&
                                     duration.TotalSeconds > 0)
                            {
                                long bits = bytes * 8;
                                video.Bitrate = (int)(bits / duration.TotalSeconds / 1000);
                            }
                        }

                        result.VideoStreams.Add(video);
                    }
                    #endregion
                }
            }

            if (doc.RootElement.TryGetProperty("format", out var format))
            {
                if (format.TryGetProperty("duration", out var durProp))
                {
                    var durStr = durProp.GetString();

                    if (double.TryParse(durStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dur))
                    {
                        result.DurationSeconds = dur;
                    }
                }
            }

            result.RawJson = json;

            return result;
        }

        private static string FixEncoding(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Re-décodage ANSI → UTF8
            var bytes = Encoding.Default.GetBytes(input);
            return Encoding.UTF8.GetString(bytes);
        }

        public static int? ExtractBitrate(JsonElement stream)
        {
            // 1) bit_rate direct
            if (stream.TryGetProperty("bit_rate", out var brProp))
            {
                if (int.TryParse(brProp.GetString(), out int br) && br > 0)
                    return br;
            }

            // 2) tags.BPS
            if (stream.TryGetProperty("tags", out var tags))
            {
                if (tags.TryGetProperty("BPS", out var bpsProp))
                {
                    if (int.TryParse(bpsProp.GetString(), out int bps) && bps > 0)
                        return bps;
                }

                // 3) NUMBER_OF_BYTES + DURATION
                if (tags.TryGetProperty("NUMBER_OF_BYTES", out var bytesProp) &&
                    tags.TryGetProperty("DURATION", out var durProp))
                {
                    if (long.TryParse(bytesProp.GetString(), out long bytes) &&
                        TimeSpan.TryParse(durProp.GetString(), out var duration) &&
                        duration.TotalSeconds > 0)
                    {
                        long bitrate = (long)(bytes * 8 / duration.TotalSeconds);
                        if (bitrate > 0)
                            return (int)bitrate;
                    }
                }
            }

            // 4) Rien trouvé → VBR
            return null;
        }
    }
}
