namespace Convert.UI.Services
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Dark";
        public string PrimaryColor { get; set; } = "DeepPurple";

        public string SupportedFileTypes { get; set; } = "mkv,mp4,mov,avi,ts,webm";

        public string Container { get; set; } = "mkv";

        public int MaxParallelJobs { get; set; } = 2;

        public string FfmpegPath { get; set; } = "";
        public string FfprobePath { get; set; } = "";

        public bool AutoDownloadFfmpeg { get; set; } = true;
        public bool EnableReports { get; set; } = false;
        public bool DumpDebugFiles { get; set; } = false;

        public bool AutoAnalyze { get; set; } = true;

        public bool StartMaximized { get; set; } = true;
        public bool EnableWindowsNotifications { get; set; } = true;

        public bool CompatibilityMode { get; set; } = true;

        public bool EnableGPUEncoding { get; set; } = true;

        public string FFmpegReleaseURL { get; set; } = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest";
    }
}
