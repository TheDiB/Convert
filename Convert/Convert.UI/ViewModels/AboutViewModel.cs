using System.Diagnostics;
using System.Reflection;

namespace Convert.UI.ViewModels
{
    public class AboutViewModel
    {
        public string AppVersion { get { return string.Concat("Version ", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)?.FileVersion ?? "inconnue"); } }
        public string AppName { get { return "Convert"; } }
        public string AppDescription { get { return "Outil de conversion audio / vidéo open source de la DiBerie !"; } }
    }
}

