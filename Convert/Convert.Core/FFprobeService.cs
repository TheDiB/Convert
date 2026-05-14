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

                    //
                    // --- AUDIO ---
                    //
                    if (type == "audio")
                    {
                        var audio = new AudioStreamInfo
                        {
                            Index = index,
                            Codec = codec,
                            Channels = stream.TryGetProperty("channels", out var ch) ? ch.GetInt32() : 0,
                            Bitrate = stream.TryGetProperty("bit_rate", out var br) && int.TryParse(br.GetString(), out var bitrate) ? bitrate : 0,

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

                    //
                    // --- SOUS-TITRES ---
                    //
                    if (type == "subtitle")
                        result.SubtitleStreams.Add(new SubtitleStreamInfo { Index = index, Codec = codec });

                    //
                    // --- VIDÉO ---
                    //
                    if (type == "video")
                    {
                        result.VideoStream = new VideoStreamInfo
                        {
                            Index = index,
                            Codec = codec,
                            Resolution = $"{stream.GetProperty("width").GetInt32()}x{stream.GetProperty("height").GetInt32()}",
                            PixelFormat = stream.TryGetProperty("pix_fmt", out var pf) ? pf.GetString() ?? "" : ""
                        };
                    }
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
    }
}
