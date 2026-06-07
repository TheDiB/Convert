using Convert.Core;
using Convert.Models;
using Convert.UI.Models;
using Convert.UI.Services;
using Convert.UI.ViewModels;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

using static Convert.Core.TranscodeJob;

public class MainViewModel : ViewModelBase
{
    private CancellationTokenSource _globalCts = new();
    public bool IsStoppingAll { get; private set; }

    private bool _stopAllRequested = false;
    public bool StopAllRequested
    {
        get => _stopAllRequested;
        set { _stopAllRequested = value; OnPropertyChanged(); }
    }

    public ObservableCollection<JobViewModel> Jobs { get; } = new();
    private SemaphoreSlim _parallelLimiter;
    public OptionsViewModel OptionsVM { get; }
    public IDialogService Dialogs { get; }
    private SettingsService _settings;
    public FFmpegService FFmpeg { get; set; }

    public ISnackbarMessageQueue SnackbarMessageQueue { get; }
        = new SnackbarMessageQueue();

    public string AppVersion { get; private set; }
    public WindowState StartMaximized { get; private set; }

    private bool _isFFmpegChecking;
    public bool IsFFmpegChecking
    {
        get => _isFFmpegChecking;
        set
        {
            if (_isFFmpegChecking != value)
            {
                _isFFmpegChecking = value;
                OnPropertyChanged();
                RefreshInputs();
            }
        }
    }

    private bool _isFFmpegDownloading;
    public bool IsFFmpegDownloading
    {
        get => _isFFmpegDownloading;
        set
        {
            if (_isFFmpegDownloading != value)
            {
                _isFFmpegDownloading = value;
                OnPropertyChanged();
                RefreshInputs();
            }
        }
    }

    public bool IsFFmpegBusy => IsFFmpegChecking || IsFFmpegDownloading;
    public string FFmpegStatusMessage { get; set; } = "Initialisation FFmpeg...";
    public string FFmpegNotificationMessage
    {
        get => _ffmpegNotificationMessage;
        set
        {
            _ffmpegNotificationMessage = value;
            OnPropertyChanged();
        }
    }
    private string _ffmpegNotificationMessage = "";

    public bool CanTranscode => Jobs.Any() && !IsFFmpegBusy;
    public bool CanAnalyze => Jobs.Any() && !IsFFmpegBusy;
    public bool CanStopAll
    {
        get
        {
            return true;
        }
    }

    private JobViewModel _selectedJob;
    public JobViewModel SelectedJob
    {
        get => _selectedJob;
        set
        {
            _selectedJob = value;
            OnPropertyChanged();
            LoadTracksFromSelectedJob();
        }
    }

    public TranscodeOptions Options { get; } = new();
    public int MaxParallelJobs => _parallelLimiter.CurrentCount;

    private readonly FFprobeService _probe;
    private readonly FFmpegEngine _engine;
    private readonly MkvmergeService _mkvmerge;
    private readonly MkvmergeEngine _mkvEngine;

    public ICommand AddFileCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand AnalyzeAllCommand { get; }
    public ICommand TranscodeAllCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ClearDoneCommand { get; }
    public ICommand StopAllCommand { get; }

