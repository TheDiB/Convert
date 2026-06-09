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
    public string SelectedLanguage { get; set; }

    public ObservableCollection<KeyValuePair<string, string>> AvailableLanguages { get; }
        = new ObservableCollection<KeyValuePair<string, string>>(
            AudioLanguageStreamInfo.LanguageMap);

    public ObservableCollection<AudioProfileItem> AvailableProfiles { get; } =
        new ObservableCollection<AudioProfileItem>
        {
            new AudioProfileItem(AudioProfile.Copy, "Copier (sans modification)"),
            new AudioProfileItem(AudioProfile.Flac_auto, "FLAC (auto)"),
            new AudioProfileItem(AudioProfile.Aac_7_1, "AAC 7.1 (896 kbps)"),
            new AudioProfileItem(AudioProfile.Eac3_5_1, "EAC3 5.1 (640 kbps)"),
            new AudioProfileItem(AudioProfile.Ac3_5_1, "AC3 5.1 (640 kbps)"),
            new AudioProfileItem(AudioProfile.Aac_2_0, "AAC 2.0 (320 kbps)"),
            new AudioProfileItem(AudioProfile.Ignore, "Ignorer cette piste")
        };

    private AudioProfile _selectedProfile = AudioProfile.Copy;
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