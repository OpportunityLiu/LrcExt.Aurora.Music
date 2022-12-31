using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Web.Http.Headers;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal interface ISearcher
    {
        Task<IEnumerable<LrcInfo>> FetchLrcListAsync(string artist, string title);
    }

    internal static class Searchers
    {
        public static ISearcher NeteaseSearcher { get; } = new NeteaseSearcher();
        public static ISearcher TTSearcher { get; } = new TTSearcher();
        public static ISearcher QQSearcher { get; } = new QQSearcher();

        public static IEnumerable<ISearcher> All
        {
            get
            {
                yield return NeteaseSearcher;
                yield return QQSearcher;
                yield return TTSearcher;
            }
        }
    }
}