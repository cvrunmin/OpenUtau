using OpenUtau.Core.USTx;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenUtau.UI.Dialogs
{
    /// <summary>
    /// OtoEditDialog.xaml 的互動邏輯
    /// </summary>
    public partial class OtoEditDialog : Window
    {
        public OtoEditDialog()
        {
            InitializeComponent();
        }

        public OtoEditDialog(USinger singer, UOto oto) : this() {
            waveformCanvas.Background = CreateWaveForm(singer, oto);
            
        }

        [DllImport("gdi32.dll")]
        private static extern void DeleteObject(IntPtr hObject);

        private ImageBrush CreateWaveForm(USinger singer, UOto oto) {
            Image img = new WaveFormRendererLib.WaveFormRenderer().Render(System.IO.Path.Combine(singer.Path, oto.File), new WaveFormRendererLib.StandardWaveFormRendererSettings() { TopHeight = 100, BottomHeight = 100, TopPeakPen = new System.Drawing.Pen(System.Drawing.Color.Blue), BottomPeakPen = new System.Drawing.Pen(System.Drawing.Color.Blue), DecibelScale = true });
            using (var bmp = new Bitmap(img))
            {
                IntPtr hBitmap = bmp.GetHbitmap();

                try
                {
                    return new ImageBrush(System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()));
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }
    }
}
