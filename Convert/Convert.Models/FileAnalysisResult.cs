namespace Convert.Models
{
    public class FileAnalysisResult
    {
        public string FilePath { get; set; }
        public List<AudioStreamInfo> AudioStreams { get; set; } = new();
        public List<SubtitleStreamInfo> SubtitleStreams { get; set; } = new();
        public double DurationSeconds { get; set; }
    }
}
