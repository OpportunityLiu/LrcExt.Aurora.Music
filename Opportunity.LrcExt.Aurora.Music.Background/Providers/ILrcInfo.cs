using Windows.Foundation;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    public interface ILrcInfo
    {
        string Album { get; }
        string Artist { get; }
        string Title { get; }

        IAsyncOperation<string> FetchLryics();
    }
}