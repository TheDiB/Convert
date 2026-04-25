using Convert.UI.ViewModels;
using System.Windows;

namespace Convert.UI
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;

            LogTextBox.TextChanged += (s, e) =>
            {
                LogTextBox.ScrollToEnd();
            };
        }
    }
}