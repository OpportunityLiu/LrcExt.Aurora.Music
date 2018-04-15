using Microsoft.Services.Store.Engagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace Opportunity.LrcExt.Aurora.Music
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            Dispatcher.Begin(async () =>
            {
                var r = await Launcher.QueryUriSupportAsync(AuroraSettings, LaunchQuerySupportType.Uri);
                this.canLaunch = (r == LaunchQuerySupportStatus.Available);
                this.Bindings.Update();
                StoreServicesCustomEventLogger.GetDefault().Log("Main View Launched");
            });
        }

        private bool canLaunch = false;

        private static Uri AuroraSettings = new Uri("as-music:///settings", UriKind.Absolute);

        private async void btnLaunch_Click(object sender, RoutedEventArgs e)
        {
            var r = await Launcher.QueryUriSupportAsync(AuroraSettings, LaunchQuerySupportType.Uri);
            var lr = false;
            if (r == LaunchQuerySupportStatus.Available)
            {
                StoreServicesCustomEventLogger.GetDefault().Log("Aurora Launched");
                lr = await Launcher.LaunchUriAsync(AuroraSettings);
            }
            else
            {
                StoreServicesCustomEventLogger.GetDefault().Log("Store Launched");
                lr = await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9nblggh6jvdt"));
            }
            if (lr)
                Application.Current.Exit();
        }

        private async void btnGithub_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/OpportunityLiu/LrcExt.Aurora.Music"));
        }

        private async void btnRating_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9p851q321td3"));
        }
    }
}
