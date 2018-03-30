using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using OpenUtau.Core;
using OpenUtau.Core.USTx;

namespace OpenUtau.UI.Models
{
    public class PitchPointHitTestResult
    {
        public UNote Note;
        public int Index;
        public bool OnPoint;
        public double X;
        public double Y;
    }

    public class VibratoHitTestResult {
        public UNote Note { get; set; }
        public bool Success { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public VibratoPart OnPoint { get; set; } = VibratoPart.No;

        public enum VibratoPart {
            No, Length, Depth, Period, In, Out, Shift, Drift
        }
    }

    class MidiViewHitTest : ICmdSubscriber
    {
        MidiViewModel midiVM;
        UProject Project => DocManager.Inst.Project;

        public MidiViewHitTest(MidiViewModel midiVM) { this.midiVM = midiVM; }

        public UNote HitTestNoteX(double x)
        {
            int tick = (int)(midiVM.CanvasToQuarter(x) * Project.Resolution);
            foreach (UNote note in midiVM.Part.Notes)
                if (note.PosTick <= tick && note.EndTick >= tick) return note;
            return null;
        }

        public UNote HitTestNote(Point mousePos)
        {
            int tick = (int)(midiVM.CanvasToQuarter(mousePos.X) * Project.Resolution);
            int noteNum = midiVM.CanvasToNoteNum(mousePos.Y);
            foreach (UNote note in midiVM.Part.Notes)
                if (note.PosTick <= tick && note.EndTick >= tick && note.NoteNum == noteNum) return note;
            return null;
        }

        public bool HitNoteResizeArea(UNote note, Point mousePos)
        {
            double x = midiVM.QuarterToCanvas((double)note.EndTick / Project.Resolution);
            return mousePos.X <= x && mousePos.X > x - UIConstants.ResizeMargin;
        }

        public VibratoHitTestResult HitTestVibrato(Point mousePos)
        {
            int tick = (int)(midiVM.CanvasToQuarter(mousePos.X) * Project.Resolution);
            double pitch = midiVM.CanvasToPitch(mousePos.Y);
            foreach (UNote note in midiVM.Part.Notes)
                if (note.PosTick + note.DurTick * (1 - note.Vibrato.Length / 100) <= tick && note.EndTick >= tick &&
                    Math.Abs(note.NoteNum - pitch) < note.Vibrato.Depth / 100) return HitTestVibrato(note,mousePos);
            return new VibratoHitTestResult() { Success = false, X = mousePos.X, Y = mousePos.Y};
        }

        public VibratoHitTestResult HitTestVibrato(UNote note, Point mousePos) {
            const int margin = UIConstants.ResizeMargin / 2;
            var result = new VibratoHitTestResult() { Note = note, Success = true, X = mousePos.X, Y = mousePos.Y};

            var tick = (midiVM.CanvasToQuarter(mousePos.X) * Project.Resolution);
            double pitch = midiVM.CanvasToPitch(mousePos.Y);

            var xLength = note.PosTick + note.DurTick * (1 - note.Vibrato.Length / 100);
            var xIn = xLength + note.DurTick * (note.Vibrato.Length * note.Vibrato.In / 10000);
            var xOut = note.PosTick + note.DurTick - note.DurTick * (note.Vibrato.Length * (note.Vibrato.Out) / 10000);
            var xDrift = xIn + note.DurTick * (note.Vibrato.Length / 100) * (1 - (note.Vibrato.In + note.Vibrato.Out) / 100) * 0.5;
            var depth = midiVM.TrackHeight * note.Vibrato.Depth / 100;
            var yCenter = midiVM.NoteNumToCanvas(note.NoteNum) + midiVM.TrackHeight / 2;
            var yTC = yCenter;
            yCenter += depth * (note.Vibrato.Drift / 100);
            var yDepth1 = yCenter - depth;
            var yDepth2 = yCenter + depth;

            if (xLength + margin >= tick && xLength - margin <= tick && Math.Abs(mousePos.Y - yTC) <= margin)
            {
                result.OnPoint = VibratoHitTestResult.VibratoPart.Length;
            }
            else if (xIn + margin >= tick && xIn - margin <= tick)
            {
                result.OnPoint = VibratoHitTestResult.VibratoPart.In;
            }
            else if (xLength + margin >= tick && xLength - margin <= tick) {
                result.OnPoint = VibratoHitTestResult.VibratoPart.Length;
            }
            else if (xOut + margin >= tick && xOut - margin <= tick)
            {
                result.OnPoint = VibratoHitTestResult.VibratoPart.Out;
            }
            else if ((xDrift + margin >= tick && xDrift - margin <= tick) && (Math.Abs(mousePos.Y - yCenter) <= margin * 2))
            {
                result.OnPoint = VibratoHitTestResult.VibratoPart.Drift;
            }
            else if (Math.Abs(mousePos.Y - yDepth1) <= margin || Math.Abs(mousePos.Y - yDepth2) <= margin)
            {
                result.OnPoint = VibratoHitTestResult.VibratoPart.Depth;
            }
            else if (mousePos.Y <= yCenter && mousePos.Y > yDepth1)
            {
                result.OnPoint = VibratoHitTestResult.VibratoPart.Period;
            }
            else if (mousePos.Y > yCenter && mousePos.Y <= yDepth2)
            {
                result.OnPoint = VibratoHitTestResult.VibratoPart.Shift;
            }
            return result;
        }

