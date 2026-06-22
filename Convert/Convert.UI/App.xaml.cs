using Convert.Core;
using Convert.UI.Services;
using Convert.UI.ViewModels;
using Convert.UI.Views;
using System.IO;
using System.Windows;

namespace Convert.UI
{
    public partial class App : Application
    {
        private SettingsService _settings;
        private bool _analysisMode = false;


        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _settings = new SettingsService();
            ApplyTheme(_settings.Settings.Theme, _settings.Settings.PrimaryColor);

            if (e.Args.Length > 0)
            {
                // Reconstituer le chemin complet
                var combined = string.Join(" ", e.Args);

                // Essayer tel quel
                if (File.Exists(combined))
                {
                    ShowAnalysisForFile(combined);
                    return;
                }

                // Essayer de détecter le fichier en testant les suffixes
                for (int i = 0; i < e.Args.Length; i++)
                {
                    var candidate = string.Join(" ", e.Args.Skip(i));
                    if (File.Exists(candidate))
                    {
                        ShowAnalysisForFile(candidate);
                        return;
                    }
                }
            }

            // Lancement normal
            var mw = new MainWindow();
            mw.Show();

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
                        mainVM.Notify("FFmpeg a été mis à jour avec succès !", Models.NotificationLevel.Success);
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

            var themeDict = new ResourceDictionary
            {
                Source = new Uri(
                    theme == "Light"
                    ? "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml"
                    : "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml"
                )
            };

            var defaults = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml")
            };

            var primary = new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.{primaryColor}.xaml"
                )
            };

            var accent = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Secondary/MaterialDesignColor.Blue.xaml"
                )
            };

            Resources.MergedDictionaries.Add(themeDict);
            Resources.MergedDictionaries.Add(defaults);
            Resources.MergedDictionaries.Add(primary);
            Resources.MergedDictionaries.Add(accent);
        }

        private async void ShowAnalysisForFile(string path)
        {
            this._analysisMode = true;
            var ffprobePath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffprobe.exe");
            var ffprobe = new FFprobeService(ffprobePath);
            var result = await ffprobe.AnalyzeAsync(path, new CancellationToken()); // méthode à ajouter si besoin
            var vm = new AnalysisViewModel(result);
            var win = new AnalysisWindow(vm);

            vm.RequestClose = () =>
            {
                win.Close();

                if (_analysisMode)
                    Shutdown(); // Quitter l'application proprement
            };

            win.Show();
        }
    }
}
