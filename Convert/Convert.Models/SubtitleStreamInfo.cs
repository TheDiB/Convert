namespace Convert.Models
{
    public class SubtitleStreamInfo
    {
        public int Index { get; set; }
        public string Codec { get; set; } = "";
        public string Title { get; set; } = "";
        public string Language { get; set; } = "";

        public bool IsBitmap =>
            Codec.Equals("hdmv_pgs_subtitle", StringComparison.OrdinalIgnoreCase) ||
            Codec.Equals("pgssub", StringComparison.OrdinalIgnoreCase) ||
            Codec.Equals("dvd_subtitle", StringComparison.OrdinalIgnoreCase);
    }
}
