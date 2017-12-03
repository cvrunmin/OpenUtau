using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;

using WinInterop = System.Windows.Interop;
using System.Runtime.InteropServices;

using OpenUtau.UI.Models;
using OpenUtau.UI.Controls;
using OpenUtau.Core;
using OpenUtau.Core.USTx;
using NAudio.Wave;

namespace OpenUtau.UI
{
    /// <summary>
    /// Interaction logic for BorderlessWindow.xaml
    /// </summary>
    public partial class MidiWindow : BorderlessWindow
    {
        internal MidiViewModel MidiVM { get; private set; }
        MidiViewHitTest midiHT;
        ContextMenu pitchCxtMenu;

        RoutedEventHandler pitchShapeDelegate;
        class PitchPointHitTestResultContainer { public PitchPointHitTestResult Result; }
        PitchPointHitTestResultContainer pitHitContainer;

        EnumTool ToolUsing
        {
            get
            {
                if (radioToolCursor.IsChecked.Value)
                {
                    return EnumTool.Cursor;
                }
                if (radioToolPaint.IsChecked.Value)
                {
                    return EnumTool.Brush;
                }
                return EnumTool.Cursor;
            }
        }

        private bool _tiny;
        public bool LyricsPresetDedicate
        {
            get { return _tiny; }
            set
            {
                _tiny = value;
                MidiVM.LyricsPresetDedicate = value;
                if (value)
                {
                    keyboardBackground.Visibility = Visibility.Collapsed;
                    MidiVM.TrackHeight = 32;
                    showPitchToggle.Visibility = Visibility.Collapsed;
                    mainButton.Visibility = Visibility.Collapsed;
                    MidiVM.ViewHeight = 22;
                }
                else
                {
                    keyboardBackground.Visibility = Visibility.Visible;
                    MidiVM.TrackHeight = 32;
                    showPitchToggle.Visibility = Visibility.Visible;
                    mainButton.Visibility = Visibility.Visible;
                }
            }
        }

        private bool _viewOnly;
        private bool _multiview;
        public bool ViewOnly { get {
                return _viewOnly;
            } set {
                _viewOnly = value;
                menuEdit.IsEnabled = !value;
                MultiView = value;
            }
        }
        public bool MultiView {
            get => _multiview;
            set {
                _multiview = value;
                MidiVM.ToggleViewMode(value);
                if (!value) _viewOnly = false;
            }
        }

        public MidiWindow()
        {
            InitializeComponent();

            this.CloseButtonClicked += (o, e) => { Hide(); };
            CompositionTargetEx.FrameUpdating += RenderLoop;

            viewScaler.Max = UIConstants.NoteMaxHeight;
            viewScaler.Min = UIConstants.NoteMinHeight;
            viewScaler.Value = UIConstants.NoteDefaultHeight;
            viewScaler.ViewScaled += viewScaler_ViewScaled;

            viewScalerX.Max = UIConstants.MidiQuarterMaxWidth;
            viewScalerX.Min = UIConstants.MidiQuarterMinWidth;
            viewScalerX.Value = UIConstants.MidiQuarterDefaultWidth;

            MidiVM = (MidiViewModel)this.Resources["midiVM"];
            SetBinding(TitleProperty, new Binding() { Path = new PropertyPath("Title"), Source = MidiVM });
            MidiVM.TimelineCanvas = this.timelineCanvas;
            MidiVM.MidiCanvas = this.notesCanvas;
            MidiVM.PhonemeCanvas = this.phonemeCanvas;
            MidiVM.ExpCanvas = this.expCanvas;
            MidiVM.Subscribe(DocManager.Inst);

            midiHT = new MidiViewHitTest(MidiVM);

            comboVMs = new List<ExpComboBoxViewModel>()
            {
                new ExpComboBoxViewModel() { Index=0 },
                new ExpComboBoxViewModel() { Index=1 },
                new ExpComboBoxViewModel() { Index=2 },
                new ExpComboBoxViewModel() { Index=3 }
            };

            comboVMs[0].CreateBindings(expCombo0);
            comboVMs[1].CreateBindings(expCombo1);
            comboVMs[2].CreateBindings(expCombo2);
            comboVMs[3].CreateBindings(expCombo3);

            InitPitchPointContextMenu();
        }
        List<ExpComboBoxViewModel> comboVMs;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            DocManager.Inst.UnSubscribe(MidiVM);
            MidiVM = null;
            foreach (var item in comboVMs)
            {
                DocManager.Inst.UnSubscribe(item);
            }
        }

        void InitPitchPointContextMenu()
        {
            pitchCxtMenu = new ContextMenu();
            pitchCxtMenu.Background = Brushes.White;
            pitchCxtMenu.Items.Add(new MenuItem() { Header = "Ease In/Out" });
            pitchCxtMenu.Items.Add(new MenuItem() { Header = "Linear" });
            pitchCxtMenu.Items.Add(new MenuItem() { Header = "Ease In" });
            pitchCxtMenu.Items.Add(new MenuItem() { Header = "Ease Out" });
            pitchCxtMenu.Items.Add(new MenuItem() { Header = "Snap to Previous Note" });
            pitchCxtMenu.Items.Add(new MenuItem() { Header = "Delete Point" });
            pitchCxtMenu.Items.Add(new MenuItem() { Header = "Add Point" });

            pitHitContainer = new PitchPointHitTestResultContainer();
            pitchShapeDelegate = (_o, _e) =>
            {
                var o = _o as MenuItem;
                var pitHit = pitHitContainer.Result;
                if (o == pitchCxtMenu.Items[4])
                {
                    if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                    DocManager.Inst.ExecuteCmd(new SnapPitchPointCommand(pitHit.Note));
                    if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                }
                else if (o == pitchCxtMenu.Items[5])
                {
                    if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                    DocManager.Inst.ExecuteCmd(new DeletePitchPointCommand(MidiVM.Part, pitHit.Note, pitHit.Index));
                    if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                }
                else if (o == pitchCxtMenu.Items[6])
                {
                    if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                    DocManager.Inst.ExecuteCmd(new AddPitchPointCommand(pitHit.Note, new PitchPoint(pitHit.X, pitHit.Y), pitHit.Index + 1));
                    if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                }
                else
                {
                    PitchPointShape shape =
                        o == pitchCxtMenu.Items[0] ? PitchPointShape.InOut :
                        o == pitchCxtMenu.Items[2] ? PitchPointShape.In :
                        o == pitchCxtMenu.Items[3] ? PitchPointShape.Out : PitchPointShape.Linear;
                    if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                    DocManager.Inst.ExecuteCmd(new ChangePitchPointShapeCommand(MidiVM.Part, pitHit.Note.PitchBend.Points[pitHit.Index], shape));
                    if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                }
            };

            foreach (var item in pitchCxtMenu.Items)
            {
                var _item = item as MenuItem;
                if (_item != null) _item.Click += pitchShapeDelegate;
            }
        }

        void viewScaler_ViewScaled(object sender, EventArgs e)
        {
            double zoomCenter = (MidiVM.OffsetY + MidiVM.ViewHeight / 2) / MidiVM.TrackHeight;
            MidiVM.TrackHeight = ((ViewScaledEventArgs)e).Value;
            MidiVM.OffsetY = MidiVM.TrackHeight * zoomCenter - MidiVM.ViewHeight / 2;
            MidiVM.MarkUpdate();
        }

