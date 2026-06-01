namespace Convert.Models
{
    public class TranscodeOptions
    {
        public string Container { get; set; }

        public Dictionary<int, AudioProfile> AudioTrackProfiles { get; set; } = new Dictionary<int, AudioProfile>();
        public Dictionary<int, VideoProfile> VideoTrackProfiles { get; set; } = new Dictionary<int, VideoProfile>();
        public Dictionary<int, SubtitleProfile> SubtitleTrackProfiles { get; set; } = new Dictionary<int, SubtitleProfile>();
    }
}
