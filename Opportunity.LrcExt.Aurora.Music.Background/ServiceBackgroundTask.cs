using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    public sealed class ServiceBackgroundTask : IBackgroundTask
    {
        private IBackgroundTaskInstance taskInstance;
        private BackgroundTaskDeferral taskDeferral;
        private AppServiceConnection appServiceConnection;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            ActivationBackgroundTask.Register();
            var appService = (AppServiceTriggerDetails)taskInstance.TriggerDetails;
            this.taskInstance = taskInstance;
            taskDeferral = taskInstance.GetDeferral();
            appServiceConnection = appService.AppServiceConnection;
            this.taskInstance.Canceled += OnBackgroundTaskCanceled;
            appServiceConnection.RequestReceived += OnAppServiceRequestReceived;
            appServiceConnection.ServiceClosed += OnServiceClosed;
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
            lock (AppServiceHandler.Handlers)
                AppServiceHandler.Handlers.Add(new AppServiceHandler(sender, args));
        }

        private void Close()
        {
            var taskDeferral = Interlocked.Exchange(ref this.taskDeferral, null);
            if (taskDeferral is null)
                return;

            AppServiceHandler.RemoveUnused(appServiceConnection);

            appServiceConnection.RequestReceived -= OnAppServiceRequestReceived;
            appServiceConnection.ServiceClosed -= OnServiceClosed;
            appServiceConnection.Dispose();
            appServiceConnection = null;

            taskInstance.Canceled -= OnBackgroundTaskCanceled;
            taskInstance = null;

            taskDeferral.Complete();
        }
    }
}
