using Convert.UI.Services;
using Convert.UI.ViewModels;
using Convert.UI.Views;
using System.Windows;

namespace Convert.UI
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            _settingsService = new SettingsService();
            DataContext = new MainViewModel(_settingsService);

            LogTextBox.TextChanged += (s, e) =>
            {
                LogTextBox.ScrollToEnd();
            };
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
    }
}