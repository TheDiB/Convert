using Convert.UI.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Convert.UI.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _service;
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
        public ObservableCollection<string> Containers { get; } = new ObservableCollection<string> { "MKV", "MP4", "MOV" };

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

        private string _container;
        public string Container
        {
            get => _container;
            set { _container = value; OnPropertyChanged(nameof(Container)); }
        }

        public int MaxParallelJobs { get; set; }

        public string SupportedFileTypes { get; set; }

        public string FfmpegPath { get; set; }
        public string FfprobePath { get; set; }
        public string FfmpegReleaseURL { get; set; }


        public bool AutoDownloadFfmpeg { get; set; }
        public bool EnableReports { get; set; }
        public bool DumpDebugFiles { get; set; }
        public bool AutoAnalyze { get; set; }
        public bool StartMaximized { get; set; }
        public bool EnableWindowsNotifications { get; set; }

        public SettingsViewModel(SettingsService service)
        {
            _service = service;

            // Charger les valeurs dans le VM
            Theme = _service.Settings.Theme;
            PrimaryColor = _service.Settings.PrimaryColor;
            Container = _service.Settings.Container;

            MaxParallelJobs = _service.Settings.MaxParallelJobs;
            SupportedFileTypes = _service.Settings.SupportedFileTypes;

            FfmpegPath = _service.Settings.FfmpegPath;
            FfprobePath = _service.Settings.FfprobePath;

            AutoDownloadFfmpeg = _service.Settings.AutoDownloadFfmpeg;
            EnableReports = _service.Settings.EnableReports;
            DumpDebugFiles = _service.Settings.DumpDebugFiles;
            AutoAnalyze = _service.Settings.AutoAnalyze;
            StartMaximized = _service.Settings.StartMaximized;
            EnableWindowsNotifications = _service.Settings.EnableWindowsNotifications;

            FfmpegReleaseURL = _service.Settings.FFmpegReleaseURL;

            SaveCommand = new RelayCommand(_ => Save());
        }


        // --- Commande Save ---
        public ICommand SaveCommand { get; }

        private void Save()
        {
            _service.Settings.Theme = Theme;
            _service.Settings.PrimaryColor = PrimaryColor;
            _service.Settings.Container = Container;

            _service.Settings.MaxParallelJobs = MaxParallelJobs;
            _service.Settings.SupportedFileTypes = SupportedFileTypes;

            _service.Settings.FfmpegPath = FfmpegPath;
            _service.Settings.FfprobePath = FfprobePath;

            _service.Settings.AutoDownloadFfmpeg = AutoDownloadFfmpeg;
            _service.Settings.EnableReports = EnableReports;
            _service.Settings.DumpDebugFiles = DumpDebugFiles;
            _service.Settings.AutoAnalyze = AutoAnalyze;
            _service.Settings.StartMaximized = StartMaximized;
            _service.Settings.EnableWindowsNotifications = EnableWindowsNotifications;

            _service.Settings.FFmpegReleaseURL = FfmpegReleaseURL;

            _service.Save();
            Saved?.Invoke();
        }
    }
}
