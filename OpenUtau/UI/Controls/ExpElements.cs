﻿using System;
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

        Pen pen3;
        Pen pen2;
        Pen pen5;
        Pen pen4;

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
                if (Part.Expressions.ContainsKey(Key))
                {
                    var _exp = Part.Expressions[Key] as IntExpression;
                    var _expTemplate = DocManager.Inst.Project.ExpressionTable[Key] as IntExpression;
                    double x1 = Math.Round(ScaleX * Part.PosTick);
                    double x2 = Math.Round(ScaleX * Part.EndTick);
                    double partValueHeight = Math.Round(VisualHeight - VisualHeight * ((int)_exp.Data - _expTemplate.Min) / (_expTemplate.Max - _expTemplate.Min));
                    double partZeroHeight = Math.Round(VisualHeight - VisualHeight * (0f - _expTemplate.Min) / (_expTemplate.Max - _expTemplate.Min));
                    cxt.DrawLine(pen5, new Point(x1 + 0.5, partZeroHeight + 0.5), new Point(x1 + 0.5, partValueHeight + 3));
                    cxt.DrawEllipse(Brushes.White, pen2, new Point(x1 + 0.5, partValueHeight), 2.5, 2.5);
                    cxt.DrawLine(pen4, new Point(x1 + 3, partValueHeight), new Point(Math.Max(x1 + 3, x2 - 3), partValueHeight));
                    cxt.DrawText(new FormattedText(_exp.Data.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"),12, new SolidColorBrush(ThemeManager.NoteFillBrushes[0].Color)), new Point(x1 + 3, partValueHeight));
                    foreach (UNote note in Part.Notes)
                    {
                        if (!midiVM.NoteIsInView(note)) continue;
                        if (note.Expressions.ContainsKey(Key))
                        {
                            var _noteExp = note.Expressions[Key] as IntExpression;
                            var _noteExpTemplate = Part.Expressions[Key] as IntExpression;
                            double noteX1 = Math.Round(ScaleX * note.PosTick);
                            double noteX2 = Math.Round(ScaleX * note.EndTick);
                            double valueHeight = Math.Round(VisualHeight - VisualHeight * (note.VirtualExpressions[Key] + (int)_noteExpTemplate.Data - _noteExpTemplate.Min) / (_noteExpTemplate.Max - _noteExpTemplate.Min));
                            double zeroHeight = partValueHeight;
                            cxt.DrawLine(pen3, new Point(noteX1 + 0.5, zeroHeight + 0.5), new Point(noteX1 + 0.5, valueHeight + 3));
                            cxt.DrawEllipse(Brushes.White, pen2, new Point(noteX1 + 0.5, valueHeight), 2.5, 2.5);
                            cxt.DrawLine(pen2, new Point(noteX1 + 3, valueHeight), new Point(Math.Max(noteX1 + 3, noteX2 - 3), valueHeight));
                            cxt.DrawText(new FormattedText(_noteExp.Data.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 12, new SolidColorBrush(ThemeManager.NoteFillBrushes[0].Color)), new Point(noteX1 + 3, valueHeight));
                        }
                    }
                }

            }
            else
            {
                cxt.DrawRectangle(Brushes.Transparent, null, new Rect(new Point(0,0), new Point(100,100)));
            }
            cxt.Close();
            _updated = false;
        }
    }
}
