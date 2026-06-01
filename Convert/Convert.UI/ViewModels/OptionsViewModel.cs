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
    }
}
