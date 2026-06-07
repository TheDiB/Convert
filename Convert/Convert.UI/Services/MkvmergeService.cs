using Convert.Core;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Convert.UI.Services
{
    public class MkvmergeService
    {
        private readonly string _binFolder;
        private readonly string _mkvmergePath;

        public event Action<string>? StatusChanged;
        public event Action? DownloadStarted;
        public event Action? DownloadCompleted;

        public string ExecutablePath => _mkvmergePath;

        public MkvmergeService()
        {
            _binFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mkvmerge\\mkvtoolnix");
            _mkvmergePath = Path.Combine(_binFolder, "mkvmerge.exe");
        }

        public bool Exists() => File.Exists(_mkvmergePath);

        public async Task EnsureExistsAsync()
        {
            Directory.CreateDirectory(_binFolder);

            if (Exists())
                return;

            await DownloadAndExtractAsync();
        }

        private async Task DownloadAndExtractAsync()
        {
            try
            {
                DownloadStarted?.Invoke();
                StatusChanged?.Invoke("Téléchargement de MKVMerge...");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("request");

                // MKVToolNix Windows portable
                var url = await GetLatestMkvmergeUrlAsync();
                var archivePath = Path.Combine(_binFolder, "mkvmerge.zip");

                using (var stream = await client.GetStreamAsync(url))
                using (var file = File.Create(archivePath))
                    await stream.CopyToAsync(file);

                StatusChanged?.Invoke("Extraction de MKVMerge...");

                // Extraction via 7zip portable (tu peux intégrer ton propre extracteur)
                SimpleExtractor.Extract(archivePath, _binFolder);

                File.Delete(archivePath);

                StatusChanged?.Invoke("MKVMerge prêt !");
            }
            finally
            {
                DownloadCompleted?.Invoke();
            }
        }

        private async Task<string> GetLatestMkvmergeUrlAsync()
        {
            var version = await GetLatestMkvtoolnixVersionAsync();
            return $"https://mkvtoolnix.download/windows/releases/{version}/mkvtoolnix-64-bit-{version}.zip";
        }

        private async Task<string> GetLatestMkvtoolnixVersionAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("request");

            string html = await client.GetStringAsync("https://mkvtoolnix.download/windows/releases/");

            // On cherche TOUT ce qui ressemble à "82.0/" ou "83.0/"
            var matches = Regex.Matches(html, @"(\d+\.\d+)/");

            if (matches.Count == 0)
                throw new Exception("Impossible de détecter les versions MKVToolNix.");

            var versions = matches
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .Select(v => Version.Parse(v))
                .OrderByDescending(v => v)
                .ToList();

            return versions.First().ToString(); // ex: "82.0"
        }
    }
}