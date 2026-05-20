using Convert.Core;
using Convert.Models;
using Convert.UI.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace Convert.UI.ViewModels
{
    public class JobViewModel : ViewModelBase
    {
        private readonly TranscodeJob _job;
        private readonly SettingsService _settings;
        private readonly FFmpegService _ffmpeg;
        private readonly StringBuilder _log = new();

        private readonly Action<JobViewModel> _onDelete;
        private readonly Func<JobViewModel, Task> _onAnalyze;
        private readonly Func<JobViewModel, Task> _onTranscode;

        public TranscodeJob Job => _job;
        public string FileName => System.IO.Path.GetFileName(_job.InputPath);
        public string Status => _job.Status;
        public double Progress => _job.Progress;
        public string Log => _log.ToString();

        public bool IsRunning => _job.Status == "Transcoding" || _job.Status == "Analyzing";

        public ObservableCollection<AudioTrackViewModel> AudioTracks { get; } = new ObservableCollection<AudioTrackViewModel>();
        public ObservableCollection<VideoTrackViewModel> VideoTracks { get; } = new ObservableCollection<VideoTrackViewModel>();

        public ICommand StopCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand TranscodeCommand { get; }

        public JobViewModel(
            TranscodeJob job,
            Action<JobViewModel> onDelete,
            Func<JobViewModel, Task> onAnalyze,
            Func<JobViewModel, Task> onTranscode,
            SettingsService settings,
            FFmpegService ffmpeg)
        {
            _job = job;
            _onDelete = onDelete;
            _onAnalyze = onAnalyze;
            _onTranscode = onTranscode;
            _settings = settings;
            _ffmpeg = ffmpeg;

            _job.StatusChanged += () =>
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(IsRunning));
            };

            _job.ProgressChanged += () => OnPropertyChanged(nameof(Progress));

            StopCommand = new RelayCommand(_ => _job.Stop());
            DeleteCommand = new RelayCommand(_ => _onDelete(this));
            AnalyzeCommand = new RelayCommand(async _ => await _onAnalyze(this));
            TranscodeCommand = new RelayCommand(async _ => await _onTranscode(this));
        }

        public void AppendLog(string line)
        {
            if (line == null) return;

            _log.AppendLine(line);
            OnPropertyChanged(nameof(Log));
        }

        public void RefreshStatus()
        {
            OnPropertyChanged(nameof(Status));
        }
    }
}
