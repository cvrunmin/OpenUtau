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

namespace OpenUtau.UI.Models
{
    class OtoViewModel : INotifyPropertyChanged, ICmdSubscriber
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Canvas otoCanvas;

        public UOto oto;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            throw new NotImplementedException();
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
        public double ScaleX { set { if (_scaleX != value) { _scaleX = value; MarkUpdate(); } } get { return _scaleX; } }
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
        }

        public virtual void RedrawIfUpdated() {
            if (!_updated) return;
            DrawingContext cxt = visual.RenderOpen();

            cxt.Close();
            _updated = false;
        }
    }
}
