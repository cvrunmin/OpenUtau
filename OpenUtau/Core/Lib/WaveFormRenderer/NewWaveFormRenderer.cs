using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WaveFormRendererLib;
using static System.Windows.Media.Imaging.WriteableBitmapExtensions;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.Lib.WaveFormRenderer
{
    public class NewWaveFormRenderer
    {
        public /*ValueTask<*/WriteableBitmap/*>*/ Render(string selectedFile, WaveFormRendererSettings settings)
        {
            return Render(selectedFile, new MaxPeakProvider(), settings);
        }

        public /*ValueTask<*/WriteableBitmap/*>*/ Render(string selectedFile, IPeakProvider peakProvider, WaveFormRendererSettings settings)
        {
            /*return new ValueTask<WriteableBitmap>(new TaskFactory().StartNew(() =>
            {*/
                using (var reader = new AudioFileReader(selectedFile))
                {
                    int bytesPerSample = (reader.WaveFormat.BitsPerSample / 8);
                    var samples = reader.Length / (bytesPerSample);
                    var samplesPerPixel = (int)(samples / settings.Width);
                    var stepSize = settings.PixelsPerPeak + settings.SpacerPixels;
                    peakProvider.Init(reader, samplesPerPixel * stepSize);
                    return Render(peakProvider, settings);
                }
            /*}));*/
        }

        private static WriteableBitmap Render(IPeakProvider peakProvider, WaveFormRendererSettings settings)
        {
            if (settings.DecibelScale)
                peakProvider = new DecibelPeakProvider(peakProvider, 48);

            var b = BitmapFactory.New((int)(settings.Width * 1.5), settings.TopHeight + settings.BottomHeight);
            if (settings.BackgroundColor == System.Drawing.Color.Transparent)
            {
                b.Clear(Colors.Transparent);
            }
            using (var g = b.GetBitmapContext())
            {
                
                b.FillRectangle(0, 0, b.PixelWidth, b.PixelHeight, settings.BackgroundColor.ToMediaColor());
                var midPoint = settings.TopHeight;

                int x = 0;
                var currentPeak = peakProvider.GetNextPeak();
                while (x < (int)(settings.Width * 1.5))
                {
                    var nextPeak = peakProvider.GetNextPeak();
                    
                        var lineHeight = settings.TopHeight * (double.IsNaN(currentPeak.Max) ? 0 : currentPeak.Max);
                        var nextLineH = settings.BottomHeight * (double.IsNaN(currentPeak.Min) ? 0 : currentPeak.Min);

                        b.DrawLine(x, (int)Math.Round(midPoint - lineHeight), x + 1, (int)Math.Round(midPoint - nextLineH), settings.TopPeakPen.Color.ToMediaColor());
                        lineHeight = settings.BottomHeight * (double.IsNaN(currentPeak.Min) ? 0 : currentPeak.Min);
                        nextLineH = settings.TopHeight * (double.IsNaN(nextPeak.Max) ? 0 : nextPeak.Max);
                        b.DrawLine(x + 1, (int)Math.Round(midPoint - lineHeight), x + 2, (int)Math.Round(midPoint - nextLineH), settings.BottomPeakPen.Color.ToMediaColor());
                        x+=3;

                    currentPeak = nextPeak;
                }
            }
            return b;
        }
    }
}
