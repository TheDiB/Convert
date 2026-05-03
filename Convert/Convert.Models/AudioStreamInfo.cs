namespace Convert.Models
{
    public class AudioStreamInfo
    {
        public int Index { get; set; }
        public string Codec { get; set; } = "";
        public int Channels { get; set; }
        public int Bitrate { get; set; }
    }
}
