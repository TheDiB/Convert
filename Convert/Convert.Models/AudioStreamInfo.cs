namespace Convert.Models
{
    public static class AudioLanguageStreamInfo
    {
        public static readonly Dictionary<string, string> LanguageMap = new()
        {
            { "fre", "French" },
            { "fra", "French" },
            { "eng", "Anglais" },
            { "spa", "Espagnol" },
            { "ita", "Italien" },
            { "ger", "Allemand" },
            { "jpn", "Japonais" },
            { "und", "Inconnu" }
        };

        public static readonly Dictionary<string, string> CodecMap = new()
        {
            { "ac3", "Dolby Digital (AC3)" },
            { "eac3", "Dolby Digital Plus (EAC3)" },
            { "dts", "DTS" },
            { "truehd", "Dolby TrueHD" },
            { "aac", "Advanced Audio Coding" },
            { "und", "Inconnu" }
        };
    }

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