        private TimeSpan lastFrame = TimeSpan.Zero;

        void RenderLoop(object sender, EventArgs e)
        {
            if (MidiVM == null || MidiVM.Part == null || MidiVM.Project == null) return;

            TimeSpan nextFrame = ((RenderingEventArgs)e).RenderingTime;
            double deltaTime = (nextFrame - lastFrame).TotalMilliseconds;
            lastFrame = nextFrame;

            DragScroll(deltaTime);
            keyboardBackground.RenderIfUpdated();
            tickBackground.RenderIfUpdated();
            timelineBackground.RenderIfUpdated();
            keyTrackBackground.RenderIfUpdated();
            expTickBackground.RenderIfUpdated();
            MidiVM.RedrawIfUpdated();
        }

        public void DragScroll(double deltaTime)
        {
            if (Mouse.Captured == this.notesCanvas && Mouse.LeftButton == MouseButtonState.Pressed)
            {

                const double scrollSpeed = 0.015;
                Point mousePos = Mouse.GetPosition(notesCanvas);
                bool needUdpate = false;
                double delta = scrollSpeed * deltaTime;
                if (mousePos.X < 0)
                {
                    this.horizontalScroll.Value = this.horizontalScroll.Value - this.horizontalScroll.SmallChange * delta;
                    needUdpate = true;
                }
                else if (mousePos.X > notesCanvas.ActualWidth)
                {
                    this.horizontalScroll.Value = this.horizontalScroll.Value + this.horizontalScroll.SmallChange * delta;
                    needUdpate = true;
                }

                if (mousePos.Y < 0 && Mouse.Captured == this.notesCanvas)
                {
                    this.verticalScroll.Value = this.verticalScroll.Value - this.verticalScroll.SmallChange * delta;
                    needUdpate = true;
                }
                else if (mousePos.Y > notesCanvas.ActualHeight && Mouse.Captured == this.notesCanvas)
                {
                    this.verticalScroll.Value = this.verticalScroll.Value + this.verticalScroll.SmallChange * delta;
                    needUdpate = true;
                }

                if (needUdpate)
                {
                    notesCanvas_MouseMove_Helper(mousePos);
                    if (Mouse.Captured == this.timelineCanvas) timelineCanvas_MouseMove_Helper(mousePos);
                    MidiVM.MarkUpdate();
                }
            }
            else if (Mouse.Captured == timelineCanvas && Mouse.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = Mouse.GetPosition(timelineCanvas);
                timelineCanvas_MouseMove_Helper(mousePos);
                MidiVM.MarkUpdate();
            }
        }

        # region Note Canvas

        Rectangle selectionBox;
        Point? selectionStart;
        int _lastNoteLength = 120;

        bool _inMove = false;
        bool _inResize = false;
        bool _vbrInLengthen = false;
        bool _vbrInDeepen = false;
        bool _vbrPeriodLengthen = false;
        bool _vbrPhaseMoving = false;
        bool _vbrInMoving = false;
        bool _vbrOutMoving = false;
        bool _vbrDriftMoving = false;
        UNote _noteHit;
        bool _inPitMove = false;
        PitchPoint _pitHit;
        int _pitHitIndex;
        int _tickMoveRelative;
        int _tickMoveStart;
        UNote _noteMoveNoteLeft;
        UNote _noteMoveNoteRight;
        UNote _noteMoveNoteMin;
        UNote _noteMoveNoteMax;
        UNote _noteResizeShortest;

