namespace Convert.Models
{
    public class AnalysisReportModel
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string AudioCodecs { get; set; } = "";
        public bool ContainsDTS { get; set; }
        public int DtsTrackCount { get; set; }
        public string VideoCodec { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public long FileSizeBytes { get; set; }
    }

}
