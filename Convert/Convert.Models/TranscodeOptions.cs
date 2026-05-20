namespace Convert.Models
{
    public class TranscodeOptions
    {
        public string OutputContainer { get; set; } = "mkv";

        public Dictionary<int, AudioProfile> AudioTrackProfiles { get; set; } = new Dictionary<int, AudioProfile>();
        public Dictionary<int, VideoProfile> VideoTrackProfiles { get; set; } = new Dictionary<int, VideoProfile>();
    }
}
