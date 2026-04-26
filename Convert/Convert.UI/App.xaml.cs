using Convert.UI.Services;
using System.Windows;

namespace Convert.UI
{
    public partial class App : Application
    {
        private SettingsService _settings;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _settings = new SettingsService();
            ApplyTheme(_settings.Settings.Theme, _settings.Settings.PrimaryColor);
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
