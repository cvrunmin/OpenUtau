using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.Script.Serialization;

using System.Xml.Linq;

using OpenUtau.Core;
using OpenUtau.Core.USTx;
using OpenUtau.Core.Lib;
using System.Windows.Media;

namespace OpenUtau.Core.Formats
{
    class USTx
    {
        static UProject Project;
        private const string thisUstxVersion = "0.1";

        internal class UNoteConvertor : JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                if (Project == null) Project = Create();
                UNote result = Project.CreateNote();
                result.Lyric = dictionary["y"] as string;
                result.NoteNum = Convert.ToInt32(dictionary["n"]);
                result.PosTick = Convert.ToInt32(dictionary["pos"]);
                result.DurTick = Convert.ToInt32(dictionary["dur"]);
                result.PitchBend.SnapFirst = Convert.ToBoolean(dictionary["pitsnap"]);

                var pho = dictionary["pho"] as ArrayList;
                result.Phonemes.Clear();
                foreach (var p in pho)
                {
                    var _p = serializer.ConvertToType<UPhoneme>(p);
                    _p.Parent = result;
                    result.Phonemes.Add(_p);
                }

                result.PitchBend.SnapFirst = Convert.ToBoolean(dictionary["pitsnap"]);
                var pit = dictionary["pit"] as ArrayList;
                var pitshape = dictionary["pitshape"] as ArrayList;
                for (int i = 0; i < pitshape.Count; i++)
                {
                    switch (pitshape[i])
                    {
                        case "io":
                            pitshape[i] = "InOut";
                            break;
                        case "i":
                            pitshape[i] = "In";
                            break;
                        case "o":
                            pitshape[i] = "Out";
                            break;
                        case "l":
                            pitshape[i] = "Linear";
                            break;
                        default:
                            break;
                    }
                }
                double x = 0, y = 0;
                result.PitchBend.Points.Clear();
                for (int i = 0; i < pit.Count; i ++ )
                {
                    if (i % 2 == 0)
                        x = Convert.ToDouble(pit[i]);
                    else
                    {
                        y = Convert.ToDouble(pit[i]);
                        result.PitchBend.AddPoint(new PitchPoint(x, y,
                            (PitchPointShape)Enum.Parse(typeof(PitchPointShape), (string)pitshape[i / 2])));
                    }
                }

                if (dictionary.ContainsKey("vbr"))
                {
                    var vbr = dictionary["vbr"] as ArrayList;
                    result.Vibrato.Length = Convert.ToDouble(vbr[0]);
                    result.Vibrato.Period = Convert.ToDouble(vbr[1]);
                    result.Vibrato.Depth = Convert.ToDouble(vbr[2]);
                    result.Vibrato.In = Convert.ToDouble(vbr[3]);
                    result.Vibrato.Out = Convert.ToDouble(vbr[4]);
                    result.Vibrato.Shift = Convert.ToDouble(vbr[5]);
                    result.Vibrato.Drift = Convert.ToDouble(vbr[6]);
                    result.Vibrato.Enable();
                }

                var exp = dictionary["exp"] as Dictionary<string, object>;
                foreach (var pair in exp.Where(pair=>result.Expressions.ContainsKey(pair.Key)))
                    result.Expressions[pair.Key].Data = pair.Value;

                return result;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
            {
                var _obj = obj as UNote;
                Dictionary<string, object> result = new Dictionary<string, object>();
                if (_obj == null) return result;

                result.Add("y", _obj.Lyric);
                result.Add("n", _obj.NoteNum);
                result.Add("pos", _obj.PosTick);
                result.Add("dur", _obj.DurTick);
                result.Add("pho", _obj.Phonemes);

                var pit = new List<double>();
                var pitshape = new List<string>();
                foreach (var p in _obj.PitchBend.Points) { pit.Add(p.X); pit.Add(p.Y); pitshape.Add(p.Shape.ToString()); }
                result.Add("pitsnap", _obj.PitchBend.SnapFirst);
                result.Add("pit", pit);
                result.Add("pitshape", pitshape);

                if (_obj.Vibrato.Length > 0 && _obj.Vibrato.Depth > 0)
                {
                    var vbr = new List<double>
                    {
                        _obj.Vibrato.Length,
                        _obj.Vibrato.Period,
                        _obj.Vibrato.Depth,
                        _obj.Vibrato.In,
                        _obj.Vibrato.Out,
                        _obj.Vibrato.Shift,
                        _obj.Vibrato.Drift
                    };
                    result.Add("vbr", vbr);
                }

                var exp = new Dictionary<string, int>();
                foreach (var pair in _obj.Expressions)
                {
                    if (pair.Value is IntExpression)
                    {
                        exp.Add(pair.Key, (int)pair.Value.Data);
                    }
                }
                result.Add("exp", exp);

                return result;
            }

