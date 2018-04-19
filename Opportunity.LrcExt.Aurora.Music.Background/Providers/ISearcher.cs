using System.Collections.Generic;
using Windows.Foundation;
using Windows.Web.Http.Headers;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    public interface ISearcher
    {
        IAsyncOperation<IEnumerable<ILrcInfo>> FetchLrcListAsync(string artist, string title);
    }

    public static class Searchers
    {
        public static ISearcher NeteaseSearcher { get; } = new NeteaseSearcher();
        public static ISearcher ViewLyricsSearcher { get; } = new ViewLyricsSearcher();
        public static ISearcher TTSearcher { get; } = new TTSearcher();
        public static ISearcher QQSearcher { get; } = new QQSearcher();

        public static IEnumerable<ISearcher> All
        {
            get
            {
                yield return NeteaseSearcher;
                yield return QQSearcher;
                yield return ViewLyricsSearcher;
                yield return TTSearcher;
            }
        }
    }
}