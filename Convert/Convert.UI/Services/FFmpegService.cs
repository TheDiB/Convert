using Convert.Core;
using Convert.Models;
using Convert.UI.Services;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public enum FFmpegCheckResult
{
    UpToDate,
    Outdated,
    NotFound
}

public class FFmpegService
{
    private readonly string _binFolder;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private SettingsService _settings;

    private string GithubLatestRelease => _settings.Settings.FFmpegReleaseURL;

    public event Action<string>? StatusChanged;
    public event Action? DownloadStarted;
    public event Action? DownloadCompleted;

    public FFmpegService()
    {
        _settings = new SettingsService();
        _binFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");
        _ffmpegPath = Path.Combine(_binFolder, "ffmpeg.exe");
        _ffprobePath = Path.Combine(_binFolder, "ffprobe.exe");
    }

    // ---------------------------------------------------------
    // 1) Vérification globale
    // ---------------------------------------------------------
    public async Task<FFmpegCheckResult> CheckAsync()
    {
        Directory.CreateDirectory(_binFolder);

        if (!File.Exists(_ffmpegPath) || !File.Exists(_ffprobePath))
            return FFmpegCheckResult.NotFound;

        StatusChanged?.Invoke("Lecture de la version locale FFmpeg...");
        var localVersion = await GetLocalVersionAsync();

        // Nightly build → pas de comparaison
        if (localVersion.StartsWith("N", StringComparison.OrdinalIgnoreCase))
        {
            return FFmpegCheckResult.UpToDate;
        }

        StatusChanged?.Invoke("Lecture de la version distante FFmpeg...");
        var remoteVersion = await GetLatestVersionAsync();

        if (localVersion == "unknown" || remoteVersion == "unknown")
            return FFmpegCheckResult.UpToDate;

        return localVersion == remoteVersion
            ? FFmpegCheckResult.UpToDate
            : FFmpegCheckResult.Outdated;
    }

    // ---------------------------------------------------------
    // 2) Récupération version locale
    // ---------------------------------------------------------
    private async Task<string> GetLocalVersionAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadLineAsync();
            process.WaitForExit();

            if (output == null)
                return "unknown";

            // Exemple : "ffmpeg version n6.1-5-g123456789"
            var parts = output.Split(' ');
            if (parts.Length < 3)
                return "unknown";

            var fullVersion = parts[2]; // n6.1-5-g123456789

            // On ne garde que "n6.1"
            var cleanVersion = fullVersion.Split('-')[0];

            return cleanVersion;
        }
        catch
        {
            return "unknown";
        }
    }

    // ---------------------------------------------------------
    // 3) Récupération version distante
    // ---------------------------------------------------------
    private async Task<string> GetLatestVersionAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("request");

            var json = await client.GetStringAsync(GithubLatestRelease);
            using var doc = JsonDocument.Parse(json);

            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "unknown";

            // On garde uniquement "n6.1"
            return tag.Split('-')[0];
        }
        catch
        {
            return "unknown";
        }
    }


    // ---------------------------------------------------------
    // 4) Téléchargement + extraction
    // ---------------------------------------------------------
    public async Task DownloadAndExtractAsync()
    {
        try
        {
            DownloadStarted?.Invoke();
            StatusChanged?.Invoke("Téléchargement de FFmpeg...");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("request");

            var json = await client.GetStringAsync(GithubLatestRelease);
            using var doc = JsonDocument.Parse(json);

            var asset = doc.RootElement
                .GetProperty("assets")
                .EnumerateArray()
                .First(a => a.GetProperty("name").GetString().EndsWith("win64-gpl.zip"));

            var url = asset.GetProperty("browser_download_url").GetString();
            var zipPath = Path.Combine(_binFolder, "ffmpeg.zip");

            // Téléchargement
            using (var stream = await client.GetStreamAsync(url))
            using (var file = File.Create(zipPath))
                await stream.CopyToAsync(file);

            StatusChanged?.Invoke("Extraction de FFmpeg...");

            // Dossier temporaire
            var tempDir = Path.Combine(_binFolder, "temp_extract");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            // Nettoyage des anciennes versions
            foreach (var dir in Directory.GetDirectories(_binFolder))
            {
                var name = Path.GetFileName(dir);

                if (name.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Delete(dir, true);
                }
            }

            // Supprimer les anciens binaires
            if (File.Exists(_ffmpegPath)) File.Delete(_ffmpegPath);
            if (File.Exists(_ffprobePath)) File.Delete(_ffprobePath);

            // Extraction dans le dossier temporaire
            ZipFile.ExtractToDirectory(zipPath, tempDir, true);
            File.Delete(zipPath);

            // Trouver le dossier "bin" dans l'arborescence extraite
            var binDir = Directory.GetDirectories(tempDir, "bin", SearchOption.AllDirectories)
                                  .FirstOrDefault();

            if (binDir == null)
                throw new Exception("Impossible de trouver le dossier 'bin' dans l'archive FFmpeg.");

            // Copier ffmpeg.exe et ffprobe.exe à la racine
            File.Copy(Path.Combine(binDir, "ffmpeg.exe"), _ffmpegPath, true);
            File.Copy(Path.Combine(binDir, "ffprobe.exe"), _ffprobePath, true);

            StatusChanged?.Invoke("FFmpeg mis à jour !");
        }
        finally
        {
            // Nettoyage
            var tempDir = Path.Combine(_binFolder, "temp_extract");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            DownloadCompleted?.Invoke();
        }
    }

    public void ExportReport(IEnumerable<AnalysisReportModel> entries, string baseName)
    {
        string path = AppPaths.GetReportPath(baseName);

        var sb = new StringBuilder();
        sb.AppendLine("FileName;FilePath;AudioCodecs;ContainsDTS;DtsTrackCount;VideoCodec;Duration;FileSizeBytes");

        foreach (var e in entries)
        {
            sb.AppendLine($"{e.FileName};{e.FilePath};{e.AudioCodecs};{e.ContainsDTS};{e.DtsTrackCount};{e.VideoCodec};{e.Duration};{e.FileSizeBytes}");
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
