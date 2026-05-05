using Convert.UI.Services;
using Convert.UI.ViewModels;
using Convert.UI.Views;
using System.Windows;

namespace Convert.UI
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly DialogService _dialogs;
        private readonly FFmpegService _FFmpeg;
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            _settingsService = new SettingsService();
            _dialogs = new DialogService();
            _FFmpeg = new FFmpegService();
            DataContext = new MainViewModel(_settingsService, _dialogs, _FFmpeg);
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var vm = new SettingsViewModel(_settingsService);
            var win = new SettingsWindow(vm)
            {
                Owner = this
            };
            win.ShowDialog();

            // Après fermeture : recharger les paramètres dans le MainViewModel
            ((MainViewModel)DataContext).ReloadSettings();
        }

        private void OpenAbout_Click(object sender, RoutedEventArgs e)
        {
            var vm = new AboutViewModel();
            var win = new AboutWindow(vm)
            {
                Owner = this
            };
            win.ShowDialog();
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.FFmpeg.DownloadCompleted += () =>
                {
                    // Toujours sur le thread UI
                    Dispatcher.Invoke(() =>
                    {
                        vm.Notify("FFmpeg a été mis à jour avec succès !");
                    });
                };
            }
        }

        private void LogTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            LogTextBox.ScrollToEnd();
        }
    }
}