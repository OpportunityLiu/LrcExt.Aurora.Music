using Opportunity.LrcParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Web.Http;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal sealed class NeteaseSearcher : ISearcher
    {
        private static HttpClient httpClient = new HttpClient
        {
            DefaultRequestHeaders =
            {
                Referer = new Uri("http://music.163.com/"),
                Cookie =
                {
                    new Windows.Web.Http.Headers.HttpCookiePairHeaderValue("appver","1.5.0.75771"),
                },
            }
        };

        private static readonly Uri SEARCH_URI = new Uri("http://music.163.com/api/search/pc");

        private static readonly DataContractJsonSerializer searchJsonSerializer = new DataContractJsonSerializer(typeof(SearchResult));

        public IAsyncOperation<IEnumerable<ILrcInfo>> FetchLrcListAsync(string artist, string title)
        {
            return AsyncInfo.Run<IEnumerable<ILrcInfo>>(async token =>
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
                using (var stream = buf.AsStream())
                {
                    var data = (SearchResult)searchJsonSerializer.ReadObject(stream);
                    if (data.code != 200)
                        return Array.Empty<ILrcInfo>();
                    var lrc = new NeteaseLrcInfo[data.result.songs.Length];
                    for (var i = 0; i < lrc.Length; i++)
                    {
                        lrc[i] = new NeteaseLrcInfo(data.result.songs[i]);
                    }
                    return lrc;
                }
            });
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
                this.id = song.id;
            }

            private readonly int id;

            public override IAsyncOperation<string> FetchLryics()
            {
                return AsyncInfo.Run(async token =>
                {
                    var uri = new Uri($"http://music.163.com/api/song/lyric?os=pc&id={this.id.ToString()}&lv=-1");
                    var buf = await httpClient.GetBufferAsync(uri);
                    using (var stream = buf.AsStream())
                    {
                        var data = (LrcResult)lrcJsonSerializer.ReadObject(stream);
                        if (data.code != 200 || data?.lrc?.lyric is null)
                            return "";
                        var lyric = Lyrics.Parse<Line>(data.lrc.lyric);
                        if (lyric.Lines.Count == 0)
                        {
                            return null;
                        }
                        lyric.Lines.Sort();
                        if (string.IsNullOrEmpty(lyric.MetaData.Title))
                            lyric.MetaData.Title = Title;
                        if (string.IsNullOrEmpty(lyric.MetaData.Artist))
                            lyric.MetaData.Artist = Artist;
                        if (string.IsNullOrEmpty(lyric.MetaData.Album))
                            lyric.MetaData.Album = Album;
                        if (lyric.Lines.Count != 0 && lyric.Lines[0].Timestamp != default)
                            lyric.Lines.Add(new Line());
                        return lyric.ToString();
                    }
                });
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
