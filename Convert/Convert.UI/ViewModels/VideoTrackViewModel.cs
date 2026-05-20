using Convert.Models;
using Convert.UI.ViewModels;
using System.Collections.ObjectModel;

public class VideoTrackViewModel : ViewModelBase
{
    public int Index { get; set; }
    public string Codec { get; set; } = "";
    public string Title { get; set; } = "";
    public int Bitrate { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FPS { get; set; }

    public ObservableCollection<VideoProfileItem> AvailableProfiles { get; } =
        new ObservableCollection<VideoProfileItem>
        {
            new VideoProfileItem(VideoProfile.Copy, "Copier (sans modification)"),
            new VideoProfileItem(VideoProfile.H265_High, "HEVC (qualité maximale)"),
            new VideoProfileItem(VideoProfile.H264_Medium, "H264 (qualité élevée)"),
            new VideoProfileItem(VideoProfile.H264_Low, "H264 (qualité moyenne)"),
            new VideoProfileItem(VideoProfile.Ignore, "Ignorer cette piste")
        };

    private VideoProfile _selectedProfile = VideoProfile.Copy;
    public VideoProfile SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_selectedProfile != value)
            {
                _selectedProfile = value;
                OnPropertyChanged();
            }
        }
    }

    public VideoTrackViewModel()
    {
        SelectedProfile = VideoProfile.Copy;
    }
}