    public MainViewModel(SettingsService settings, IDialogService dialogs, FFmpegService ffmpeg)
    {
        _settings = settings;
        Dialogs = dialogs;
        FFmpeg = ffmpeg;

        var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
        AppVersion = string.Concat("Convert v", version.FileMajorPart, '.', version.FileMinorPart, '.', version.FileBuildPart);

        StartMaximized = _settings.Settings.StartMaximized ? WindowState.Maximized : WindowState.Normal;
        _parallelLimiter = new SemaphoreSlim(_settings.Settings.MaxParallelJobs);

        OptionsVM = new OptionsViewModel(Options);
        OptionsVM.Options.Container = _settings.Settings.Container;
        OptionsVM.Options.DumpDebugFiles = _settings.Settings.DumpDebugFiles;
        OptionsVM.Options.CompatibilityMode = _settings.Settings.CompatibilityMode;

        Jobs = new ObservableCollection<JobViewModel>();

        var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        var ffprobePath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffprobe.exe");
        _probe = new FFprobeService(ffprobePath);
        _engine = new FFmpegEngine(ffmpegPath);
        _mkvmerge = new MkvmergeService();
        _mkvEngine = new MkvmergeEngine(_mkvmerge.ExecutablePath);

        AddFileCommand = new RelayCommand(async _ => await AddFileAsync());
        AddFolderCommand = new RelayCommand(_ => AddFolder());
        AnalyzeAllCommand = new RelayCommand(async _ => await AnalyzeAllAsync());
        TranscodeAllCommand = new RelayCommand(async _ => await TranscodeAllAsync());
        ClearCommand = new RelayCommand(_ => ClearAll());
        ClearDoneCommand = new RelayCommand(_ => ClearDone(), () => Jobs.Any(s => s.Status == "Done"));
        StopAllCommand = new RelayCommand(async _ => StopAll(), () => !IsStoppingAll);

        Jobs.CollectionChanged += (_, __) => RefreshInputs();
    }

    public void ReloadSettings()
    {
        OptionsVM.Options.Container = _settings.Settings.Container;
        OptionsVM.Options.DumpDebugFiles = _settings.Settings.DumpDebugFiles;
        OptionsVM.Options.CompatibilityMode = _settings.Settings.CompatibilityMode;
    }

