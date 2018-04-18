using System.Linq;
using Windows.ApplicationModel.Background;
using Windows.UI.Notifications;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    public sealed class ActivationBackgroundTask : IBackgroundTask
    {
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
