using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.UI.Notifications;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    public sealed class ActivationBackgroundTask : IBackgroundTask
    {
        private static int registered = 0;

        public static void Register()
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
            var triggerDetails = (ToastNotificationActionTriggerDetail)taskInstance.TriggerDetails;
            var id = int.Parse(triggerDetails.Argument.Split('&').First());
            var item = AppServiceHandler.Handlers.Find(i => i.GetHashCode() == id);
            if (item is null)
                return;
            item.ToastActivated(triggerDetails);
        }
    }
}
