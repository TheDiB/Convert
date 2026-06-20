namespace Convert.Models
{
    public class TranscodeOptions
    {
        public string Container { get; set; }
        public bool DumpDebugFiles { get; set; } = false;
        public bool CompatibilityMode { get; set; } = false;
        public bool EnableGPUEncoding { get; set; } = false;

        public Dictionary<int, string> AudioTrackLanguages { get; set; } = new();
        public Dictionary<int, string> SubtitleTrackLanguages { get; set; } = new();

        public Dictionary<int, AudioProfile> AudioTrackProfiles { get; set; } = new Dictionary<int, AudioProfile>();
        public Dictionary<int, VideoProfile> VideoTrackProfiles { get; set; } = new Dictionary<int, VideoProfile>();
        public Dictionary<int, HDRProfile> VideoTrackHDRModes { get; set; } = new Dictionary<int, HDRProfile>();
        public Dictionary<int, SubtitleProfile> SubtitleTrackProfiles { get; set; } = new Dictionary<int, SubtitleProfile>();
    }
}
