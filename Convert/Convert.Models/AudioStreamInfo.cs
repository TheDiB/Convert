namespace Convert.Models
{
    public class AudioStreamInfo
    {
        public int Index { get; set; }
        public string Codec { get; set; } = "";
        public string Title { get; set; } = "";
        public int Channels { get; set; }
        public int Bitrate { get; set; }
        public string ChannelLayout { get; set; } = "";
        public string Language { get; set; } = "";
        public string Profile { get; set; } = "";
        public string CodecTagString { get; set; } = "";
    }
}
