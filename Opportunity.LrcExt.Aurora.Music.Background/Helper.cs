using System;
using System.Text;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal static class Helper
    {
        internal static readonly HttpClient HttpClient = getClient();

        private static HttpClient getClient()
        {

            var r = new HttpClient
            {
                DefaultRequestHeaders =
                {
                   ["User-Agent"]="MiniLyrics"
                }
            };
            return r;
        }
    }
}