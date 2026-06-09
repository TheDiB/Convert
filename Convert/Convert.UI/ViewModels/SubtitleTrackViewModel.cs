using Convert.Models;
using System.Collections.ObjectModel;

namespace Convert.UI.ViewModels
{
    public class SubtitleTrackViewModel : ViewModelBase
    {
        public int Index { get; set; }
        public string RawCodec { get; set; }
        public string Codec { get; set; }
        public string LanguageName { get; set; }
        public string Title { get; set; }

        public bool IsBitmap =>
            RawCodec is "hdmv_pgs_subtitle"
                     or "dvd_subtitle"
                     or "xsub"
                     or "dvb_subtitle"
                     or "pgssub";

        public string SelectedLanguage { get; set; }

        public ObservableCollection<KeyValuePair<string, string>> AvailableLanguages { get; }
            = new ObservableCollection<KeyValuePair<string, string>>(
                AudioLanguageStreamInfo.LanguageMap);

        public ObservableCollection<SubtitleProfileItem> AvailableProfiles { get; }
            = new ObservableCollection<SubtitleProfileItem>();

        private SubtitleProfileItem _selectedProfileItem;
        public SubtitleProfileItem SelectedProfileItem
        {
            get => _selectedProfileItem;
            set
            {
                if (_selectedProfileItem != value)
                {
                    _selectedProfileItem = value;
                    OnPropertyChanged();
                    SelectedProfile = _selectedProfileItem?.Profile ?? SubtitleProfile.Copy;
                }
            }
        }

        private SubtitleProfile _selectedProfile = SubtitleProfile.Copy;
        public SubtitleProfile SelectedProfile
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

        public SubtitleTrackViewModel()
        {
            // Par défaut : profil texte
            AvailableProfiles.Add(new SubtitleProfileItem(SubtitleProfile.Copy, "Copier (sans modification)"));
            AvailableProfiles.Add(new SubtitleProfileItem(SubtitleProfile.ConvertToSrt, "Conversion (vers SRT)"));
            AvailableProfiles.Add(new SubtitleProfileItem(SubtitleProfile.Ignore, "Ignorer cette piste"));

            SelectedProfileItem = AvailableProfiles.First();
        }

        public void SetAvailableProfiles(IEnumerable<SubtitleProfileItem> profiles)
        {
            var list = profiles.ToList();

            AvailableProfiles.Clear();
            foreach (var p in list)
                AvailableProfiles.Add(p);

            // On recale SelectedProfileItem sur la liste
            var match = AvailableProfiles.FirstOrDefault(p => p.Profile == SelectedProfile);
            SelectedProfileItem = match ?? AvailableProfiles.FirstOrDefault();
        }
    }
}
