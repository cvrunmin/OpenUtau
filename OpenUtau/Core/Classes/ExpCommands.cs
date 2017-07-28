using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;

namespace OpenUtau.Core
{
    public abstract class ExpCommand : UCommand
    {
        public UVoicePart Part;
        public UNote Note;
        public string Key;
        public override void Execute()
        {
            DocManager.Inst.Project.Tracks[Part.TrackNo].Amended = true;
        }
        public override void Unexecute()
        {
            DocManager.Inst.Project.Tracks[Part.TrackNo].Amended = true;
        }
    }

    public class SetIntExpCommand : ExpCommand
    {
        public int NewValue, OldValue;
        public int NewOffsetValue, OldOffsetValue;
        public SetIntExpCommand(UVoicePart part, UNote note, string key, int newValue)
        {
            this.Part = part;
            this.Note = note;
            this.Key = key;
            this.NewValue = newValue;
            this.OldValue = (int)Note.Expressions[Key].Data;
            this.NewOffsetValue = NewValue - (int)Part.Expressions[Key].Data;
            this.OldOffsetValue = Note.VirtualExpressions[Key];
        }
        public override string ToString() { return "Set note expression " + Key; }
        public override void Execute()
        {
            Note.Expressions[Key].Data = NewValue;
            Note.VirtualExpressions[Key] = NewOffsetValue;
            base.Execute();
        }
        public override void Unexecute()
        {
            Note.Expressions[Key].Data = OldValue;
            Note.VirtualExpressions[Key] = OldOffsetValue;
            base.Unexecute();
        }
    }

    public class GlobelSetIntExpCommand : ExpCommand
    {
        public int NewValue, OldValue;
        public LinkedList<int> OldValues = new LinkedList<int>();
        public GlobelSetIntExpCommand(UVoicePart part, string key, int newValue)
        {
            this.Part = part;
            this.Key = key;
            this.NewValue = newValue;
            OldValue = (int)part.Expressions[key].Data;
            LinkedListNode<int> before = null;
            foreach (var note in part.Notes)
            {
                if (before == null) {
                    before = OldValues.AddFirst((int)note.Expressions[Key].Data);
                }
                else
                {
                    before = OldValues.AddAfter(before, (int)note.Expressions[Key].Data);
                }
            }
        }
        public override string ToString() { return "Set all notes expression " + Key; }
        public override void Execute() {
            Part.Expressions[Key].Data = NewValue;
            foreach (var note in Part.Notes)
            {
                note.Expressions[Key].Data = NewValue + note.VirtualExpressions[Key];
            }
            base.Execute();
        }
        public override void Unexecute()
        {
            Part.Expressions[Key].Data = OldValue;
            int i = 0;
            foreach (var note in Part.Notes)
            {
                note.Expressions[Key].Data = OldValues.ElementAt(i);
                i++;
            }
            base.Unexecute();
        }
    }

    public class UpdateNoteVibratoCommand : ExpCommand
    {
        public UNote note { get; set; }
        public double Length { get; private set; }
        public double Depth { get; private set; }
        public double In { get; private set; }
        public double Out { get; private set; }
        public double Shift { get; private set; }
        public double Drift { get; private set; }
        public double Period { get; private set; }
        public UpdateNoteVibratoCommand(UNote note, double len = double.NaN, double per = double.NaN, double dep = double.NaN, double din = double.NaN, double dout = double.NaN, double shift = double.NaN, double drift = double.NaN) {
            this.note = note;
            Length = len;
            Depth = dep;
            Period = per;
            In = din;
            Out = dout;
            Shift = shift;
            Drift = drift;
        }
        public override void Execute()
        {
            if (!double.IsNaN(Length)) note.Vibrato.Length = Length;
            if (!double.IsNaN(Period)) note.Vibrato.Period = Period;
            if (!double.IsNaN(Depth)) note.Vibrato.Depth = Depth;
            if (!double.IsNaN(In)) note.Vibrato.In = In;
            if (!double.IsNaN(Out)) note.Vibrato.Out = Out;
            if (!double.IsNaN(Shift)) note.Vibrato.Shift = Shift;
            if (!double.IsNaN(Drift)) note.Vibrato.Drift = Drift;
        }
        public override string ToString()
        {
            return "update note vibrato";
        }
    }

