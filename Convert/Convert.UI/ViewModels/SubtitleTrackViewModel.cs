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

        public bool IsBitmap => RawCodec is "hdmv_pgs_subtitle"
                     or "dvd_subtitle"
                     or "xsub"
                     or "dvb_subtitle";

        public ObservableCollection<SubtitleProfileItem> AvailableProfiles { get; } =
            new ObservableCollection<SubtitleProfileItem>
            {
                    new SubtitleProfileItem(SubtitleProfile.Copy, "Copier (sans modification)"),
                    new SubtitleProfileItem(SubtitleProfile.ConvertToSrt, "Conversion (vers SRT)"),
                    new SubtitleProfileItem(SubtitleProfile.Ignore, "Ignorer cette piste")
            };

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
            SelectedProfile = SubtitleProfile.Copy;
        }
    }
}
