using Convert.UI.Services;
using System.Windows;

namespace Convert.UI
{
    public partial class App : Application
    {
        private SettingsService _settings;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _settings = new SettingsService();
            ApplyTheme(_settings.Settings.Theme, _settings.Settings.PrimaryColor);

            if (_settings.Settings.AutoDownloadFfmpeg)
            {
                var dialogs = new DialogService();
                var ffmpeg = new FFmpegService();
                var mainVM = new MainViewModel(_settings, dialogs, ffmpeg);

                // On connecte les événements du service au ViewModel
                ffmpeg.StatusChanged += msg => mainVM.FFmpegStatusMessage = msg;
                ffmpeg.DownloadStarted += () => mainVM.IsFFmpegDownloading = true;
                ffmpeg.DownloadCompleted += () =>
                {
                    mainVM.IsFFmpegDownloading = false;
                    Dispatcher.Invoke(() =>
                    {
                        mainVM.Notify("FFmpeg a été mis à jour avec succès !");
                    });
                };

                // Lancement du check FFmpeg en arrière-plan
                Task.Run(async () =>
                {
                    mainVM.IsFFmpegChecking = true;

                    var result = await ffmpeg.CheckAsync();

                    mainVM.IsFFmpegChecking = false;

                    switch (result)
                    {
                        case FFmpegCheckResult.NotFound:
                            if (mainVM.Dialogs.Confirm("FFmpeg est introuvable. Voulez-vous le télécharger ?", "FFmpeg manquant"))
                                await ffmpeg.DownloadAndExtractAsync();
                            else
                                mainVM.Dialogs.Alert("Impossible d'utiliser l'application sans FFmpeg.", "Erreur");
                            break;

                        case FFmpegCheckResult.Outdated:
                            if (mainVM.Dialogs.Confirm("Une mise à jour FFmpeg est disponible. Voulez-vous l'installer ?", "Mise à jour FFmpeg"))
                                await ffmpeg.DownloadAndExtractAsync();
                            break;

                        case FFmpegCheckResult.UpToDate:
                            // Rien à faire
                            break;
                    }
                });
            }
        }

        public void ApplyTheme(string theme, string primaryColor)
        {
            Resources.MergedDictionaries.Clear();

            // 1) Thème clair ou sombre
            var themeDict = new ResourceDictionary
            {
                Source = new Uri(
                    theme == "Light"
                    ? "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml"
                    : "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml"
                )
            };

            // 2) Defaults (obligatoire)
            var defaults = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml")
            };

            // 3) Couleur primaire
            var primary = new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.{primaryColor}.xaml"
                )
            };

            // 4) Accent (obligatoire)
            var accent = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Secondary/MaterialDesignColor.Blue.xaml"
                )
            };

            // 5) Styles MaterialDesign (obligatoire)
            var materialDesign = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml")
            };

            // Ajout dans le bon ordre
            Resources.MergedDictionaries.Add(materialDesign);
            Resources.MergedDictionaries.Add(themeDict);
            Resources.MergedDictionaries.Add(defaults);
            Resources.MergedDictionaries.Add(primary);
            Resources.MergedDictionaries.Add(accent);
        }
    }
}
