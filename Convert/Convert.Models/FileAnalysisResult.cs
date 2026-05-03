namespace Convert.Models
{
    public class FileAnalysisResult
    {
        public string FilePath { get; set; } = string.Empty;
        public List<AudioStreamInfo> AudioStreams { get; set; } = new();
        public List<SubtitleStreamInfo> SubtitleStreams { get; set; } = new();
        public VideoStreamInfo? VideoStream { get; set; } = new();
        public double DurationSeconds { get; set; }

        public AnalysisReportModel ToReportEntry()
        {
            return new AnalysisReportModel
            {
                FileName = Path.GetFileName(FilePath),
                FilePath = FilePath,
                AudioCodecs = string.Join(", ", AudioStreams.Select(a => a.Codec)),
                ContainsDTS = AudioStreams.Any(a => a.Codec.Contains("dts", StringComparison.OrdinalIgnoreCase)),
                DtsTrackCount = AudioStreams.Count(a => a.Codec.Contains("dts", StringComparison.OrdinalIgnoreCase)),
                VideoCodec = VideoStream?.Codec ?? "",
                Duration = TimeSpan.FromSeconds(DurationSeconds),
                FileSizeBytes = new FileInfo(FilePath).Length
            };
        }
    }
}
