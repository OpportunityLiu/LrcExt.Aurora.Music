using System.Text;
using System;
using Windows.Foundation;
using System.Diagnostics;

namespace Opportunity.LrcExt.Aurora.Music.Background
{
    [DebuggerDisplay(@"{Artist,nq} - {Title,nq} - {Album,nq}")]
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