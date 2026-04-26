using Convert.UI.ViewModels;
using System.Windows;

namespace Convert.UI.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow(AboutViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
