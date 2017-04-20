using OpenUtau.Core;
using OpenUtau.Core.Lib.WaveFormRenderer;
using OpenUtau.Core.USTx;
using OpenUtau.UI.Models;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenUtau.UI.Dialogs
{
    /// <summary>
    /// OtoEditDialog.xaml 的互動邏輯
    /// </summary>
    public partial class OtoEditDialog : Window, ICmdSubscriber
    {
        public OtoEditDialog()
        {
            InitializeComponent();
        }
        public UOto EditingOto { get; set; }
        OtoViewModel model;
        public OtoEditDialog(USinger singer, UOto oto) : this()
        {
            SavedSinger = singer;
            this.EditingOto = oto;
            this.DataContext = EditingOto;
            Resources["oto"] = EditingOto;
            model = new OtoViewModel() { otoCanvas = waveformCanvas, owner = this };
            waveformCanvas.Children.Add(model.element);
            this.Subscribe(DocManager.Inst);
            model.Subscribe(DocManager.Inst);
            if (string.IsNullOrWhiteSpace(EditingOto.Alias))
            {
                var i = EditingOto.File.LastIndexOf('\\');
                EditingOto = EditingOto.SetAlias(EditingOto.File.Substring(i > -1 ? i : 0).Replace(".wav", ""));
                ForceUpdateTextBox();
            }
            CreateWaveForm(singer, oto);
        }
        private USinger SavedSinger;
        private void CreateWaveForm(USinger singer, UOto oto)
        {
            waveformCanvas.Background = CreateWaveFormAsync(singer, oto);
            RenderOptions.SetBitmapScalingMode(waveformCanvas, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(waveformCanvas, EdgeMode.Aliased);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        [DllImport("gdi32.dll")]
        private static extern void DeleteObject(IntPtr hObject);

        private ImageBrush CreateWaveFormAsync(USinger singer, UOto oto)
        {
            WriteableBitmap img = new NewWaveFormRenderer().Render(System.IO.Path.Combine(singer.Path, oto.File), new WaveFormRendererLib.StandardWaveFormRendererSettings() { TopHeight = 100, BottomHeight = 100, Width = (int)(800 * (model.element.ScaleX != 0 ? model.element.ScaleX : 1)), TopPeakPen = new System.Drawing.Pen(System.Drawing.Color.Blue), BottomPeakPen = new System.Drawing.Pen(System.Drawing.Color.Blue), DecibelScale = true, SpacerPixels = 1, TopSpacerPen = new System.Drawing.Pen(System.Drawing.Color.Cyan), BottomSpacerPen = new System.Drawing.Pen(System.Drawing.Color.Cyan) });
            return new ImageBrush(img);
        }

        private void waveformCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ((Canvas)sender).CaptureMouse();
            DocManager.Inst.StartUndoGroup();
            System.Windows.Point mousePos = e.GetPosition((UIElement)sender);
            waveformCanvas_GetPosHelper(mousePos, true);
            waveformCanvas_SetValHelper(mousePos);
        }

        private void waveformCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point mousePos = e.GetPosition((UIElement)sender);
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                waveformCanvas_GetPosHelper(mousePos, true);
                waveformCanvas_SetValHelper(mousePos);
            }
            else
            {
                waveformCanvas_GetPosHelper(mousePos, false);
            }
        }

        private void waveformCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DocManager.Inst.EndUndoGroup();
            ((Canvas)sender).ReleaseMouseCapture();
            Status = EditStatus.NotCaptured;
        }

        internal double ActualOffsetPosX    =>       EditingOto.Offset    / EditingOto.Duration  * waveformCanvas.ActualWidth;
        internal double ActualConsonantPosX => (     EditingOto.Consonant / EditingOto.Duration  * waveformCanvas.ActualWidth) + ActualOffsetPosX;
        internal double ActualOverlapPosX   => (     EditingOto.Overlap   / EditingOto.Duration  * waveformCanvas.ActualWidth) + ActualOffsetPosX;
        internal double ActualPreutterPosX  => (     EditingOto.Preutter  / EditingOto.Duration  * waveformCanvas.ActualWidth) + ActualOffsetPosX;
        internal double ActualCutoffPosX    => (1d - EditingOto.Cutoff    / EditingOto.Duration) * waveformCanvas.ActualWidth;
        internal EditStatus Status = EditStatus.NotCaptured;

        private void waveformCanvas_SetValHelper(System.Windows.Point pt)
        {
            if (Status != EditStatus.NotCaptured)
            {
                double newVal;
                string key;
                switch (Status)
                {
                    case EditStatus.Offset:
                        newVal = pt.X / waveformCanvas.ActualWidth * EditingOto.Duration;
                        key = "Offset";
                        break;
                    case EditStatus.Cutoff:
                        newVal = (1 - pt.X / waveformCanvas.ActualWidth) * EditingOto.Duration;
                        key = "Cutoff";
                        break;
                    case EditStatus.Consonant:
                        newVal = (pt.X - ActualOffsetPosX) / waveformCanvas.ActualWidth * EditingOto.Duration;
                        key = "Consonant";
                        break;
                    case EditStatus.Pretturance:
                        newVal = (pt.X - ActualOffsetPosX) / waveformCanvas.ActualWidth * EditingOto.Duration;
                        key = "Preutter";
                        break;
                    case EditStatus.Overlap:
                        newVal = (pt.X - ActualOffsetPosX) / waveformCanvas.ActualWidth * EditingOto.Duration;
                        key = "Overlap";
                        break;
                    default:
                        return;
                }
                DocManager.Inst.ExecuteCmd(new UpdateOtoCommand(this, key, Math.Round(newVal)));
            }
        }

        private void waveformCanvas_GetPosHelper(System.Windows.Point pt, bool pressed)
        {            
            if (Math.Abs(ActualPreutterPosX - pt.X) < 5 && waveformCanvas.ActualHeight * .3 <= pt.Y && pt.Y <= waveformCanvas.ActualHeight * .7)
            {
                txtblkStatus.Text = Lang.LanguageManager.GetLocalized("Pretturance")+": " + EditingOto.Preutter + "ms";
                Mouse.OverrideCursor = Cursors.Cross;
                if(pressed) Status = EditStatus.Pretturance;
            }
            else if (Math.Abs(ActualOverlapPosX - pt.X) < 5 && waveformCanvas.ActualHeight * .2 <= pt.Y && pt.Y <= waveformCanvas.ActualHeight * .8)
            {
                txtblkStatus.Text = Lang.LanguageManager.GetLocalized("Overlap") + ": " + EditingOto.Overlap + "ms";
                Mouse.OverrideCursor = Cursors.Cross;
                if (pressed) Status = EditStatus.Overlap;
            }
            else if (Math.Abs(ActualConsonantPosX - pt.X) < 5)
            {
                txtblkStatus.Text = Lang.LanguageManager.GetLocalized("Consonant") + ": " + EditingOto.Consonant + "ms";
                Mouse.OverrideCursor = Cursors.Cross;
                if (pressed) Status = EditStatus.Consonant;
            }
            else if (Math.Abs(ActualOffsetPosX - pt.X) < 5)
            {
               txtblkStatus.Text = Lang.LanguageManager.GetLocalized("Offset2") + ": " + EditingOto.Offset + "ms";
                Mouse.OverrideCursor = Cursors.Cross;
                if (pressed) Status = EditStatus.Offset;
            }
            else if (Math.Abs(ActualCutoffPosX - pt.X) < 5)
            {
                txtblkStatus.Text = Lang.LanguageManager.GetLocalized("Cutoff2") + ": " + EditingOto.Cutoff + "ms";
                Mouse.OverrideCursor = Cursors.Cross;
                if (pressed) Status = EditStatus.Cutoff;
            }
            else
            {
                txtblkStatus.Text = "";
                Mouse.OverrideCursor = null;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            model.MarkUpdate();
            model.element.MarkUpdate();
            model.RedrawIfUpdated();
        }

        private void waveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            model.MarkUpdate();
            model.element.MarkUpdate();
            model.RedrawIfUpdated();
        }

        private void waveformCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            txtblkStatus.Text = "";
            Mouse.OverrideCursor = null;
        }

        public void Subscribe(ICmdPublisher publisher)
        {
            if (publisher != null) publisher.Subscribe(this);
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is UpdateOtoCommand)
            {
                DataContext = EditingOto;
                var _cmd = cmd as UpdateOtoCommand;
                OnPropertyChanged(_cmd.Key);
                ForceUpdateTextBox();
            }
        }

        private void ForceUpdateTextBox()
        {
            foreach (var item in gridProperties.Children)
            {
                if (item is TextBox textbox)
                {
                    textbox.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                }
            }
        }

        private void butCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void butSave_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void PART_ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            if (model.element.ScaleX < 16)
            {
                model.element.ScaleX *= 2;
                canvasGrid.Width *= 2;
                CreateWaveForm(SavedSinger, EditingOto);
            }
        }

        private void PART_ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (model.element.ScaleX > 0.125)
            {
                model.element.ScaleX *= 0.5;
                canvasGrid.Width *= 0.5;
                CreateWaveForm(SavedSinger, EditingOto);
            }
        }
    }

    enum EditStatus {
        NotCaptured, Offset, Consonant, Overlap, Pretturance, Cutoff
    }
}
