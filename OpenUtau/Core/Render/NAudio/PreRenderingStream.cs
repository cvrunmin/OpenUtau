using NAudio.Wave;
using OpenUtau.Core.USTx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.Render.NAudio
{
    class PreRenderingStream : WaveStream, ICmdSubscriber
    {
        private WaveFormat _waveFormat = new WaveFormat(Core.Util.Preferences.Default.SamplingRate, Core.Util.Preferences.Default.BitDepth, 2);

        public override WaveFormat WaveFormat => _waveFormat;

        public UWaveMixerStream Mixer { get; private set; }

        private Dictionary<int, (UWaveMixerStream Source, TrackWaveChannel Wrap)> Tracks = new Dictionary<int, (UWaveMixerStream Source, TrackWaveChannel Wrap)>();

        private Dictionary<int, (float Volume, float Pan)> TrackModifiers = new Dictionary<int, (float Volume, float Pan)>();

        private Dictionary<int, (WaveStream Source, UWaveOffsetStream Warp)> Parts = new Dictionary<int, (WaveStream Source, UWaveOffsetStream Warp)>();

        private Dictionary<(int Part, int Note), List<RenderItemSampleProvider>> Notes = new Dictionary<(int, int), List<RenderItemSampleProvider>>();

        public override long Length => Mixer.Length;

        public override long Position { get => Mixer.Position; set => Mixer.Position = value; }

        public PreRenderingStream(WaveFormat format) {
            _waveFormat = format;
            Mixer = new UWaveMixerStream(format);
            Subscribe(DocManager.Inst);
        }

        public void AddTrack(USTx.UProject project, USTx.UTrack track)
        {
            var parts = project.Parts.Where(part => part.TrackNo == track.TrackNo);
            //var tc = await RenderDispatcher.Inst.RenderTrackStream(project, track, System.Threading.CancellationToken.None, true);
            var mix = new UWaveMixerStream();
            foreach (var item in parts)
            {
                if (item is UWavePart wp)
                {
                    AddPart(project, wp);
                }
                else if (item is UVoicePart vp)
                {
                    AddPart(project, vp);
                }
                mix.AddInputStream(Parts[item.PartNo].Warp);
            }
            AddTrack(track.TrackNo, mix, (float)track.Volume, (float)track.Pan);
        }

        public void AddTrack(int trackno, UWaveMixerStream track, float volume = 1, float pan = 0) {
            var warp = new TrackWaveChannel(track, volume, pan);
            if (!Tracks.ContainsKey(trackno)) Tracks.Add(trackno, (track, warp));
            else
            {
                Mixer.RemoveInputStream(Tracks[trackno].Wrap);
                Tracks[trackno] = (track, warp);
            }
            Mixer.AddInputStream(warp);
        }

        public bool RemoveTrack(int trackno) {
            Mixer.RemoveInputStream(Tracks[trackno].Wrap);
            return Tracks.Remove(trackno);
        }

        public void AddPart(USTx.UProject project, UWavePart part) {
            try {
                var stream = new AudioFileReader(part.FilePath);
                var s1 = new UWaveOffsetStream(stream, TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)), TimeSpan.FromMilliseconds(project.TickToMillisecond(part.HeadTrimTick, part.PosTick)), TimeSpan.FromMilliseconds(project.TickToMillisecond(part.DurTick, part.PosTick)));
                if (Parts.ContainsKey(part.PartNo)) Parts[part.PartNo] = (stream, s1);
                else Parts.Add(part.PartNo, (stream, s1));
            }
            catch {
                System.Diagnostics.Debug.WriteLine($"cannot add wave part {part.PartNo}\"{part.Name}\"");
            }
        }

        public void AddPart(USTx.UProject project, UVoicePart part)
        {
            try
            {
                var sp = new UWaveMixerStream(WaveFormat);
                foreach (var note in part.Notes)
                {
                    AddNote(project, part, note);
                }
                foreach (var item in Notes.Where(pair=>pair.Key.Part == part.PartNo).SelectMany(pair=>pair.Value))
                {
                    sp.AddInputStream(item);
                }
                var s1 = new UWaveOffsetStream(sp, TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)), TimeSpan.Zero, TimeSpan.FromMilliseconds(project.TickToMillisecond(part.DurTick, part.PosTick)));
                if (Parts.ContainsKey(part.PartNo)) Parts[part.PartNo] = (sp, s1);
                else Parts.Add(part.PartNo, (sp, s1));
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"cannot add voice part {part.PartNo}\"{part.Name}\"");
            }
        }

        public void RemovePart(int part) {
            var ps = Parts[part];
            var tr = DocManager.Inst.Project.Parts[part].TrackNo;
            Tracks[tr].Source.RemoveInputStream(ps.Warp);
            Parts.Remove(part);
        }

        public void AddNote(USTx.UProject project, UVoicePart part, UNote note) {
            var items = ResamplerInterface.RenderNote(project, part, note);
            (int PartNo, int NoteNo) key = (part.PartNo, note.NoteNo);
            List<RenderItemSampleProvider> list = items.Select(item => new RenderItemSampleProvider(item)).ToList();
            if (Notes.ContainsKey(key))
            {
                Notes[key] = list;
            }
            else
            {
                Notes.Add(key, list);
            }
        }

        public void RemoveNote(int part, int note) {
            if(Parts[part].Source is UWaveMixerStream str)
            {
                var ri = Notes[(part, note)];
                ri.ForEach(risp => str.RemoveInputStream(risp));
            }
            Notes.Remove((part, note));
        }

        public TrackWaveChannel GetTrack(int track) => Tracks[track].Wrap;

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Mixer.Read(buffer, offset, count);
        }

        private ICmdPublisher publisher;

        public void Subscribe(ICmdPublisher publisher)
        {
            if (this.publisher != null) this.publisher.UnSubscribe(this);
            this.publisher = publisher;
            publisher?.Subscribe(this);
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is PartCommand pc)
            {

            }
            else if(cmd is NoteCommand nc)
            {

            }
            else if (cmd is ExpCommand ec)
            {

            }
            else if (cmd is TrackCommand tc)
            {

            }
        }

        public void PostOnNext(UCommandGroup cmds, bool isUndo)
        {
            foreach (var cmd in cmds.Commands)
            {
                if (cmd is TrackCommand tc)
                {
                    if (tc is AddTrackCommand) {
                        AddTrack(tc.project, tc.track);
                    }
                    else if (tc is RemoveTrackCommand)
                    {
                        RemoveTrack(tc.track.TrackNo);
                    }
                }
                else if (cmd is PartCommand pc)
                {
                    if (pc is RemovePartCommand) {
                        RemovePart(pc.part.PartNo);
                    }
                    else if(pc is ReplacePartCommand rpc)
                    {
                        //AddPart(pc.project, isUndo ? rpc.PartReplaced : rpc.PartReplacing)
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) {
                publisher?.UnSubscribe(this);
                Mixer.Dispose();
                foreach (var track in Tracks.Values)
                {
                    track.Wrap.Dispose();
                }
                Tracks.Clear();
            }
        }

    }
}
