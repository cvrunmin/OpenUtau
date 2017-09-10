using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OpenUtau.Core.USTx
{
    public class UTrack
    {
        public string Name { get; set; } = "New Track";
        public string Comment { get; set; } = "";
        public USinger Singer { get; set; }

        public string SingerName { get { if (Singer != null) return Singer.DisplayName; else return "[No Signer]"; } }
        public int TrackNo { set; get; }
        public int DisplayTrackNo { get { return TrackNo + 1; } }
        public bool Mute { set; get; }
        public bool Solo { set; get; }
        public double Volume { set; get; }
        public double Pan { set; get; }
        public bool Amended { get; set; }
        public string OverrideRenderEngine { get; set; }

        public Color Color { get; set; }

        public UTrack() { }
    }
}
