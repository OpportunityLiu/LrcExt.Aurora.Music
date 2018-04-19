﻿using System.Collections.Generic;
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

        private static int registered = 0;

        private void register()
        {
            if (Interlocked.Exchange(ref registered, 1) != 0)
                return;
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == nameof(ActivationBackgroundTask))
                {
                    return;
                }
            }
            var builder = new BackgroundTaskBuilder
            {
                Name = nameof(ActivationBackgroundTask),
                TaskEntryPoint = "Opportunity.LrcExt.Aurora.Music.Background." + nameof(ActivationBackgroundTask)
            };
            builder.SetTrigger(new ToastNotificationActionTrigger());
            builder.Register();
        }

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            register();
            var appService = (AppServiceTriggerDetails)taskInstance.TriggerDetails;
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
            lock (AppServiceHandler.Handlers)
                AppServiceHandler.Handlers.Add(new AppServiceHandler(sender, args));
        }

        private void Close()
        {
            var taskDeferral = Interlocked.Exchange(ref this.taskDeferral, null);
            if (taskDeferral is null)
                return;

            AppServiceHandler.RemoveUnused(this.appServiceConnection);

            this.appServiceConnection.RequestReceived -= OnAppServiceRequestReceived;
            this.appServiceConnection.ServiceClosed -= OnServiceClosed;
            this.appServiceConnection.Dispose();
            this.appServiceConnection = null;

            this.taskInstance.Canceled -= OnBackgroundTaskCanceled;
            this.taskInstance = null;

            taskDeferral.Complete();
        }
    }
}
