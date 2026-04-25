using Convert.Core;
using Convert.Models;
using Convert.UI.Services;
using Convert.UI.ViewModels;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using System.Windows.Input;
using static Convert.Core.TranscodeJob;

public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<JobViewModel> Jobs { get; } = new();
    private readonly SemaphoreSlim _parallelLimiter = new SemaphoreSlim(4);
    public OptionsViewModel OptionsVM { get; }

    private JobViewModel _selectedJob;
    public JobViewModel SelectedJob
    {
        get => _selectedJob;
        set { _selectedJob = value; OnPropertyChanged(); }
    }

    public TranscodeOptions Options { get; } = new();
    public int MaxParallelJobs
    {
        get => _parallelLimiter.CurrentCount;
    }

    private readonly FFprobeService _probe;
    private readonly FFmpegEngine _engine;

    public ICommand AddFileCommand { get; }
    public ICommand AnalyzeAllCommand { get; }
    public ICommand TranscodeAllCommand { get; }
    public ICommand ClearCommand { get; }

    public MainViewModel()
    {
        OptionsVM = new OptionsViewModel(Options);
        Jobs = new ObservableCollection<JobViewModel>();
        var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        var ffprobePath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffprobe.exe");
        _probe = new FFprobeService(ffprobePath);
        _engine = new FFmpegEngine(ffmpegPath);

        AddFileCommand = new RelayCommand(async _ => await AddFileAsync());
        AnalyzeAllCommand = new RelayCommand(async _ => await AnalyzeAllAsync());
        TranscodeAllCommand = new RelayCommand(async _ => await TranscodeAllAsync());
        ClearCommand = new RelayCommand(_ => ClearAll());
    }

    private async Task AddFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Vidéos|*.mkv;*.mp4;*.mov;*.avi;*.ts;*.webm"
        };

        if (dialog.ShowDialog() == true)
        {
            var job = new TranscodeJob(dialog.FileName);
            job.SetPending();
            var vm = new JobViewModel(job, RemoveJob, AnalyzeOneAsync, TranscodeOneAsync);
            Jobs.Add(vm);
            //SelectedJob = vm;
            //vm.RefreshStatus();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private async Task AnalyzeAllAsync()
    {
        foreach (var job in Jobs)
        {
            job.Job.Mode = JobMode.AnalyzeOnly;
            await job.Job.RunAsync(
                    _probe,
                    _engine,
                    Options,
                    log => job.AppendLog(log));

            job.RefreshStatus();
        }
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
        await _parallelLimiter.WaitAsync();
        try
        {
            jobVM.Job.Mode = mode;
            await jobVM.Job.RunAsync(_probe, _engine, Options, log => jobVM.AppendLog(log));
        }
        finally
        {
            _parallelLimiter.Release();
        }
    }


    private void RemoveJob(JobViewModel jobVM)
    {
        // Si le job tourne encore, on le stoppe proprement
        jobVM.Job.Stop();
        Jobs.Remove(jobVM);
    }

    private void ClearAll()
    {
        foreach (var job in Jobs.ToList())
        {
            job.Job.Stop();   // stoppe FFmpeg si en cours
            Jobs.Remove(job); // supprime de la liste
        }
    }

    private async Task AnalyzeOneAsync(JobViewModel jobVM)
    {
        jobVM.Job.Mode = JobMode.AnalyzeOnly;
        await jobVM.Job.RunAsync(_probe, _engine, Options, log => jobVM.AppendLog(log));
    }

    private async Task TranscodeOneAsync(JobViewModel jobVM)
    {
        jobVM.Job.Mode = JobMode.Transcode;
        await jobVM.Job.RunAsync(_probe, _engine, Options, log => jobVM.AppendLog(log));
    }

}
