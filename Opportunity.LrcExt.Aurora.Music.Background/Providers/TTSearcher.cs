using Opportunity.Helpers.Universal.AsyncHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Windows.Data.Html;
using Windows.Foundation;
using Windows.Web.Http;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal sealed class TTSearcher : ISearcher
    {

        public async Task<IEnumerable<LrcInfo>> FetchLrcListAsync(string artist, string title)
        {
            var result = await _SendRequestAsync<SearchResult>("search", new()
                {
                    { "pageSize", "20" },
                    { "type", "1" },
                    { "word", title },
                });
            if (result.typeTrack == null || result.typeTrack.Length == 0) return null;
            return result.typeTrack.Where(t => !string.IsNullOrEmpty(t.lyric)).Select(t => new TTLrcInfo(t));
        }

        private sealed class TTLrcInfo : LrcInfo
        {
            private readonly string lrcUrl;
            internal TTLrcInfo(Track track) : base(track.title, track.artist?.Select(a => a.name), track.albumTitle)
            {
                lrcUrl = track.lyric;
            }

            protected override async Task<string> FetchDataAsync()
            {
                if (string.IsNullOrEmpty(lrcUrl)) return null;
                return await httpClient.GetStringAsync(new Uri(lrcUrl));
            }
        }

        private static readonly HttpClient httpClient = createClient();
        private static HttpClient createClient()
        {
            var r = new HttpClient();
            r.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            r.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36 Edg/108.0.1462.54");
            r.DefaultRequestHeaders.Add("device-id", "device-id");
            r.DefaultRequestHeaders.Add("from", "web");
            r.DefaultRequestHeaders.Referer = new Uri("https://music.91q.com/");
            return r;
        }

        private static async Task<T> _SendRequestAsync<T>(string method, Dictionary<string, string> param)
        {
            var appid = 16073360;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sign = md5(string.Join('&', param.Concat(new[] {
                    KeyValuePair.Create("appid", appid.ToString()),
                    KeyValuePair.Create("timestamp", timestamp.ToString()),
                }).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")) + "0b50b02fd0d73a9c4c8c3a781c30845f");
            var uri = $"https://music.91q.com/v1/{method}?sign={sign}&{string.Join('&', param.OrderBy(kv => kv.Key).Select(kv => $"{HttpUtility.UrlEncode(kv.Key)}={HttpUtility.UrlEncode(kv.Value)}"))}&appid={appid}&timestamp={timestamp}";

            var buf = await httpClient.GetBufferAsync(new(uri));
            using var stream = buf.AsStream();
            var serializer = new DataContractJsonSerializer(typeof(Response<T>));
            var response = (Response<T>)serializer.ReadObject(stream);
            if (response is null) throw new InvalidOperationException("Malformed response");
            if (!response.state) throw new InvalidOperationException(response.errmsg ?? "Bad state");
            if (response.data is null) throw new InvalidOperationException("Empty response data");
            return response.data;

            static string md5(string input)
            {
                using (var hasher = System.Security.Cryptography.MD5.Create())
                {
                    var inputBytes = Encoding.UTF8.GetBytes(input);
                    var hashBytes = hasher.ComputeHash(inputBytes);
                    var sb = new StringBuilder();
                    for (var i = 0; i < hashBytes.Length; i++)
                    {
                        sb.Append(hashBytes[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
        }


        [DataContract]
        public class Response<T>
        {
            [DataMember]
            public string errmsg { get; set; }
            [DataMember]
            public int errno { get; set; }

            [DataMember]
            public bool state { get; set; }

            [DataMember]
            public T data { get; set; }
        }

        [DataContract]
        public class SearchResult
        {
            [DataMember]
            public Track[] typeTrack { get; set; }
        }

        [DataContract]
        public class Track
        {
            [DataMember]
            public string id { get; set; }
            [DataMember]
            public string title { get; set; }
            [DataMember]
            public Artist[] artist { get; set; }
            [DataMember]
            public string albumTitle { get; set; }
            [DataMember]
            public string lyric { get; set; }
        }

        [DataContract]
        public class Artist
        {
            [DataMember]
            public string artistTypeName { get; set; }
            [DataMember]
            public string name { get; set; }
        }
    }
}