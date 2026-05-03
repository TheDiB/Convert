namespace Convert.Core
{
    using Convert.Models;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text.Json;

    public class FFprobeService
    {
        private readonly string _ffprobePath;


        public FFprobeService(string ffprobePath)
        {
            _ffprobePath = ffprobePath;
        }

        public async Task<FileAnalysisResult> AnalyzeAsync(string filePath)
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

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Extraction JSON robuste (comme ton script)
            int start = output.IndexOf("{");
            int end = output.LastIndexOf("}");

            if (start < 0 || end < 0 || end <= start)
                throw new Exception("Impossible d'extraire un JSON valide depuis ffprobe.");

            string json = output.Substring(start, end - start + 1);

            using var doc = JsonDocument.Parse(json);

            var result = new FileAnalysisResult { FilePath = filePath };

            if (doc.RootElement.TryGetProperty("streams", out var streamsProp))
            {
                foreach (var stream in doc.RootElement.GetProperty("streams").EnumerateArray())
                {
                    string codec = stream.TryGetProperty("codec_name", out var codecProp) ? codecProp.GetString() ?? "unknown" : "unknown";
                    int index = stream.TryGetProperty("index", out var indexProp) ? indexProp.GetInt32() : -1;
                    string type = stream.TryGetProperty("codec_type", out var typeProp) ? typeProp.GetString() ?? "unknown" : "unknown";

                    if (type == "audio")
                        result.AudioStreams.Add(new AudioStreamInfo
                        {
                            Index = stream.GetProperty("index").GetInt32(),
                            Codec = codec,
                            Channels = stream.TryGetProperty("channels", out var ch) ? ch.GetInt32() : 0,
                            Bitrate = stream.TryGetProperty("bit_rate", out var br) && int.TryParse(br.GetString(), out var bitrate) ? bitrate : 0
                        });

                    if (type == "subtitle")
                        result.SubtitleStreams.Add(new SubtitleStreamInfo { Index = index, Codec = codec });

                    if (type == "video")
                    {
                        result.VideoStream = new VideoStreamInfo
                        {
                            Index = stream.GetProperty("index").GetInt32(),
                            Codec = codec,
                            Resolution = $"{stream.GetProperty("width").GetInt32()}x{stream.GetProperty("height").GetInt32()}",
                            PixelFormat = stream.TryGetProperty("pix_fmt", out var pf) ? pf.GetString() ?? "" : ""
                        };
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
            }

            return result;
        }
    }
}
