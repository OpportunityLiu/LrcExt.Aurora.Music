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

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal sealed class AppServiceHandler
    {
        private AppServiceDeferral deferral;
        internal AppServiceConnection Connection;
        private AppServiceRequest request;

        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        private List<ILrcInfo> lrcCandidates = new List<ILrcInfo>();

        public AppServiceHandler(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            this.Connection = sender;
            this.deferral = args.GetDeferral();
            this.request = args.Request;
            start();
        }

        private string title, artist, album;

        private async void start()
        {
            try
            {
                await semaphore.WaitAsync();
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
                foreach (var task in searchTasks)
                {
                    try
                    {
                        this.lrcCandidates.AddRange(task.Result);
                    }
                    catch { }
                }
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
            bg.Children.Add(new AdaptiveText { Text = this.title });
            if (string.IsNullOrEmpty(this.artist) || string.IsNullOrEmpty(this.album))
            {
                bg.Children.Add(new AdaptiveText { Text = this.artist + this.album });
            }
            else
            {
                bg.Children.Add(new AdaptiveText { Text = this.artist + " - " + this.album });
            }

            var se = new ToastSelectionBox("lrc") { DefaultSelectionBoxItemId = "0" };
            for (var i = 0; i < Math.Min(this.lrcCandidates.Count, 5); i++)
            {
                var item = this.lrcCandidates[i];
                se.Items.Add(new ToastSelectionBoxItem(i.ToString(), $"{item.Title} ({item.Artist} - {item.Album})"));
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
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        private ToastNotification toast;

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
                foreach (var item in this.lrcCandidates)
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
            def.Complete();
            this.request = null;
            this.Connection = null;
            semaphore.Release();
        }

        private bool closeToast()
        {
            var toast = Interlocked.Exchange(ref this.toast, null);
            if (toast is null)
                return false;
            toast.Dismissed -= this.ToastNotif_Dismissed;
            toast.Failed -= this.ToastNotif_Failed;
            toast.Activated -= this.ToastNotif_Activated;
            return true;
        }

        private void ToastNotif_Activated(ToastNotification sender, object args)
        {
            var e = (ToastActivatedEventArgs)args;
            if (string.IsNullOrEmpty(e.Arguments))
            {
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

        private void sortResult() => this.lrcCandidates.Sort((i, j) =>
        {
            return getScore(i).CompareTo(getScore(j));

            int getScore(ILrcInfo info)
            {
                var td = StringHelper.LevenshteinDistance(info.Title, this.title);
                if (td == info.Title.Length || td == this.title.Length)
                    td = 1000;

                var ad = StringHelper.LevenshteinDistance(info.Artist, this.artist);
                if (ad == info.Artist.Length || ad == this.artist.Length)
                    ad = 1000;

                var ud = StringHelper.LevenshteinDistance(info.Album, this.album);
                if (ud == info.Album.Length || ud == this.album.Length)
                    ud = 1000;

                // title has more weight.
                return td * 20 + ad * 8 + ud * 4;
            }
        });

        public static readonly List<AppServiceHandler> Handlers = new List<AppServiceHandler>();

        public static void RemoveUnused()
        {
            var toremove = Handlers.Where(i => i.deferral is null).ToList();

            lock (Handlers)
                Handlers.RemoveAll(i => i.deferral is null);
        }
    }
}
