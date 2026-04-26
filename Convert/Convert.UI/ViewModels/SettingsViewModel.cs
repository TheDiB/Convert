using Convert.UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Convert.UI.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _service;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action Saved;

        public ObservableCollection<string> Themes { get; } = new ObservableCollection<string> { "Dark", "Light" };
        public ObservableCollection<string> PrimaryColors { get; } =
        new ObservableCollection<string>
        {
            "Red", "Pink", "Purple", "DeepPurple", "Indigo",
            "Blue", "LightBlue", "Cyan", "Teal",
            "Green", "LightGreen", "Lime", "Yellow",
            "Amber", "Orange", "DeepOrange", "Brown", "Grey", "BlueGrey"
        };
        public ObservableCollection<string> Containers { get; } = new ObservableCollection<string> { "mkv", "mp4", "mov" };
        public ObservableCollection<string> VideoCodecs { get; } = new ObservableCollection<string> { "copy", "h264", "hevc" };
        public ObservableCollection<string> AudioCodecs { get; } = new ObservableCollection<string> { "copy", "aac", "ac3", "eac3" };
        public ObservableCollection<string> VideoEngines { get; } = new ObservableCollection<string> { "CPU", "NVENC", "QSV", "AMF" };

        public SettingsViewModel(SettingsService service)
        {
            _service = service;

            // Charger les valeurs dans le VM
            Theme = _service.Settings.Theme;
            PrimaryColor = _service.Settings.PrimaryColor;
            DefaultContainer = _service.Settings.DefaultContainer;
            DefaultVideoCodec = _service.Settings.DefaultVideoCodec;
            DefaultAudioCodec = _service.Settings.DefaultAudioCodec;

            ConvertDtsToEac3 = _service.Settings.ConvertDtsToEac3;
            ConvertMovTextToSrt = _service.Settings.ConvertMovTextToSrt;

            MaxParallelJobs = _service.Settings.MaxParallelJobs;

            PreferredVideoEngine = _service.Settings.PreferredVideoEngine;

            FfmpegPath = _service.Settings.FfmpegPath;
            FfprobePath = _service.Settings.FfprobePath;

            AutoDownloadFfmpeg = _service.Settings.AutoDownloadFfmpeg;

            SaveCommand = new RelayCommand(_ => Save());
        }

        // --- Propriétés bindables ---
        private string _theme;
        public string Theme
        {
            get => _theme;
            set { _theme = value; OnPropertyChanged(nameof(Theme)); }
        }

        private string _primaryColor;
        public string PrimaryColor
        {
            get => _primaryColor;
            set { _primaryColor = value; OnPropertyChanged(nameof(PrimaryColor)); }
        }

        private string _defaultContainer;
        public string DefaultContainer
        {
            get => _defaultContainer;
            set { _defaultContainer = value; OnPropertyChanged(nameof(DefaultContainer)); }
        }

        private string _defaultVideoCodec;
        public string DefaultVideoCodec
        {
            get => _defaultVideoCodec;
            set { _defaultVideoCodec = value; OnPropertyChanged(nameof(DefaultVideoCodec)); }
        }

        private string _defaultAudioCodec;
        public string DefaultAudioCodec
        {
            get => _defaultAudioCodec;
            set { _defaultAudioCodec = value; OnPropertyChanged(nameof(DefaultAudioCodec)); }
        }

        public bool ConvertDtsToEac3 { get; set; }
        public bool ConvertMovTextToSrt { get; set; }

        public int MaxParallelJobs { get; set; }

        public string PreferredVideoEngine { get; set; }

        public string FfmpegPath { get; set; }
        public string FfprobePath { get; set; }

        public bool AutoDownloadFfmpeg { get; set; }

        // --- Commande Save ---
        public ICommand SaveCommand { get; }

        private void Save()
        {
            _service.Settings.Theme = Theme;
            _service.Settings.PrimaryColor = PrimaryColor;

            _service.Settings.DefaultContainer = DefaultContainer;
            _service.Settings.DefaultVideoCodec = DefaultVideoCodec;
            _service.Settings.DefaultAudioCodec = DefaultAudioCodec;

            _service.Settings.ConvertDtsToEac3 = ConvertDtsToEac3;
            _service.Settings.ConvertMovTextToSrt = ConvertMovTextToSrt;

            _service.Settings.MaxParallelJobs = MaxParallelJobs;

            _service.Settings.PreferredVideoEngine = PreferredVideoEngine;

            _service.Settings.FfmpegPath = FfmpegPath;
            _service.Settings.FfprobePath = FfprobePath;

            _service.Settings.AutoDownloadFfmpeg = AutoDownloadFfmpeg;

            _service.Save();
            Saved?.Invoke();
        }

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
