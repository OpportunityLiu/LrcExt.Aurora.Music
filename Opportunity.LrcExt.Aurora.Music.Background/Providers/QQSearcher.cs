using Opportunity.LrcParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Windows.Web.Http;

// 参考
// https://github.com/jsososo/QQMusicApi/blob/master/routes/search.js
// https://github.com/jsososo/QQMusicApi/blob/master/routes/lyric.js

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal sealed class QQSearcher : ISearcher
    {
        private static HttpClient httpClient = new()
        {
            DefaultRequestHeaders =
            {
                Referer = new ("https://y.qq.com/"),
            }
        };

        private static readonly DataContractJsonSerializer searchJsonSerializer = new DataContractJsonSerializer(typeof(SearchResult));

        public async Task<IEnumerable<LrcInfo>> FetchLrcListAsync(string artist, string title)
        {
            var res = await httpClient.PostAsync(new Uri("https://u.y.qq.com/cgi-bin/musicu.fcg"), new HttpStringContent($@"{{""req_1"":{{""method"":""DoSearchForQQMusicDesktop"",""module"":""music.search.SearchCgiService"",""param"":{{""num_per_page"":20,""page_num"":1,""query"": ""{HttpUtility.JavaScriptStringEncode(title)}"",""search_type"":0}}}}}}"));
            var buf = await res.Content.ReadAsBufferAsync();
            using var stream = buf.AsStream();
            var result = (SearchResult)searchJsonSerializer.ReadObject(stream);
            if (result.code != 0 || result.req_1.code != 0 || result.req_1.data.code != 0)
                return Array.Empty<LrcInfo>();
            var ret = result.req_1.data.body;
            if (ret?.song?.list is null || ret.song.list.Length == 0)
                return Array.Empty<LrcInfo>();
            var lrc = new QQLrcInfo[ret.song.list.Length];
            for (var i = 0; i < lrc.Length; i++)
            {
                lrc[i] = new QQLrcInfo(ret.song.list[i]);
            }
            return lrc;
        }

        private sealed class QQLrcInfo : LrcInfo
        {
            private static readonly DataContractJsonSerializer lrcJsonSerializer = new DataContractJsonSerializer(typeof(LrcResult));

            [DataContract]
            public class LrcResult
            {
                [DataMember]
                public int code { get; set; }
                [DataMember]
                public string lyric { get; set; }
            }

            internal QQLrcInfo(Song song)
                : base(song.title ?? "",
                       song.singer?.Select(s => s?.name ?? ""),
                       song.album?.name ?? "")
            {
                id = song.id;
            }

            private readonly int id;

            protected override async Task<string> FetchDataAsync()
            {
                var uri = new Uri("https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg?format=json&musicid=" + id);
                var buf = await httpClient.GetBufferAsync(uri);
                using var stream = buf.AsStream();
                var data = (LrcResult)lrcJsonSerializer.ReadObject(stream);
                if (data.code != 0 || string.IsNullOrEmpty(data.lyric) ||
                    //"[00:00:00]此歌曲为没有填词的纯音乐，请您欣赏"
                    data.lyric == "WzAwOjAwOjAwXeatpOatjOabsuS4uuayoeacieWhq+ivjeeahOe6r+mfs+S5kO+8jOivt+aCqOaso+i1jw==")
                    return null;
                return Encoding.UTF8.GetString(Convert.FromBase64String(data.lyric));
            }
        }


        [DataContract]
        public class SearchResult
        {
            [DataMember]
            public int code { get; set; }
            [DataMember]
            public SearchReqResult req_1 { get; set; }
        }
        [DataContract]
        public class SearchReqResult
        {
            [DataMember]
            public int code { get; set; }
            [DataMember]
            public SearchReqResultData data { get; set; }
        }
        [DataContract]
        public class SearchReqResultData
        {
            [DataMember]
            public int code { get; set; }
            [DataMember]
            public Data body { get; set; }
        }

        [DataContract]
        public class Data
        {
            [DataMember]
            public Songs song { get; set; }
        }

        [DataContract]
        public class Songs
        {
            [DataMember]
            public Song[] list { get; set; }
        }

        [DataContract]
        public class Song
        {
            [DataMember]
            public Singer[] singer { get; set; }
            [DataMember]
            public int id { get; set; }
            [DataMember]
            public string title { get; set; }
            [DataMember]
            public Album album { get; set; }
        }

        [DataContract]
        public class Singer
        {
            [DataMember]
            public int id { get; set; }
            [DataMember]
            public string name { get; set; }
        }
        [DataContract]
        public class Album
        {
            [DataMember]
            public int id { get; set; }
            [DataMember]
            public string name { get; set; }
        }
    }
}