    public abstract class PitchExpCommand : ExpCommand { }

    public class DeletePitchPointCommand : PitchExpCommand
    {
        public int Index;
        public PitchPoint Point;
        public DeletePitchPointCommand(UVoicePart part, UNote note, int index)
        {
            this.Part = part;
            this.Note = note;
            this.Index = index;
            this.Point = Note.PitchBend.Points[Index];
        }
        public override string ToString() { return "Delete pitch point"; }
        public override void Execute() { Note.PitchBend.Points.RemoveAt(Index);
            base.Execute();
        }
        public override void Unexecute() { Note.PitchBend.Points.Insert(Index, Point);
            base.Unexecute();
        }
    }

    public class ChangePitchPointShapeCommand : PitchExpCommand
    {
        public PitchPoint Point;
        public PitchPointShape NewShape;
        public PitchPointShape OldShape;
        public ChangePitchPointShapeCommand(UVoicePart part, PitchPoint point, PitchPointShape shape)
        {
            this.Part = part;
            this.Point = point;
            this.NewShape = shape;
            this.OldShape = point.Shape;
        }
        public override string ToString() { return "Change pitch point shape"; }
        public override void Execute() { Point.Shape = NewShape;
            base.Execute();
        }
        public override void Unexecute() { Point.Shape = OldShape;
            base.Unexecute();
        }
    }

    public class SnapPitchPointCommand : PitchExpCommand
    {
        double X;
        double Y;
        public SnapPitchPointCommand(UNote note)
        {
            this.Note = note;
            this.Part = DocManager.Inst.Project.Parts[note.PartNo] as UVoicePart;
            this.X = Note.PitchBend.Points.First().X;
            this.Y = Note.PitchBend.Points.First().Y;
        }
        public override string ToString() { return "Toggle pitch snap"; }
        public override void Execute()
        {
            Note.PitchBend.SnapFirst = !Note.PitchBend.SnapFirst;
            if (!Note.PitchBend.SnapFirst)
            {
                Note.PitchBend.Points.First().X = this.X;
                Note.PitchBend.Points.First().Y = this.Y;
            }
            base.Execute();
        }
        public override void Unexecute()
        {
            Note.PitchBend.SnapFirst = !Note.PitchBend.SnapFirst;
            if (!Note.PitchBend.SnapFirst)
            {
                Note.PitchBend.Points.First().X = this.X;
                Note.PitchBend.Points.First().Y = this.Y;
            }
            base.Unexecute();
        }
    }

    public class AddPitchPointCommand : PitchExpCommand
    {
        public int Index;
        public PitchPoint Point;
        public AddPitchPointCommand(UNote note, PitchPoint point, int index)
        {
            this.Note = note;
            Part = DocManager.Inst.Project.Parts[note.PartNo] as UVoicePart;
            this.Index = index;
            this.Point = point;
        }
        public override string ToString() { return "Add pitch point"; }
        public override void Execute() { Note.PitchBend.Points.Insert(Index, Point);
            base.Execute();
        }
        public override void Unexecute() { Note.PitchBend.Points.RemoveAt(Index);
            base.Unexecute();
        }
    }

    public class MovePitchPointCommand : PitchExpCommand
    {
        public PitchPoint Point;
        public double DeltaX, DeltaY;
        public MovePitchPointCommand(UVoicePart part, PitchPoint point, double deltaX, double deltaY)
        {
            this.Part = part;
            this.Point = point;
            this.DeltaX = deltaX;
            this.DeltaY = deltaY;
        }
        public override string ToString() { return "Move pitch point"; }
        public override void Execute() { Point.X += DeltaX; Point.Y += DeltaY;
            base.Execute();
        }
        public override void Unexecute() { Point.X -= DeltaX; Point.Y -= DeltaY;
            base.Unexecute();
        }
    }
}
