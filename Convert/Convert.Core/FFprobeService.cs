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

            foreach (var stream in doc.RootElement.GetProperty("streams").EnumerateArray())
            {
                string codec = stream.GetProperty("codec_name").GetString();
                int index = stream.GetProperty("index").GetInt32();
                string type = stream.GetProperty("codec_type").GetString();

                if (type == "audio")
                    result.AudioStreams.Add(new AudioStreamInfo { Index = index, Codec = codec });

                if (type == "subtitle")
                    result.SubtitleStreams.Add(new SubtitleStreamInfo { Index = index, Codec = codec });
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
    }

}
