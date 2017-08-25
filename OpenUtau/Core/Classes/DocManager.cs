using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core;
using OpenUtau.Core.Lib;
using OpenUtau.Core.USTx;

namespace OpenUtau.Core
{
    class DocManager : ICmdPublisher
    {
        DocManager() {
            _project = new UProject();
        }

        static DocManager _s;
        static DocManager GetInst() { if (_s == null) { _s = new DocManager(); } return _s; }
        public static DocManager Inst { get { return GetInst(); } }

        public int playPosTick = 0;

        Dictionary<string, USinger> _singers;
        public Dictionary<string, USinger> Singers { get { return _singers; } }
        UProject _project;
        public UProject Project { get { return _project; } }

        public void SearchAllSingers()
        {
            _singers = Formats.UtauSoundbank.FindAllSingers();
        }

        # region Command Queue

        Deque<UCommandGroup> undoQueue = new Deque<UCommandGroup>();
        Deque<UCommandGroup> redoQueue = new Deque<UCommandGroup>();
        UCommandGroup undoGroup = null;
        UCommandGroup savedPoint = null;

        public bool ChangesSaved
        {
            get
            {
                return Project.Saved && (undoQueue.Count > 0 && savedPoint == undoQueue.Last() || undoQueue.Count == 0 && savedPoint == null);
            }
        }

        public void ExecuteCmd(UCommand cmd, bool quiet = false)
        {
            if (cmd is UNotification)
            {
                if (cmd is SaveProjectNotification)
                {
                    var _cmd = cmd as SaveProjectNotification;
                    if (undoQueue.Count > 0) savedPoint = undoQueue.Last();
                    if (_cmd.Path == "") OpenUtau.Core.Formats.USTx.Save(Project.FilePath, Project);
                    else OpenUtau.Core.Formats.USTx.Save(_cmd.Path, Project);
                }
                else if (cmd is LoadProjectNotification)
                {
                    PlaybackManager.GetActiveManager().StopPlayback();
                    undoQueue.Clear();
                    redoQueue.Clear();
                    Render.RenderDispatcher.Inst.trackCache.Clear();
                    Render.RenderDispatcher.Inst.trackCache.Capacity = 0;
                    Render.RenderCache.Inst.Clear();
                    undoGroup = null;
                    savedPoint = null;
                    this._project = ((LoadProjectNotification)cmd).project;
                    this.playPosTick = 0;
                } else if (cmd is UpdateProjectPropertiesNotification uppn) {
                    this.playPosTick = 0;
                    _project.BPM = uppn.bpm;
                    _project.BeatPerBar = uppn.beatPerBar;
                    _project.BeatUnit = uppn.beatUnit;
                    foreach (var item in _project.Parts)
                    {
                        if (item is UWavePart wave) {
                            wave.DurTick = _project.MillisecondToTick(wave.FileDurMillisecond);
                        }
                    }
                }
                else if (cmd is UpdateProjectBpmsNotification upbn)
                {
                    if (upbn.removal && _project.SubBPM.ContainsKey(upbn.value.Key))
                    {
                        _project.SubBPM.Remove(upbn.value.Key);
                    }
                    else
                    {
                        if (_project.SubBPM.ContainsKey(upbn.value.Key)) {
                            _project.SubBPM[upbn.value.Key] = upbn.value.Value;
                        }
                        else
                        {
                            _project.SubBPM.Add(upbn.value.Key, upbn.value.Value);
                            var removalParts = new List<UPart>();
                            var additalParts = new List<UPart>();
                            foreach (var part in _project.Parts)
                            {
                                if (part.PosTick < upbn.value.Key && part.EndTick > upbn.value.Key)
                                {
                                    removalParts.Add(part);
                                    additalParts.AddRange(Util.Utils.SplitPart(part, upbn.value.Key));
                                }
                            }
                            StartUndoGroup();
                            foreach (var item in removalParts)
                            {
                                ExecuteCmd(new RemovePartCommand(_project, item));
                            }
                            foreach (var item in additalParts)
                            {
                                ExecuteCmd(new AddPartCommand(_project, item));
                            }
                            EndUndoGroup();
                        }
                    }
                }
                else if (cmd is SetPlayPosTickNotification)
                {
                    var _cmd = cmd as SetPlayPosTickNotification;
                    this.playPosTick = _cmd.playPosTick;
                }
                Publish(cmd);
                if (!quiet) System.Diagnostics.Debug.WriteLine("Publish notification " + cmd.ToString());
                return;
            }
            else if (undoGroup == null) { System.Diagnostics.Debug.WriteLine("Null undoGroup"); return; }
            else
            {
                undoGroup.Commands.Add(cmd);
                cmd.Execute();
                Publish(cmd);
            }
            if (!quiet) System.Diagnostics.Debug.WriteLine("ExecuteCmd " + cmd.ToString());
        }

        public void StartUndoGroup()
        {
            if (undoGroup != null) { System.Diagnostics.Debug.WriteLine("undoGroup already started"); EndUndoGroup(); }
            undoGroup = new UCommandGroup();
            System.Diagnostics.Debug.WriteLine("undoGroup started");
        }

        public void EndUndoGroup()
        {
            if (undoGroup != null && undoGroup.Commands.Count > 0) { undoQueue.AddToBack(undoGroup); redoQueue.Clear(); }
            if (undoQueue.Count > Core.Util.Preferences.Default.UndoLimit) undoQueue.RemoveFromFront();
            undoGroup = null;
            System.Diagnostics.Debug.WriteLine("undoGroup ended");
        }

        public void Undo()
        {
            if (undoQueue.Count == 0) return;
            var cmdg = undoQueue.RemoveFromBack();
            for (int i = cmdg.Commands.Count - 1; i >= 0; i--) { var cmd = cmdg.Commands[i]; cmd.Unexecute(); if (!(cmd is NoteCommand)) Publish(cmd, true); }
            redoQueue.AddToBack(cmdg);
        }

        public void Redo()
        {
            if (redoQueue.Count == 0) return;
            var cmdg = redoQueue.RemoveFromBack();
            foreach (var cmd in cmdg.Commands) { cmd.Execute(); Publish(cmd); }
            undoQueue.AddToBack(cmdg);
        }

        # endregion

        # region ICmdPublisher

        private List<ICmdSubscriber> subscribers = new List<ICmdSubscriber>();
        public void Subscribe(ICmdSubscriber sub) { if (!subscribers.Contains(sub)) subscribers.Add(sub); }
        public void UnSubscribe(ICmdSubscriber sub) { subscribers.Remove(sub); }
        public void Publish(UCommand cmd, bool isUndo = false) { foreach (var sub in subscribers) sub.OnNext(cmd, isUndo); }

        # endregion

        # region Command handeling

        # endregion
    }
}
