using Convert.UI.ViewModels;
using System.Windows;

namespace Convert.UI.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            vm.Saved += () =>
            {
                ((App)Application.Current).ApplyTheme(vm.Theme, vm.PrimaryColor);
                Close();
            };
        }
    }
}
