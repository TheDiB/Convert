namespace Convert.Models
{
    public enum AudioProfile
    {
        Copy,
        Flac_auto,
        Aac_7_1,
        Eac3_5_1,
        Ac3_5_1,
        Aac_2_0,
        Ignore
    }

    public class AudioProfileItem
    {
        public AudioProfile Profile { get; }
        public string Label { get; }

        public AudioProfileItem(AudioProfile profile, string label)
        {
            Profile = profile;
            Label = label;
        }
    }
}
