namespace Convert.Models
{
    public class TranscodeOptions
    {
        public string OutputContainer { get; set; } = "mkv";

        public string VideoCodec { get; set; } = "copy"; // copy, h264, hevc, hevc_nvenc, etc.
        public string AudioCodec { get; set; } = "copy"; // copy, eac3, aac, etc.

        public bool ConvertDtsToEac3 { get; set; } = true;
        public bool ConvertMovTextToSrt { get; set; } = true;

        public int AudioBitrateKbps { get; set; } = 640;

        public int MaxParallelJobs { get; set; } = 4;
    }

}
