using Convert.Core;
using Convert.Models;
using Convert.UI.Services;
using Convert.UI.ViewModels;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
    private JobViewModel _selectedJob;
    private SettingsService _settings;
    public FFmpegService FFmpeg { get; set; }

    public ISnackbarMessageQueue SnackbarMessageQueue { get; }
        = new SnackbarMessageQueue();

    public string AppVersion { get; private set; }

    public string SelectedContainer { get; private set; }
    public string SelectedVideoCodec { get; private set; }
    public string SelectedAudioCodec { get; private set; }
    public string PreferredVideoEngine { get; private set; }

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
            if (!Jobs.Any())
                return false;
            else
            {
                if (Jobs.Any(j => j.Status == "Transcoding" || j.Status == "Analyzing"))
                    return true;
                else
                {
                    if (Jobs.All(j => j.Status == "Stopped" || j.Status == "Pending" || j.Status == "Done" || j.Status == "Faled" || j.Status == "Error" || j.Status == "Failed"))
                        return false;
                    else
                        return true;
                }
            }
        }
    }

    public JobViewModel SelectedJob
    {
        get => _selectedJob;
        set { _selectedJob = value; OnPropertyChanged(); LoadAudioTracksFromSelectedJob(); }
    }

    public TranscodeOptions Options { get; } = new();
    public int MaxParallelJobs
    {
        get => _parallelLimiter.CurrentCount;
    }

    public ObservableCollection<AudioProfileItem> AudioProfiles { get; } =
        new ObservableCollection<AudioProfileItem>
        {
        new AudioProfileItem(AudioProfile.Copy, "Copie (sans modification)"),
        new AudioProfileItem(AudioProfile.Eac3_5_1, "Dolby Digital Plus EAC3 5.1 (640 kbps)"),
        new AudioProfileItem(AudioProfile.Ac3_5_1, "Dolby Digital AC3 5.1 (640 kbps)"),
        new AudioProfileItem(AudioProfile.Ac3_2_0, "Stéréo AC3 2.0 (192 kbps)"),
        new AudioProfileItem(AudioProfile.Mp3_2_0, "Stéréo MP3 2.0 (320 kbps)")
        };

    public ObservableCollection<AudioTrackViewModel> AudioTracks { get; }
    = new ObservableCollection<AudioTrackViewModel>();

    private readonly FFprobeService _probe;
    private readonly FFmpegEngine _engine;

    public ICommand AddFileCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand AnalyzeAllCommand { get; }
    public ICommand TranscodeAllCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand StopAllCommand { get; }

    public MainViewModel(SettingsService settings, IDialogService dialogs, FFmpegService ffmpeg)
    {
        _settings = settings;
        Dialogs = dialogs;
        FFmpeg = ffmpeg;

        var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
        AppVersion = string.Concat("Convert v", version.FileMajorPart, '.', version.FileMinorPart);

        // appliquer les valeurs par défaut
        SelectedContainer = _settings.Settings.DefaultContainer;
        SelectedVideoCodec = _settings.Settings.DefaultVideoCodec;
        SelectedAudioCodec = _settings.Settings.DefaultAudioCodec;
        _parallelLimiter = new SemaphoreSlim(_settings.Settings.MaxParallelJobs);

        OptionsVM = new OptionsViewModel(Options);
        Jobs = new ObservableCollection<JobViewModel>();
        var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        var ffprobePath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffprobe.exe");
        _probe = new FFprobeService(ffprobePath);
        _engine = new FFmpegEngine(ffmpegPath);

        AddFileCommand = new RelayCommand(async _ => await AddFileAsync());
        AddFolderCommand = new RelayCommand(_ => AddFolder());
        AnalyzeAllCommand = new RelayCommand(async _ => await AnalyzeAllAsync());
        TranscodeAllCommand = new RelayCommand(async _ => await TranscodeAllAsync());
        ClearCommand = new RelayCommand(_ => ClearAll());
        StopAllCommand = new RelayCommand(async _ => StopAll(), () => !IsStoppingAll);

        Jobs.CollectionChanged += (_, __) =>
        {
            RefreshInputs();
        };
    }

    public void ReloadSettings()
    {
        // Réappliquer les valeurs par défaut
        SelectedContainer = _settings.Settings.DefaultContainer;
        SelectedVideoCodec = _settings.Settings.DefaultVideoCodec;
        SelectedAudioCodec = _settings.Settings.DefaultAudioCodec;
        PreferredVideoEngine = _settings.Settings.PreferredVideoEngine;
    }

    private async Task AddFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"Vidéos|{string.Join(";", _settings.Settings.SupportedFileTypes.Split(',').Select(ext => $"*.{ext}"))}"
        };

        if (dialog.ShowDialog() == true)
        {
            AddJobFromFile(dialog.FileName);
            Notify($"Fichier ajouté avec succès");
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
                AddJobFromFile(file);
            }

            if (files.Count() > 0)
                Notify($"{files.Count()} fichier(s) ajouté(s) avec succès");
        }
    }

    public void Notify(string message)
    {
        SnackbarMessageQueue.Enqueue(message);
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
                    Options,
                    log => job.AppendLog(log), _globalCts.Token, () => StopAllRequested);

            LoadAudioTracksFromSelectedJob();

            if (job.Job.Status != "Error")
                entries.Add(job.Job.Analysis.ToReportEntry());
            else
                entries.Add(new AnalysisReportModel { FilePath = job.Job.InputPath, FileName = Path.GetFileName(job.Job.InputPath), VideoCodec = "unknown", AudioCodecs = "unknown", FileSizeBytes = new FileInfo(job.Job.InputPath).Length });

            job.RefreshStatus();
        }

        if (_settings.Settings.EnableReports)
            FFmpeg.ExportReport(entries, "Convert_Global_Analysis");
    }

    private async Task TranscodeAllAsync()
    {
        var tasks = new List<Task>();

        foreach (var jobVM in Jobs)
        {
            tasks.Add(RunJobWithLimit(jobVM, JobMode.Transcode));
        }

        await Task.WhenAll(tasks);
    }

    private async Task RunJobWithLimit(JobViewModel jobVM, JobMode mode)
    {
        // Si STOP ALL est actif → on annule immédiatement ce job
        if (StopAllRequested)
        {
            jobVM.Job.Status = "Canceled";
            return;
        }

        await _parallelLimiter.WaitAsync();

        try
        {
            // Double sécurité : STOP ALL peut avoir été activé entre temps
            if (StopAllRequested)
            {
                jobVM.Job.Status = "Canceled";
                return;
            }

            jobVM.Job.Mode = mode;

            Options.AudioTrackProfiles.Clear();
            foreach (var track in AudioTracks)
            {
                Options.AudioTrackProfiles[track.Index] = track.SelectedProfile;
            }

            await jobVM.Job.RunAsync(
                _probe,
                _engine,
                Options,
                log => jobVM.AppendLog(log),
                _globalCts.Token,
                () => StopAllRequested
            );
        }
        finally
        {
            _parallelLimiter.Release();
        }
    }


    private void AddJobFromFile(string filePath)
    {
        if (Jobs.Any(j => j.FileName == Path.GetFileName(filePath)))
            return;

        StopAllRequested = false;
        var job = new TranscodeJob(filePath);
        job.SetPending();
        var vm = new JobViewModel(job, RemoveJob, AnalyzeOneAsync, TranscodeOneAsync);
        Jobs.Add(vm);
        AudioTracks.Clear();
    }

    private void RemoveJob(JobViewModel jobVM)
    {
        // Si le job tourne encore, on le stoppe proprement
        jobVM.Job.Stop();
        Jobs.Remove(jobVM);

        if (SelectedJob == jobVM)
        {
            SelectedJob = null;
            AudioTracks.Clear();
        }
    }

    private void ClearAll()
    {
        foreach (var job in Jobs.ToList())
        {
            job.Job.Stop();   // stoppe FFmpeg si en cours
            Jobs.Remove(job); // supprime de la liste
        }

        AudioTracks.Clear();
        SelectedJob = null;

        Notify($"Liste d'attente effacée");
    }

    private void StopAll()
    {
        if (StopAllRequested)
            return; // ← clics suivants ignorés

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
                    job.Job.Cts = new CancellationTokenSource(); // ← IMPORTANT
                    job.Job.Status = "Canceled";
                    break;

                case "Done":
                case "Failed":
                case "Canceled":
                    // on ne touche pas
                    break;
            }
        }

        Notify("Toutes les tâches ont été stoppées");
        IsStoppingAll = false;

        // On recrée un token pour la prochaine session
        _globalCts = new CancellationTokenSource();
    }

    private async Task AnalyzeOneAsync(JobViewModel jobVM)
    {
        jobVM.Job.Mode = JobMode.AnalyzeOnly;
        Options.AudioTrackProfiles.Clear();
        foreach (var track in AudioTracks)
        {
            Options.AudioTrackProfiles[track.Index] = track.SelectedProfile;
        }
        await jobVM.Job.RunAsync(_probe, _engine, Options, log => jobVM.AppendLog(log), _globalCts.Token, () => StopAllRequested);
        LoadAudioTracksFromSelectedJob();
        RefreshInputs();

        AnalysisReportModel reportEntry;
        if (jobVM.Job.Status != "Error")
            reportEntry = jobVM.Job.Analysis.ToReportEntry();
        else
            reportEntry = new AnalysisReportModel { FilePath = jobVM.Job.InputPath, FileName = Path.GetFileName(jobVM.Job.InputPath), VideoCodec = "unknown", AudioCodecs = "unknown", FileSizeBytes = new FileInfo(jobVM.Job.InputPath).Length };

        if (_settings.Settings.EnableReports)
            FFmpeg.ExportReport(new[] { reportEntry }, "Convert_Single_Analysis");
    }

    private async Task TranscodeOneAsync(JobViewModel jobVM)
    {
        jobVM.Job.Mode = JobMode.Transcode;

        Options.AudioTrackProfiles.Clear();
        foreach (var track in AudioTracks)
        {
            Options.AudioTrackProfiles[track.Index] = track.SelectedProfile;
        }

        await jobVM.Job.RunAsync(_probe, _engine, Options, log => jobVM.AppendLog(log), _globalCts.Token, () => StopAllRequested);
        RefreshInputs();
    }

    private void LoadAudioTracksFromSelectedJob()
    {
        AudioTracks.Clear();

        if (SelectedJob?.Job?.Analysis?.AudioStreams == null)
            return;

        foreach (var audio in SelectedJob.Job.Analysis.AudioStreams)
        {
            AudioTracks.Add(new AudioTrackViewModel
            {
                Index = audio.Index,
                Codec = audio.Codec,
                Channels = audio.Channels,
                LanguageName = audio.Language
            });
        }
    }

    private void RefreshInputs()
    {
        OnPropertyChanged(nameof(IsFFmpegBusy));
        OnPropertyChanged(nameof(CanTranscode));
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(CanStopAll));
    }
}
