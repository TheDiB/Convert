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

    public enum HDRProfile
    {
        Keep,       // Conserver tel quel
        StripDv,    // Supprimer les métadonnées DV (RPU)
        ForceHdr10, // Idem StripDv, mais sémantique différente (évolutif)
        ForceSdr    // Tonemapping vers SDR
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
