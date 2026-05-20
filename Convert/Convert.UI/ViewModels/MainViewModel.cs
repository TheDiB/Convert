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
    private SettingsService _settings;
    public FFmpegService FFmpeg { get; set; }

    public ISnackbarMessageQueue SnackbarMessageQueue { get; }
        = new SnackbarMessageQueue();

    public string AppVersion { get; private set; }

    public string SelectedContainer { get; private set; }
    public string SelectedVideoCodec { get; private set; }
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
            LoadAudioTracksFromSelectedJob();
        }
    }

    public TranscodeOptions Options { get; } = new();
    public int MaxParallelJobs => _parallelLimiter.CurrentCount;

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

        SelectedContainer = _settings.Settings.DefaultContainer;
        SelectedVideoCodec = _settings.Settings.DefaultVideoCodec;
        PreferredVideoEngine = _settings.Settings.PreferredVideoEngine;

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

        Jobs.CollectionChanged += (_, __) => RefreshInputs();
    }

    public void ReloadSettings()
    {
        SelectedContainer = _settings.Settings.DefaultContainer;
        SelectedVideoCodec = _settings.Settings.DefaultVideoCodec;
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
            var vm = AddJobFromFile(dialog.FileName);
            Notify($"Fichier ajouté avec succès");

            if (_settings.Settings.AutoAnalyze)
            {
                await Task.Yield(); // laisse le temps au constructeur de VM de finir
                _ = AnalyzeOneAsync(vm);
            }
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
                log => job.AppendLog(log),
                _globalCts.Token,
                () => StopAllRequested);

            LoadAudioTracksFromSelectedJob();

            //if (job.Job.Status != "Error")
            //    entries.Add(job.Job.Analysis.ToReportEntry());
            //else
            //    entries.Add(new AnalysisReportModel
            //    {
            //        FilePath = job.Job.InputPath,
            //        FileName = Path.GetFileName(job.Job.InputPath),
            //        VideoCodec = "unknown",
            //        AudioCodecs = "unknown",
            //        FileSizeBytes = new FileInfo(job.Job.InputPath).Length
            //    });

            job.RefreshStatus();
        }

        //if (_settings.Settings.EnableReports)
        //    FFmpeg.ExportReport(entries, "Convert_Global_Analysis");
    }

    private async Task TranscodeAllAsync()
    {
        var tasks = new List<Task>();

        foreach (var jobVM in Jobs)
            tasks.Add(RunJobWithLimit(jobVM, JobMode.Transcode));

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

            var result = await jobVM.Job.RunAsync(
                _probe,
                _engine,
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
        Notify($"Liste d'attente effacée");
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

        Notify("Toutes les tâches ont été stoppées");
        IsStoppingAll = false;

        _globalCts = new CancellationTokenSource();
    }

    private async Task AnalyzeOneAsync(JobViewModel jobVM)
    {
        jobVM.Job.Mode = JobMode.AnalyzeOnly;

        Options.AudioTrackProfiles.Clear();
        foreach (var track in jobVM.AudioTracks)
            Options.AudioTrackProfiles[track.Index] = track.SelectedProfile;

        await jobVM.Job.RunAsync(
            _probe,
            _engine,
            Options,
            log => jobVM.AppendLog(log),
            _globalCts.Token,
            () => StopAllRequested);

        LoadAudioTracksFromSelectedJob();
        RefreshInputs();

        //if (_settings.Settings.EnableReports)
        //{
        //    var entries = new List<AnalysisReportModel>();
        //    if (jobVM.Job.Status != "Error")
        //        entries.Add(jobVM.Job.Analysis.ToReportEntry());
        //    else
        //        entries.Add(new AnalysisReportModel
        //        {
        //            FilePath = jobVM.Job.InputPath,
        //            FileName = Path.GetFileName(jobVM.Job.InputPath),
        //            VideoCodec = "unknown",
        //            AudioCodecs = "unknown",
        //            FileSizeBytes = new FileInfo(jobVM.Job.InputPath).Length
        //        });
        //    FFmpeg.ExportReport(entries, "Convert_Unique_Analysis");
        //}
    }

    private async Task TranscodeOneAsync(JobViewModel jobVM)
    {
        jobVM.Job.Status = "Queued";
        await RunJobWithLimit(jobVM, JobMode.Transcode);
        jobVM.Job.Status = "Done";
        RefreshInputs();
    }

    private void LoadAudioTracksFromSelectedJob()
    {
        if (SelectedJob?.Job?.Analysis?.AudioStreams == null)
            return;

        var tracks = SelectedJob.AudioTracks;
        tracks.Clear();

        foreach (var audio in SelectedJob.Job.Analysis.AudioStreams)
        {
            tracks.Add(new AudioTrackViewModel
            {
                Index = audio.Index,
                Codec = AudioLanguageStreamInfo.CodecMap.ContainsKey(audio.Codec)
                        ? AudioLanguageStreamInfo.CodecMap[audio.Codec]
                        : audio.Codec,
                Channels = audio.Channels,
                LanguageName = AudioLanguageStreamInfo.LanguageMap.ContainsKey(audio.Language)
                        ? AudioLanguageStreamInfo.LanguageMap[audio.Language]
                        : audio.Language,
                Bitrate = audio.Bitrate,
                Title = audio.Title
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

    private void OnAnalysisCompleted(FileAnalysisResult analysis)
    {
        if (!_settings.Settings.EnableReports)
            return;

        var entry = analysis.ToReportEntry();
        FFmpeg.ExportReport(new[] { entry }, "Convert_Unique_Analysis");
    }
}
