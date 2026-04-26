namespace Convert.UI.Services
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Dark";
        public string PrimaryColor { get; set; } = "DeepPurple";

        public string DefaultContainer { get; set; } = "mkv";
        public string DefaultVideoCodec { get; set; } = "copy";
        public string DefaultAudioCodec { get; set; } = "copy";

        public bool ConvertDtsToEac3 { get; set; } = true;
        public bool ConvertMovTextToSrt { get; set; } = true;

        public int MaxParallelJobs { get; set; } = 2;

        public string PreferredVideoEngine { get; set; } = "CPU";

        public string FfmpegPath { get; set; } = "";
        public string FfprobePath { get; set; } = "";

        public bool AutoDownloadFfmpeg { get; set; } = false;
    }
}
