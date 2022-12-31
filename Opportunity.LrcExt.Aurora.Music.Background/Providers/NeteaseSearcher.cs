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
    internal sealed class NeteaseSearcher : ISearcher
    {
        private static HttpClient httpClient = _CreateClient();

        private static HttpClient _CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Referer = new("https://music.163.com/");
            client.DefaultRequestHeaders.Add("Origin", "https://music.163.com");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36 Edg/108.0.1462.54");
            return client;
        }

        private static readonly Uri SEARCH_URI = new("https://music.163.com/api/search/pc");

        private static readonly DataContractJsonSerializer searchJsonSerializer = new DataContractJsonSerializer(typeof(SearchResult));

        public async Task<IEnumerable<LrcInfo>> FetchLrcListAsync(string artist, string title)
        {
            var s = new Dictionary<string, string>
            {
                ["s"] = title,
                ["offset"] = "0",
                ["limit"] = "5",
                ["type"] = "1",
            };
            var r = await httpClient.PostAsync(SEARCH_URI, new HttpFormUrlEncodedContent(s));
            var buf = await r.Content.ReadAsBufferAsync();
            using var stream = buf.AsStream();
            var data = (SearchResult)searchJsonSerializer.ReadObject(stream);
            if (data.code != 200 || data.result.songs is null || data.result.songs.Length == 0)
                return Array.Empty<LrcInfo>();
            var lrc = new NeteaseLrcInfo[data.result.songs.Length];
            for (var i = 0; i < lrc.Length; i++)
            {
                lrc[i] = new NeteaseLrcInfo(data.result.songs[i]);
            }
            return lrc;
        }

        private sealed class NeteaseLrcInfo : LrcInfo
        {
            private static readonly DataContractJsonSerializer lrcJsonSerializer = new DataContractJsonSerializer(typeof(LrcResult));

            [DataContract]
            public class LrcResult
            {
                [DataMember]
                public Lrc lrc { get; set; }
                [DataMember]
                public int code { get; set; }
            }

            [DataContract]
            public class Lrc
            {
                [DataMember]
                public int version { get; set; }
                [DataMember]
                public string lyric { get; set; }
            }

            internal NeteaseLrcInfo(Song song)
                : base(song.name ?? "",
                      song.artists is null ? "" : string.Join(", ", song.artists.Select(a => a?.name ?? "")),
                      song?.album?.name ?? "")
            {
                id = song.id;
            }

            private readonly int id;

            protected override async Task<string> FetchDataAsync()
            {
                var uri = new Uri($"http://music.163.com/api/song/lyric?os=pc&id={id}&lv=-1");
                var buf = await httpClient.GetBufferAsync(uri);
                using var stream = buf.AsStream();
                var data = (LrcResult)lrcJsonSerializer.ReadObject(stream);
                if (data.code != 200)
                    return null;
                return data?.lrc?.lyric;
            }
        }


        [DataContract]
        public class SearchResult
        {
            [DataMember]
            public Result result { get; set; }
            [DataMember]
            public int code { get; set; }
        }

        [DataContract]
        public class Result
        {
            [DataMember]
            public Song[] songs { get; set; }
            [DataMember]
            public int songCount { get; set; }
        }

        [DataContract]
        public class Song
        {
            [DataMember]
            public string name { get; set; }
            [DataMember]
            public int id { get; set; }
            [DataMember]
            public Artist[] artists { get; set; }
            [DataMember]
            public Album album { get; set; }
        }

        [DataContract]
        public class Album
        {
            [DataMember]
            public string name { get; set; }
            [DataMember]
            public int id { get; set; }
            [DataMember]
            public Artist[] artists { get; set; }
        }

        [DataContract]
        public class Artist
        {
            [DataMember]
            public string name { get; set; }
            [DataMember]
            public int id { get; set; }
        }
    }
}
