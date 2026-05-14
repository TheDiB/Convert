namespace Convert.UI.Services
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Dark";
        public string PrimaryColor { get; set; } = "DeepPurple";

        public string SupportedFileTypes { get; set; } = "mkv,mp4,mov,avi,ts,webm";

        public string DefaultContainer { get; set; } = "mkv";
        public string DefaultVideoCodec { get; set; } = "copy";
        //public string DefaultAudioCodec { get; set; } = "copy";

        public int MaxParallelJobs { get; set; } = 2;

        public string PreferredVideoEngine { get; set; } = "CPU";

        public string FfmpegPath { get; set; } = "";
        public string FfprobePath { get; set; } = "";

        public bool AutoDownloadFfmpeg { get; set; } = true;
        public bool EnableReports { get; set; } = false;

        public string FFmpegReleaseURL { get; set; } = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest";
    }
}