            public override IEnumerable<Type> SupportedTypes
            {
                get { return new List<Type>(new Type[] { typeof(UNote) }); }
            }
        }

        internal class UPhonemeConverter : JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                dictionary.TryGetValue("dur", out object dur);
                UPhoneme result = new UPhoneme()
                {
                    PosTick = Convert.ToInt32(dictionary["pos"]),
                    DurTick = Convert.ToInt32(dur ?? 0),
                    Phoneme = dictionary["pho"] as string,
                    AutoEnvelope = Convert.ToBoolean(dictionary["autoenv"]),
                    AutoRemapped = Convert.ToBoolean(dictionary["remap"])
                };

                if (!result.AutoEnvelope)
                {
                    var env = dictionary["env"] as ArrayList;
                    for (int i = 0; i < 10; i++)
                    {
                        if (i % 2 == 0)
                            result.Envelope.Points[i / 2].X = Convert.ToDouble(env[i]);
                        else
                            result.Envelope.Points[i / 2].Y = Convert.ToDouble(env[i]);
                    }
                }
                return result;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
            {
                var _obj = obj as UPhoneme;
                Dictionary<string, object> result = new Dictionary<string, object>();
                if (_obj == null) return result;

                result.Add("pos", _obj.PosTick);
                result.Add("dur", _obj.DurTick);
                result.Add("pho", _obj.Phoneme);
                result.Add("autoenv", _obj.AutoEnvelope);
                result.Add("remap", _obj.AutoRemapped);

                if (!_obj.AutoEnvelope)
                {
                    var env = new List<double>();
                    foreach (var p in _obj.Envelope.Points) { env.Add(p.X); env.Add(p.Y); }
                    result.Add("env", env);
                }

                return result;
            }

