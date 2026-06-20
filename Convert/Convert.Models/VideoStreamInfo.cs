namespace Convert.Models
{
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
        public bool HasDolbyVision { get; set; }
        public int DolbyVisionProfile { get; set; }
        public bool HasHDR10 { get; set; }
        public bool HasHDR10Plus { get; set; }

    }
}
