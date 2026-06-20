using Convert.Models;
using Convert.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;

public class VideoTrackViewModel : ViewModelBase
{
    public class HDRProfileItem
    {
        public HDRProfile Mode { get; set; }
        public string Label { get; set; }

        public HDRProfileItem(HDRProfile mode, string label)
        {
            Mode = mode;
            Label = label;
        }
    }

    public enum HdrType
    {
        SDR,
        HDR10,
        HDR10Plus,
        DolbyVision,
        DolbyVisionAndHDR10
    }

    public bool ShowHdrColumn =>
        Hdr == HdrType.SDR || Hdr == HdrType.HDR10 || Hdr == HdrType.HDR10Plus;

    public bool ShowDolbyVisionColumn =>
        Hdr == HdrType.DolbyVision || Hdr == HdrType.DolbyVisionAndHDR10;

    private HdrType _hdr;
    public HdrType Hdr
    {
        get => _hdr;
        set
        {
            if (_hdr != value)
            {
                _hdr = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HdrLabel));
                OnPropertyChanged(nameof(ShowDolbyVisionOptions));
                OnPropertyChanged(nameof(ShowHdrColumn));
                OnPropertyChanged(nameof(ShowDolbyVisionColumn));
            }
        }
    }

    public string HdrLabel =>
        Hdr switch
        {
            HdrType.SDR => "Non",
            HdrType.HDR10 => "HDR10",
            HdrType.HDR10Plus => "HDR10+",
            HdrType.DolbyVision => "Dolby Vision",
            HdrType.DolbyVisionAndHDR10 => "Dolby Vision + HDR10",
            _ => "Non"
        };

    public bool ShowDolbyVisionOptions =>
        Hdr == HdrType.DolbyVision || Hdr == HdrType.DolbyVisionAndHDR10;

    public ObservableCollection<HDRProfileItem> AvailableHDRModes { get; }
        = new ObservableCollection<HDRProfileItem>
        {
        new HDRProfileItem(HDRProfile.Keep, "Conserver Dolby Vision"),
        new HDRProfileItem(HDRProfile.ForceHdr10, "Forcer HDR10"),
        new HDRProfileItem(HDRProfile.ForceSdr, "Convertir en SDR")
        };

    private HDRProfile _selectedHDRMode = HDRProfile.Keep;
    public HDRProfile SelectedHDRMode
    {
        get => _selectedHDRMode;
        set
        {
            if (_selectedHDRMode != value)
            {
                _selectedHDRMode = value;
                OnPropertyChanged();

                if (value == HDRProfile.ForceHdr10 || value == HDRProfile.ForceSdr)
                {
                    if (SelectedProfile == VideoProfile.Copy)
                        SelectedProfile = VideoProfile.H265_High;
                }

                if (value == HDRProfile.Keep)
                {
                    // On ne change rien, l’utilisateur peut rester en H265 ou revenir à Copy
                }
            }
        }
    }

    public int Index { get; set; }
    public string Codec { get; set; } = "";
    public string Title { get; set; } = "";
    public int Bitrate { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FPS { get; set; }

    public ObservableCollection<VideoProfileItem> AvailableProfiles { get; }
        = new ObservableCollection<VideoProfileItem>
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
            if (value == VideoProfile.Copy && (SelectedHDRMode == HDRProfile.ForceHdr10 || SelectedHDRMode == HDRProfile.ForceSdr))
            {
                value = VideoProfile.H265_High;
                MessageBox.Show("Afin de neutraliser Dolby Vision, un transcodage est obligatoire.\r\nLa copie est impossible, la piste vidéo sera réencodée selon le profil 'HEVC (qualité maximale)'", "Transcodage imposé", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (_selectedProfile != value)
            {
                _selectedProfile = value;
            }

            OnPropertyChanged();
        }
    }

    public VideoTrackViewModel()
    {
        SelectedProfile = VideoProfile.Copy;
        Hdr = HdrType.SDR;
    }
}