namespace Convert.Models
{
    public enum AudioProfile
    {
        Copy,
        Eac3_5_1,
        Ac3_5_1,
        Ac3_2_0,
        Mp3_2_0,
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
