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
    }
}
