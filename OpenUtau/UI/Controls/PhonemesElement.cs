using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using OpenUtau.Core;
using OpenUtau.Core.USTx;

namespace OpenUtau.UI.Controls
{
    class PhonemesElement : NotesElement
    {
        public new double Y { set { } get { return 0; } }

        bool _hidePhoneme = false;
        public bool HidePhoneme { set { if (_hidePhoneme != value) { _hidePhoneme = value; MarkUpdate(); } } get => _hidePhoneme;
        }

        protected Pen penEnv;
        protected Pen penEnvSel;
        protected Brush brushEnv;
        protected Brush brushEnvSel;

        public PhonemesElement()
            : base()
        {
            penEnv = new Pen( ThemeManager.NoteFillBrushes[0] , 1);
            penEnv.Freeze();
            penEnvSel = new Pen(ThemeManager.NoteFillSelectedBrush, 1);
            penEnvSel.Freeze();
            brushEnv = ThemeManager.NoteFillErrorBrushes[0];
            brushEnvSel = ThemeManager.NoteFillSelectedErrorBrush;
        }

        public override void RedrawIfUpdated()
        {
            if (!_updated) return;
            if (HidePhoneme) return;
            DrawingContext cxt = visual.RenderOpen();
            if (Part != null)
            {
                bool inView, lastInView = false;
                UNote lastNote = null;
                foreach (var note in Part.Notes)
                {
                    inView = midiVM.NoteIsInView(note);

                    if (inView && !lastInView)
                        if (lastNote != null)
                            DrawPhoneme(lastNote, cxt);

                    if (inView || !inView && lastInView)
                        DrawPhoneme(note, cxt);

                    lastNote = note;
                    lastInView = inView;
                }
            }
            cxt.Close();
            _updated = false;
        }

        protected virtual void DrawPhoneme(UNote note, DrawingContext cxt)
        {
            const double y = 23.5;
            const double height = 24;
            double DrawEnvelope(EnvelopeExpression envelope, double offset, bool error = false)
            {
                var globalOffset = DocManager.Inst.Project.TickToMillisecond(DocManager.Inst.Project.Parts[note.PartNo].PosTick + offset);
                double x = Math.Round((offset) * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution) + 0.5;
                double x0 = (offset + DocManager.Inst.Project.MillisecondToTick(envelope.Points[0].X, globalOffset))
                    * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution;
                double y0 = (1 - envelope.Points[0].Y / 100) * height;
                double x1 = (offset + DocManager.Inst.Project.MillisecondToTick(envelope.Points[1].X, globalOffset))
                    * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution;
                double y1 = (1 - envelope.Points[1].Y / 100) * height;
                double x2 = (offset + DocManager.Inst.Project.MillisecondToTick(envelope.Points[2].X, globalOffset))
                    * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution;
                double y2 = (1 - envelope.Points[2].Y / 100) * height;
                double x3 = (offset + DocManager.Inst.Project.MillisecondToTick(envelope.Points[3].X, globalOffset))
                    * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution;
                double y3 = (1 - envelope.Points[3].Y / 100) * height;
                double x4 = (offset + DocManager.Inst.Project.MillisecondToTick(envelope.Points[4].X, globalOffset))
                    * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution;
                double y4 = (1 - envelope.Points[4].Y / 100) * height;

                Pen pen = note.Selected ? penEnvSel : penEnv;
                Brush brush = note.Selected ? brushEnvSel : brushEnv;
                if (error)
                {
                    var penb = new SolidColorBrush((pen.Brush as SolidColorBrush).Color);
                    penb.Opacity = 0.25;
                    pen = new Pen(penb, pen.Thickness);
                    brush = new SolidColorBrush((brush as SolidColorBrush).Color);
                    brush.Opacity = 0.25;
                }
                StreamGeometry g = new StreamGeometry();
                List<Point> poly = new List<Point>() {
                    new Point(x1, y + y1),
                    new Point(x2, y + y2),
                    new Point(x3, y + y3),
                    new Point(x4, y + y4),
                    new Point(x0, y + y0)
                };

                using (var gcxt = g.Open())
                {
                    gcxt.BeginFigure(new Point(x0, y + y0), true, false);
                    gcxt.PolyLineTo(poly, true, false);
                    gcxt.Close();
                }
                cxt.DrawGeometry(brush, pen, g);

                cxt.DrawLine(penEnvSel, new Point(x, y), new Point(x, y + height));
                return x;
            }

            DrawEnvelope(note.Envelope, note.PosTick, true);
            for (int i = 0; i < note.Phonemes.Count; i++)
            {
                var phoneme = note.Phonemes[i];
                var x = DrawEnvelope(phoneme.Envelope, note.PosTick + phoneme.PosTick, note.Error || phoneme.PhonemeError);

                string text = phoneme.Phoneme;
                if (!fTextPool.ContainsKey(text)) AddToFormattedTextPool(text);
                var fText = fTextPool[text];
                if (midiVM.QuarterWidth > UIConstants.MidiQuarterMinWidthShowPhoneme)
                    cxt.DrawText(fText, new Point(Math.Round(x), 8));
            }
        }

        protected override void AddToFormattedTextPool(string text)
        {
            var fText = new FormattedText(
                    text,
                    System.Threading.Thread.CurrentThread.CurrentUICulture,
                    FlowDirection.LeftToRight, SystemFonts.CaptionFontFamily.GetTypefaces().First(),
                    12,
                    Brushes.Black);
            fTextPool.Add(text, fText);
            fTextWidths.Add(text, fText.Width);
            fTextHeights.Add(text, fText.Height);
        }
    }

    class ViewOnlyPhonemesElement : PhonemesElement {
        public override void RedrawIfUpdated()
        {
            if (!_updated) return;
            if (HidePhoneme) return;
            DrawingContext cxt = visual.RenderOpen();
            foreach(var Part in DocManager.Inst.Project.Parts.OfType<UVoicePart>())
            {
                if (DocManager.Inst.Project.Tracks[Part.TrackNo].ActuallyMuted) continue;
                bool inView, lastInView = false;
                UNote lastNote = null;
                penEnv = new Pen(new SolidColorBrush(DocManager.Inst.Project.Tracks[Part.TrackNo].Color), 1);
                penEnv.Freeze();
                brushEnv = new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, 127));
                brushEnv.Freeze();
                foreach (var note in Part.Notes)
                {
                    inView = midiVM.NoteIsInView(note);

                    if (inView && !lastInView)
                        if (lastNote != null)
                            DrawPhoneme(lastNote, cxt);

                    if (inView || !inView && lastInView)
                        DrawPhoneme(note, cxt);

                    lastNote = note;
                    lastInView = inView;
                }
            }
            cxt.Close();
            _updated = false;
        }

        protected override void DrawPhoneme(UNote note, DrawingContext cxt)
        {
            UNote note1 = note.Clone();
            note1.PosTick += DocManager.Inst.Project.Parts.Find(part => part.PartNo == note1.PartNo)?.PosTick ?? 0;
            base.DrawPhoneme(note1, cxt);
        }
    }
}
