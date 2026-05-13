using Convert.Models;

namespace Convert.UI.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
        public TranscodeOptions Options { get; }

        public OptionsViewModel(TranscodeOptions options)
        {
            Options = options;
        }

        public IEnumerable<string> Containers => new[] { "mkv", "mp4" };

        public IEnumerable<string> VideoCodecs => new[]
        {
            "copy",
            "h264",
            "hevc"
        };
    }
}