            public override IEnumerable<Type> SupportedTypes
            {
                get { return new List<Type>(new Type[] { typeof(UPhoneme) }); }
            }
        }

        internal class UPartConvertor : JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                UPart result = null;
                if (dictionary.ContainsKey("notes"))
                {
                    result = Project.CreateVoicePart(0,0);
                    var _result = result as UVoicePart;

                    var notes = dictionary["notes"] as ArrayList;
                    foreach (var note in notes)
                    {
                        UNote uNote = serializer.ConvertToType<UNote>(note);
                        uNote.NoteNo = _result.Notes.Count;
                        _result.Notes.Add(uNote);
                    }
                    foreach (var pair in (Dictionary<string, object>)(dictionary["expression"]))
                    {
                        var exp = serializer.ConvertToType(pair.Value, typeof(IntExpression)) as IntExpression;
                        var _exp = new IntExpression(null, pair.Key, exp.Abbr)
                        {
                            Min = exp.Min,
                            Max = exp.Max,
                            Data = exp.Data
                        };
                        if (!_result.Expressions.ContainsKey(pair.Key))
                            _result.Expressions.Add(pair.Key, _exp);
                        else _result.Expressions[pair.Key] = _exp;
                    }
                }
                else if (dictionary.ContainsKey("path"))
                {
                    Uri.TryCreate(dictionary["path"] as string, UriKind.RelativeOrAbsolute, out var uri);
                    if (uri.IsAbsoluteUri)
                    {
                        result = Wave.CreatePart(dictionary["path"] as string);
                    }
                    else
                    {
                        var abs = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Project.FilePath)), Uri.UnescapeDataString(uri.OriginalString));
                        result = Wave.CreatePart(abs);
                        ((UWavePart)result).UseRelativePath = true;
                    }
                    if (dictionary.ContainsKey("headtrimtick"))
                    {
                        ((UWavePart)result).HeadTrimTick = Convert.ToInt32(dictionary["headtrimtick"]);
                        ((UWavePart)result).TailTrimTick = Convert.ToInt32(dictionary["tailtrimtick"]);
                    }
                }

                if (result != null)
                {
                    result.Name = dictionary["name"] as string;
                    result.Comment = dictionary["comment"] as string;
                    result.TrackNo = Convert.ToInt32(dictionary["trackno"]);
                    result.PosTick = Convert.ToInt32(dictionary["pos"]);
                    result.DurTick = Convert.ToInt32(dictionary["dur"]);
                }

                return result;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                var _part = obj as UPart;
                if (_part == null) return result;

                result.Add("name", _part.Name);
                result.Add("comment", _part.Comment);
                result.Add("trackno", _part.TrackNo);
                result.Add("pos", _part.PosTick);
                result.Add("dur", _part.DurTick);

                if (obj is UWavePart)
                {
                    var _obj = obj as UWavePart;
                    if (_obj.UseRelativePath)
                    {
                        Uri.TryCreate(_obj.FilePath, UriKind.Absolute, out var uri1);
                        Uri.TryCreate(Path.GetDirectoryName(DocManager.Inst.Project.FilePath), UriKind.Absolute, out var uri2);
                        var uri = uri2.MakeRelativeUri(uri1);
                        result.Add("path", uri.OriginalString);
                    }
                    else
                    {
                        result.Add("path", _obj.FilePath);
                    }
                    result.Add("headtrimtick", _obj.HeadTrimTick);
                    result.Add("tailtrimtick", _obj.TailTrimTick);
                }
                else if (obj is UVoicePart)
                {
                    var _obj = obj as UVoicePart;
                    result.Add("notes", _obj.Notes);
                    result.Add("expression", _obj.Expressions);
                }

                return result;
            }

            public override IEnumerable<Type> SupportedTypes
            {
                get { return new List<Type>(new Type[] { typeof(UPart), typeof(UVoicePart), typeof(UWavePart) }); }
            }
        }

        internal class UProjectConvertor : JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                UProject result = Create();
                string ustxVersion = dictionary["ustxversion"] as string;
                if (Project == null)
                {
                    USTx.Project = result;
                }
                else
                {
                    result = Project;
                }
                result.Name = dictionary["name"] as string;
                result.Comment = dictionary["comment"] as string;
                result.OutputDir = dictionary["output"] as string;
                result.CacheDir = dictionary["cache"] as string;
                result.BPM = Convert.ToDouble(dictionary["bpm"]);
                result.BeatPerBar = Convert.ToInt32(dictionary["bpbar"]);
                result.BeatUnit = Convert.ToInt32(dictionary["bunit"]);
                result.Resolution = Convert.ToInt32(dictionary["res"]);

                if(dictionary.ContainsKey("subbpm"))
                foreach (var pair in (Dictionary<string, object>)dictionary["subbpm"])
                {
                    result.SubBPM.Add(Convert.ToInt32(pair.Key), Convert.ToDouble(pair.Value));
                }

                foreach (var pair in (Dictionary<string, object>)(dictionary["exptable"]))
                {
                    var exp = serializer.ConvertToType(pair.Value, typeof(IntExpression)) as IntExpression;
                    var _exp = new IntExpression(null, pair.Key, exp.Abbr)
                    {
                        Min = exp.Min,
                        Max = exp.Max,
                        Data = exp.Data
                    };
                    if (!result.ExpressionTable.ContainsKey(pair.Key))
                        result.ExpressionTable.Add(pair.Key, _exp);
                    else result.ExpressionTable[pair.Key] = _exp;
                }

                var singers = dictionary["singers"] as ArrayList;
                foreach (var singer in singers)
                    result.Singers.Add(serializer.ConvertToType(singer, typeof(USinger)) as USinger);

                foreach (var track in dictionary["tracks"] as ArrayList)
                {
                    var _tarck = serializer.ConvertToType(track, typeof(UTrack)) as UTrack;
                    result.Tracks.Add(_tarck);
                }

                foreach (var part in dictionary["parts"] as ArrayList)
                {
                    UPart uPart = serializer.ConvertToType(part, typeof(UPart)) as UPart;
                    uPart.PartNo = result.Parts.Count;
                    if (uPart is UVoicePart voice)
                    {
                        foreach (var note in voice.Notes)
                        {
                            note.PartNo = uPart.PartNo;
                        }
                    }
                    result.Parts.Add(uPart);
                }

                USTx.Project = null;
                return result;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
            {
                Dictionary<string, object> result = new Dictionary<string, object>();

                if (obj is UProject _obj)
                {
                    result.Add("ustxversion", thisUstxVersion);
                    result.Add("name", _obj.Name);
                    result.Add("comment", _obj.Comment);
                    result.Add("output", _obj.OutputDir);
                    result.Add("cache", _obj.CacheDir);
                    result.Add("bpm", _obj.BPM);
                    var processed = new Dictionary<string, object>();
                    foreach (var item in _obj.SubBPM)
                    {
                        processed.Add(item.Key.ToString(), item.Value);
                    }
                    result.Add("subbpm", processed);
                    result.Add("bpbar", _obj.BeatPerBar);
                    result.Add("bunit", _obj.BeatUnit);
                    result.Add("res", _obj.Resolution);
                    result.Add("singers", _obj.Singers);
                    result.Add("tracks", _obj.Tracks.ToArray());
                    result.Add("parts", _obj.Parts);
                    result.Add("exptable", _obj.ExpressionTable);
                }

                return result;
            }

            public override IEnumerable<Type> SupportedTypes
            {
                get { return new List<Type>(new Type[] { typeof(UProject) }); }
            }
        }

        internal class MiscConvertor : JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                if (type == typeof(IntExpression))
                {
                    IntExpression result = new IntExpression(null, "", dictionary["abbr"] as string)
                    {
                        Min = Convert.ToInt32(dictionary["min"]),
                        Max = Convert.ToInt32(dictionary["max"]),
                        Data = dictionary["data"]
                    };
                    return result;
                }
                else if (type == typeof(UTrack))
                {
                    UTrack result = new UTrack()
                    {
                        Name = dictionary["name"] as string,
                        Comment = dictionary["comment"] as string,
                        TrackNo = Convert.ToInt32(dictionary["trackno"]),
                        Singer = string.IsNullOrWhiteSpace(dictionary["singer"] as string) ? null : new USinger() { Name = dictionary["singer"] as string }
                    };
                    if (dictionary.ContainsKey("override-engine"))
                        result.OverrideRenderEngine = dictionary["override-engine"] as string;
                    if(dictionary.ContainsKey("color"))
                    {
                        result.Color = (Color) ColorConverter.ConvertFromString(dictionary["color"] as string);
                    }
                    else
                    {
                        result.Color = AddTrackCommand.GenerateColor();
                    }
                    if (dictionary.TryGetValue("mute", out var mute)) result.Mute = (bool)mute;
                    if (dictionary.TryGetValue("solo", out var solo)) result.Solo = (bool)solo;
                    if (dictionary.TryGetValue("vol", out var vol)) result.Volume = Convert.ToDouble(vol);
                    if (dictionary.TryGetValue("pan", out var pan)) result.Pan = Convert.ToDouble(pan);

                    return result;
                }
                else if (type == typeof(USinger))
                {
                    USinger result = new USinger()
                    {
                        Name = dictionary["name"] as string,
                        Path = dictionary["path"] as string
                    };
                    return result;
                }
                else return null;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
            {
                Dictionary<string, object> result = new Dictionary<string, object>();

                if (obj is UTrack)
                {
                    if (obj is UTrack _obj)
                    {
                        result.Add("trackno", _obj.TrackNo);
                        result.Add("name", _obj.Name);
                        result.Add("comment", _obj.Comment);
                        result.Add("singer", _obj.Singer == null ? "" : _obj.Singer.Name);
                        if (!string.IsNullOrWhiteSpace(_obj.OverrideRenderEngine))
                            result.Add("override-engine", _obj.OverrideRenderEngine);
                        result.Add("color", _obj.Color.ToString());
                        result.Add("mute", _obj.Mute);
                        result.Add("solo", _obj.Solo);
                        result.Add("vol", _obj.Volume);
                        result.Add("pan", _obj.Pan);
                    }
                }
                else if (obj is USinger)
                {
                    if (obj is USinger _obj)
                    {
                        result.Add("name", _obj.Name);
                        result.Add("path", _obj.Path);
                    }
                }
                else if (obj is IntExpression)
                {
                    if (obj is IntExpression _obj)
                    {
                        result.Add("abbr", _obj.Abbr);
                        result.Add("type", _obj.Type);
                        result.Add("min", _obj.Min);
                        result.Add("max", _obj.Max);
                        result.Add("data", _obj.Data);
                    }
                }
                return result;
            }

            public override IEnumerable<Type> SupportedTypes
            {
                get {
                    return new List<Type>(new Type[] {
                        typeof(IntExpression),
                        typeof(UTrack),
                        typeof(USinger)
                    });
                }
            }
        }

        public static UProject Create()
        {
            UProject project = new UProject() { Saved = false };
            project.RegisterExpression(new IntExpression(null, "velocity", "VEL") { Data = 100, Min = 0, Max = 200 });
            project.RegisterExpression(new IntExpression(null, "volume", "VOL") { Data = 100, Min = 0, Max = 200 });
            project.RegisterExpression(new IntExpression(null, "breathiness", "BRE") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "gender", "GEN") { Data = 0, Min = -100, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "lowpass", "LPF") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "highpass", "HPF") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "accent", "ACC") { Data = 100, Min = 0, Max = 200 });
            project.RegisterExpression(new IntExpression(null, "decay", "DEC") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "release", "REL") { Data = 0, Min = 0, Max = 100 });
            return project;
        }

        public static void Save(string file, UProject project)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.RegisterConverters(
                new List<JavaScriptConverter>()
                    {
                        new UProjectConvertor(),
                        new MiscConvertor(),
                        new UPartConvertor(),
                        new UNoteConvertor(),
                        new UPhonemeConverter()
                    });
            StringBuilder str = new StringBuilder();
            try
            {
                project.FilePath = file;
                jss.Serialize(project, str);
                var f_out = new StreamWriter(file);
                f_out.Write(str.ToString());
                f_out.Close();
                project.Saved = true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }

        public static UProject Load(string file)
        {
            AddTrackCommand.colorRandCount = 0;
            UProject project;
            Project = Create();
            Project.FilePath = file;

            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.RegisterConverters(
                new List<JavaScriptConverter>()
                    {
                        new UProjectConvertor(),
                        new MiscConvertor(),
                        new UPartConvertor(),
                        new UNoteConvertor(),
                        new UPhonemeConverter()
                    });

            try
            {
                project = jss.Deserialize(File.ReadAllText(file), typeof(UProject)) as UProject;
                project.Saved = true;
                project.FilePath = file;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                return null;
            }

            project.Singers.RemoveAll(singer => singer == null);

            // Load singers
            for (int i = 0; i < project.Singers.Count; i++)
            {
                var _singer = UtauSoundbank.GetSinger(project.Singers[i].Path, EncodingUtil.DetectFileEncoding(file), DocManager.Inst.Singers);
                    if (_singer != null && project.Singers[i].Name == _singer.Name)
                    {
                        project.Singers[i] = _singer;
                    }
            }

            foreach (var track in project.Tracks)
            {
                if (track.Singer == null) continue;
                foreach (var singer in project.Singers)
                {
                    if (singer.Loaded && track.Singer.Name == singer.Name)
                    {
                        track.Singer = singer;
                    }
                }
            }

            foreach (var part in project.Parts)
            {
                if (part is UVoicePart _part) {
                    foreach (var item in _part.Expressions)
                    {
                        foreach (var note in _part.Notes)
                        {
                            note.VirtualExpressions[item.Key] = (int)note.Expressions[item.Key].Data - (int)item.Value.Data;
                        }
                    }
                }
            }
            
            return project;
        }
    }
}
