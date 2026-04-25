using Convert.Models;
using System.ComponentModel;

namespace Convert.UI.ViewModels
{
    public class OptionsViewModel : INotifyPropertyChanged
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
        "hevc",
        "hevc_nvenc",
        "h264_nvenc"
    };

        public IEnumerable<string> AudioCodecs => new[]
        {
        "copy",
        "eac3",
        "aac"
    };

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
