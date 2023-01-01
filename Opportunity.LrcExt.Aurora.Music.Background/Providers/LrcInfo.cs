using Opportunity.LrcParser;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    [DebuggerDisplay(@"{Artist,nq} - {Title,nq} - {Album,nq}")]
    internal abstract class LrcInfo
    {
        internal LrcInfo(string title, string artist, string album)
        {
            Title = title;
            Artist = artist;
            Album = album;
        }

        internal LrcInfo(string title, IEnumerable<string> artists, string album)
            : this(title, artists is null ? null : string.Join(", ", artists), album)
        { }

        public string Artist { get; }
        public string Title { get; }
        public string Album { get; }

        protected abstract Task<string> FetchDataAsync();

        private string _FormatLycis(string lrc)
        {
            if (string.IsNullOrWhiteSpace(lrc)) return null;
            var lyrics = Lyrics.Parse(lrc).Lyrics;
            if (lyrics.Lines.Count == 0) return null;
            lyrics.Lines.Sort();
            if (string.IsNullOrEmpty(lyrics.MetaData.Title))
                lyrics.MetaData.Title = Title;
            if (string.IsNullOrEmpty(lyrics.MetaData.Artist))
                lyrics.MetaData.Artist = Artist;
            if (string.IsNullOrEmpty(lyrics.MetaData.Album))
                lyrics.MetaData.Album = Album;
            if (lyrics.Lines[0].Timestamp != default)
            {
                if (string.IsNullOrWhiteSpace(lyrics.Lines[0].Content))
                    lyrics.Lines[0].Timestamp = default;
                else
                    lyrics.Lines.Insert(0, new Line());
            }
            foreach (var line in lyrics.Lines)
            {
                line.Content = HttpUtility.HtmlDecode(line.Content);
            }
            foreach (var meta in MetaDataType.PreDefined.Values)
            {
                if(meta.DataType == typeof(string) && lyrics.MetaData.TryGetValue(meta, out var value))
                {
                    lyrics.MetaData[meta] = HttpUtility.HtmlDecode(value);
                }
            }
            lyrics.PreApplyOffset();
            return lyrics.ToString();
        }

        public async Task<string> FetchAsync()
        {
            var lrc = await FetchDataAsync();
            return _FormatLycis(lrc);
        }
    }
}