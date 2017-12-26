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

            public override IEnumerable<Type> SupportedTypes => new List<Type>(new Type[] { typeof(UNote) });
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

            public override IEnumerable<Type> SupportedTypes => new List<Type>(new Type[] { typeof(UPhoneme) });
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

                    _result.ConvertStyle = dictionary.ContainsKey("convertStyle") ? (dictionary["convertStyle"].Equals("null") ? (bool?)null : Convert.ToBoolean(dictionary["convertStyle"])) : (bool?)null;

                    var notes = dictionary["notes"] as ArrayList;
                    foreach (var note in notes)
                    {
                        UNote uNote = serializer.ConvertToType<UNote>(note);
                        uNote.NoteNo = _result.Notes.Count;
                        _result.Notes.Add(uNote);
                    }
                    foreach (var pair in (Dictionary<string, object>)(dictionary["expression"]))
                    {
                        var exp = ResolveExpression(pair.Key, pair.Value,serializer);
                        if (!_result.Expressions.ContainsKey(pair.Key))
                            _result.Expressions.Add(pair.Key, exp);
                        else _result.Expressions[pair.Key] = exp;
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
                    result.Add("convertStyle", _obj.ConvertStyle.HasValue ? _obj.ConvertStyle.Value.ToString() : "null");
                    result.Add("notes", _obj.Notes);
                    result.Add("expression", _obj.Expressions);
                }

                return result;
            }

            public override IEnumerable<Type> SupportedTypes => new List<Type>(new Type[] { typeof(UPart), typeof(UVoicePart), typeof(UWavePart) });
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
                    var exp = ResolveExpression(pair.Key, pair.Value, serializer);
                    if (!result.ExpressionTable.ContainsKey(pair.Key))
                        result.ExpressionTable.Add(pair.Key, exp);
                    else result.ExpressionTable[pair.Key] = exp;
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

            public override IEnumerable<Type> SupportedTypes => new List<Type>(new Type[] { typeof(UProject) });
        }

        internal class MiscConvertor : JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                if (type == typeof(FlagIntExpression))
                {
                    var result = new FlagIntExpression(null, "", dictionary["abbr"] as string)
                    {
                        Min = Convert.ToInt32(dictionary["min"]),
                        Max = Convert.ToInt32(dictionary["max"]),
                        Data = Convert.ToInt32(dictionary["data"]),
                        Flag = Convert.ToString(dictionary["flag"])
                    };
                    if (dictionary.ContainsKey("default")) result.Default = Convert.ToInt32(dictionary["default"]);
                    return result;
                }
                else if (type == typeof(IntExpression))
                {
                    IntExpression result = new IntExpression(null, "", dictionary["abbr"] as string)
                    {
                        Min = Convert.ToInt32(dictionary["min"]),
                        Max = Convert.ToInt32(dictionary["max"]),
                        Data = Convert.ToInt32(dictionary["data"])
                    };
                    if (dictionary.ContainsKey("default")) result.Default = Convert.ToInt32(dictionary["default"]);
                    return result;
                }
                else if (type == typeof(FlagFloatExpression))
                {
                    var result = new FlagFloatExpression(null, "", dictionary["abbr"] as string)
                    {
                        Min = (float)Convert.ToDouble(dictionary["min"]),
                        Max = (float)Convert.ToDouble(dictionary["max"]),
                        Data = (float)Convert.ToDouble(dictionary["data"]),
                        Flag = Convert.ToString(dictionary["flag"])
                    };
                    if (dictionary.ContainsKey("default")) result.Default = (float)Convert.ToDouble(dictionary["default"]);
                    return result;
                }
                else if (type == typeof(FloatExpression))
                {
                    var result = new FloatExpression(null, "", dictionary["abbr"] as string)
                    {
                        Min = (float)Convert.ToDouble(dictionary["min"]),
                        Max = (float)Convert.ToDouble(dictionary["max"]),
                        Data = (float)Convert.ToDouble(dictionary["data"])
                    };
                    if (dictionary.ContainsKey("default")) result.Default = (float)Convert.ToDouble(dictionary["default"]);
                    return result;
                }
                else if (type == typeof(FlagBoolExpression))
                {
                    var result = new FlagBoolExpression(null, "", dictionary["abbr"] as string)
                    {
                        Data = Convert.ToBoolean(dictionary["data"]),
                        Flag = Convert.ToString(dictionary["flag"])
                    };
                    if (dictionary.ContainsKey("default")) result.Default = Convert.ToBoolean(dictionary["default"]);
                    return result;
                }
                else if (type == typeof(BoolExpression))
                {
                    var result = new BoolExpression(null, "", dictionary["abbr"] as string)
                    {
                        Data = Convert.ToBoolean(dictionary["data"])
                    };
                    if (dictionary.ContainsKey("default")) result.Default = Convert.ToBoolean(dictionary["default"]);
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
                else if (obj is FlagIntExpression)
                {
                    var _obj = obj as FlagIntExpression;
                    result.Add("abbr", _obj.Abbr);
                    result.Add("type", _obj.Type);
                    result.Add("min", _obj.Min);
                    result.Add("max", _obj.Max);
                    result.Add("data", _obj.Data);
                    result.Add("flag", _obj.Flag);
                    result.Add("default", _obj.Default);
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
                        result.Add("default", _obj.Default);
                    }
                }
                else if (obj is FlagFloatExpression)
                {
                    var _obj = obj as FlagFloatExpression;
                    result.Add("abbr", _obj.Abbr);
                    result.Add("type", _obj.Type);
                    result.Add("min", _obj.Min);
                    result.Add("max", _obj.Max);
                    result.Add("data", _obj.Data);
                    result.Add("flag", _obj.Flag);
                    result.Add("default", _obj.Default);
                }
                else if (obj is FloatExpression)
                {
                    if (obj is FloatExpression _obj)
                    {
                        result.Add("abbr", _obj.Abbr);
                        result.Add("type", _obj.Type);
                        result.Add("min", _obj.Min);
                        result.Add("max", _obj.Max);
                        result.Add("data", _obj.Data);
                        result.Add("default", _obj.Default);
                    }
                }
                else if (obj is FlagBoolExpression)
                {
                    var _obj = obj as FlagBoolExpression;
                    result.Add("abbr", _obj.Abbr);
                    result.Add("type", _obj.Type);
                    result.Add("data", _obj.Data);
                    result.Add("flag", _obj.Flag);
                    result.Add("default", _obj.Default);
                }
                else if (obj is BoolExpression)
                {
                    if (obj is BoolExpression _obj)
                    {
                        result.Add("abbr", _obj.Abbr);
                        result.Add("type", _obj.Type);
                        result.Add("data", _obj.Data);
                        result.Add("default", _obj.Default);
                    }
                }
                return result;
            }

            public override IEnumerable<Type> SupportedTypes => new List<Type>(new Type[] {
                typeof(IntExpression),
                typeof(FloatExpression),
                typeof(FlagIntExpression),
                typeof(FlagFloatExpression),
                typeof(BoolExpression),
                typeof(FlagBoolExpression),
                typeof(UTrack),
                typeof(USinger)
            });
        }
        private static UExpression ResolveExpression(string key, object value, JavaScriptSerializer serializer)
        {
            if (((IDictionary<string, object>)value).TryGetValue("type", out var re))
            {
                switch (Convert.ToString(re))
                {
                    case "int":
                        var exp = serializer.ConvertToType<IntExpression>(value);
                        return new IntExpression(exp.Parent, key, exp.Abbr) { Max = exp.Max, Min = exp.Min, Data = (int)exp.Data, Default = exp.Default };
                    case "flag_int":
                        var exp1 = serializer.ConvertToType<FlagIntExpression>(value);
                        return new FlagIntExpression(exp1.Parent, key, exp1.Abbr, exp1.Flag) { Max = exp1.Max, Min = exp1.Min,Data = (int)exp1.Data, Default = exp1.Default };
                    case "float":
                        var exp2 = serializer.ConvertToType<FloatExpression>(value);
                        return new FloatExpression(exp2.Parent, key, exp2.Abbr) { Max = exp2.Max, Min = exp2.Min,Data = (float)exp2.Data, Default = exp2.Default };
                    case "flag_float":
                        var exp3 = serializer.ConvertToType<FlagFloatExpression>(value);
                        return new FlagFloatExpression(exp3.Parent, key, exp3.Abbr, exp3.Flag) { Max = exp3.Max, Min = exp3.Min, Data = (float)exp3.Data, Default = exp3.Default, };
                    case "bool":
                        var exp4 = serializer.ConvertToType<BoolExpression>(value);
                        return new BoolExpression(exp4.Parent, key, exp4.Abbr) { Data = (bool)exp4.Data, Default = exp4.Default };
                    case "flag_bool":
                        var exp5 = serializer.ConvertToType<FlagBoolExpression>(value);
                        return new FlagBoolExpression(exp5.Parent, key, exp5.Abbr, exp5.Flag) { Data = (bool)exp5.Data, Default = exp5.Default };
                    default:
                        break;
                }
            }
            return null;
        }

        public static UProject Create()
        {
            UProject project = new UProject() { Saved = false };
            project.RegisterExpression(new IntExpression(null, "velocity", "VEL") { Data = 100, Min = 0, Max = 200, Default = 100 });
            project.RegisterExpression(new IntExpression(null, "volume", "VOL") { Data = 100, Min = 0, Max = 200,Default = 100 });
            project.RegisterExpression(new FlagIntExpression(null, "breathiness", "BRE", "Y") { Data = 0, Min = 0, Max = 100, Default = 100 });
            project.RegisterExpression(new FlagIntExpression(null, "gender", "GEN", "g") { Data = 0, Min = -100, Max = 100, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "lowpass", "LPF", "H") { Data = 0, Min = 0, Max = 100, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "highpass", "HPF", "C") { Data = 0, Min = 0, Max = 100,Default = 0 });
            project.RegisterExpression(new IntExpression(null, "accent", "ACC") { Data = 100, Min = 0, Max = 200,Default = 100 });
            project.RegisterExpression(new IntExpression(null, "decay", "DEC") { Data = 0, Min = 0, Max = 100, Default = 0 });
            project.RegisterExpression(new IntExpression(null, "release", "REL") { Data = 0, Min = 0, Max = 100, Default = 0 });
            /*
             * Standard Flag
             project.RegisterExpression(new FlagIntExpression(null, "peak compress", "PKC", "P") { Min = 0, Max = 100, Data = 0, Default = 86 });
             project.RegisterExpression(new FlagIntExpression(null, "amplitude modulation", "AMD", "A") { Min = -100, Max = 100, Data = 0, Default = 0 });
             project.RegisterExpression(new FlagIntExpression(null, "unvoiced gain", "UCG", "b") { Data = 0, Min = -20, Max = 100, Default = 0 });
             project.RegisterExpression(new FlagBoolExpression(null, "stretching", "STR", "e") { Data = false, Default = false });
             project.RegisterExpression(new FlagBoolExpression(null, "direct", "DIR", "u") { Data = false, Default = false });
             */
            // Moresampler Flag
            project.RegisterExpression(new FlagBoolExpression(null, "looping", "LOP", "Me") { Data = false, Default = false });
            project.RegisterExpression(new FlagIntExpression(null, "tenseness", "TNS", "Mt") { Min = -100, Max = 100, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "Mbreathiness", "MBR", "Mb") { Min = -100, Max = 100, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "openness", "OPE", "Mo") { Min = -100, Max = 100, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "resonance", "RES", "Mr") { Min = -100, Max = 100, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "dryness", "DRY", "Md") { Min = -100, Max = 100, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "coarseness", "CRS", "MC") { Min = 0, Max = 100, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "growl", "GRW", "MG") { Min = 0, Max = 100, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "distort", "DIT", "MD") { Min = 0, Max = 100, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "stablize", "STB", "Ms") { Min = 0, Max = 10, Data = 0, Default = 0 });
            project.RegisterExpression(new FlagIntExpression(null, "model intepolate", "MIT", "Mm") { Min = 0, Max = 100, Data = 100, Default = 100 });
            project.RegisterExpression(new FlagIntExpression(null, "formant emphasis", "FRE", "ME") { Min = -100, Max = 100, Data = 0, Default = 0 });
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
                            switch (item.Value.Type.Replace("flag_", ""))
                            {
                                case "int":
                                    note.VirtualExpressions[item.Key] = new IntExpression.UExpDiff() { Data = (int)note.Expressions[item.Key].Data - (int)item.Value.Data };
                                    break;
                                case "float":
                                    note.VirtualExpressions[item.Key] = new FloatExpression.UExpDiff() { Data = (float)note.Expressions[item.Key].Data - (float)item.Value.Data };
                                    break;
                                case "bool":
                                    note.VirtualExpressions[item.Key] = new BoolExpression.UExpDiff() { Data = (bool)note.Expressions[item.Key].Data != (bool)item.Value.Data};
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            
            return project;
        }
    }
}