        private void notesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MidiVM.Part == null || ViewOnly) return;
            Point mousePos = e.GetPosition((Canvas)sender);

            var hit = VisualTreeHelper.HitTest(notesCanvas, mousePos).VisualHit;
            System.Diagnostics.Debug.WriteLine("Mouse hit " + hit.ToString());
            if (MidiVM.AnyNotesEditing)
            {
                MidiVM.notesElement?.LyricBox?.RaiseEvent(new RoutedEventArgs() { RoutedEvent = LostFocusEvent });
            }
            var pitHitResult = midiHT.HitTestPitchPoint(mousePos);
            var vbrResult = midiHT.HitTestVibrato(mousePos);

            if (pitHitResult != null)
            {
                if (pitHitResult.OnPoint)
                {
                    _inPitMove = true;
                    _pitHit = pitHitResult.Note.PitchBend.Points[pitHitResult.Index];
                    _pitHitIndex = pitHitResult.Index;
                    _noteHit = pitHitResult.Note;
                    if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                }
            }
            else
            {
                UNote noteHit = midiHT.HitTestNote(mousePos);
                if (noteHit != null) System.Diagnostics.Debug.WriteLine("Mouse hit" + noteHit.ToString());

                if (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    selectionStart = new Point(MidiVM.CanvasToQuarter(mousePos.X), MidiVM.CanvasToNoteNum(mousePos.Y));

                    if (Keyboard.IsKeyUp(Key.LeftShift) && Keyboard.IsKeyUp(Key.RightShift)) MidiVM.DeselectAll();

                    if (selectionBox == null)
                    {
                        selectionBox = new Rectangle()
                        {
                            Stroke = Brushes.Black,
                            StrokeThickness = 2,
                            Fill = ThemeManager.BarNumberBrush,
                            Width = 0,
                            Height = 0,
                            Opacity = 0.5,
                            RadiusX = 8,
                            RadiusY = 8,
                            IsHitTestVisible = false
                        };
                        notesCanvas.Children.Add(selectionBox);
                        Panel.SetZIndex(selectionBox, 1000);
                        selectionBox.Visibility = System.Windows.Visibility.Visible;
                    }
                    else
                    {
                        selectionBox.Width = 0;
                        selectionBox.Height = 0;
                        Panel.SetZIndex(selectionBox, 1000);
                        selectionBox.Visibility = System.Windows.Visibility.Visible;
                    }
                    Mouse.OverrideCursor = Cursors.Cross;
                }
                else
                {
                    if (noteHit != null)
                    {
                        _noteHit = noteHit;
                        if (!MidiVM.SelectedNotes.Contains(noteHit)) MidiVM.DeselectAll();
                        MidiVM.SelectNote(noteHit);
                        if (!noteHit.IsLyricBoxActive)
                        {
                            if (e.ClickCount >= 2)
                            {
                                noteHit.IsLyricBoxActive = true;
                                MidiVM.AnyNotesEditing = true;
                                MidiVM.UpdateViewRegion(noteHit.EndTick + MidiVM.Part.PosTick);
                                MidiVM.MarkUpdate();
                                MidiVM.notesElement?.MarkUpdate();
                                MidiVM.RedrawIfUpdated();
                            }
                            else
                            {
                                if (MidiVM.ShowPitch && noteHit.Vibrato.IsEnabled)
                                {
                                    if (vbrResult.Success && vbrResult.Note == noteHit && vbrResult.OnPoint != VibratoHitTestResult.VibratoPart.No)
                                    {
                                        switch (vbrResult.OnPoint)
                                        {
                                            case VibratoHitTestResult.VibratoPart.Length:
                                                _vbrInLengthen = true;
                                                Mouse.OverrideCursor = Cursors.SizeWE;
                                                break;
                                            case VibratoHitTestResult.VibratoPart.Depth:
                                                _vbrInDeepen = true;
                                                Mouse.OverrideCursor = Cursors.SizeNS;
                                                break;
                                            case VibratoHitTestResult.VibratoPart.Period:
                                                _vbrPeriodLengthen = true;
                                                Mouse.OverrideCursor = Cursors.SizeWE;
                                                break;
                                            case VibratoHitTestResult.VibratoPart.In:
                                                _vbrInMoving = true;
                                                Mouse.OverrideCursor = Cursors.SizeWE;
                                                break;
                                            case VibratoHitTestResult.VibratoPart.Out:
                                                _vbrOutMoving = true;
                                                Mouse.OverrideCursor = Cursors.SizeWE;
                                                break;
                                            case VibratoHitTestResult.VibratoPart.Shift:
                                                _vbrPhaseMoving = true;
                                                Mouse.OverrideCursor = Cursors.SizeWE;
                                                break;
                                            case VibratoHitTestResult.VibratoPart.Drift:
                                                _vbrDriftMoving = true;
                                                Mouse.OverrideCursor = Cursors.SizeNS;
                                                break;
                                            default:
                                                break;
                                        }
                                        if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                                    }
                                    else if (midiHT.HitNoteResizeArea(noteHit, mousePos))
                                    {
                                        if (Keyboard.IsKeyDown(Key.RightAlt))
                                        {
                                            _vbrOutMoving = true;
                                            Mouse.OverrideCursor = Cursors.SizeWE;
                                        }
                                        else
                                        {
                                            // Resize note
                                            _inResize = true;
                                            Mouse.OverrideCursor = Cursors.SizeWE;
                                            if (MidiVM.SelectedNotes.Count != 0)
                                            {
                                                _noteResizeShortest = noteHit;
                                                foreach (UNote note in MidiVM.SelectedNotes)
                                                    if (note.DurTick < _noteResizeShortest.DurTick) _noteResizeShortest = note;
                                            }
                                        }
                                        if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                                    }
                                    else
                                    {
                                        // Move note
                                        _inMove = true;
                                        _tickMoveRelative = MidiVM.CanvasToSnappedTick(mousePos.X) - noteHit.PosTick;
                                        _tickMoveStart = noteHit.PosTick;
                                        _lastNoteLength = noteHit.DurTick;
                                        if (MidiVM.SelectedNotes.Count > 1)
                                        {
                                            _noteMoveNoteMax = _noteMoveNoteMin = noteHit;
                                            _noteMoveNoteLeft = _noteMoveNoteRight = noteHit;
                                            foreach (UNote note in MidiVM.SelectedNotes)
                                            {
                                                if (note.PosTick < _noteMoveNoteLeft.PosTick) _noteMoveNoteLeft = note;
                                                if (note.EndTick > _noteMoveNoteRight.EndTick) _noteMoveNoteRight = note;
                                                if (note.NoteNum < _noteMoveNoteMin.NoteNum) _noteMoveNoteMin = note;
                                                if (note.NoteNum > _noteMoveNoteMax.NoteNum) _noteMoveNoteMax = note;
                                            }
                                        }
                                        if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                                    }
                                }
                                else if (midiHT.HitNoteResizeArea(noteHit, mousePos))
                                {
                                    if (Keyboard.IsKeyDown(Key.RightAlt))
                                    {
                                        _vbrOutMoving = true;
                                        Mouse.OverrideCursor = Cursors.SizeWE;
                                    }
                                    else
                                    {
                                        // Resize note
                                        _inResize = true;
                                        Mouse.OverrideCursor = Cursors.SizeWE;
                                        if (MidiVM.SelectedNotes.Count != 0)
                                        {
                                            _noteResizeShortest = noteHit;
                                            foreach (UNote note in MidiVM.SelectedNotes)
                                                if (note.DurTick < _noteResizeShortest.DurTick) _noteResizeShortest = note;
                                        }
                                    }
                                    if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                                }
                                else
                                {
                                    // Move note
                                    _inMove = true;
                                    _tickMoveRelative = MidiVM.CanvasToSnappedTick(mousePos.X) - noteHit.PosTick;
                                    _tickMoveStart = noteHit.PosTick;
                                    _lastNoteLength = noteHit.DurTick;
                                    if (MidiVM.SelectedNotes.Count > 1)
                                    {
                                        _noteMoveNoteMax = _noteMoveNoteMin = noteHit;
                                        _noteMoveNoteLeft = _noteMoveNoteRight = noteHit;
                                        foreach (UNote note in MidiVM.SelectedNotes)
                                        {
                                            if (note.PosTick < _noteMoveNoteLeft.PosTick) _noteMoveNoteLeft = note;
                                            if (note.EndTick > _noteMoveNoteRight.EndTick) _noteMoveNoteRight = note;
                                            if (note.NoteNum < _noteMoveNoteMin.NoteNum) _noteMoveNoteMin = note;
                                            if (note.NoteNum > _noteMoveNoteMax.NoteNum) _noteMoveNoteMax = note;
                                        }
                                    }
                                    if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                                }
                            }
                        }
                    }
                    else if (vbrResult.Success)
                    {
                        _noteHit = vbrResult.Note;
                        if (vbrResult.OnPoint == VibratoHitTestResult.VibratoPart.Drift)
                        {
                            _vbrDriftMoving = true;
                            Mouse.OverrideCursor = Cursors.SizeNS;
                            if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                        }
                        else if (vbrResult.OnPoint == VibratoHitTestResult.VibratoPart.Depth)
                        {
                            _vbrInDeepen = true;
                            Mouse.OverrideCursor = Cursors.SizeNS;
                            if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                        }
                        else if (vbrResult.OnPoint == VibratoHitTestResult.VibratoPart.Period)
                        {
                            _vbrPeriodLengthen = true;
                            Mouse.OverrideCursor = Cursors.SizeWE;
                            if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                        }
                        else if (vbrResult.OnPoint == VibratoHitTestResult.VibratoPart.Shift)
                        {
                            _vbrPhaseMoving = true;
                            Mouse.OverrideCursor = Cursors.SizeWE;
                            if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                        }
                    }
                    else if (!MidiVM.SelectedNotes.Any()) // Add note
                    {
                        UNote newNote = DocManager.Inst.Project.CreateNote(
                            MidiVM.CanvasToNoteNum(mousePos.Y),
                            MidiVM.CanvasToSnappedTick(mousePos.X),
                            _lastNoteLength);
                        newNote.PartNo = MidiVM.Part.PartNo;
                        newNote.NoteNo = MidiVM.Part.Notes.Count;
                        foreach (var item in newNote.Expressions)
                        {
                            newNote.Expressions[item.Key].Data = MidiVM.Part.Expressions[item.Key].Data;
                        }

                        if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                        DocManager.Inst.ExecuteCmd(new AddNoteCommand(MidiVM.Part, newNote));
                        if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                        MidiVM.MarkUpdate();
                        // Enable drag
                        MidiVM.DeselectAll();
                        MidiVM.SelectNote(newNote);
                        _inMove = true;
                        _noteHit = newNote;
                        _tickMoveRelative = 0;
                        _tickMoveStart = newNote.PosTick;
                        if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                    }
                    else
                    {
                        MidiVM.DeselectAll();
                    }
                }
            }
            ((UIElement)sender).CaptureMouse();
        }

        private void notesCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (MidiVM.Part == null || ViewOnly) return;
            if (_inMove || _inResize || _vbrInDeepen || _vbrInLengthen || _vbrPeriodLengthen || _vbrPhaseMoving || _vbrInMoving || _vbrOutMoving || _vbrDriftMoving)
            {
                if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
            }
            _inMove = false;
            _inResize = false;
            _vbrInLengthen = false;
            _vbrInDeepen = false;
            _vbrPeriodLengthen = false;
            _vbrPhaseMoving = false;
            _vbrInMoving = false;
            _vbrOutMoving = false;
            _vbrDriftMoving = false;
            _noteHit = null;
            _inPitMove = false;
            _pitHit = null;
            // End selection
            selectionStart = null;
            if (selectionBox != null)
            {
                Canvas.SetZIndex(selectionBox, -100);
                selectionBox.Visibility = System.Windows.Visibility.Hidden;
            }
            MidiVM.DoneTempSelect();
            ((Canvas)sender).ReleaseMouseCapture();
            Mouse.OverrideCursor = null;
        }

        private void notesCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!ViewOnly)
            {
                Point mousePos = e.GetPosition((Canvas)sender);
                notesCanvas_MouseMove_Helper(mousePos);
            }
        }

        private void notesCanvas_MouseMove_Helper(Point mousePos)
        {
            if (MidiVM.Part == null) return;
            if (selectionStart != null) // Selection
            {
                double top = MidiVM.NoteNumToCanvas(Math.Max(MidiVM.CanvasToNoteNum(mousePos.Y), (int)selectionStart.Value.Y));
                double bottom = MidiVM.NoteNumToCanvas(Math.Min(MidiVM.CanvasToNoteNum(mousePos.Y), (int)selectionStart.Value.Y) - 1);
                double left = Math.Min(mousePos.X, MidiVM.QuarterToCanvas(selectionStart.Value.X));
                selectionBox.Width = Math.Abs(mousePos.X - MidiVM.QuarterToCanvas(selectionStart.Value.X));
                selectionBox.Height = bottom - top;
                Canvas.SetLeft(selectionBox, left);
                Canvas.SetTop(selectionBox, top);
                MidiVM.TempSelectInBox(selectionStart.Value.X, MidiVM.CanvasToQuarter(mousePos.X), (int)selectionStart.Value.Y, MidiVM.CanvasToNoteNum(mousePos.Y));
            }
            else if (_inPitMove)
            {
                double tickX = MidiVM.CanvasToQuarter(mousePos.X) * DocManager.Inst.Project.Resolution / MidiVM.BeatPerBar - _noteHit.PosTick;
                double deltaX = DocManager.Inst.Project.TickToMillisecond(tickX, MidiVM.Part.PosTick) - _pitHit.X;
                if (_pitHitIndex != 0) deltaX = Math.Max(deltaX, _noteHit.PitchBend.Points[_pitHitIndex - 1].X - _pitHit.X);
                if (_pitHitIndex != _noteHit.PitchBend.Points.Count - 1) deltaX = Math.Min(deltaX, _noteHit.PitchBend.Points[_pitHitIndex + 1].X - _pitHit.X);
                double deltaY = Keyboard.Modifiers == ModifierKeys.Shift ? Math.Round(MidiVM.CanvasToPitch(mousePos.Y) - _noteHit.NoteNum) * 10 - _pitHit.Y :
                    (MidiVM.CanvasToPitch(mousePos.Y) - _noteHit.NoteNum) * 10 - _pitHit.Y;
                if (_noteHit.PitchBend.Points.First() == _pitHit && _noteHit.PitchBend.SnapFirst || _noteHit.PitchBend.Points.Last() == _pitHit) deltaY = 0;
                if (deltaX != 0 || deltaY != 0)
                    DocManager.Inst.ExecuteCmd(new MovePitchPointCommand(MidiVM.Part, _pitHit, deltaX, deltaY));
            }
            else if (_inMove) // Move Note
            {
                if (MidiVM.SelectedNotes.Count <= 1)
                {
                    int newNoteNum = Math.Max(0, Math.Min(UIConstants.MaxNoteNum - 1, MidiVM.CanvasToNoteNum(mousePos.Y)));
                    int newPosTick = Math.Max(0, Math.Min((int)(MidiVM.QuarterCount * MidiVM.Project.Resolution / MidiVM.BeatPerBar) - _noteHit.DurTick,
                        (int)(MidiVM.Project.Resolution * MidiVM.CanvasToSnappedQuarter(mousePos.X) / MidiVM.BeatPerBar) - _tickMoveRelative));
                    if (newNoteNum != _noteHit.NoteNum || newPosTick != _noteHit.PosTick)
                        DocManager.Inst.ExecuteCmd(new MoveNoteCommand(MidiVM.Part, _noteHit, newPosTick - _noteHit.PosTick, newNoteNum - _noteHit.NoteNum));
                }
                else
                {
                    int deltaNoteNum = MidiVM.CanvasToNoteNum(mousePos.Y) - _noteHit.NoteNum;
                    int deltaPosTick = ((int)(MidiVM.Project.Resolution * MidiVM.CanvasToSnappedQuarter(mousePos.X) / MidiVM.BeatPerBar) - _tickMoveRelative) - _noteHit.PosTick;

                    if (deltaNoteNum != 0 || deltaPosTick != 0)
                    {
                        bool changeNoteNum = deltaNoteNum + _noteMoveNoteMin.NoteNum >= 0 && deltaNoteNum + _noteMoveNoteMax.NoteNum < UIConstants.MaxNoteNum;
                        bool changePosTick = deltaPosTick + _noteMoveNoteLeft.PosTick >= 0 && deltaPosTick + _noteMoveNoteRight.EndTick <= MidiVM.QuarterCount * MidiVM.Project.Resolution / MidiVM.BeatPerBar;
                        if (changeNoteNum || changePosTick)

                            DocManager.Inst.ExecuteCmd(new MoveNoteCommand(MidiVM.Part, MidiVM.SelectedNotes,
                                    changePosTick ? deltaPosTick : 0, changeNoteNum ? deltaNoteNum : 0));
                    }
                }
                Mouse.OverrideCursor = Cursors.SizeAll;
            }
            else if (_inResize) // resize
            {
                if (MidiVM.SelectedNotes.Count <= 1)
                {
                    int newDurTick = (int)(MidiVM.CanvasRoundToSnappedQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.BeatPerBar) - _noteHit.PosTick;
                    if (newDurTick != _noteHit.DurTick && newDurTick >= MidiVM.GetSnapUnit() * MidiVM.Project.Resolution / MidiVM.BeatPerBar)
                    {
                        DocManager.Inst.ExecuteCmd(new ResizeNoteCommand(MidiVM.Part, _noteHit, newDurTick - _noteHit.DurTick));
                        _lastNoteLength = newDurTick;
                    }
                }
                else
                {
                    int deltaDurTick = (int)(MidiVM.CanvasRoundToSnappedQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.BeatPerBar) - _noteHit.EndTick;
                    if (deltaDurTick != 0 && deltaDurTick + _noteResizeShortest.DurTick > MidiVM.GetSnapUnit())
                    {
                        DocManager.Inst.ExecuteCmd(new ResizeNoteCommand(MidiVM.Part, MidiVM.SelectedNotes, deltaDurTick));
                        _lastNoteLength = _noteHit.DurTick;
                    }
                }
            }
            else if (_vbrInLengthen)
            {
                int deltaDurTick = _noteHit.EndTick - (int)(MidiVM.CanvasToQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.BeatPerBar);
                if (deltaDurTick > 0)
                {
                    var newlen = (double)deltaDurTick / _noteHit.DurTick * 100;
                    DocManager.Inst.ExecuteCmd(new UpdateNoteVibratoCommand(_noteHit, len: newlen));
                }
            }
            else if (_vbrInDeepen)
            {
                double pitch = MidiVM.CanvasToPitch(mousePos.Y);
                DocManager.Inst.ExecuteCmd(new UpdateNoteVibratoCommand(_noteHit, dep: Math.Abs(_noteHit.NoteNum - pitch) * 100));
            }
            else if (_vbrPeriodLengthen)
            {
                int deltaDurTick = _noteHit.EndTick - (int)(MidiVM.CanvasToQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.BeatPerBar);
                double lengthX = _noteHit.DurTick * _noteHit.Vibrato.Length / 100;
                DocManager.Inst.ExecuteCmd(new UpdateNoteVibratoCommand(_noteHit, per: (lengthX - deltaDurTick) / lengthX * (512 - 64) + 64));
            }
            else if (_vbrPhaseMoving)
            {
                int deltaDurTick = _noteHit.EndTick - (int)(MidiVM.CanvasToQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.BeatPerBar);
                double lengthX = _noteHit.DurTick * _noteHit.Vibrato.Length / 100;
                DocManager.Inst.ExecuteCmd(new UpdateNoteVibratoCommand(_noteHit, shift: (lengthX - deltaDurTick) / lengthX * 100));
            }
            else if (_vbrInMoving)
            {
                int deltaDurTick = _noteHit.EndTick - (int)(MidiVM.CanvasToQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.BeatPerBar);
                double lengthX = _noteHit.DurTick * _noteHit.Vibrato.Length / 100;
                DocManager.Inst.ExecuteCmd(new UpdateNoteVibratoCommand(_noteHit, din: (lengthX - deltaDurTick) / lengthX * 100));
            }
            else if (_vbrOutMoving)
            {
                int deltaDurTick = _noteHit.EndTick - (int)(MidiVM.CanvasToQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.BeatPerBar);
                double lengthX = _noteHit.DurTick * _noteHit.Vibrato.Length / 100;
                DocManager.Inst.ExecuteCmd(new UpdateNoteVibratoCommand(_noteHit, dout: (1 - (lengthX - deltaDurTick) / lengthX) * 100));
            }
            else if (_vbrDriftMoving)
            {
                double pitch = MidiVM.CanvasToPitch(mousePos.Y);
                DocManager.Inst.ExecuteCmd(new UpdateNoteVibratoCommand(_noteHit, drift: (_noteHit.NoteNum - pitch) * 100));
            }
            else if (Mouse.RightButton == MouseButtonState.Pressed && ToolUsing == EnumTool.Brush) // Remove Note
            {
                UNote noteHit = midiHT.HitTestNote(mousePos);
                if (noteHit != null) DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(MidiVM.Part, noteHit));
            }
            else if (Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released)
            {
                var pitHit = midiHT.HitTestPitchPoint(mousePos);
                if (pitHit != null)
                {
                    Mouse.OverrideCursor = Cursors.Hand;
                }
                else
                {
                    UNote noteHit = midiHT.HitTestNote(mousePos);
                    if (noteHit != null && midiHT.HitNoteResizeArea(noteHit, mousePos))
                    {
                        Mouse.OverrideCursor = Cursors.SizeWE;
                    }
                    else if (showPitchToggle.IsChecked.Value)
                    {
                        VibratoHitTestResult vibHit = midiHT.HitTestVibrato(mousePos);
                        if (vibHit.Success)
                        {
                            switch (vibHit.OnPoint)
                            {
                                case VibratoHitTestResult.VibratoPart.Length:
                                case VibratoHitTestResult.VibratoPart.Period:
                                case VibratoHitTestResult.VibratoPart.In:
                                case VibratoHitTestResult.VibratoPart.Out:
                                case VibratoHitTestResult.VibratoPart.Shift:
                                    Mouse.OverrideCursor = Cursors.SizeWE;
                                    break;
                                case VibratoHitTestResult.VibratoPart.Depth:
                                case VibratoHitTestResult.VibratoPart.Drift:
                                    Mouse.OverrideCursor = Cursors.SizeNS;
                                    break;
                                default:
                                    Mouse.OverrideCursor = null;
                                    break;
                            }
                        }
                        else Mouse.OverrideCursor = null;
                    }
                    else Mouse.OverrideCursor = null;
                }
            }
        }

        private void notesCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MidiVM.Part == null || ViewOnly) return;
            Point mousePos = e.GetPosition((Canvas)sender);

            var pitHit = midiHT.HitTestPitchPoint(mousePos);
            if (pitHit != null)
            {
                Mouse.OverrideCursor = null;
                pitHitContainer.Result = pitHit;

                if (pitHit.OnPoint)
                {
                    ((MenuItem)pitchCxtMenu.Items[4]).Header = pitHit.Note.PitchBend.SnapFirst ? "Unsnap from previous point" : "Snap to previous point";
                    ((MenuItem)pitchCxtMenu.Items[4]).Visibility = pitHit.Index == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

                    if (pitHit.Index == 0 || pitHit.Index == pitHit.Note.PitchBend.Points.Count - 1) ((MenuItem)pitchCxtMenu.Items[5]).Visibility = System.Windows.Visibility.Collapsed;
                    else ((MenuItem)pitchCxtMenu.Items[5]).Visibility = System.Windows.Visibility.Visible;

                    ((MenuItem)pitchCxtMenu.Items[6]).Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    ((MenuItem)pitchCxtMenu.Items[4]).Visibility = System.Windows.Visibility.Collapsed;
                    ((MenuItem)pitchCxtMenu.Items[5]).Visibility = System.Windows.Visibility.Collapsed;
                    ((MenuItem)pitchCxtMenu.Items[6]).Visibility = System.Windows.Visibility.Visible;
                }

                pitchCxtMenu.IsOpen = true;
                pitchCxtMenu.PlacementTarget = this.notesCanvas;
            }
            else
            {
                UNote noteHit = midiHT.HitTestNote(mousePos);
                if (ToolUsing == EnumTool.Brush)
                {
                    if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                    if (noteHit != null && MidiVM.SelectedNotes.Contains(noteHit))
                        DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(MidiVM.Part, noteHit));
                    else MidiVM.DeselectAll();
                    ((UIElement)sender).CaptureMouse();
                    Mouse.OverrideCursor = Cursors.No;
                }
                else if (ToolUsing == EnumTool.Cursor)
                {
                    if (noteHit != null)
                    {
                        if (!MidiVM.SelectedNotes.Contains(noteHit)) {
                            MidiVM.DeselectAll();
                            MidiVM.SelectNote(noteHit);
                        }
                        bool vibratoenabled = noteHit.Vibrato.IsEnabled;
                        var menu = new ContextMenu();
                        var i0 = new MenuItem() { Header = Lang.LanguageManager.GetLocalized("DeleteNote") };
                        i0.Click += (_o, _e) =>
                        {
                            if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                            DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(MidiVM.Part, noteHit));
                            if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                        };
                        menu.Items.Add(i0);
                        var i1 = new MenuItem()
                        {
                            Header = Lang.LanguageManager.GetLocalized("Vibrato" + (vibratoenabled ? "Dis" : "En"))
                        };
                        i1.Click += (_o, _e) =>
                        {
                            if (vibratoenabled)
                            {
                                noteHit.Vibrato.Disable();
                            }
                            else
                            {
                                noteHit.Vibrato.Enable(true);
                            }
                        };
                        menu.Items.Add(i1);
                        var i2 = new MenuItem() { Header = "Play selection"};
                        i2.Click += (_o, _e) => {
                            Task.Run(() =>
                            {
                                var player = PlaybackManager.GetActiveManager().CreatePlayer();
                                var Sampler = new List<Core.Render.RenderItemSampleProvider>();
                                int c = 0;
                                var sel = new List<UNote>(MidiVM.SelectedNotes).Where(note=>!note.Error).OrderBy(note=>note.PosTick).ToList();
                                var firstN = sel.FirstOrDefault();
                                foreach (var note in sel)
                                {
                                    var cl = note.Clone();
                                    cl.PosTick -= firstN.PosTick;
                                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)Math.Round((double)c / sel.Count), $"Rendering note {cl.Lyric} {c}/{sel.Count}"));
                                    Core.Render.ResamplerInterface.RenderNote(MidiVM.Project, MidiVM.Part, cl).ToList().ForEach(ri => Sampler.Add(new Core.Render.RenderItemSampleProvider(ri)));
                                    ++c;
                                }
                                DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, ""));
                                var ss = new Core.Render.SequencingSampleProvider(Sampler);
                                player.Init(ss);
                                player.PlaybackStopped += (_o1, _e1) =>
                                {
                                    player.Dispose();
                                };
                                player.Play();
                            });
                        };
                        menu.Items.Add(i2);
                        menu.IsOpen = true;
                        menu.PlacementTarget = this.notesCanvas;
                    }
                    else
                    {
                        void SwitchPart(object _o, RoutedEventArgs _e) {
                            if (_o is MenuItem item) {
                                DocManager.Inst.ExecuteCmd(new LoadPartNotification(MidiVM.Project.Parts[(int)item.Tag], MidiVM.Project));
                            }
                        }
                        var tick = MidiVM.CanvasToSnappedTick(mousePos.X);
                        var menu = new ContextMenu();
                        var i0 = new MenuItem() { Header = "Switch Part" };
                        foreach (var item in MidiVM.Project.Parts.OfType<UVoicePart>().Where(part=>part.PosTick <= tick && part.EndTick >= tick).OrderBy(part=>part.TrackNo))
                        {
                            var i1 = new MenuItem() { Header = $"[{item.TrackNo}]{{{item.PartNo}}} {item.Name}", Tag = item.PartNo};
                            i1.Click += SwitchPart;
                            i0.Items.Add(i1);
                        }
                        menu.Items.Add(i0);
                        menu.IsOpen = true;
                        menu.PlacementTarget = this.notesCanvas;
                        MidiVM.DeselectAll();
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("Total notes: " + MidiVM.Part.Notes.Count + " selected: " + MidiVM.SelectedNotes.Count);
        }

        private void notesCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (MidiVM.Part == null || ViewOnly) return;
            Mouse.OverrideCursor = null;
            ((UIElement)sender).ReleaseMouseCapture();
            if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
        }

        private void notesCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                timelineCanvas_MouseWheel(sender, e);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                MidiVM.OffsetX -= MidiVM.ViewWidth * 0.001 * e.Delta;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
            }
            else
            {
                verticalScroll.Value -= verticalScroll.SmallChange * e.Delta / 100;
                verticalScroll.Value = Math.Max(verticalScroll.Minimum, Math.Min(verticalScroll.Maximum, verticalScroll.Value));
            }
        }

