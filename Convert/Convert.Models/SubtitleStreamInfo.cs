namespace Convert.Models
{
    public class SubtitleStreamInfo
    {
        public static readonly Dictionary<string, string> LanguageMap = new()
        {
            { "fre", "Francais" },
            { "fra", "Francais" },
            { "eng", "Anglais" },
            { "spa", "Espagnol" },
            { "ita", "Italien" },
            { "ger", "Allemand" },
            { "jpn", "Japonais" },
            { "und", "Inconnu" }
        };

        public static readonly Dictionary<string, string> CodecMap = new()
        {
            { "srt", "SubRip Text (SRT)" },
            { "subrip", "SubRip Text (SRT)" },
            { "ass", "Advanced SubStation Alpha (ASS)" },
            { "pgs", "Presentation Graphic Stream (PGS)" },
            { "hdmv_pgs_subtitle", "Presentation Graphic Stream (PGS)" },
            { "dvd_subtitle ", "VobSub SubTitle File (SUB)" },
            { "xsub ", "XSUB" },
            { "dvb_subtitle ", "DVB" },
            { "und", "Inconnu" }
        };

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
