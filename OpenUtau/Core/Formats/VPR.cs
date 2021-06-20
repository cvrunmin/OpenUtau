using JsonFx.Serialization.Resolvers;
using OpenUtau.Core.USTx;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.Formats
{
    static class VPR
    {
        [DataContract]
        private class VPRSequance
        {
            [DataMember(Name = "title")]
            public string Title { get; set; }

            [DataMember(Name = "masterTrack")]
            public _MasterTrack MasterTrack { get; set; }

            [DataMember(Name = "tracks")]
            public _Track[] Tracks { get; set; }

            [DataContract]
            public class _MasterTrack
            {
                [DataMember(Name = "tempo")]
                public TempoData Tempo { get; set; }

                [DataContract]
                public class TempoData
                {
                    [DataMember(Name = "global")]
                    public _GlobalTempo GlobalTempo { get; set; }

                    [DataMember(Name = "events")]
                    public EventDetail[] Events { get; set; }

                    [DataContract]
                    public class _GlobalTempo
                    {
                        [DataMember(Name = "isEnabled")]
                        public bool Enable { get; set; }
                        [DataMember(Name = "value")]
                        public int Value { get; set; }
                    }
                }
            }

            [DataContract]
            public class EventDetail
            {
                [DataMember(Name = "pos")]
                public int Position { get; set; }
                [DataMember(Name = "value")]
                public int Value { get; set; }
            }

            [DataContract]
            public class _Track
            {
                [DataMember(Name = "type")]
                public int Type { get; set; }
                [DataMember(Name = "name")]
                public string Name { get; set; }

                [DataMember(Name = "parts")]
                public _Part[] Parts { get; set; }

                [DataContract]
                public class _Part
                {
                    [DataMember(Name = "name")]
                    public string Name { get; set; }
                    [DataMember(Name = "pos")]
                    public int Position { get; set; }
                    [DataMember(Name = "duration")]
                    public int Duration { get; set; }
                    [DataMember(Name = "notes")]
                    public _Note[] Notes { get; set; }

                    [DataContract]
                    public class _Note
                    {
                        [DataMember(Name = "lyric")]
                        public string Lyric { get; set; }
                        [DataMember(Name = "pos")]
                        public int Position { get; set; }
                        [DataMember(Name = "duration")]
                        public int Duration { get; set; }
                        [DataMember(Name = "number")]
                        public int NoteNum { get; set; }
                        [DataMember(Name = "velocity")]
                        public int Velocity { get; set; }
                    }
                }
            }
        }
        public static UProject Load(string path)
        {
            using (var vpr = ZipFile.OpenRead(path))
            {
                var entry = vpr.GetEntry("Project\\sequence.json");
                var content = new JsonFx.Json.JsonReader(new JsonFx.Serialization.DataReaderSettings(new DataContractResolverStrategy())).Read<VPRSequance>(new StreamReader(entry.Open()));

                UProject uproject = new UProject();
                uproject.RegisterExpression(new IntExpression(null, "velocity", "VEL") { Data = 64, Min = 0, Max = 127 });
                uproject.RegisterExpression(new IntExpression(null, "volume", "VOL") { Data = 100, Min = 0, Max = 200 });
                uproject.RegisterExpression(new IntExpression(null, "opening", "OPE") { Data = 127, Min = 0, Max = 127 });
                uproject.RegisterExpression(new IntExpression(null, "accent", "ACC") { Data = 50, Min = 0, Max = 100 });
                uproject.RegisterExpression(new IntExpression(null, "decay", "DEC") { Data = 50, Min = 0, Max = 100 });
                uproject.RegisterExpression(new IntExpression(null, "release", "REL") { Data = 50, Min = 0, Max = 100 });

                if (content.MasterTrack.Tempo.GlobalTempo.Enable)
                {
                    uproject.BPM = content.MasterTrack.Tempo.GlobalTempo.Value / 100D;
                }
                else
                {
                    uproject.BPM = content.MasterTrack.Tempo.Events[0].Value / 100D;
                    for (int i = 1; i < content.MasterTrack.Tempo.Events.Length; i++)
                    {
                        var _event = content.MasterTrack.Tempo.Events[i];
                        uproject.SubBPM.Add(_event.Position, _event.Value / 100D);
                    }
                }
                uproject.Name = content.Title;
                uproject.FilePath = path;
                foreach (var _track in content.Tracks)
                {
                    if (_track.Type != 0) continue; //Not Voice Part
                    var track = new UTrack() { Name = _track.Name, TrackNo = uproject.Tracks.Count, Color = AddTrackCommand.GenerateColor() };
                    uproject.Tracks.Add(track);
                    foreach (var _part in _track.Parts)
                    {
                        var part = uproject.CreateVoicePart(track.TrackNo, _part.Position);
                        part.Name = _part.Name;
                        part.DurTick = _part.Duration;
                        for (int i = 0; i < _part.Notes.Length; i++)
                        {
                            var _note = _part.Notes[i];
                            var note = uproject.CreateNote(_note.NoteNum, _note.Position, _note.Duration);
                            note.Lyric = _note.Lyric;
                            note.Expressions["velocity"].Data = _note.Velocity;
                            part.Notes.Add(note);
                        }
                        uproject.Parts.Add(part);
                    }
                }
                return uproject;
            }
        }
    }


}