    private async Task AddFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"Vidéos|{string.Join(";", _settings.Settings.SupportedFileTypes.Split(',').Select(ext => $"*.{ext}"))}",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var f in dialog.FileNames)
            {
                var vm = AddJobFromFile(f);

                if (_settings.Settings.AutoAnalyze)
                {
                    await Task.Yield(); // laisse le temps au constructeur de VM de finir
                    _ = AnalyzeOneAsync(vm);
                }
            }

            if (dialog.FileNames.Length == 1)
                Notify($"Fichier ajouté avec succès", NotificationLevel.Info);
            else if (dialog.FileNames.Length > 1)
                Notify($"{dialog.FileNames.Length} fichiers ajoutés avec succès", NotificationLevel.Info);
        }
    }

    private async void AddFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = false,
            Title = "Sélectionnez un dossier"
        };
        bool? result = dialog.ShowDialog();
        if (result == true)
        {
            var path = dialog.FolderName;
            var validExtensions = _settings.Settings.SupportedFileTypes.Split(',').Select(ext => $".{ext}").ToArray();

            var files = Directory
                .EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLower()));

            foreach (var file in files)
            {
                var vm = AddJobFromFile(file);
                if (_settings.Settings.AutoAnalyze)
                {
                    await Task.Yield(); // laisse le temps au constructeur de VM de finir
                    _ = AnalyzeOneAsync(vm);
                }
            }

            if (files.Count() > 0)
                Notify($"{files.Count()} fichier(s) ajouté(s) avec succès", NotificationLevel.Info);
        }
    }

    public void Notify(string message, NotificationLevel level)
    {
        SnackbarMessageQueue.Enqueue(message);

        if (_settings.Settings.EnableWindowsNotifications && (level == NotificationLevel.Warning
            || level == NotificationLevel.Error || level == NotificationLevel.Critical || level == NotificationLevel.Success))
            WindowsNotificationService.Show(message);
    }

    private async Task AnalyzeAllAsync()
    {
        var entries = new List<AnalysisReportModel>();

        foreach (var job in Jobs)
        {
            SelectedJob = job;
            job.Job.Mode = JobMode.AnalyzeOnly;

            await job.Job.RunAsync(
                _probe,
                _engine,
                _mkvEngine,
                Options,
                log => job.AppendLog(log),
                _globalCts.Token,
                () => StopAllRequested);

            LoadTracksFromSelectedJob();

            job.RefreshStatus();
        }
    }

    private async Task TranscodeAllAsync()
    {
        StopAllRequested = false;
        var tasks = new List<Task>();

        foreach (var jobVM in Jobs)
        {
            switch (jobVM.Job.Status)
            {
                case "Error":
                case "Failed":
                case "Canceled":
                    jobVM.Job.ResetForRetry();
                    break;

                case "Done":
                    continue; // on ne touche pas aux jobs terminés
            }

            tasks.Add(RunJobWithLimit(jobVM, JobMode.Transcode));
        }

        await Task.WhenAll(tasks);
    }


    private async Task RunJobWithLimit(JobViewModel jobVM, JobMode mode)
    {
        if (StopAllRequested)
        {
            jobVM.Job.Status = "Canceled";
            return;
        }

        await _parallelLimiter.WaitAsync();

        try
        {
            if (StopAllRequested)
            {
                jobVM.Job.Status = "Canceled";
                return;
            }

            jobVM.Job.Mode = mode;

            Options.AudioTrackProfiles.Clear();
            foreach (var track in jobVM.AudioTracks)
                Options.AudioTrackProfiles[track.Index] = track.SelectedProfile;

            Options.VideoTrackProfiles.Clear();
            foreach (var track in jobVM.VideoTracks)
                Options.VideoTrackProfiles[track.Index] = track.SelectedProfile;

            Options.SubtitleTrackProfiles.Clear();
            foreach (var track in jobVM.SubtitleTracks)
                Options.SubtitleTrackProfiles[track.Index] = track.SelectedProfile;

            var result = await jobVM.Job.RunAsync(
                _probe,
                _engine,
                _mkvEngine,
                Options,
                log => jobVM.AppendLog(log),
                _globalCts.Token,
                () => StopAllRequested);

            jobVM.Job.Status = result switch
            {
                JobResult.Success => "Done",
                JobResult.Canceled => "Canceled",
                JobResult.Failed => "Failed",
                JobResult.Error => "Error",
                _ => "Unknown"
            };

            // 🔥 Vérifier si tous les jobs sont terminés
            bool allFinished = Jobs.All(j =>
                j.Job.Status is "Done" or "Canceled" or "Failed" or "Error");

            // 🔥 Vérifier s'il y a des erreurs
            bool hasErrors = Jobs.Any(j =>
                j.Job.Status is "Failed" or "Error");

            if (allFinished)
            {
                if (hasErrors)
                    Notify("Toutes les tâches sont terminées, des erreurs sont survenues", NotificationLevel.Warning);
                else
                    Notify("Toutes les tâches sont terminées !", NotificationLevel.Success);
            }
        }
        catch (Exception)
        {
            Notify("Une erreur est survenue !", NotificationLevel.Error);
        }
        finally
        {
            _parallelLimiter.Release();
        }
    }

    private JobViewModel AddJobFromFile(string filePath)
    {
        if (Jobs.Any(j => j.FileName == Path.GetFileName(filePath)))
            return null;

        StopAllRequested = false;

        var job = new TranscodeJob(filePath);
        job.SetPending();
        job.AnalysisCompleted += OnAnalysisCompleted;

        var vm = new JobViewModel(job, RemoveJob, AnalyzeOneAsync, TranscodeOneAsync, _settings, FFmpeg);
        Jobs.Add(vm);

        return vm;
    }

    private void RemoveJob(JobViewModel jobVM)
    {
        jobVM.Job.Stop();
        Jobs.Remove(jobVM);

        if (SelectedJob == jobVM)
            SelectedJob = null;
    }

    private void ClearAll()
    {
        foreach (var job in Jobs.ToList())
        {
            job.Job.Stop();
            Jobs.Remove(job);
        }

        SelectedJob = null;
        Notify($"Liste d'attente effacée", NotificationLevel.Info);
    }
    private void ClearDone()
    {
        foreach (var job in Jobs.Where(j => j.Status == "Done").ToList())
            Jobs.Remove(job);

        SelectedJob = null;

        Notify($"Jobs terminés retirés de file", NotificationLevel.Info);
    }

    private void StopAll()
    {
        if (StopAllRequested)
            return;

        StopAllRequested = true;
        IsStoppingAll = true;
        _globalCts.Cancel();

        foreach (var job in Jobs)
        {
            switch (job.Status)
            {
                case "Pending":
                case "Analyzing":
                case "Transcoding":
                    job.Job.Cts.Cancel();
                    job.Job.Cts.Dispose();
                    job.Job.Cts = new CancellationTokenSource();
                    job.Job.Status = "Canceled";
                    break;
            }
        }

        Notify("Toutes les tâches ont été stoppées", NotificationLevel.Info);
        StopAllRequested = false;
        IsStoppingAll = false;
        _globalCts = new CancellationTokenSource();
    }

    private async Task AnalyzeOneAsync(JobViewModel jobVM)
    {
        jobVM.Job.Mode = JobMode.AnalyzeOnly;

        Options.AudioTrackProfiles.Clear();
        foreach (var track in jobVM.AudioTracks)
            Options.AudioTrackProfiles[track.Index] = track.SelectedProfile;

        Options.VideoTrackProfiles.Clear();
        foreach (var track in jobVM.VideoTracks)
            Options.VideoTrackProfiles[track.Index] = track.SelectedProfile;

        Options.SubtitleTrackProfiles.Clear();
        foreach (var track in jobVM.SubtitleTracks)
            Options.SubtitleTrackProfiles[track.Index] = track.SelectedProfile;

        await jobVM.Job.RunAsync(
            _probe,
            _engine,
            _mkvEngine,
            Options,
            log => jobVM.AppendLog(log),
            _globalCts.Token,
            () => StopAllRequested);

        LoadTracksFromSelectedJob();
        RefreshInputs();
    }

    private async Task TranscodeOneAsync(JobViewModel jobVM)
    {
        StopAllRequested = false;

        // Si le job est en erreur → on le remet à zéro
        if (jobVM.Job.Status == "Error" || jobVM.Job.Status == "Failed" || jobVM.Job.Status == "Canceled")
        {
            jobVM.Job.ResetForRetry();
        }

        jobVM.Job.Status = "Queued";

        await RunJobWithLimit(jobVM, JobMode.Transcode);
        RefreshInputs();
    }

    private void LoadTracksFromSelectedJob()
    {
        if (SelectedJob?.Job?.Analysis == null)
            return;

        //
        // AUDIO
        //
        foreach (var audio in SelectedJob.Job.Analysis.AudioStreams)
        {
            var existing = SelectedJob.AudioTracks.FirstOrDefault(t => t.Index == audio.Index);

            if (existing == null)
            {
                SelectedJob.AudioTracks.Add(new AudioTrackViewModel
                {
                    Index = audio.Index,
                    Codec = AudioLanguageStreamInfo.CodecMap.TryGetValue(audio.Codec, out var pretty)
                            ? pretty
                            : audio.Codec,
                    Channels = audio.Channels,
                    LanguageName = AudioLanguageStreamInfo.LanguageMap.TryGetValue(audio.Language, out var lang)
                            ? lang
                            : audio.Language,
                    Bitrate = audio.Bitrate,
                    Title = audio.Title
                });
            }
            else
            {
                // On met à jour les infos techniques
                existing.Channels = audio.Channels;
                existing.LanguageName = AudioLanguageStreamInfo.LanguageMap.TryGetValue(audio.Language, out var lang)
                            ? lang
                            : audio.Language;
                existing.Bitrate = audio.Bitrate;
                existing.Title = audio.Title;

                // MAIS ON NE TOUCHE PAS À existing.Codec
            }
        }

        //
        // VIDEO
        //
        foreach (var video in SelectedJob.Job.Analysis.VideoStreams)
        {
            var existing = SelectedJob.VideoTracks.FirstOrDefault(t => t.Index == video.Index);

            if (existing == null)
            {
                SelectedJob.VideoTracks.Add(new VideoTrackViewModel
                {
                    Index = video.Index,
                    Codec = VideoLanguageStreamInfo.CodecMap.TryGetValue(video.Codec, out var pretty)
                            ? pretty
                            : video.Codec,
                    Width = video.Width,
                    Height = video.Height,
                    FPS = video.FPS,
                    Bitrate = video.Bitrate
                });
            }
            else
            {
                existing.Width = video.Width;
                existing.Height = video.Height;
                existing.FPS = video.FPS;
                existing.Bitrate = video.Bitrate;

                // NE PAS toucher à existing.Codec
            }
        }

        //
        // SOUS-TITRES
        //
        foreach (var sub in SelectedJob.Job.Analysis.SubtitleStreams)
        {
            var existing = SelectedJob.SubtitleTracks.FirstOrDefault(t => t.Index == sub.Index);

            if (existing == null)
            {
                var vm = new SubtitleTrackViewModel
                {
                    Index = sub.Index,
                    RawCodec = sub.Codec,
                    Codec = SubtitleStreamInfo.CodecMap.TryGetValue(sub.Codec, out var pretty)
                            ? pretty
                            : sub.Codec,
                    LanguageName = SubtitleStreamInfo.LanguageMap.TryGetValue(sub.Language, out var lang)
                            ? lang
                            : sub.Language,
                    Title = sub.Title
                };

                if (vm.IsBitmap)
                {
                    vm.SetAvailableProfiles(new[]
                    {
                        new SubtitleProfileItem(SubtitleProfile.Copy, "Copier (sans modification)"),
                        new SubtitleProfileItem(SubtitleProfile.Ignore, "Ignorer cette piste")
                    });

                    vm.SelectedProfile = SubtitleProfile.Copy;
                }
                else
                {
                    vm.SetAvailableProfiles(new[]
                    {
                        new SubtitleProfileItem(SubtitleProfile.Copy, "Copier (sans modification)"),
                        new SubtitleProfileItem(SubtitleProfile.ConvertToSrt, "Conversion (vers SRT)"),
                        new SubtitleProfileItem(SubtitleProfile.Ignore, "Ignorer cette piste")
                    });

                    vm.SelectedProfile = SubtitleProfile.Copy;
                }

                SelectedJob.SubtitleTracks.Add(vm);
            }
            else
            {
                existing.RawCodec = sub.Codec;
                existing.LanguageName = SubtitleStreamInfo.LanguageMap.TryGetValue(sub.Language, out var lang)
                        ? lang
                        : sub.Language;
                existing.Title = sub.Title;

                if (existing.IsBitmap)
                {
                    existing.SetAvailableProfiles(new[]
                    {
                new SubtitleProfileItem(SubtitleProfile.Copy, "Copier (sans modification)"),
                new SubtitleProfileItem(SubtitleProfile.Ignore, "Ignorer cette piste")
            });

                    if (existing.SelectedProfile == SubtitleProfile.ConvertToSrt)
                        existing.SelectedProfile = SubtitleProfile.Copy;
                }
                else
                {
                    existing.SetAvailableProfiles(new[]
                    {
                new SubtitleProfileItem(SubtitleProfile.Copy, "Copier (sans modification)"),
                new SubtitleProfileItem(SubtitleProfile.ConvertToSrt, "Conversion (vers SRT)"),
                new SubtitleProfileItem(SubtitleProfile.Ignore, "Ignorer cette piste")
            });
                }
            }
        }
    }

    private void RefreshInputs()
    {
        OnPropertyChanged(nameof(IsFFmpegBusy));
        OnPropertyChanged(nameof(CanTranscode));
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(CanStopAll));
    }

    private void OnAnalysisCompleted(FileAnalysisResult analysis)
    {
        if (!_settings.Settings.EnableReports)
            return;

        var entry = analysis.ToReportEntry();
        FFmpeg.ExportReport(new[] { entry }, "Convert_Unique_Analysis");
    }
    public async Task InitializeMkvmergeAsync()
    {
        await _mkvmerge.EnsureExistsAsync();
    }
}
