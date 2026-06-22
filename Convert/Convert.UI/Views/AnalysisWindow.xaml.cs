using Convert.UI.ViewModels;
using System.Windows;

namespace Convert.UI.Views
{
    public partial class AnalysisWindow : Window
    {
        public AnalysisWindow(AnalysisViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            vm.RequestClose = () => this.Close();
        }
    }
}
