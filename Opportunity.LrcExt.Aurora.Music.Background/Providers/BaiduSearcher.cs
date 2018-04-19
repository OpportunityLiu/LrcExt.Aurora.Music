using Opportunity.LrcParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Web.Http;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal sealed class BaiduSearcher : ISearcher
    {
        private static HttpClient httpClient = new HttpClient
        {
            DefaultRequestHeaders =
            {
            }
        };

        private static readonly DataContractJsonSerializer searchJsonSerializer = new DataContractJsonSerializer(typeof(SearchResult));

        public IAsyncOperation<IEnumerable<ILrcInfo>> FetchLrcListAsync(string artist, string title)
        {
            return AsyncInfo.Run<IEnumerable<ILrcInfo>>(async token =>
            {
                var buf = await httpClient.GetBufferAsync(new Uri("http://tingapi.ting.baidu.com/v1/restserver/ting?from=webapp_music&method=baidu.ting.search.catalogSug&format=json&query=" + title));
                using (var stream = buf.AsStream())
                {
                    var data = (SearchResult)searchJsonSerializer.ReadObject(stream);
                    if (data.error_code != 22000 || data.song is null || data.song.Length == 0)
                        return Array.Empty<ILrcInfo>();
                    var lrc = new BaiduLrcInfo[data.song.Length];
                    for (var i = 0; i < lrc.Length; i++)
                    {
                        lrc[i] = new BaiduLrcInfo(data.song[i]);
                    }
                    return lrc;
                }
            });
        }

        private sealed class BaiduLrcInfo : LrcInfo
        {
            private static readonly DataContractJsonSerializer lrcJsonSerializer = new DataContractJsonSerializer(typeof(LrcResult));

            [DataContract]
            public class LrcResult
            {
                [DataMember]
                public string lrcContent { get; set; }
            }

            internal BaiduLrcInfo(Song song)
                : base(song.songname ?? "",
                      song.artistname ?? "",
                      "")
            {
                this.id = song.songid;
            }

            private readonly string id;

            public override IAsyncOperation<string> FetchLryics()
            {
                return AsyncInfo.Run(async token =>
                {
                    var uri = new Uri("http://tingapi.ting.baidu.com/v1/restserver/ting?from=webapp_music&method=baidu.ting.song.lry&format=json&songid=" + this.id);
                    var buf = await httpClient.GetBufferAsync(uri);
                    using (var stream = buf.AsStream())
                    {
                        var data = (LrcResult)lrcJsonSerializer.ReadObject(stream);
                        return data.lrcContent;
                    }
                });
            }
        }

        [DataContract]
        public class SearchResult
        {
            [DataMember]
            public Song[] song { get; set; }
            [DataMember]
            public int error_code { get; set; }
        }

        [DataContract]
        public class Song
        {
            [DataMember]
            public string songname { get; set; }
            [DataMember]
            public string songid { get; set; }
            [DataMember]
            public string artistname { get; set; }
        }

    }
}
