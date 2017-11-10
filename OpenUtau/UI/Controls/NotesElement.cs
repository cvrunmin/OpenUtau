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
    class NotesElement : ExpElement
    {
        public new double X { set { if (tTrans.X != Math.Round(value)) { tTrans.X = Math.Round(value); MarkUpdate(); } } get { return tTrans.X; } }
        public double Y { set { if (tTrans.Y != Math.Round(value)) { tTrans.Y = Math.Round(value); } } get { return tTrans.Y; } }

        double _trackHeight;
        public double TrackHeight { set { if (_trackHeight != value) { _trackHeight = value; MarkUpdate(); } } get { return _trackHeight; } }

        double _quarterWidth;
        public double QuarterWidth { set { if (_quarterWidth != value) { _quarterWidth = value; MarkUpdate(); } } get { return _quarterWidth; } }

        bool _showPitch = true;
        public bool ShowPitch { set { if (_showPitch != value) { _showPitch = value; MarkUpdate(); } } get { return _showPitch; } }

        public override UVoicePart Part { set { _part = value; ClearFormattedTextPool(); MarkUpdate(); } get { return _part; } }

        public OpenUtau.UI.Models.MidiViewModel midiVM;

        protected Pen penPit;

        protected Dictionary<string, FormattedText> fTextPool = new Dictionary<string, FormattedText>();
        protected Dictionary<string, double> fTextWidths = new Dictionary<string, double>();
        protected Dictionary<string, double> fTextHeights = new Dictionary<string, double>();

        public NotesElement()
        {
            penPit = new Pen(ThemeManager.WhiteKeyNameBrushNormal, 1);
            penPit.Freeze();
            this.IsHitTestVisible = false;
        }

        public override void RedrawIfUpdated()
        {
            if (!_updated) return;
            DrawingContext cxt = visual.RenderOpen();
            if (Part != null)
            {
                bool inView, lastInView = false;
                UNote lastNote = null;
                foreach (var note in Part.Notes)
                {
                    inView = midiVM.NoteIsInView(note);

                    if (inView && !lastInView && lastNote != null)
                            DrawNote(lastNote, cxt);

                    if (inView || lastInView)
                        DrawNote(note, cxt);

                    lastNote = note;
                    lastInView = inView;
                }
            }
            cxt.Close();
            _updated = false;
        }

        private void ClearFormattedTextPool()
        {
            fTextPool.Clear();
            fTextWidths.Clear();
            fTextHeights.Clear();
        }

        protected virtual void DrawNote(UNote note, DrawingContext cxt)
        {
            DrawNoteBody(note, cxt);
            if (!note.Error)
            {
                if (ShowPitch) DrawPitchBend(note, cxt);
                if (ShowPitch) DrawVibrato(note, cxt);
            }
            if (note.IsLyricBoxActive) DrawLyricBox(note);
        }
        private TextBox lyricBox = null;
        public TextBox LyricBox { get { return lyricBox; } }
        protected virtual void DrawLyricBox(UNote note)
        {
            double left = note.PosTick * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar + 1 - midiVM.OffsetX;
            double top = midiVM.TrackHeight * ((double)UIConstants.MaxNoteNum - 1 - note.NoteNum) + 1 - midiVM.OffsetY;
            double width = Math.Max(2, note.DurTick * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar - 1);
            double height = Math.Max(2, midiVM.TrackHeight - 2);
            DrawLyricBox(note,left,top,width,height);
        }

        protected void DrawLyricBox(UNote note, double left, double top, double width, double height)
        {
            if (lyricBox == null)
            {
                lyricBox = new TextBox() { Width = width, Height = height, Visibility = Visibility.Visible, MaxLength = 32767, Text = note.Lyric, VerticalContentAlignment = VerticalAlignment.Center };
                void OnEnterPressed(object sender)
                {
                    if (lyricBox == null) return; //already updated
                    if (!(((midiVM.MidiCanvas.Parent as Grid).Parent as Grid).Parent as MidiWindow).LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                    DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(Part, note, lyricBox.Text));
                    if (!(((midiVM.MidiCanvas.Parent as Grid).Parent as Grid).Parent as MidiWindow).LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                    midiVM.MidiCanvas.Children.Remove(lyricBox);
                    note.IsLyricBoxActive = false;
                    midiVM.AnyNotesEditing = false;
                    midiVM.MarkUpdate();
                    lyricBox = null;
                    midiVM.DeselectNote(note);
                    try
                    {
                        var a = Part.Notes.ToDictionary(unote => unote.NoteNo);
                        if (note.NoteNo < a.Count - 1)
                        {
                            midiVM.SelectNote(a[note.NoteNo + 1]);
                        }
                    }
                    catch (Exception)
                    {
                        int i = 0;
                        foreach (var item in Part.Notes)
                        {
                            item.NoteNo = i;
                            i++;
                        }
                        var a = Part.Notes.ToDictionary(unote => unote.NoteNo);
                        if (note.NoteNo < a.Count - 1)
                        {
                            midiVM.SelectNote(a[note.NoteNo + 1]);
                        }
                    }
                }
                lyricBox.InputBindings.Add(new System.Windows.Input.KeyBinding() { Command = new DelegateCommand(OnEnterPressed), Key = System.Windows.Input.Key.Return });
                lyricBox.InputBindings.Add(new System.Windows.Input.KeyBinding() { Command = new DelegateCommand(OnEnterPressed), Key = System.Windows.Input.Key.Enter });
                lyricBox.LostFocus += (sender, e) =>
                {
                    if (lyricBox == null || Part == null) return; //already updated
                    if (!(((midiVM.MidiCanvas.Parent as Grid).Parent as Grid).Parent as MidiWindow).LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                    DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(Part, note, lyricBox.Text));
                    if (!(((midiVM.MidiCanvas.Parent as Grid).Parent as Grid).Parent as MidiWindow).LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                    midiVM.MidiCanvas.Children.Remove(lyricBox);
                    note.IsLyricBoxActive = false;
                    midiVM.AnyNotesEditing = false;
                    midiVM.MarkUpdate();
                    lyricBox = null;
                    midiVM.DeselectNote(note);
                    var a = Part.Notes.ToDictionary(unote => unote.NoteNo);
                    if (note.NoteNo < a.Count - 1)
                    {
                        midiVM.SelectNote(a[note.NoteNo + 1]);
                    }
                };
                lyricBox.Focus();
                lyricBox.SelectAll();
                midiVM.MidiCanvas.Children.Add(lyricBox);
            }
            lyricBox.Width = width;
            lyricBox.Height = height;
            Canvas.SetLeft(lyricBox, left);
            Canvas.SetTop(lyricBox, top);
            lyricBox.Focus();
        }

        private void DrawNoteBody(UNote note, DrawingContext cxt) {
            DrawNoteBody(note, cxt, ThemeManager.NoteFillBrushes[0], ThemeManager.NoteFillSelectedBrush);
        }

        protected virtual void DrawNoteBody(UNote note, DrawingContext cxt, Brush fill, Brush selected)
        {
            double left = note.PosTick * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar + 1;
            double top = midiVM.TrackHeight * ((double)UIConstants.MaxNoteNum - 1 - note.NoteNum) + 1;
            double width = Math.Max(2, note.DurTick * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar - 1);
            double height = Math.Max(2, midiVM.TrackHeight - 2);
            cxt.DrawRoundedRectangle(
                note.Error ?
                (note.Selected ? new SolidColorBrush(ThemeManager.GetColorVariationAlpha(((SolidColorBrush)selected).Color, 127)) : new SolidColorBrush(ThemeManager.GetColorVariationAlpha(((SolidColorBrush)fill).Color, 127))) :
                (note.Selected ? selected : fill),
                null, new Rect(new Point(left, top), new Size(width, height)), 2, 2);
            if (height >= 10)
            {
                if (string.IsNullOrEmpty(note.Lyric)) return;
                string displayLyric = note.Lyric;

                if (!fTextPool.ContainsKey(displayLyric)) AddToFormattedTextPool(displayLyric);
                var fText = fTextPool[displayLyric];

                if (fTextWidths[displayLyric] + 5 > width)
                {
                    displayLyric = note.Lyric[0] + "..";
                    if (!fTextPool.ContainsKey(displayLyric)) AddToFormattedTextPool(displayLyric);
                    fText = fTextPool[displayLyric];
                    if (fTextWidths[displayLyric] + 5 > width) return;
                }

                cxt.DrawText(fText, new Point((int)left + 5, Math.Round(top + (height - fTextHeights[displayLyric]) / 2)));
            }
        }

        protected virtual void AddToFormattedTextPool(string text)
        {
            var fText = new FormattedText(
                    text,
                    System.Threading.Thread.CurrentThread.CurrentUICulture,
                    FlowDirection.LeftToRight, SystemFonts.CaptionFontFamily.GetTypefaces().First(),
                    12,
                    Brushes.White);
            fTextPool.Add(text, fText);
            fTextWidths.Add(text, fText.Width);
            fTextHeights.Add(text, fText.Height);
        }
        Pen pen6;
        protected virtual void DrawVibrato(UNote note, DrawingContext cxt)
        {
            if (note.Vibrato == null) return;
            var vibrato = note.Vibrato;
            if (note.Vibrato.IsEnabled)
            {
                double periodPix = DocManager.Inst.Project.MillisecondToTick(vibrato.Period) * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar;
                double lengthPix = note.DurTick * vibrato.Length / 100 * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar;

                double startX = (note.PosTick + note.DurTick * (1 - vibrato.Length / 100)) * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar;
                double startY = TrackHeight * (UIConstants.MaxNoteNum - 1.0 - note.NoteNum) + TrackHeight / 2;
                double inPix = lengthPix * vibrato.In / 100;
                double outPix = lengthPix * vibrato.Out / 100;
                double depthPix = vibrato.Depth / 100 * midiVM.TrackHeight;

                if (vibrato.Length > 0 && vibrato.Depth > 0)
                {
                    StreamGeometry g = new StreamGeometry();
                    using (var gcxt = g.Open())
                    {
                        DrawVibratoFrame(vibrato, lengthPix, startX, startY, inPix, outPix, depthPix, gcxt);
                    }
                    if (pen6 == null)
                    {
                        pen6 = new Pen(Brushes.Black, 1);
                        pen6.Freeze();
                    }
                    cxt.DrawGeometry(null, pen6, g);
                }

                double _x0 = 0, _y0 = 0, _x1 = 0, _y1 = 0;
                while (_x1 < lengthPix && _x1 >= 0)
                {
                    cxt.DrawLine(penPit, new Point(startX + _x0, startY + _y0), new Point(startX + _x1, startY + _y1));
                    _x0 = _x1;
                    _y0 = _y1;
                    _x1 += Math.Min(2, periodPix / 8);
                    _y1 = -Math.Sin(2 * Math.PI * (_x1 / periodPix + vibrato.Shift / 100)) * depthPix;
                    _y1 += vibrato.Drift / 100 * depthPix * (_x1 < 5 ? _x1 / 5 : _x1 >= lengthPix - 5 ? (lengthPix - _x1) / 5 : 1);
                    if (_x1 < inPix) _y1 = _y1 * _x1 / inPix;
                    else if (_x1 > lengthPix - outPix) _y1 = _y1 * (lengthPix - _x1) / outPix;
                }
            }
        }

        protected virtual void DrawVibratoFrame(VibratoExpression vibrato, double lengthPix, double startX, double startY, double inPix, double outPix, double depthPix, StreamGeometryContext gcxt)
        {
            gcxt.BeginFigure(new Point(startX, startY), true, true);
            gcxt.LineTo(new Point(startX + inPix, startY + depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX + inPix, startY - depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX + lengthPix - outPix, startY - depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX + lengthPix - outPix, startY + depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX + lengthPix, startY), true, false);
            gcxt.LineTo(new Point(startX + lengthPix - outPix, startY - depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX + inPix, startY + depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX + lengthPix - outPix, startY + depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX + inPix, startY - depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX + inPix, startY - depthPix + depthPix * vibrato.Drift / 100), true, false);
            gcxt.LineTo(new Point(startX, startY), true, false);
            gcxt.LineTo(new Point(startX + lengthPix, startY), true, true);
            gcxt.Close();
        }

        protected virtual void DrawPitchBend(UNote note, DrawingContext cxt, bool drawPt = true)
        {
            var _pitchExp = note.PitchBend as PitchBendExpression;
            var _pts = _pitchExp.Data as List<PitchPoint>;
            if (_pts.Count < 2) return;

            double pt0Tick = note.PosTick + MusicMath.MillisecondToTick(_pts[0].X, DocManager.Inst.Project.BPM, DocManager.Inst.Project.BeatUnit, DocManager.Inst.Project.Resolution);
            double pt0X = midiVM.QuarterWidth * pt0Tick / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar;
            double pt0Pit = note.NoteNum + _pts[0].Y / 10.0;
            double pt0Y = TrackHeight * ((double)UIConstants.MaxNoteNum - 1.0 - pt0Pit) + TrackHeight / 2;

            if(drawPt)
            if (note.PitchBend.SnapFirst) cxt.DrawEllipse(ThemeManager.WhiteKeyNameBrushNormal, penPit, new Point(pt0X, pt0Y), 2.5, 2.5);
            else cxt.DrawEllipse(null, penPit, new Point(pt0X, pt0Y), 2.5, 2.5);

            for (int i = 1; i < _pts.Count; i++)
            {
                double pt1Tick = note.PosTick + MusicMath.MillisecondToTick(_pts[i].X, DocManager.Inst.Project.BPM, DocManager.Inst.Project.BeatUnit, DocManager.Inst.Project.Resolution);
                double pt1X = midiVM.QuarterWidth * pt1Tick / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar;
                double pt1Pit = note.NoteNum + _pts[i].Y / 10.0;
                double pt1Y = TrackHeight * ((double)UIConstants.MaxNoteNum - 1.0 - pt1Pit) + TrackHeight / 2;

                // Draw arc
                double _x = pt0X;
                double _x2 = pt0X;
                double _y = pt0Y;
                double _y2 = pt0Y;
                if (pt1X - pt0X < 5)
                {
                    cxt.DrawLine(penPit, new Point(pt0X, pt0Y), new Point(pt1X, pt1Y));
                }
                else
                {
                    while (_x2 < pt1X)
                    {
                        _x = Math.Min(_x + 4, pt1X);
                        _y = MusicMath.InterpolateShape(pt0X, pt1X, pt0Y, pt1Y, _x, _pts[i - 1].Shape);
                        cxt.DrawLine(penPit, new Point(_x, _y), new Point(_x2, _y2));
                        _x2 = _x;
                        _y2 = _y;
                    }
                }

                pt0Tick = pt1Tick;
                pt0X = pt1X;
                pt0Pit = pt1Pit;
                pt0Y = pt1Y;
                if(drawPt) cxt.DrawEllipse(null, penPit, new Point(pt0X, pt0Y), 2.5, 2.5);
            }
        }
    }

    internal class ViewOnlyNotesElement : NotesElement
    {
        internal ViewOnlyNotesElement() : base() { }

        public override void RedrawIfUpdated()
        {
            if (!_updated) return;
            DrawingContext cxt = visual.RenderOpen();
            foreach(var Part in DocManager.Inst.Project.Parts.OfType<UVoicePart>())
            {
                if (DocManager.Inst.Project.Tracks[Part.TrackNo].ActuallyMuted) continue;
                bool inView, lastInView = false;
                UNote lastNote = null;
                Brush brush = new SolidColorBrush(ThemeManager.GetColorVariationAlpha(DocManager.Inst.Project.Tracks[Part.TrackNo].Color, (byte)(Part != this.Part ? 0x7f : 0xff)));
                foreach (var note in Part.Notes)
                {
                    inView = midiVM.NoteIsInView(note);

                    if (inView && !lastInView && lastNote != null)
                        DrawNote(lastNote, cxt,brush,brush);

                    if (inView || lastInView)
                        DrawNote(note, cxt,brush,brush);

                    lastNote = note;
                    lastInView = inView;
                }
            }
            cxt.Close();
            _updated = false;
        }
        private void DrawNote(UNote note, DrawingContext cxt, Brush fill, Brush selected)
        {
            DrawNoteBody(note, cxt, fill, selected);
            if (!note.Error)
            {
                if (ShowPitch) DrawPitchBend(note, cxt, false);
                if (ShowPitch) DrawVibrato(note, cxt);
            }
            if (note.IsLyricBoxActive) DrawLyricBox(note);
        }

        protected override void DrawNoteBody(UNote note, DrawingContext cxt, Brush fill, Brush selected)
        {
            UNote note1 = note.Clone();
            note1.PosTick += DocManager.Inst.Project.Parts.Find(part => part.PartNo == note1.PartNo)?.PosTick ?? 0;
            base.DrawNoteBody(note1, cxt, fill, selected);
        }

        protected override void DrawPitchBend(UNote note, DrawingContext cxt, bool drawPt = true)
        {
            UNote note1 = note.Clone();
            note1.PosTick += DocManager.Inst.Project.Parts.Find(part => part.PartNo == note1.PartNo)?.PosTick ?? 0;
            base.DrawPitchBend(note1, cxt, drawPt);
        }

        protected override void DrawVibrato(UNote note, DrawingContext cxt)
        {
            UNote note1 = note.Clone();
            note1.PosTick += DocManager.Inst.Project.Parts.Find(part => part.PartNo == note1.PartNo)?.PosTick ?? 0;
            base.DrawVibrato(note1, cxt);
        }

        protected override void DrawLyricBox(UNote note)
        {
            double left = (DocManager.Inst.Project.Parts[note.PartNo].PosTick + note.PosTick) * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar + 1 - midiVM.OffsetX;
            double top = midiVM.TrackHeight * ((double)UIConstants.MaxNoteNum - 1 - note.NoteNum) + 1 - midiVM.OffsetY;
            double width = Math.Max(2, note.DurTick * midiVM.QuarterWidth / DocManager.Inst.Project.Resolution * midiVM.BeatPerBar - 1);
            double height = Math.Max(2, midiVM.TrackHeight - 2);
            DrawLyricBox(note, left, top, width, height);
        }

        protected override void DrawVibratoFrame(VibratoExpression vibrato, double lengthPix, double startX, double startY, double inPix, double outPix, double depthPix, StreamGeometryContext gcxt)
        {

        }
    }
}
