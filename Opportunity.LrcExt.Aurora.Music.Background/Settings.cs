using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    public static class Settings
    {
        public static bool UseToast
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(nameof(UseToast), out var r))
                {
                    return (bool)r;
                }
                return true;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[nameof(UseToast)] = value;
            }
        }
        public static string PreviousArtist
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(nameof(PreviousArtist), out var r))
                {
                    return (string)r;
                }
                return "";
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[nameof(PreviousArtist)] = value;
            }
        }
        public static string PreviousTitle
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(nameof(PreviousTitle), out var r))
                {
                    return (string)r;
                }
                return "";
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[nameof(PreviousTitle)] = value;
            }
        }
        public static string PreviousAlbum
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(nameof(PreviousAlbum), out var r))
                {
                    return (string)r;
                }
                return "";
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[nameof(PreviousAlbum)] = value;
            }
        }
        public static string PreviousLrc
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(nameof(PreviousLrc), out var r))
                {
                    return (string)r;
                }
                return "";
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[nameof(PreviousLrc)] = value;
            }
        }
    }
}
