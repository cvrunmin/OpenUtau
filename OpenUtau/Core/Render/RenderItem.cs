using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;

namespace OpenUtau.Core.Render
{
    public class RenderItem
    {
        public bool Error;
        // For resampler
        public string RawFile;
        public string MidFile;
        public string OutFile;
        public int NoteNum;
        public int Velocity;
        public int Volume;
        public int Modulation;
        public string StrFlags;
        public List<int> PitchData;
        public int DurTick;
        public int RequiredLength;
        public int LengthAdjustment;
		public double Tempo;
        public UOto Oto;
        public UPhoneme Phoneme;

        // For connector
        public double SkipOver;
        public double Overlap;
        public double PosMs;
        public double DurMs;
        public List<ExpPoint> Envelope;
        public bool ln;

        // Sound data
        public CachedSound Sound = null;

        public RenderItem() { }

        public uint HashParameters()
        {
            return Lib.xxHash.CalcStringHash(RawFile + " " + GetResamplerExeArgs());
        }

        public string GetResamplerExeArgs()
        {
            // fresamp.exe <infile> <outfile> <tone> <velocity> <flags> <offset> <length_req>
            // <fixed_length> <endblank> <volume> <modulation> <pitch>
            return $"{MusicMath.GetNoteString(NoteNum)} {Velocity:D} {StrFlags} {Oto.Offset} {RequiredLength:D} {Oto.Consonant} {Oto.Cutoff} {Volume:D} {Modulation:D} !{Tempo} {(String.Join(",", PitchData))}";
        }
    }
}
