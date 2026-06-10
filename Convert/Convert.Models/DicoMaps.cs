namespace Convert.Models
{
    public static class DicoMaps
    {
        public static readonly Dictionary<string, string> VideoCodecMap = new()
        {
            { "avc", "AVC (H264)" },
            { "hevc", "HEVC (H265)" },
            { "und", "Inconnu" }
        };

        public static readonly Dictionary<string, string> AudioLanguageMap = new()
        {
            { "fre", "Francais" },
            { "eng", "Anglais" },
            { "spa", "Espagnol" },
            { "ita", "Italien" },
            { "ger", "Allemand" },
            { "jpn", "Japonais" },
            { "chi", "Chinois" },
            { "kor", "Coreen" },
            { "dut", "Neerlandais" },
            { "und", "Inconnu" }
        };

        public static readonly Dictionary<string, string> AudioCodecMap = new()
        {
            { "ac3", "Dolby Digital (AC3)" },
            { "eac3", "Dolby Digital Plus (EAC3)" },
            { "dts", "DTS" },
            { "truehd", "Dolby TrueHD" },
            { "aac", "Advanced Audio Coding" },
            { "und", "Inconnu" }
        };

        public static readonly Dictionary<string, string> SubtitleLanguageMap = new()
        {
            { "fre", "Francais" },
            { "eng", "Anglais" },
            { "spa", "Espagnol" },
            { "ita", "Italien" },
            { "ger", "Allemand" },
            { "jpn", "Japonais" },
            { "chi", "Chinois" },
            { "kor", "Coreen" },
            { "dut", "Neerlandais" },
            { "und", "Inconnu" }
        };

        public static readonly Dictionary<string, string> SubtitleCodecMap = new()
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

        public static readonly Dictionary<string, string> MarkerMap = new()
        {
            // Subtitles
            { "complets", "Full" },
            { "complet", "Full" },
            { "full", "Full" },
            { "forced", "Forced" },
            { "forcé", "Forced" },
            { "forced only", "Forced" },
            { "sdh", "SDH" },
            { "cc", "CC" },
            { "closed captions", "CC" },
            { "hearing impaired", "HI" },
            { "hi", "HI" },

            // Songs & Signs
            { "signs", "Signs" },
            { "songs", "Songs" },
            { "signs & songs", "Signs & Songs" },

            // Commentary
            { "commentary", "Commentary" },
            { "director", "Director Commentary" },

            // Audio Description
            { "audio description", "AD" },
            { "descriptive audio", "AD" },
            { "description", "AD" },
            { "narration", "AD" },
            { "vi", "VI" },

            // Locales
            { "vfi", "VFI" },
            { "vo", "VO" },
            { "Vfq", "VFQ" },
            { "vff", "VFF" },
            { "tfrench", "VFF" },
            { "true french", "VFF" }
        };
    }
}
