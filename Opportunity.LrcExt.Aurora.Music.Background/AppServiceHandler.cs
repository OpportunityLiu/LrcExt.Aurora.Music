﻿using System;
using System.Collections.Generic;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Microsoft.Services.Store.Engagement;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Threading;
using System.Diagnostics;
using System.Linq;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal sealed class AppServiceHandler
    {
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private static readonly ToastNotifier toastNotifier = ToastNotificationManager.CreateToastNotifier();

        private class Cache
        {
            public string Artist;
            public string Title;
            public string Album;
            public string Lrc;
        }

        private static Cache cache;

        private AppServiceDeferral deferral;
        private AppServiceConnection connection;
        private AppServiceRequest request;

        private List<IGrouping<(string Title, string Artist, string Album), LrcInfo>> lrcCandidates;

        public AppServiceHandler(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            connection = sender;
            deferral = args.GetDeferral();
            request = args.Request;
            _Start();
        }

        private string title, artist, album;

        private async void _Start()
        {
            try
            {
                var message = request.Message;
                if (!message.TryGetValue("q", out var query) || query.ToString() != "lyric")
                {
                    sendFault("Wrong Input");
                    return;
                }

                message.TryGetValue("title", out var t);
                message.TryGetValue("artist", out var a);
                message.TryGetValue("album", out var al);
                title = (t ?? "").ToString();
                artist = (a ?? "").ToString();
                album = (al ?? "").ToString();

                await semaphore.WaitAsync();

                var lrcCache = cache;
                if (lrcCache != null &&
                    title == lrcCache.Title &&
                    artist == lrcCache.Artist &&
                    album == lrcCache.Album)
                {
                    sendCached(lrcCache.Lrc);
                    return;
                }

                var candidates = new List<LrcInfo>();
                foreach (var task in Searchers.All.Select(s => s.FetchLrcListAsync(artist, title)).ToArray())
                {
                    try
                    {
                        var result = await task;
                        if (result is null) continue;
                        candidates.AddRange(result);
                    }
                    catch { }
                }
                lrcCandidates = candidates.GroupBy(i => (i.Title, i.Artist, i.Album)).ToList();
                sortResult();
                if (lrcCandidates.Count > 1 && Settings.UseToast)
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
            bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.ContentLine1(title) });
            var arNull = string.IsNullOrEmpty(artist);
            var alNull = string.IsNullOrEmpty(album);
            if (arNull && alNull)
            {
                // No Line 2
            }
            if (arNull)
            {
                bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.ContentLine2NoArtist(album) });
            }
            else if (alNull)
            {
                bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.ContentLine2NoAlbum(artist) });
            }
            else
            {
                bg.Children.Add(new AdaptiveText { Text = Strings.Resources.Toast.ContentLine2(artist, album) });
            }

            var se = new ToastSelectionBox("lrc") { DefaultSelectionBoxItemId = "0" };
            var i = 0;
            foreach (var item in lrcCandidates.Take(5))
            {
                var content = "";
                var sArNull = string.IsNullOrEmpty(item.Key.Artist);
                var sAuNull = string.IsNullOrEmpty(item.Key.Album);
                if (sArNull && sAuNull)
                {
                    content = Strings.Resources.Toast.SelectionTitleOnly(item.Key.Title);
                }
                if (sArNull)
                {
                    content = Strings.Resources.Toast.SelectionNoArtist(item.Key.Title, item.Key.Album);
                }
                else if (sAuNull)
                {
                    content = Strings.Resources.Toast.SelectionNoAlbum(item.Key.Title, item.Key.Artist);
                }
                else
                {
                    content = Strings.Resources.Toast.Selection(item.Key.Title, item.Key.Artist, item.Key.Album);
                }

                se.Items.Add(new ToastSelectionBoxItem(i.ToString(), content));
                i++;
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
                        new ToastButton( Strings.Resources.Toast.Select , GetHashCode().ToString()+ "&sel")
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
            toast.Activated += ToastNotif_Activated;
            toast.Dismissed += ToastNotif_Dismissed;
            toast.Failed += ToastNotif_Failed;

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
            toast.Dismissed -= ToastNotif_Dismissed;
            toast.Failed -= ToastNotif_Failed;
            toast.Activated -= ToastNotif_Activated;
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
                    var item = lrcCandidates[index];
                    lrcCandidates.RemoveAt(index);
                    lrcCandidates.Insert(0, item);
                }
                sendSelection();
            }
        }

        private async void sendCached(string lrc)
        {
            try
            {
                StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Cached");
                await request.SendResponseAsync(new ValueSet { ["status"] = 1, ["result"] = lrc });
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
                cache = new Cache
                {
                    Album = album,
                    Artist = artist,
                    Title = title,
                    Lrc = lrc,
                };
                StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Succeed");
                await request.SendResponseAsync(new ValueSet { ["status"] = 1, ["result"] = lrc });
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
                await request.SendResponseAsync(new ValueSet { ["status"] = 0 });
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
                foreach (var info in lrcCandidates)
                {
                    foreach (var item in info)
                    {
                        var lrc = default(string);
                        try
                        {
                            Debug.WriteLine($"Load lrc: {item}, {GetHashCode()}");
                            lrc = await item.FetchAsync();
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
            var def = Interlocked.Exchange(ref deferral, null);
            if (def is null)
                return;
            request = null;
            connection = null;
            def.Complete();
            semaphore.Release();
        }

        private void sortResult() => lrcCandidates.Sort((i, j) =>
        {
            return getScore(i.Key).CompareTo(getScore(j.Key));

            int getScore((string Title, string Artist, string Album) info)
            {
                var td = distance(info.Title, title);

                var ad = distance(info.Artist, artist);

                var ud = distance(info.Album, album);

                // title has more weight.
                return td * 20 + ad * 8 + ud * 4;

                int distance(string value, string cbase)
                {
                    var vc = value.Contains(cbase, StringComparison.CurrentCultureIgnoreCase);
                    var cc = cbase.Contains(value, StringComparison.CurrentCultureIgnoreCase);
                    if (vc && cc)
                        return 0;
                    var ed = value.Distance(cbase, StringComparison.CurrentCultureIgnoreCase);

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
