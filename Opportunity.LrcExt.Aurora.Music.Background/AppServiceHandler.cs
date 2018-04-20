using System;
using System.Collections.Generic;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Microsoft.Services.Store.Engagement;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.Storage;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal sealed class AppServiceHandler
    {
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private static readonly ToastNotifier toastNotifier = ToastNotificationManager.CreateToastNotifier();

        private AppServiceDeferral deferral;
        private AppServiceConnection connection;
        private AppServiceRequest request;

        private List<IGrouping<(string Title, string Artist, string Album), ILrcInfo>> lrcCandidates;

        public AppServiceHandler(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            this.connection = sender;
            this.deferral = args.GetDeferral();
            this.request = args.Request;
            start();
        }

        private string title, artist, album;

        private async void start()
        {
            try
            {
                var message = this.request.Message;
                if (!message.TryGetValue("q", out var query) || query.ToString() != "lyric")
                {
                    sendFault("Wrong Input");
                    return;
                }

                message.TryGetValue("title", out var t);
                message.TryGetValue("artist", out var a);
                message.TryGetValue("album", out var al);
                this.title = (t ?? "").ToString();
                this.artist = (a ?? "").ToString();
                this.album = (al ?? "").ToString();

                await semaphore.WaitAsync();

                if (this.title == Settings.PreviousTitle &&
                    this.artist == Settings.PreviousArtist &&
                    this.album == Settings.PreviousAlbum)
                {
                    sendCached();
                    return;
                }

                var searchTasks = Searchers.All.Select(s => s.FetchLrcListAsync(this.artist, this.title).AsTask()).ToArray();
                try
                {
                    await Task.WhenAll(searchTasks);
                }
                catch { }
                var candidates = new List<ILrcInfo>();
                foreach (var task in searchTasks)
                {
                    try
                    {
                        candidates.AddRange(task.Result);
                    }
                    catch { }
                }
                this.lrcCandidates = candidates.GroupBy(i => (i.Title, i.Artist, i.Album)).ToList();
                sortResult();
                if (this.lrcCandidates.Count > 1 && Settings.UseToast)
                {
                    createToast();
                    return;
                }
                else
                    sendSelection();
            }
            catch (Exception ex)
            {
                sendFault(ex.Message);
            }
        }

        private void createToast()
        {
            var bg = new ToastBindingGeneric();
            bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.Title });
            bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.ContentLine1(this.title) });
            if (string.IsNullOrEmpty(this.artist))
            {
                bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.ContentLine2NoArtist(this.album) });
            }
            else if (string.IsNullOrEmpty(this.album))
            {
                bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.ContentLine2NoAlbum(this.artist) });
            }
            else
            {
                bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.ContentLine2(this.artist, this.album) });
            }

            var se = new ToastSelectionBox("lrc") { DefaultSelectionBoxItemId = "0" };
            for (var i = 0; i < Math.Min(this.lrcCandidates.Count, 5); i++)
            {
                var item = this.lrcCandidates[i];
                var content = "";
                if (string.IsNullOrEmpty(item.Key.Artist))
                {
                    content = Strings.Resources.Toast.SelectionNoArtist(item.Key.Title, item.Key.Album);
                }
                else if (string.IsNullOrEmpty(item.Key.Album))
                {
                    content = Strings.Resources.Toast.SelectionNoAlbum(item.Key.Title, item.Key.Artist);
                }
                else
                {
                    content = Strings.Resources.Toast.Selection(item.Key.Title, item.Key.Artist, item.Key.Album);
                }

                se.Items.Add(new ToastSelectionBoxItem(i.ToString(), content));
            }

            var toastContent = new ToastContent()
            {
                Visual = new ToastVisual()
                {
                    BindingGeneric = bg,
                },
                Actions = new ToastActionsCustom()
                {
                    Inputs = { se },
                    Buttons =
                    {
                        new ToastButton( Strings.Resources.Toast.Select , this.GetHashCode().ToString()+ "&sel")
                        {
                            ActivationType = ToastActivationType.Background
                        }
                    }
                }
            };

            // Create the toast notification
            var toast = this.toast = new ToastNotification(toastContent.GetXml())
            {
                ExpirationTime = DateTimeOffset.Now.AddMinutes(1),
                NotificationMirroring = NotificationMirroring.Disabled,
            };
            toast.Activated += this.ToastNotif_Activated;
            toast.Dismissed += this.ToastNotif_Dismissed;
            toast.Failed += this.ToastNotif_Failed;

            // And send the notification
            toastNotifier.Show(toast);
            StoreServicesCustomEventLogger.GetDefault().Log("Choose Toast Send");
        }

        private ToastNotification toast;

        private bool closeToast()
        {
            var toast = Interlocked.Exchange(ref this.toast, null);
            if (toast is null)
                return false;
            toast.Dismissed -= this.ToastNotif_Dismissed;
            toast.Failed -= this.ToastNotif_Failed;
            toast.Activated -= this.ToastNotif_Activated;
            toastNotifier.Hide(toast);
            return true;
        }

        private void ToastNotif_Activated(ToastNotification sender, object args)
        {
            StoreServicesCustomEventLogger.GetDefault().Log("Choose Toast Activated");
            var e = (ToastActivatedEventArgs)args;
            if (string.IsNullOrEmpty(e.Arguments))
            {
                // Foreground activation
                if (closeToast())
                    sendSelection();
            }
        }
        private void ToastNotif_Failed(ToastNotification sender, ToastFailedEventArgs args)
        {
            if (closeToast())
                sendSelection();
        }
        private void ToastNotif_Dismissed(ToastNotification sender, ToastDismissedEventArgs args)
        {
            StoreServicesCustomEventLogger.GetDefault().Log("Choose Toast Dismissed");
            if (closeToast())
                sendSelection();
        }
        public void ToastActivated(ToastNotificationActionTriggerDetail args)
        {
            if (closeToast())
            {
                if (args.Argument.EndsWith("sel"))
                {
                    var index = int.Parse(args.UserInput["lrc"].ToString());
                    var item = this.lrcCandidates[index];
                    this.lrcCandidates.RemoveAt(index);
                    this.lrcCandidates.Insert(0, item);
                }
                sendSelection();
            }
        }

        private async void sendCached()
        {
            try
            {
                StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Cached");
                await this.request.SendResponseAsync(new ValueSet { ["status"] = 1, ["result"] = Settings.PreviousLrc });
            }
            finally
            {
                Close();
            }
        }

        private async void sendResult(string lrc)
        {
            try
            {
                Settings.PreviousAlbum = this.album;
                Settings.PreviousArtist = this.artist;
                Settings.PreviousTitle = this.title;
                Settings.PreviousLrc = lrc;
                StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Succeed");
                await this.request.SendResponseAsync(new ValueSet { ["status"] = 1, ["result"] = lrc });
            }
            finally
            {
                Close();
            }
        }

        private async void sendFault(string message)
        {
            try
            {
                StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Failed " + message);
                await this.request.SendResponseAsync(new ValueSet { ["status"] = 0 });
            }
            finally
            {
                Close();
            }
        }

        private async void sendSelection()
        {
            try
            {
                foreach (var info in this.lrcCandidates)
                {
                    foreach (var item in info)
                    {
                        var lrc = default(string);
                        try
                        {
                            Debug.WriteLine($"Load lrc: {item}, {GetHashCode()}");
                            lrc = await item.FetchLryics();
                        }
                        catch
                        {
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(lrc))
                            continue;

                        sendResult(lrc);
                        return;
                    }
                }

                // Not found in all providers.
                sendFault("Not Found");
            }
            catch (Exception ex)
            {
                sendFault(ex.Message);
            }
        }

        public void Close()
        {
            closeToast();
            var def = Interlocked.Exchange(ref this.deferral, null);
            if (def is null)
                return;
            this.request = null;
            this.connection = null;
            def.Complete();
            semaphore.Release();
        }

        private void sortResult() => this.lrcCandidates.Sort((i, j) =>
        {
            return getScore(i.Key).CompareTo(getScore(j.Key));

            int getScore((string Title, string Artist, string Album) info)
            {
                var td = distance(info.Title, this.title);

                var ad = distance(info.Artist, this.artist);

                var ud = distance(info.Album, this.album);

                // title has more weight.
                return td * 20 + ad * 8 + ud * 4;

                int distance(string value, string cbase)
                {
                    var vc = value.Contains(cbase, StringComparison.OrdinalIgnoreCase);
                    var cc = cbase.Contains(value, StringComparison.OrdinalIgnoreCase);
                    if (vc && cc)
                        return 0;
                    var ed = StringHelper.LevenshteinDistance(value, cbase);

                    // give same base score for all irrelevant strings
                    if (ed == value.Length || ed == cbase.Length)
                        ed = cbase.Length * 2;

                    // bonus for sub strings
                    if (vc || cc)
                        ed /= 2;
                    return ed;
                }
            }
        });

        public static readonly List<AppServiceHandler> Handlers = new List<AppServiceHandler>();

        public static void RemoveUnused(AppServiceConnection closingConnection)
        {
            lock (Handlers)
                Handlers.RemoveAll(i =>
                {
                    if (i.deferral is null)
                        return true;
                    if (i.connection == closingConnection)
                    {
                        i.Close();
                        return true;
                    }
                    return false;
                });
        }
    }
}
