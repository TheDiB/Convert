using System.IO;
using System.Text.Json;

namespace Convert.UI.Services
{
    public class SettingsService
    {
        private const string FileName = "settings.json";

        public AppSettings Settings { get; private set; }

        public SettingsService()
        {
            Load();
        }

        public void Load()
        {
            if (File.Exists(FileName))
            {
                var json = File.ReadAllText(FileName);
                Settings = JsonSerializer.Deserialize<AppSettings>(json);
            }
            else
            {
                Settings = new AppSettings();
                Save();
            }
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FileName, json);
        }
    }
}
