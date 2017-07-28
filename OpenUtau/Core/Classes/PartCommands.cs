using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;

namespace OpenUtau.Core
{
    public abstract class PartCommand : UCommand
    {
        public UProject project;
        public UPart part;
        public override void Execute()
        {
            project.Tracks[part.TrackNo].Amended = true;
        }
        public override void Unexecute()
        {
            project.Tracks[part.TrackNo].Amended = true;
        }
    }

    public class AddPartCommand : PartCommand
    {
        public AddPartCommand(UProject project, UPart part) { this.project = project; this.part = part; }
        public override string ToString() { return "Add part"; }
        public override void Execute() {
            project.Parts.Add(part);
            if(part is UVoicePart voice)
            {
                foreach (var pair in project.ExpressionTable) {
                    if(!voice.Expressions.ContainsKey(pair.Key))
                    voice.Expressions.Add(pair.Key, pair.Value);
                }
                foreach (var item in voice.Notes)
                {
                    item.PartNo = part.PartNo;
                }
            }
            base.Execute();
        }
        public override void Unexecute() { project.Parts.Remove(part);
            base.Unexecute();
        }
    }

    public class RemovePartCommand : PartCommand
    {
        public RemovePartCommand(UProject project, UPart part) { this.project = project; this.part = part; }
        public override string ToString() { return "Remove parts"; }
        public override void Execute() {
            project.Parts.Remove(part);
            for (int i = 0; i < project.Parts.Count; i++)
            {
                project.Parts[i].PartNo = i;
                if (project.Parts[i] is UVoicePart voice)
                {
                    foreach (var item in voice.Notes)
                    {
                        item.PartNo = i;
                    }
                }
            }
            base.Execute();
        }
        public override void Unexecute() {
            project.Parts.Add(part);
            for (int i = 0; i < project.Parts.Count; i++)
            {
                project.Parts[i].PartNo = i;
                if (project.Parts[i] is UVoicePart voice)
                {
                    foreach (var item in voice.Notes)
                    {
                        item.PartNo = i;
                    }
                }
            }
            base.Unexecute();
        }
    }

    public class ReplacePartCommand : PartCommand
    {
        public UPart PartReplacing { get; private set; }
        public UPart PartReplaced { get; private set; }
        public ReplacePartCommand(UProject project, UPart replaced, UPart replacing) {
            this.project = project;
            this.part = replacing;
            PartReplaced = replaced;
            PartReplacing = replacing;
        }
        public override void Execute()
        {
            project.Parts.Remove(PartReplaced);
            project.Parts.Insert(PartReplacing.PartNo, PartReplacing);
            for (int i = 0; i < project.Parts.Count; i++)
            {
                project.Parts[i].PartNo = i;
                if (project.Parts[i] is UVoicePart voice)
                {
                    foreach (var item in voice.Notes)
                    {
                        item.PartNo = i;
                    }
                }
            }
            base.Execute();
        }
        public override void Unexecute()
        {
            project.Parts.Remove(PartReplacing);
            project.Parts.Insert(PartReplaced.PartNo, PartReplaced);
            for (int i = 0; i < project.Parts.Count; i++)
            {
                project.Parts[i].PartNo = i;
                if (project.Parts[i] is UVoicePart voice)
                {
                    foreach (var item in voice.Notes)
                    {
                        item.PartNo = i;
                    }
                }
            }
            base.Unexecute();
        }
        public override string ToString()
        {
            return "Replace a part";
        }
    }

    public class MovePartCommand : PartCommand
    {
        int newPos, oldPos, newTrackNo, oldTrackNo;
        public MovePartCommand(UProject project, UPart part, int newPos, int newTrackNo)
        {
            this.project = project;
            this.part = part;
            this.newPos = newPos;
            this.newTrackNo = newTrackNo;
            this.oldPos = part.PosTick;
            this.oldTrackNo = part.TrackNo;
        }
        public override string ToString() { return "Move parts"; }
        public override void Execute() { part.PosTick = newPos; part.TrackNo = newTrackNo;
            base.Execute();
        }
        public override void Unexecute() { part.PosTick = oldPos; part.TrackNo = oldTrackNo;
            base.Unexecute();
        }
    }

    public class ResizePartCommand : PartCommand
    {
        int newDur, oldDur;
        public ResizePartCommand(UProject project, UPart part, int newDur) { this.project = project; this.part = part; this.newDur = newDur; this.oldDur = part.DurTick; }
        public override string ToString() { return "Change parts duration"; }
        public override void Execute() { part.DurTick = newDur;
            base.Execute();
        }
        public override void Unexecute() { part.DurTick = oldDur;
            base.Unexecute();
        }
    }
}