#endregion

#region Navigate Drag

        private void navigateDrag_NavDrag(object sender, EventArgs e)
        {
            MidiVM.OffsetX += ((NavDragEventArgs)e).X * MidiVM.SmallChangeX;
            MidiVM.OffsetY += ((NavDragEventArgs)e).Y * MidiVM.SmallChangeY * 0.5;
            MidiVM.MarkUpdate();
        }

#endregion

#region Timeline Canvas

        private void timelineCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            const double zoomSpeed = 0.0012;
            Point mousePos = e.GetPosition((UIElement)sender);
            double zoomCenter;
            if (MidiVM.OffsetX == 0 && mousePos.X < 128) zoomCenter = 0;
            else zoomCenter = (MidiVM.OffsetX + mousePos.X) / MidiVM.QuarterWidth;
            MidiVM.QuarterWidth *= 1 + e.Delta * zoomSpeed;
            MidiVM.OffsetX = Math.Max(0, Math.Min(MidiVM.TotalWidth, zoomCenter * MidiVM.QuarterWidth - mousePos.X));
        }

        private void viewScalerX_ViewScaled(object sender, EventArgs e)
        {
            if (e is ViewScaledEventArgs args)
            {
                double zoomCenter = (MidiVM.OffsetX + MidiVM.ViewWidth / 2) / MidiVM.QuarterWidth;
                MidiVM.QuarterWidth = args.Value;
                MidiVM.OffsetX = Math.Max(0, Math.Min(MidiVM.TotalWidth, zoomCenter * MidiVM.QuarterWidth - MidiVM.ViewWidth / 2));
            }
        }

        private void timelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MidiVM.Part == null) return;
            Point mousePos = e.GetPosition((UIElement)sender);
            int tick = (int)(MidiVM.CanvasToSnappedQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.Project.BeatPerBar);
            DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(Math.Max(0, tick) + (ViewOnly ? 0 : MidiVM.Part.PosTick)));
            ((Canvas)sender).CaptureMouse();
        }

        private void timelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            timelineCanvas_MouseMove_Helper(e.GetPosition(sender as UIElement));
        }

        private void timelineCanvas_MouseMove_Helper(Point mousePos)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed && Mouse.Captured == timelineCanvas)
            {
                int tick = (int)(MidiVM.CanvasToSnappedQuarter(mousePos.X) * MidiVM.Project.Resolution / MidiVM.Project.BeatPerBar);
                if (MidiVM.playPosTick != tick + (ViewOnly ? 0 : MidiVM.Part.PosTick))
                    DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(Math.Max(0, tick) + (ViewOnly ? 0 : MidiVM.Part.PosTick)));
            }
        }

        private void timelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((Canvas)sender).ReleaseMouseCapture();
        }

