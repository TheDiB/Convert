namespace Convert.Models
{
    public enum VideoProfile
    {
        Copy,
        Ignore,
        H265_High,
        H264_Medium,
        H264_Low,        
    }

    public class VideoProfileItem
    {
        public VideoProfile Profile { get; }
        public string Label { get; }

        public VideoProfileItem(VideoProfile profile, string label)
        {
            Profile = profile;
            Label = label;
        }
    }
}
