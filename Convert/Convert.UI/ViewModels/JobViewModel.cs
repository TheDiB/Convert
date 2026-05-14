using Convert.Core;
using Convert.UI.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace Convert.UI.ViewModels
{
    public class JobViewModel : ViewModelBase
    {
        public TranscodeJob Job => _job;
        private readonly TranscodeJob _job;
        private readonly StringBuilder _log = new();

        public string FileName => System.IO.Path.GetFileName(_job.InputPath);
        public string Status => _job.Status;
        public double Progress => _job.Progress;
        public string Log => _log.ToString();

        public ICommand StopCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand TranscodeCommand { get; }

        public bool IsRunning => _job.Status == "Transcoding" || _job.Status == "Analyzing";

        public ObservableCollection<AudioTrackViewModel> AudioTracks { get; } = new ObservableCollection<AudioTrackViewModel>();

        public JobViewModel(TranscodeJob job, Action<JobViewModel> onDelete, Func<JobViewModel, Task> onAnalyze, Func<JobViewModel, Task> onTranscode)
        {
            _job = job;

            StopCommand = new RelayCommand(_ => _job.Stop());
            DeleteCommand = new RelayCommand(_ => onDelete(this));
            AnalyzeCommand = new RelayCommand(async _ => await onAnalyze(this));
            TranscodeCommand = new RelayCommand(async _ => await onTranscode(this));

            _job.StatusChanged += () =>
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(IsRunning));
            };

            _job.ProgressChanged += () => OnPropertyChanged(nameof(Progress));
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