        public PitchPointHitTestResult HitTestPitchPoint(Point mousePos)
        {
            if (midiVM.ShowPitch)
                foreach (var note in midiVM.Part.Notes)
                {
                    if (midiVM.NoteIsInView(note)) // FIXME this is not enough
                    {
                        if (note.Error) continue;
                        double lastX = 0, lastY = 0;
                        PitchPointShape lastShape = PitchPointShape.Linear;
                        for (int i = 0; i < note.PitchBend.Points.Count; i++)
                        {
                            var pit = note.PitchBend.Points[i];
                            int posTick = note.PosTick + Project.MillisecondToTick(pit.X);
                            double noteNum = note.NoteNum + pit.Y / 10;
                            double x = midiVM.TickToCanvas(posTick);
                            double y = midiVM.NoteNumToCanvas(noteNum) + midiVM.TrackHeight / 2;
                            if (Math.Abs(mousePos.X - x) < 4 && Math.Abs(mousePos.Y - y) < 4)
                                return new PitchPointHitTestResult() { Note = note, Index = i, OnPoint = true };
                            else if (mousePos.X < x && i > 0 && mousePos.X > lastX)
                            {
                                // Hit test curve
                                var lastPit = note.PitchBend.Points[i - 1];
                                double castY = MusicMath.InterpolateShape(lastX, x, lastY, y, mousePos.X, lastShape) - mousePos.Y;
                                if (y >= lastY)
                                {
                                    if (mousePos.Y - y > 3 || lastY - mousePos.Y > 3) break;
                                }
                                else
                                {
                                    if (y - mousePos.Y > 3 || mousePos.Y - lastY > 3) break;
                                }
                                double castX = MusicMath.InterpolateShapeX(lastX, x, lastY, y, mousePos.Y, lastShape) - mousePos.X;
                                double dis = double.IsNaN(castX) ? Math.Abs(castY) : Math.Cos(Math.Atan2(Math.Abs(castY), Math.Abs(castX))) * Math.Abs(castY);
                                if (dis < 3)
                                {
                                    double msX = DocManager.Inst.Project.TickToMillisecond(midiVM.CanvasToQuarter(mousePos.X) * DocManager.Inst.Project.Resolution - note.PosTick);
                                    double msY = (midiVM.CanvasToPitch(mousePos.Y) - note.NoteNum) * 10;
                                    return (new PitchPointHitTestResult() { Note = note, Index = i - 1, OnPoint = false, X = msX, Y = msY });
                                }
                                else break;
                            }
                            lastX = x;
                            lastY = y;
                            lastShape = pit.Shape;
                        }
                    }
                }
            return null;
        }

        # region ICmdSubscriber

        public void Subscribe(ICmdPublisher publisher) { if (publisher != null) publisher.Subscribe(this); }

        public void OnNext(UCommand cmd, bool isUndo)
        {

            if (cmd is RedrawNotesNotification) { }
        }

        public void PostOnNext(UCommandGroup cmds, bool isUndo) { }

        # endregion
    }
}
