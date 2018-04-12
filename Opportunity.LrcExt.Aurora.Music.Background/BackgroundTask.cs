using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Microsoft.Services.Store.Engagement;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Xaml;
using Windows.Storage;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    public sealed class AppServiceHandler
    {
        private AppServiceDeferral deferral;
        internal AppServiceConnection Connection;
        private AppServiceRequest request;

        private List<ILrcInfo> lrcCandidates = new List<ILrcInfo>();

        public AppServiceHandler(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            this.Connection = sender;
            this.deferral = args.GetDeferral();
            this.request = args.Request;
            start();
        }

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

        private string title, artist, album;

        private async void start()
        {
            try
            {
                var message = this.request.Message;
                if (!message.TryGetValue("q", out var query) || query.ToString() != "lyric")
                {
                    await this.request.SendResponseAsync(createFailed("Wrong Input"));
                    Close();
                    return;
                }

                message.TryGetValue("title", out var t);
                message.TryGetValue("artist", out var a);
                message.TryGetValue("album", out var al);
                this.title = (t ?? "").ToString();
                this.artist = (a ?? "").ToString();
                this.album = (al ?? "").ToString();

                foreach (var searcher in Searchers.All)
                {
                    try
                    {
                        var rt = await searcher.FetchLrcListAsync(this.artist, this.title);
                        this.lrcCandidates.AddRange(rt);
                    }
                    catch { }
                }
                sortResult();
                if (this.lrcCandidates.Count > 1 && UseToast)
                {
                    createToast();
                    return;
                }
                else
                    sendResult();
            }
            catch (Exception ex)
            {
                await this.request.SendResponseAsync(createFailed(ex.Message));
                Close();
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
            var toastNotif = new ToastNotification(toastContent.GetXml())
            {
                ExpirationTime = DateTimeOffset.Now.AddMinutes(1),
                NotificationMirroring = NotificationMirroring.Disabled,
            };
            toastNotif.Activated += this.ToastNotif_Activated;
            toastNotif.Dismissed += this.ToastNotif_Dismissed;
            toastNotif.Failed += this.ToastNotif_Failed;

            // And send the notification
            ToastNotificationManager.CreateToastNotifier().Show(toastNotif);
        }

        private ToastNotification toast;

        private async void sendResult()
        {
            try
            {
                foreach (var item in this.lrcCandidates)
                {
                    var lrc = default(string);
                    try
                    {
                        lrc = await item.FetchLryics();
                    }
                    catch
                    {
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(lrc))
                        continue;
                    await this.request.SendResponseAsync(createSucceed(lrc));
                    return;
                }

                // Not found in all providers.
                await this.request.SendResponseAsync(createFailed("Not Found"));
            }
            catch (Exception ex)
            {
                await this.request.SendResponseAsync(createFailed(ex.Message));
            }
            finally
            {
                Close();
            }
        }

        public void Close()
        {
            if (this.deferral is null)
                return;
            this.deferral.Complete();
            this.request = null;
            this.Connection = null;
            closeToast();
        }

        private void closeToast()
        {
            if (this.toast is null)
                return;
            this.toast.Dismissed -= this.ToastNotif_Dismissed;
            this.toast.Failed -= this.ToastNotif_Failed;
            this.toast = null;
        }


        private void ToastNotif_Activated(ToastNotification sender, object args)
        {
            var e = (ToastActivatedEventArgs)args;
            if (string.IsNullOrEmpty(e.Arguments))
            {
                closeToast();
                sendResult();
            }
        }
        private void ToastNotif_Failed(ToastNotification sender, ToastFailedEventArgs args)
        {
            closeToast();
            sendResult();
        }
        private void ToastNotif_Dismissed(ToastNotification sender, ToastDismissedEventArgs args)
        {
            closeToast();
            sendResult();
        }

        public void ToastActivated(ToastNotificationActionTriggerDetail args)
        {
            closeToast();
            if (args.Argument.EndsWith("sel"))
            {
                var index = int.Parse(args.UserInput["lrc"].ToString());
                var item = this.lrcCandidates[index];
                this.lrcCandidates.RemoveAt(index);
                this.lrcCandidates.Insert(0, item);
            }
            sendResult();
        }

        private static ValueSet createFailed(string reason)
        {
            StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Failed " + reason);
            return new ValueSet { ["status"] = 0 };
        }

        private static ValueSet createSucceed(string lrc)
        {
            StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Succeed");
            return new ValueSet { ["status"] = 1, ["result"] = lrc };
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
    }

    public sealed class BackgroundTask : IBackgroundTask
    {

        private static void handleNotification(ToastNotificationActionTriggerDetail triggerDetails)
        {
            var id = int.Parse(triggerDetails.Argument.Split('&').First());
            var item = handlers.Find(i => i.GetHashCode() == id);
            if (item is null)
                return;
            item.ToastActivated(triggerDetails);
        }

        private IBackgroundTaskInstance taskInstance;
        private BackgroundTaskDeferral taskDeferral;
        private AppServiceConnection appServiceConnection;

        private static List<AppServiceHandler> handlers = new List<AppServiceHandler>();

        private void register()
        {
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == "BackgroundTask")
                {
                    return;
                }
            }
            var builder = new BackgroundTaskBuilder();

            builder.Name = "BackgroundTask";
            builder.TaskEntryPoint = "Opportunity.LrcExt.Aurora.Music.Background.BackgroundTask";
            builder.SetTrigger(new ToastNotificationActionTrigger());
            builder.Register();
        }

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            register();
            var appService = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            if (appService is null)
            {
                handleNotification((ToastNotificationActionTriggerDetail)taskInstance.TriggerDetails);
                return;
            }
            this.taskInstance = taskInstance;
            this.taskDeferral = taskInstance.GetDeferral();
            this.appServiceConnection = appService.AppServiceConnection;
            this.taskInstance.Canceled += OnBackgroundTaskCanceled;
            this.appServiceConnection.RequestReceived += OnAppServiceRequestReceived;
            this.appServiceConnection.ServiceClosed += OnServiceClosed;
        }

        private void OnBackgroundTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Close();
        }

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            Close();
        }

        private void OnAppServiceRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            handlers.Add(new AppServiceHandler(sender, args));
        }

        private void Close()
        {
            if (this.taskDeferral is null)
                return;
            var toremove = handlers.Where(i => i.Connection == this.appServiceConnection).ToList();
            handlers.RemoveAll(i => i.Connection == this.appServiceConnection);
            foreach (var item in toremove)
            {
                item.Close();
            }

            this.taskDeferral.Complete();
            this.taskDeferral = null;
            this.appServiceConnection.RequestReceived -= OnAppServiceRequestReceived;
            this.appServiceConnection.ServiceClosed -= OnServiceClosed;
            this.taskInstance.Canceled -= OnBackgroundTaskCanceled;
        }
    }
}
