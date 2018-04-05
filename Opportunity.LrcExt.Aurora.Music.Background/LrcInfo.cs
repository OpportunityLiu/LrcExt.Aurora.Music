using System.Text;
using System;
using Windows.Foundation;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    internal abstract class LrcInfo : ILrcInfo
    {
        public string Artist { get; }
        public string Title { get; }
        public string Album { get; }

        public abstract IAsyncOperation<string> FetchLryics();

        internal LrcInfo(string title, string artist, string album)
        {
            this.Title = title;
            this.Artist = artist;
            this.Album = album;
        }
    }
}