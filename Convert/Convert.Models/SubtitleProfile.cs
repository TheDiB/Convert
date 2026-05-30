namespace Convert.Models
{
    public enum SubtitleProfile
    {
        Copy,
        ConvertToSrt,
        Ignore
    }

    public class SubtitleProfileItem
    {
        public SubtitleProfile Profile { get; }
        public string Label { get; }

        public SubtitleProfileItem(SubtitleProfile profile, string label)
        {
            Profile = profile;
            Label = label;
        }
    }
}
