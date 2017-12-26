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
        public int DisplayTrackNo => TrackNo + 1;
        private bool _mute;
        private bool _solo;
        public bool Mute { set => _mute = value;
            get => ActuallyMuted;
        }
        public bool Solo { set => _solo = value;
            get => _solo;
        }
        public bool ActuallyMuted => _mute || (!Solo && (DocManager.Inst?.Project?.Tracks?.Any(t=>t.Solo) ?? false));
        public double Volume { set; get; }
        public double Pan { set; get; }
        public bool Amended { get; set; }
        public string OverrideRenderEngine { get; set; }

        public Color Color { get; set; } = Colors.Transparent;

        public UTrack() { }
    }
}
