using Convert.Models;
using Convert.UI.Services;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Windows.Media.Core;

namespace Convert.UI.ViewModels
{
    public class AnalysisViewModel : ViewModelBase
    {
        private string _analysisText;
        public string AnalysisText
        {
            get => _analysisText;
            set => SetProperty(ref _analysisText, value);
        }

        private string _filename;
        public string Filename
        {
            get => _filename;
            set => SetProperty(ref _filename, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ICommand CloseCommand { get; }
        public ICommand CopyCommand { get; }

        public Action RequestClose { get; set; }

        public AnalysisViewModel(FileAnalysisResult fileAnalysis)
        {
            AnalysisText = string.Concat(FormatSummary(fileAnalysis), Environment.NewLine, fileAnalysis.RawJson);
            Filename = Path.GetFileName(fileAnalysis.FilePath);
            Title = $"Analyse FFprobe - {Filename}";
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
            CopyCommand = new RelayCommand(_ => Clipboard.SetText(AnalysisText ?? string.Empty));
        }

        public string FormatSummary(FileAnalysisResult result)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"📝 Fichier : {Path.GetFileName(result.FilePath)}");

            foreach (var videotrack in result.VideoStreams)
            {
                var hdrMode = videotrack.HasDolbyVision ? "DoVi" : videotrack.HasHDR10Plus ? "HDR10+" : videotrack.HasHDR10 ? "HDR10" : "No";
                sb.AppendLine($"🎬 Vidéo : {videotrack.Codec} {videotrack.Width}x{videotrack.Height} @{videotrack.FPS} fps | Bitrate : {videotrack.Bitrate} Kbps | HDR : {hdrMode} ");
            }

            foreach (var audiotrack in result.AudioStreams)
            {
                sb.AppendLine($"🔊 Audio : {audiotrack.Codec} ({audiotrack.Language}) {audiotrack.Channels} ch ({audiotrack.ChannelLayout}) | Bitrate : {audiotrack.Bitrate} Kbps");
            }

            foreach (var subtrack in result.SubtitleStreams)
            {
                sb.AppendLine($"💬 Sous-titres : {subtrack.Codec} ({subtrack.Language})");
            }

            return sb.ToString();
        }
    }
}
