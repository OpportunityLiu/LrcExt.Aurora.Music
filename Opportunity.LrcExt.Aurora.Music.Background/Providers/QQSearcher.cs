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
    internal sealed class QQSearcher : ISearcher
    {
        private static HttpClient httpClient = new HttpClient
        {
            DefaultRequestHeaders =
            {
                Referer = new Uri("https://y.qq.com/"),
            }
        };

        private static readonly DataContractJsonSerializer searchJsonSerializer = new DataContractJsonSerializer(typeof(SearchResult));

        public IAsyncOperation<IEnumerable<ILrcInfo>> FetchLrcListAsync(string artist, string title)
        {
            return AsyncInfo.Run<IEnumerable<ILrcInfo>>(async token =>
            {
                var buf = await httpClient.GetBufferAsync(new Uri("https://c.y.qq.com/soso/fcgi-bin/client_search_cp?format=json&remoteplace=txt.yqq.song&w=" + title));
                using (var stream = buf.AsStream())
                {
                    var data = (SearchResult)searchJsonSerializer.ReadObject(stream);
                    if (data.code != 0 || data.data.song.list is null || data.data.song.list.Length == 0)
                        return Array.Empty<ILrcInfo>();
                    var lrc = new QQLrcInfo[data.data.song.list.Length];
                    for (var i = 0; i < lrc.Length; i++)
                    {
                        lrc[i] = new QQLrcInfo(data.data.song.list[i]);
                    }
                    return lrc;
                }
            });
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
                : base(song.songname ?? "",
                      song.singer is null ? "" : string.Join(", ", song.singer.Select(s => s?.name ?? "")),
                      song.albumname ?? "")
            {
                this.id = song.songid;
            }

            private readonly int id;

            public override IAsyncOperation<string> FetchLryics()
            {
                return AsyncInfo.Run(async token =>
                {
                    var uri = new Uri("https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric.fcg?format=json&callback=cb&musicid=" + this.id);
                    var str = await httpClient.GetStringAsync(uri);
                    str = str.Substring(3, str.Length - 4);
                    using (var stream = Encoding.UTF8.GetBytes(str).AsBuffer().AsStream())
                    {
                        var data = (LrcResult)lrcJsonSerializer.ReadObject(stream);
                        if (data.code != 0 || string.IsNullOrEmpty(data.lyric) ||
                            //"[00:00:00]此歌曲为没有填词的纯音乐，请您欣赏"
                            data.lyric == "WzAwOjAwOjAwXeatpOatjOabsuS4uuayoeacieWhq+ivjeeahOe6r+mfs+S5kO+8jOivt+aCqOaso+i1jw==")
                            return "";
                        var lyric = Lyrics.Parse<Line>(Encoding.UTF8.GetString(Convert.FromBase64String(data.lyric))).Lyrics;
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
            public int code { get; set; }
            [DataMember]
            public Data data { get; set; }
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
            public string lyric { get; set; }
            [DataMember]
            public Singer[] singer { get; set; }
            [DataMember]
            public int songid { get; set; }
            [DataMember]
            public string songname { get; set; }
            [DataMember]
            public string albumname { get; set; }
        }

        [DataContract]
        public class Singer
        {
            [DataMember]
            public int id { get; set; }
            [DataMember]
            public string name { get; set; }
        }
    }
}
