namespace Convert.Models
{
    public static class VideoLanguageStreamInfo
    {
        public static readonly Dictionary<string, string> CodecMap = new()
        {
            { "avc", "AVC (H264)" },
            { "hevc", "HEVC (H265)" },
            { "und", "Inconnu" }
        };
    }

    public class VideoStreamInfo
    {
        public int Index { get; set; }
        public string Codec { get; set; } = "";
        public string Resolution { get; set; } = "";
        public string PixelFormat { get; set; } = "";
        public string Title { get; set; } = "";
        public int Bitrate { get; set; }
        public string Language { get; set; } = "";
        public string Profile { get; set; } = "";
        public string CodecTagString { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public double FPS { get; set; }
    }
}