#endregion

#region Keys Action

        // TODO : keys mouse over, click, release, click and move

        private void keysCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void keysCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
        }

        private void keysCanvas_MouseMove(object sender, MouseEventArgs e)
        {
        }

        private void keysCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            //this.notesVerticalScroll.Value = this.notesVerticalScroll.Value - 0.01 * notesVerticalScroll.SmallChange * e.Delta;
            //ncModel.updateGraphics();
        }

#endregion

        protected override void OnKeyDown(KeyEventArgs e)
        {
            Window_KeyDown(this, e);
            if (!MidiVM.AnyNotesEditing && !LyricsPresetDedicate)
                e.Handled = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.F4)
            {
                this.Close();
            }
            else if (MidiVM.Part != null && !MidiVM.AnyNotesEditing)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control) // Ctrl
                {
                    if (e.Key == Key.A)
                    {
                        MidiVM.SelectAll();
                    }
                    else if (!LyricsPresetDedicate)
                    {
                        if (e.Key == Key.Z)
                        {
                            MidiVM.DeselectAll();
                            DocManager.Inst.Undo();
                        }
                        else if (e.Key == Key.Y)
                        {
                            MidiVM.DeselectAll();
                            DocManager.Inst.Redo();
                        }
                        else if (e.Key == Key.X)
                        {
                            MenuCut_Click(this, new RoutedEventArgs());
                        }
                        else if (e.Key == Key.C)
                        {
                            MenuCopy_Click(this, new RoutedEventArgs());
                        }
                        else if (e.Key == Key.V)
                        {
                            MenuPaste_Click(this, new RoutedEventArgs());
                        }
                        else if (e.Key == Key.U)
                        {
                            MenuMergeNotes_Click(this, new RoutedEventArgs());
                        }
                    }
                }
                else if (Keyboard.Modifiers == 0) // No midifiers
                {
                    if (e.Key == Key.Delete)
                    {
                        MenuDelete_Click(this, new RoutedEventArgs());
                    }
                    else if (e.Key == Key.I)
                    {
                        if (!LyricsPresetDedicate) MidiVM.ShowPitch = !MidiVM.ShowPitch;
                    }
                    else if (e.Key == Key.O)
                    {
                        if (!LyricsPresetDedicate) MidiVM.ShowPhoneme = !MidiVM.ShowPhoneme;
                    }
                    else if (e.Key == Key.P)
                    {
                        if (!LyricsPresetDedicate) MidiVM.Snap = !MidiVM.Snap;
                    }
                    else if (e.Key == Key.Enter)
                    {
                        if (Core.Util.Preferences.Default.EnterToEdit && MidiVM.SelectedNotes.Any())
                        {
                            MidiVM.SelectedNotes.First().IsLyricBoxActive = true;
                            MidiVM.AnyNotesEditing = true;
                            MidiVM.UpdateViewRegion(MidiVM.SelectedNotes.First().EndTick + MidiVM.Part.PosTick);
                            MidiVM.MarkUpdate();
                            MidiVM.notesElement?.MarkUpdate();
                            MidiVM.RedrawIfUpdated();
                        }
                    }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //e.Cancel = true;
            //this.Hide();
        }
        private void expCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!ViewOnly)
            {
                ((Canvas)sender).CaptureMouse();
                if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                Point mousePos = e.GetPosition((UIElement)sender);
                expCanvas_SetExpHelper(mousePos);
            }
        }

        private void expCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!ViewOnly)
            {
                if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                ((Canvas)sender).ReleaseMouseCapture();
            }
        }

        private void expCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!ViewOnly && Mouse.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition((UIElement)sender);
                expCanvas_SetExpHelper(mousePos);
            }
        }

        private void expCanvas_SetExpHelper(Point mousePos)
        {
            if (MidiVM.Part == null) return;
            string _key = MidiVM.visibleExpElement.Key;
            var _expTemplate = DocManager.Inst.Project.ExpressionTable[_key];
            UNote note = midiHT.HitTestNoteX(mousePos.X);
            var pass = MidiVM.SelectedNotes.Count == 0 || MidiVM.SelectedNotes.Contains(note);
            if (!pass) return;
            if (_expTemplate is IntExpression || _expTemplate is FloatExpression)
            {
                int newValue;
                    var ie = _expTemplate as IntExpression;
                    if (Keyboard.Modifiers == ModifierKeys.Alt) newValue = (int)_expTemplate.Data;
                    else newValue = (int)Math.Max(ie.Min, Math.Min(ie.Max, (1 - mousePos.Y / expCanvas.ActualHeight) * (ie.Max - ie.Min) + ie.Min));
                if (note != null)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Alt) newValue = (int)(MidiVM.Part.Expressions[_key] as IntExpression).Data;
                    DocManager.Inst.ExecuteCmd(new SetIntExpCommand(MidiVM.Part, note, MidiVM.visibleExpElement.Key, newValue));
                }
                else
                {
                    DocManager.Inst.ExecuteCmd(new GlobelSetIntExpCommand(MidiVM.Part, MidiVM.visibleExpElement.Key, newValue));
                }
            }
            else if (_expTemplate is FloatExpression) {
                float newValue;
                    var fe = _expTemplate as FloatExpression;
                    if (Keyboard.Modifiers == ModifierKeys.Alt) newValue = (float)fe.Data;
                    else newValue = (float)Math.Max(fe.Min, Math.Min(fe.Max, (1 - mousePos.Y / expCanvas.ActualHeight) * (fe.Max - fe.Min) + fe.Min));
                if (note != null)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Alt) newValue = (float)(MidiVM.Part.Expressions[_key] as FloatExpression).Data;
                    DocManager.Inst.ExecuteCmd(new SetFloatExpCommand(MidiVM.Part, note, MidiVM.visibleExpElement.Key, newValue));
                }
                else
                {
                    DocManager.Inst.ExecuteCmd(new GlobelSetFloatExpCommand(MidiVM.Part, MidiVM.visibleExpElement.Key, newValue));
                }
            }
            else if (_expTemplate is BoolExpression)
            {
                bool newValue;
                if (Keyboard.Modifiers == ModifierKeys.Alt) newValue = (bool)_expTemplate.Data;
                else newValue = !(bool)MidiVM.Part.Expressions[_expTemplate.Name].Data;
                if (note != null)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Alt) newValue = (bool)(MidiVM.Part.Expressions[_key] as BoolExpression).Data;
                    DocManager.Inst.ExecuteCmd(new SetBoolExpCommand(MidiVM.Part, note, MidiVM.visibleExpElement.Key, newValue));
                }
                else
                {
                    DocManager.Inst.ExecuteCmd(new GlobelSetBoolExpCommand(MidiVM.Part, MidiVM.visibleExpElement.Key, newValue));
                }

            }
        }

        private void mainButton_Click(object sender, RoutedEventArgs e)
        {
            DocManager.Inst.ExecuteCmd(new ShowPitchExpNotification());
        }

        private void horizontalScroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MidiVM.HorizontalPropertiesChanged();
            MidiVM.shadowExpElement?.MarkUpdate();
            MidiVM.visibleExpElement?.MarkUpdate();
            MidiVM.MarkUpdate();
            MidiVM.RedrawIfUpdated();
        }

        private void MenuUndo_Click(object sender, RoutedEventArgs e) { DocManager.Inst.Undo(); }
        private void MenuRedo_Click(object sender, RoutedEventArgs e) { DocManager.Inst.Redo(); }
        private void MenuCut_Click(object sender, RoutedEventArgs e)
        {
            MidiVM.CopyNotes();
            var pre = new List<UNote>(MidiVM.SelectedNotes);
            if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
            foreach (var item in MidiVM.SelectedNotes)
            {
                DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(MidiVM.Part, item), true);
            }
            if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
            MidiVM.DeselectAll();
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            MidiVM.CopyNotes();
        }

        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            int basedelta = int.MaxValue;
            foreach (var note in MidiVM.ClippedNotes)
            {
                basedelta = Math.Min(basedelta, note.PosTick);
            }
            if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
            foreach (var note in MidiVM.ClippedNotes)
            {
                var copied = note.Clone();
                copied.PosTick = DocManager.Inst.playPosTick - MidiVM.Part.PosTick + note.PosTick - basedelta;
                DocManager.Inst.ExecuteCmd(new AddNoteCommand(MidiVM.Part, copied));
            }
            if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
            MidiVM.MarkUpdate();
        }
        
        private void MenuMergeNotes_Click(object sender, RoutedEventArgs e)
        {
            if (MidiVM.SelectedNotes.Count > 1)
            {
                var sn = new List<UNote>(MidiVM.SelectedNotes);
                var nl = Core.Util.Utils.MakeCompletePhonemes(sn.Select(note => new string(note.Lyric.SkipWhile((c, index)=>c == '-' && index == 0).ToArray())).ToArray());
                KeyValuePair<string, UDictionaryNote>? v = DocManager.Inst.Project.Tracks[MidiVM.Part.TrackNo].Singer?.PresetLyricsMap.FirstOrDefault(pair => pair.Value.MatchNotes(sn));
                if (v.HasValue && !string.IsNullOrEmpty(v.GetValueOrDefault().Key)) {
                    nl = v.Value.Key;
                }
                var dur = sn.Sum(note => note.DurTick);
                var notem = sn.OrderBy(note => note.PosTick).First();
                MidiVM.DeselectAll();
                if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(MidiVM.Part, sn.Skip(1).ToList()), true);
                DocManager.Inst.ExecuteCmd(new ResizeNoteCommand(MidiVM.Part, notem, dur - notem.DurTick), true);
                DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(MidiVM.Part, notem, nl), true);
                foreach (var note in sn.Skip(1))
                {
                    foreach (var item in note.PitchBend.Points)
                    {
                        var pre = item.Clone();
                        pre.X += DocManager.Inst.Project.TickToMillisecond(note.PosTick - notem.PosTick);
                        pre.Y += (note.NoteNum - notem.NoteNum) * 10;
                        var index = notem.PitchBend.Points.FindIndex(pt => pt.X > pre.X);
                        DocManager.Inst.ExecuteCmd(new AddPitchPointCommand(notem, pre, index != -1 ? index : notem.PitchBend.Points.Count));
                    }
                }
                if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
                MidiVM.SelectNote(notem);
                MidiVM.MarkUpdate();
            }
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if (MidiVM.SelectedNotes.Count > 0)
            {
                if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(MidiVM.Part, MidiVM.SelectedNotes));
                if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
            }
        }

        private void MenuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            MidiVM.SelectAll();
        }

        private void MenuPlaybackCtrl_Click(object sender, RoutedEventArgs e)
        {
            switch ((sender as MenuItem)?.Tag as string)
            {
                case "Play":
                    PlaybackManager.GetActiveManager().Play(MidiVM.Project);
                    break;
                case "Pause":
                    PlaybackManager.GetActiveManager().PausePlayback();
                    break;
                case "Stop":
                    PlaybackManager.GetActiveManager().StopPlayback();
                    break;
                default:
                    break;
            }
        }

        private void MenuSeek_Click(object sender, RoutedEventArgs e)
        {
            switch ((sender as MenuItem)?.Tag as string)
            {
                case "Start":
                    DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(0, MidiVM.Project));
                    break;
                case "FirstNote":
                    UNote uNote = MidiVM.Project.Parts.OfType<UVoicePart>().OrderBy(part => part.PosTick).SelectMany(part => part.Notes).FirstOrDefault();
                    DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification((uNote?.PosTick ?? 0) + MidiVM.Project.Parts.Find(part=>part.PartNo == (uNote?.PartNo ?? -1))?.PosTick ?? 0, MidiVM.Project));
                    break;
                case "LastNote":
                    UNote uNote1 = MidiVM.Project.Parts.OfType<UVoicePart>().OrderBy(part => part.PosTick).SelectMany(part => part.Notes).LastOrDefault();
                    DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification((uNote1?.EndTick ?? 0) + MidiVM.Project.Parts.Find(part => part.PartNo == (uNote1?.PartNo ?? -1))?.PosTick ?? 0, MidiVM.Project));
                    break;
                case "End":
                    DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(MidiVM.Project.Parts.OrderBy(part => part.EndTick).LastOrDefault()?.EndTick ?? 0, MidiVM.Project));
                    break;
                default:
                    break;
            }
        }

        private void MenuConvertStyle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi) {
                OpenUtau.Core.Util.SamplingStyleHelper.Style style;
                switch (mi.Tag)
                {
                    case "CV":
                        style = Core.Util.SamplingStyleHelper.Style.CV;
                        break;
                    case "VCV":
                        style = Core.Util.SamplingStyleHelper.Style.VCV;
                        break;
                    case "CVVC":
                        style = Core.Util.SamplingStyleHelper.Style.CVVC;
                        break;
                    default:
                        style = Core.Util.SamplingStyleHelper.Style.Others;
                        break;
                }
                if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                SortedSet<UNote> notesBak = new SortedSet<UNote>(MidiVM.Part.Notes);
                var selNote = new List<UNote>(MidiVM.SelectedNotes);
                if (selNote.Count == 0) selNote.AddRange(notesBak);
                foreach (var note in selNote)
                {
                    UNote former = notesBak.FirstOrDefault(note1 => note1 != note && Math.Abs(note.PosTick - note1.EndTick) < DocManager.Inst.Project.Resolution / 64);
                    UNote lator = notesBak.FirstOrDefault(note1 => note1 != note && Math.Abs(note1.PosTick - note.EndTick) < DocManager.Inst.Project.Resolution / 64);
                    var mod = Core.Util.SamplingStyleHelper.GetCorrespondingPhoneme(note.Lyric,former,lator, style);
                    if (style == Core.Util.SamplingStyleHelper.Style.CVVC)
                    {
                        var pts = mod.Split('\t');
                        var note1 = note.Clone();
                        note1.PosTick = note.PosTick;
                        note1.DurTick = (int)Math.Round(note.DurTick * 0.75);
                        note1.Vibrato = note.Vibrato.Split(note1, note1.PosTick) as VibratoExpression;
                        note1.Lyric = pts[0];
                        var note2 = note.Clone();
                        if (pts.Length > 1)
                        {
                            note2.PosTick = note1.EndTick;
                            note2.DurTick = (int)Math.Round(note.DurTick * 0.25);
                            note2.Vibrato = note.Vibrato.Split(note2, note2.PosTick) as VibratoExpression;
                            note2.Lyric = pts[1];
                        }
                        DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(MidiVM.Part, note));
                        DocManager.Inst.ExecuteCmd(new AddNoteCommand(MidiVM.Part, note1));
                        if (pts.Length > 1)
                            DocManager.Inst.ExecuteCmd(new AddNoteCommand(MidiVM.Part, note2));
                    }
                    else
                    {
                        DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(MidiVM.Part, note, mod));
                    }
                }
                if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
            }
        }

        private void MenuConvertRH_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                if (!LyricsPresetDedicate) DocManager.Inst.StartUndoGroup();
                foreach (var item in new SortedSet<UNote>(MidiVM.Part.Notes))
                {
                    if (!Core.Util.HiraganaRomajiHelper.IsSupported(item.Lyric)) continue;
                    string mod;
                    switch (mi.Tag)
                    {
                        case "Romaji":
                            mod = Core.Util.HiraganaRomajiHelper.ToRomaji(item.Lyric);
                            break;
                        case "Hiragana":
                            mod = Core.Util.HiraganaRomajiHelper.ToHiragana(item.Lyric);
                            break;
                        default:
                            return;
                    }
                    DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(MidiVM.Part, item, mod));
                }
                if (!LyricsPresetDedicate) DocManager.Inst.EndUndoGroup();
            }
        }

        private void convertToggle_Click(object sender, RoutedEventArgs e)
        {
            MidiVM.Part.ConvertStyle = convertToggle.IsChecked;
        }
    }
}
