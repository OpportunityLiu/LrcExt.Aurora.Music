using Opportunity.Helpers.Universal.AsyncHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.Foundation;
using Windows.Web.Http;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal sealed class ViewLyricsSearcher : ISearcher
    {
        private static readonly HttpClient HttpClient = getClient();

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

        private const string url = "http://search.crintsoft.com/searchlyrics.htm";
        private const string clientUserAgent = "MiniLyrics";
        private const string clientTag = "client=\"ViewLyricsOpenSearcher\"";
        private const string searchQueryBase = "<?xml version='1.0' encoding='utf-8' ?><searchV1 artist=\"{0}\" title=\"{1}\" client=\"MiniLyrics\" RequestPage='0' />";
        private const string searchQueryPage = " RequestPage='{0}'";

        private static readonly byte[] magickey = Encoding.UTF8.GetBytes("Mlv1clt4.0");

        public Task<IEnumerable<LrcInfo>> FetchLrcListAsync(string artist, string title)
        {
            return searchQuery(string.Format(searchQueryBase, artist, title));
        }

        private static async Task<IEnumerable<LrcInfo>> searchQuery(string searchQuery)
        {
            var r = await HttpClient.PostAsync(new Uri(url), new HttpBufferContent(assembleQuery(searchQuery).AsBuffer()));

            var data = (await r.Content.ReadAsBufferAsync()).ToArray();
            var xml = decryptResultXML(data);
            return parseResultXML(xml);
        }

        private static byte[] assembleQuery(string value)
        {
            var vData = Encoding.UTF8.GetBytes(value);

            // Create the variable POG to be used in a dirt code
            var pog = new byte[vData.Length + magickey.Length];

            // POG = XMLQuery + Magic Key
            Array.Copy(vData, 0, pog, 0, vData.Length);
            Array.Copy(magickey, 0, pog, vData.Length, magickey.Length);

            // POG is hashed using MD5
            var md5 = System.Security.Cryptography.MD5.Create();
            var pog_md5 = md5.ComputeHash(pog);

            // Prepare encryption key
            var j = 0;
            for (var i = 0; i < vData.Length; i++)
            {
                j += vData[i];
            }
            var k = (byte)(j / vData.Length);

            // Value is encrypted
            for (var m = 0; m < vData.Length; m++)
                vData[m] = (byte)(k ^ vData[m]);

            // Prepare result code
            var result = new MemoryStream(vData.Length + pog_md5.Length + 6);
            // Write Header
            result.WriteByte(0x02);
            result.WriteByte(k);
            result.WriteByte(0x04);
            result.WriteByte(0x00);
            result.WriteByte(0x00);
            result.WriteByte(0x00);

            // Write Generated MD5 of POG problaby to be used in a search cache
            result.Write(pog_md5, 0, pog_md5.Length);

            // Write encrypted value
            result.Write(vData, 0, vData.Length);

            // Return magic encoded query
            return result.GetBuffer();
        }

        private static string decryptResultXML(byte[] value)
        {
            // Get Magic key value
            var magickey = value[1];

            // Prepare output
            var neomagic = new MemoryStream(value.Length - 22);

            // Decrypts only the XML
            for (var i = 22; i < value.Length; i++)
                neomagic.WriteByte((byte)(value[i] ^ magickey));

            // Return value
            return Encoding.UTF8.GetString(neomagic.GetBuffer());
        }

        private static IEnumerable<LrcInfo> parseResultXML(string resultXML)
        {
            var doc = new XmlDocument();
            doc.LoadXml(resultXML);

            var server = new Uri(doc.SelectSingleNode("/return/@server_url").Value);
            var nodelist = doc.SelectNodes("/return/fileinfo");
            var lrclist = new VLLrcInfo[nodelist.Count];
            for (var i = 0; i < lrclist.Length; i++)
            {
                var element = (XmlElement)nodelist[i];
                var album = element.GetAttribute("album");
                var artist = element.GetAttribute("artist");
                var title = element.GetAttribute("title");
                var uri = element.GetAttribute("link");
                lrclist[i] = new VLLrcInfo(title, artist, album, new Uri(server, uri));
            }
            return lrclist;
        }

        private sealed class VLLrcInfo : LrcInfo
        {
            private static readonly HttpClient httpClient = new HttpClient();

            private readonly Uri lrcUri;

            internal VLLrcInfo(string title, string artist, string album, Uri lrcUri) : base(title, artist, album)
            {
                this.lrcUri = lrcUri;
            }

            protected override Task<string> FetchDataAsync() => httpClient.GetStringAsync(lrcUri).AsTask();
        }
    }
}