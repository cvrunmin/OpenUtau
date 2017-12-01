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
    enum ExpDisMode {Hidden, Visible, Shadow};

    class ExpElement : FrameworkElement
    {
        protected DrawingVisual visual;

        protected override int VisualChildrenCount { get { return 1; } }
        protected override Visual GetVisualChild(int index) { return visual; }

        protected UVoicePart _part;
        public virtual UVoicePart Part { set { _part = value; MarkUpdate(); } get { return _part; } }

        public string Key;
        protected TranslateTransform tTrans;

        protected double _visualHeight;
        public double VisualHeight { set { if (_visualHeight != value) { _visualHeight = value; MarkUpdate(); } } get { return _visualHeight; } }
        protected double _scaleX;
        public double ScaleX { set { if (_scaleX != value) { _scaleX = value; MarkUpdate(); } } get { return _scaleX; } }

        public ExpElement()
        {
            tTrans = new TranslateTransform();
            this.RenderTransform = tTrans;
            visual = new DrawingVisual();
            MarkUpdate();
            this.AddVisualChild(visual);
        }

        public double X { set { if (tTrans.X != Math.Round(value)) { tTrans.X = Math.Round(value); } } get { return tTrans.X; } }

        ExpDisMode displayMode;

        public ExpDisMode DisplayMode
        {
            set
            {
                if (displayMode != value)
                {
                    displayMode = value;
                    this.Opacity = displayMode == ExpDisMode.Shadow ? 0.3 : 1;
                    this.Visibility = displayMode == ExpDisMode.Hidden ? Visibility.Hidden : Visibility.Visible;
                    if (this.Parent is Canvas)
                    {
                        if (value == ExpDisMode.Visible) Canvas.SetZIndex(this, UIConstants.ExpressionVisibleZIndex);
                        else if (value == ExpDisMode.Shadow) Canvas.SetZIndex(this, UIConstants.ExpressionShadowZIndex);
                        else Canvas.SetZIndex(this, UIConstants.ExpressionHiddenZIndex);
                    }
                }
            }
            get { return displayMode; }
        }

        protected bool _updated = false;
        public void MarkUpdate() { _updated = true; }

        public virtual void RedrawIfUpdated() { }
    }

    class FloatExpElement : ExpElement
    {
        public OpenUtau.UI.Models.MidiViewModel midiVM;

        protected Pen pen3;
        protected Pen pen2;
        protected Pen pen5;
        protected Pen pen4;

        protected static readonly double PartSquarePointSide = Math.Sqrt(Math.Pow(7.5, 2) / 2);

        public FloatExpElement()
        {
            pen3 = new Pen(ThemeManager.NoteFillBrushes[0], 3);
            pen2 = new Pen(ThemeManager.NoteFillBrushes[0], 2);
            pen5 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(ThemeManager.NoteFillBrushes[0].Color, 0xaa)), 3);
            pen4 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(ThemeManager.NoteFillBrushes[0].Color, 0xaa)), 2);
            pen3.Freeze();
            pen2.Freeze();
            pen5.Freeze();
            pen4.Freeze();
        }

        public override void RedrawIfUpdated()
        {
            if (!_updated) return;
            DrawingContext cxt = visual.RenderOpen();
            if (Part != null)
            {
                DrawExp(cxt,pen2,pen3,pen4,pen5);

            }
            else
            {
                cxt.DrawRectangle(Brushes.Transparent, null, new Rect(new Point(0,0), new Point(100,100)));
            }
            cxt.Close();
            _updated = false;
        }

        protected void DrawExp(DrawingContext cxt, Pen pen2, Pen pen3, Pen pen4, Pen pen5)
        {
            if (Part == null) return;
            if (Part.Expressions.ContainsKey(Key))
            {
                float data = 0, max = 0, min = 0, de = 0;
                if (Part.Expressions[Key] is IntExpression exp1)
                {
                    data = (int)exp1.Data;
                    de = exp1.Default;
                    max = exp1.Max;
                    min = exp1.Min;
                }
                else if (Part.Expressions[Key] is FloatExpression exp2)
                {
                    data = (float)exp2.Data;
                    de = exp2.Default;
                    max = exp2.Max;
                    min = exp2.Min;
                }
                double x1 = 0;
                double x2 = Math.Round(ScaleX * Part.DurTick * DocManager.Inst.Project.BeatPerBar);
                double partValueHeight = Math.Round(VisualHeight - VisualHeight * (data - min) / (max - min));
                double partZeroHeight = Math.Round(VisualHeight - VisualHeight * (de - min) / (max - min));
                cxt.DrawLine(pen5, new Point(x1 + 0.5, partZeroHeight + 0.5), new Point(x1 + 0.5, partValueHeight + 3));
                //cxt.DrawEllipse(Brushes.White, pen2, new Point(x1 + 0.5, partValueHeight), 2.5, 2.5);
                cxt.PushTransform(new RotateTransform(45, x1 + 0.5, partValueHeight + 0.5));
                cxt.DrawRectangle(Brushes.White, pen2, new Rect(x1 + 0.5 - PartSquarePointSide / 2, partValueHeight - PartSquarePointSide / 2, PartSquarePointSide, PartSquarePointSide));
                cxt.Pop();
                cxt.DrawLine(pen4, new Point(x1 + 3, partValueHeight), new Point(Math.Max(x1 + 3, x2 - 3), partValueHeight));
                cxt.DrawText(new FormattedText(data.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 12, new SolidColorBrush(ThemeManager.NoteFillBrushes[0].Color)), new Point(x1 + 3, partValueHeight));
                foreach (UNote note in Part.Notes)
                {
                    if (!midiVM.NoteIsInView(note)) continue;
                    if (note.Expressions.ContainsKey(Key))
                    {
                        float nD = note.Expressions[Key] is IntExpression ? (int)note.Expressions[Key].Data : note.Expressions[Key] is FloatExpression ? (float)note.Expressions[Key].Data : 0;
                        double noteX1 = Math.Round(ScaleX * note.PosTick * DocManager.Inst.Project.BeatPerBar);
                        double noteX2 = Math.Round(ScaleX * note.EndTick * DocManager.Inst.Project.BeatPerBar);
                        double valueHeight = Math.Round(VisualHeight - VisualHeight * ((int)note.VirtualExpressions[Key].Data + data - min) / (max - min));
                        double zeroHeight = partValueHeight;
                        cxt.DrawLine(pen3, new Point(noteX1 + 0.5, zeroHeight + 0.5), new Point(noteX1 + 0.5, valueHeight + 3));
                        cxt.DrawEllipse(Brushes.White, pen2, new Point(noteX1 + 0.5, valueHeight), 2.5, 2.5);
                        cxt.DrawLine(pen2, new Point(noteX1 + 3, valueHeight), new Point(Math.Max(noteX1 + 3, noteX2 - 3), valueHeight));
                        cxt.DrawText(new FormattedText(nD.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 12, new SolidColorBrush(ThemeManager.NoteFillBrushes[0].Color)), new Point(noteX1 + 3, valueHeight));
                    }
                }
            }
        }
    }

    class BoolExpElement : ExpElement
    {
        public OpenUtau.UI.Models.MidiViewModel midiVM;

        protected Pen pen3;
        protected Pen pen2;
        protected Pen pen5;
        protected Pen pen4;

        protected static readonly double PartSquarePointSide = Math.Sqrt(Math.Pow(7.5, 2) / 2);

        public BoolExpElement()
        {
            pen3 = new Pen(ThemeManager.NoteFillBrushes[0], 3);
            pen2 = new Pen(ThemeManager.NoteFillBrushes[0], 2);
            pen5 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(ThemeManager.NoteFillBrushes[0].Color, 0xaa)), 3);
            pen4 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(ThemeManager.NoteFillBrushes[0].Color, 0xaa)), 2);
            pen3.Freeze();
            pen2.Freeze();
            pen5.Freeze();
            pen4.Freeze();
        }

        public override void RedrawIfUpdated()
        {
            if (!_updated) return;
            DrawingContext cxt = visual.RenderOpen();
            if (Part != null)
            {
                DrawExp(cxt, pen2, pen3, pen4, pen5);

            }
            else
            {
                cxt.DrawRectangle(Brushes.Transparent, null, new Rect(new Point(0, 0), new Point(100, 100)));
            }
            cxt.Close();
            _updated = false;
        }

        protected void DrawExp(DrawingContext cxt, Pen pen2, Pen pen3, Pen pen4, Pen pen5)
        {
            if (Part.Expressions.ContainsKey(Key))
            {
                bool data = false, de = false;
                if (Part.Expressions[Key] is BoolExpression exp1)
                {
                    data = (bool)exp1.Data;
                    de = exp1.Default;
                }
                double x1 = 0;
                double x2 = Math.Round(ScaleX * Part.DurTick * DocManager.Inst.Project.BeatPerBar);
                cxt.DrawLine(pen5, new Point(x1 + 0.5, 0.5), new Point(x1 + 0.5, VisualHeight + 3));
                if (data) cxt.DrawRectangle(pen4.Brush, null, new Rect(new Point(x1 + 1.5, 0.5), new Point(Math.Max(x1 + 3, x2 - 3), VisualHeight)));
                cxt.DrawText(new FormattedText(data.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 12, new SolidColorBrush(ThemeManager.NoteFillBrushes[0].Color)), new Point(x1 + 3, VisualHeight));
                foreach (UNote note in Part.Notes)
                {
                    if (!midiVM.NoteIsInView(note)) continue;
                    if (note.Expressions.ContainsKey(Key))
                    {
                        bool nD = note.Expressions[Key] is BoolExpression ? (bool)note.Expressions[Key].Data : false;
                        double noteX1 = Math.Round(ScaleX * note.PosTick * DocManager.Inst.Project.BeatPerBar);
                        double noteX2 = Math.Round(ScaleX * note.EndTick * DocManager.Inst.Project.BeatPerBar);
                        cxt.DrawLine(pen3, new Point(noteX1 + 0.5, 0.5), new Point(noteX1 + 0.5, VisualHeight + 3));
                        if (nD) cxt.DrawRectangle(pen2.Brush, null, new Rect(new Point(noteX1 + 3, 0.5), new Point(Math.Max(noteX1 + 3, noteX2 - 3), VisualHeight)));
                        cxt.DrawText(new FormattedText(nD.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 12, new SolidColorBrush(ThemeManager.NoteFillBrushes[0].Color)), new Point(noteX1 + 3, VisualHeight - 24));
                    }
                }
            }
        }
    }

    class ViewOnlyFloatExpElement : FloatExpElement
    {
        public override void RedrawIfUpdated()
        {
            if (!_updated) return;
            DrawingContext cxt = visual.RenderOpen();
            foreach (var Part in DocManager.Inst.Project.Parts.OfType<UVoicePart>())
            {
                if (DocManager.Inst.Project.Tracks[Part.TrackNo].ActuallyMuted) continue;
                pen3 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(Part == this.Part ? 0xff : 0x7f))), 3);
                pen2 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(Part == this.Part ? 0xff : 0x7f))), 2);
                pen5 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(0xaa * (Part == this.Part ? 0xff : 0x7f)))), 3);
                pen4 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(0xaa * (Part == this.Part ? 0xff : 0x7f)))), 2);
                pen3.Freeze();
                pen2.Freeze();
                pen5.Freeze();
                pen4.Freeze();
                if (Part.Expressions.ContainsKey(Key))
                {
                    DrawExp(cxt, pen2, pen3, pen4, pen5);
                }

            }
            cxt.Close();
            _updated = false;
        }
    }

    class ViewOnlyBoolExpElement : BoolExpElement
    {
        public override void RedrawIfUpdated()
        {
            if (!_updated) return;
            DrawingContext cxt = visual.RenderOpen();
            int id = 0;
            var vp = DocManager.Inst.Project.Parts.OfType<UVoicePart>();
            foreach (var Part in vp)
            {
                if (DocManager.Inst.Project.Tracks[Part.TrackNo].ActuallyMuted) continue;
                pen3 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(Part == this.Part ? 0xff : 0x7f))), 3);
                pen2 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(Part == this.Part ? 0xff : 0x7f))), 2);
                pen5 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(0xaa * (Part == this.Part ? 0xff : 0x7f)))), 3);
                pen4 = new Pen(new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(0xaa * (Part == this.Part ? 0xff : 0x7f)))), 2);
                pen3.Freeze();
                pen2.Freeze();
                pen5.Freeze();
                pen4.Freeze();
                if (Part.Expressions.ContainsKey(Key))
                {
                    var tg = new TransformGroup();
                    tg.Children.Add(new ScaleTransform(1, 1f / vp.Count(), 0.5, 0));
                    tg.Children.Add(new TranslateTransform(0, VisualHeight * ((float)id) / vp.Count()));
                    cxt.PushTransform(tg);
                    DrawExp(cxt, pen2, pen3, pen4, pen5);
                    cxt.Pop();
                }
                id++;
            }
            cxt.Close();
            _updated = false;
        }
    }
}
