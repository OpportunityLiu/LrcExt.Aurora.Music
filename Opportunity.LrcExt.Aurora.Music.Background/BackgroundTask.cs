using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Microsoft.Services.Store.Engagement;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    public sealed class BackgroundTask : IBackgroundTask
    {
        private IBackgroundTaskInstance taskInstance;
        private BackgroundTaskDeferral taskDeferral;
        private AppServiceConnection appServiceConnection;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.taskInstance = taskInstance;
            this.taskDeferral = taskInstance.GetDeferral();

            var appService = taskInstance.TriggerDetails as AppServiceTriggerDetails;
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

        private int LowerOfThree(int first, int second, int third)
        {
            int min = Math.Min(first, second);
            return Math.Min(min, third);
        }

        private int LevenshteinDistance(string str1, string str2)
        {
            int[,] Matrix;
            int n = str1.Length;
            int m = str2.Length;

            int temp = 0;
            char ch1;
            char ch2;
            int i = 0;
            int j = 0;
            if (n == 0)
            {
                return m;
            }
            if (m == 0)
            {

                return n;
            }
            Matrix = new int[n + 1, m + 1];

            for (i = 0; i <= n; i++)
            {
                //初始化第一列
                Matrix[i, 0] = i;
            }

            for (j = 0; j <= m; j++)
            {
                //初始化第一行
                Matrix[0, j] = j;
            }

            for (i = 1; i <= n; i++)
            {
                ch1 = str1[i - 1];
                for (j = 1; j <= m; j++)
                {
                    ch2 = str2[j - 1];
                    if (ch1.Equals(ch2))
                    {
                        temp = 0;
                    }
                    else
                    {
                        temp = 1;
                    }
                    Matrix[i, j] = LowerOfThree(Matrix[i - 1, j] + 1, Matrix[i, j - 1] + 1, Matrix[i - 1, j - 1] + temp);
                }
            }
            for (i = 0; i <= n; i++)
            {
                for (j = 0; j <= m; j++)
                {
                    Console.Write(" {0} ", Matrix[i, j]);
                }
                Console.WriteLine("");
            }

            return Matrix[n, m];
        }

        private async void OnAppServiceRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var messageDeferral = args.GetDeferral();
            try
            {
                var message = args.Request.Message;
                if (!message.TryGetValue("q", out var query) || query.ToString() != "lyric")
                {
                    await args.Request.SendResponseAsync(createFailed("Wrong Input"));
                    return;
                }

                message.TryGetValue("title", out var t);
                message.TryGetValue("artist", out var a);
                var title = (t ?? "").ToString();
                var artist = (a ?? "").ToString();

                var r = new List<ILrcInfo>();
                foreach (var searcher in Searchers.All)
                {
                    try
                    {
                        var rt = await searcher.FetchLrcListAsync(artist, title);
                        r.AddRange(rt);
                    }
                    catch { }
                }

                // order by similarity with search query.
                r.Sort((i, j) =>
                {
                    return getScore(i).CompareTo(getScore(j));

                    int getScore(ILrcInfo info)
                    {
                        var td = LevenshteinDistance(info.Title, title);
                        if (td == info.Title.Length || td == title.Length)
                            td = 1000;
                        var ad = LevenshteinDistance(info.Artist, artist);
                        if (ad == info.Artist.Length || ad == artist.Length)
                            ad = 1000;

                        // title has more weight.
                        return td * 2 + ad;
                    }
                });

                foreach (var item in r)
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
                    await args.Request.SendResponseAsync(createSucceed(lrc));
                    return;
                }

                // Not found in all providers.
                await args.Request.SendResponseAsync(createFailed("Not Found"));
            }
            catch (Exception ex)
            {
                await args.Request.SendResponseAsync(createFailed(ex.Message));
            }
            finally
            {
                messageDeferral.Complete();
            }

            ValueSet createFailed(string reason)
            {
                StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Failed " + reason);
                return new ValueSet { ["status"] = 0 };
            }

            ValueSet createSucceed(string lrc)
            {
                StoreServicesCustomEventLogger.GetDefault().Log("Request Lyrics Succeed");
                return new ValueSet { ["status"] = 1, ["result"] = lrc };
            }
        }

        private void Close()
        {
            if (this.taskDeferral is null)
                return;
            this.taskDeferral.Complete();
            this.taskDeferral = null;
            this.appServiceConnection.RequestReceived -= OnAppServiceRequestReceived;
            this.appServiceConnection.ServiceClosed -= OnServiceClosed;
            this.taskInstance.Canceled -= OnBackgroundTaskCanceled;
        }
    }
}
