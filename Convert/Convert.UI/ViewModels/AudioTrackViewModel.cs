using Convert.Models;
using Convert.UI.ViewModels;
using System.Collections.ObjectModel;

public class AudioTrackViewModel : ViewModelBase
{
    public int Index { get; set; }
    public string Codec { get; set; } = "";
    public int Channels { get; set; }
    public string LanguageName { get; set; } = "";
    public string Title { get; set; } = "";
    public int Bitrate { get; set; }

    public ObservableCollection<AudioProfileItem> AvailableProfiles { get; } =
        new ObservableCollection<AudioProfileItem>
        {
            new AudioProfileItem(AudioProfile.Copy, "Copier (sans modification)"),
            new AudioProfileItem(AudioProfile.Eac3_5_1, "EAC3 5.1 (640 kbps)"),
            new AudioProfileItem(AudioProfile.Ac3_5_1, "AC3 5.1 (640 kbps)"),
            new AudioProfileItem(AudioProfile.Ac3_2_0, "AC3 2.0 (192 kbps)"),
            new AudioProfileItem(AudioProfile.Mp3_2_0, "MP3 2.0 (320 kbps)"),
            new AudioProfileItem(AudioProfile.Ignore, "Ignorer cette piste")
        };

    private AudioProfile _selectedProfile;
    public AudioProfile SelectedProfile
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

    public AudioTrackViewModel()
    {
        SelectedProfile = AudioProfile.Copy;
    }
}