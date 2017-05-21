using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenUtau.Core;
using OpenUtau.Core.USTx;
using OpenUtau.UI.Controls;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using OpenUtau.UI.Dialogs;

namespace OpenUtau.UI.Models
{
    class OtoViewModel : INotifyPropertyChanged, ICmdSubscriber
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Canvas otoCanvas;
        
        public OtoViewElement element;

        public OtoEditDialog owner;
        public OtoViewModel() {
            element = new OtoViewElement() { model = this, ScaleX = 1d };
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool _updated = false;
        public void MarkUpdate() { _updated = true; }

        public void RedrawIfUpdated() {
            if (_updated)
            {
                element.VisualHeight = otoCanvas.ActualHeight;
                _updated = false;
            }
            element.RedrawIfUpdated();
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is UpdateOtoCommand) {
                element.MarkUpdate();
            }
            RedrawIfUpdated();
        }

        public void Subscribe(ICmdPublisher publisher)
        {
            if (publisher != null) publisher.Subscribe(this);
        }
    }

    class OtoViewElement : FrameworkElement {
        public OtoViewModel model;
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
        public double ScaleX {
            set {
                if (_scaleX != value && value >= 0.125 && value <= 16) {
                    _scaleX = value;
                    MarkUpdate();
                }
            }
            get => _scaleX;
        }
        Brush offsetBrush;
        Brush consonantBrush;
        Brush cutoffBrush;
        Brush preutteranceBrush;
        Brush overlapBrush;
        protected bool _updated = false;
        public void MarkUpdate() { _updated = true; }

        public OtoViewElement()
        {
            tTrans = new TranslateTransform();
            VisualTransform = tTrans;
            visual = new DrawingVisual();
            MarkUpdate();
            this.AddVisualChild(visual);
            offsetBrush = new SolidColorBrush(ThemeManager.GetColorVariationAlpha(Colors.DarkBlue, 0x7f));
            consonantBrush = new SolidColorBrush(ThemeManager.GetColorVariationAlpha(Colors.BlueViolet, 0x7f));
            preutteranceBrush = new SolidColorBrush(Colors.Red);
            overlapBrush = new SolidColorBrush(Colors.Green);
            cutoffBrush = new SolidColorBrush(ThemeManager.GetColorVariationAlpha(Colors.DarkBlue, 0x7f));
            offsetBrush.Freeze();
            consonantBrush.Freeze();
            preutteranceBrush.Freeze();
            overlapBrush.Freeze();
            cutoffBrush.Freeze();
        }

        public virtual void RedrawIfUpdated() {
            if (!_updated) return;
            Dispatcher.Invoke(() => { 
            DrawingContext cxt = visual.RenderOpen();
            cxt.DrawRectangle(offsetBrush, null, new Rect(new Point(0, 0), new Point(model.owner.ActualOffsetPosX, model.otoCanvas.ActualHeight)));
            cxt.DrawRectangle(consonantBrush, null, new Rect(new Point(model.owner.ActualOffsetPosX, 0), new Point(model.owner.ActualConsonantPosX, model.otoCanvas.ActualHeight)));
            cxt.DrawRectangle(cutoffBrush, null, new Rect(new Point(model.owner.ActualCutoffPosX, 0), new Point(model.otoCanvas.ActualWidth, model.otoCanvas.ActualHeight)));
            cxt.DrawLine(new Pen(overlapBrush, 2), new Point(model.owner.ActualOverlapPosX, model.otoCanvas.ActualHeight * .2), new Point(model.owner.ActualOverlapPosX, model.otoCanvas.ActualHeight * .8));
            cxt.DrawLine(new Pen(preutteranceBrush, 2), new Point(model.owner.ActualPreutterPosX, model.otoCanvas.ActualHeight * .3), new Point(model.owner.ActualPreutterPosX, model.otoCanvas.ActualHeight * .7));
            cxt.Close();
            });
            _updated = false;
        }
    }

    class UpdateOtoCommand : UCommand
    {
        public string Key;
        public object NewValue;
        public object OldValue;
        public OtoEditDialog dialog;
        public UpdateOtoCommand(OtoEditDialog dialog, string key, object value) {
            this.dialog = dialog;
            this.Key = key;
            NewValue = value;
            var oto = this.dialog.EditingOto;
            switch (key)
            {
                case "Alias":
                    OldValue = oto.Alias;
                    break;
                case "Offset":
                    OldValue = oto.Offset;
                    break;
                case "Cutoff":
                    OldValue = oto.Cutoff;
                    break;
                case "Consonant":
                    OldValue = oto.Consonant;
                    break;
                case "Preutter":
                    OldValue = oto.Preutter;
                    break;
                case "Overlap":
                    OldValue = oto.Overlap;
                    break;
                default:
                    break;
            }
        }
        public override void Execute()
        {
            switch (Key)
            {
                case "Alias":
                    dialog.EditingOto = dialog.EditingOto.SetAlias((string)NewValue);
                    break;
                case "Offset":
                    dialog.EditingOto = dialog.EditingOto.SetOffset((double)NewValue);
                    break;
                case "Cutoff":
                    dialog.EditingOto = dialog.EditingOto.SetCutoff((double)NewValue);
                    break;
                case "Consonant":
                    dialog.EditingOto = dialog.EditingOto.SetConsonant((double)NewValue);
                    break;
                case "Preutter":
                    dialog.EditingOto = dialog.EditingOto.SetPreutter((double)NewValue);
                    break;
                case "Overlap":
                    dialog.EditingOto = dialog.EditingOto.SetOverlap((double)NewValue);
                    break;
                default:
                    break;
            }
        }

        public override string ToString()
        {
            return "Update the " + Key + " of " + dialog.EditingOto.Alias + "(" + dialog.EditingOto.File + ") to " + NewValue;
        }

        public override void Unexecute()
        {
            switch (Key)
            {
                case "Alias":
                    dialog.EditingOto = dialog.EditingOto.SetAlias((string)OldValue);
                    break;
                case "Offset":
                    dialog.EditingOto = dialog.EditingOto.SetOffset((double)OldValue);
                    break;
                case "Cutoff":
                    dialog.EditingOto = dialog.EditingOto.SetCutoff((double)OldValue);
                    break;
                case "Consonant":
                    dialog.EditingOto = dialog.EditingOto.SetConsonant((double)OldValue);
                    break;
                case "Preutter":
                    dialog.EditingOto = dialog.EditingOto.SetPreutter((double)OldValue);
                    break;
                case "Overlap":
                    dialog.EditingOto = dialog.EditingOto.SetOverlap((double)OldValue);
                    break;
                default:
                    break;
            }
        }
    }
}
