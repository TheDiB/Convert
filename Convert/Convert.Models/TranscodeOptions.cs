namespace Convert.Models
{
    public class TranscodeOptions
    {
        public string OutputContainer { get; set; } = "mkv";

        public string VideoCodec { get; set; } = "copy"; // copy, h264, hevc, hevc_nvenc, etc.

        //public AudioProfile AudioProfile { get; set; } = AudioProfile.Copy;

        public Dictionary<int, AudioProfile> AudioTrackProfiles { get; set; } = new Dictionary<int, AudioProfile>();

        public int AudioBitrateKbps { get; set; } = 640;

        public int MaxParallelJobs { get; set; } = 4;
    }
}
