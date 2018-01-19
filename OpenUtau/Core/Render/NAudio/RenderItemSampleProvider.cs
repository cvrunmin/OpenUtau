using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core.Render.NAudio;

namespace OpenUtau.Core.Render
{
    class RenderItemSampleProvider : WaveStream, ISampleProvider
    {
        private int firstSample;
        private int lastSample;
        //private ISampleProvider signalChain;
        private WaveStream signalChain;
        private CachedSoundSampleProvider cachedSound;

        public RenderItemSampleProvider(RenderItem renderItem)
        {
            this.RenderItem = renderItem;
            if (this.RenderItem.Error)
            {

            }
            else
            {
                cachedSound = new CachedSoundSampleProvider(RenderItem.Sound);
                try
                {
                    var offsetSampleProvider = new UWaveOffsetStream(new EnvelopeSampleProvider(cachedSound, RenderItem.Envelope, RenderItem.SkipOver).ToWaveStream())
                    {
                        //DelayBySamples = (int)(RenderItem.PosMs * (cachedSound.WaveFormat?.SampleRate).GetValueOrDefault() / 1000),
                        //TakeSamples = (int)(RenderItem.DurMs * (cachedSound.WaveFormat?.SampleRate).GetValueOrDefault() / 1000),
                        //SkipOverSamples = (int)(RenderItem.SkipOver * (cachedSound.WaveFormat?.SampleRate).GetValueOrDefault() / 1000)
                        StartTime = TimeSpan.FromMilliseconds(RenderItem.PosMs),
                        SourceOffset = TimeSpan.FromMilliseconds(RenderItem.SkipOver),
                        SourceLength = TimeSpan.FromMilliseconds(RenderItem.DurMs)
                    };
                    this.signalChain = offsetSampleProvider;
                    //this.firstSample = offsetSampleProvider.DelayBySamples + offsetSampleProvider.SkipOverSamples;
                    this.firstSample = (int) (offsetSampleProvider.StartTime.TotalSeconds * offsetSampleProvider.WaveFormat.SampleRate + offsetSampleProvider.SourceOffset.TotalSeconds * offsetSampleProvider.WaveFormat.SampleRate);
                    //this.lastSample = this.firstSample + offsetSampleProvider.TakeSamples;
                    this.lastSample = this.firstSample + (int)(offsetSampleProvider.SourceLength.TotalSeconds * offsetSampleProvider.WaveFormat.SampleRate);
                }
                catch (Exception e)
                {
# if DEBUG
                    throw e;
#endif
                }
            }

        }

        public RenderItemSampleProvider Clone() {
            return new RenderItemSampleProvider(RenderItem);
        }

        /// <summary>
        /// Position of first sample
        /// </summary>
        public int FirstSample => firstSample;

        /// <summary>
        /// Position of last sample (not included)
        /// </summary>
        public int LastSample => lastSample;

        public RenderItem RenderItem { set; get; }

        public int Read(float[] buffer, int offset, int count)
        {
            if (signalChain == null) return 0;
            var waveBuffer = new WaveBuffer((offset + count) * 4);
            var bytesRead = signalChain.Read(waveBuffer.ByteBuffer, offset * 4, count * 4);
            //Array.Copy(waveBuffer.FloatBuffer, buffer, waveBuffer.FloatBufferCount);
            for (int i = 0; i < bytesRead / 4; i++)
            {
                buffer[offset + i] = waveBuffer.FloatBuffer[offset + i];
            }
            return bytesRead / 4;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return (signalChain?.Read(buffer, offset, count)).GetValueOrDefault();
        }

        public override WaveFormat WaveFormat => signalChain?.WaveFormat;

        public override long Length => (signalChain?.Length).GetValueOrDefault();

        public override long Position { get => (signalChain?.Position).GetValueOrDefault(); set { if (signalChain != null) signalChain.Position = value; } }
    }

    public static class ProviderHelper {
        public static WaveStream ToWaveStream(this ISampleProvider provider) {
            return provider.ToWaveProvider().ToWaveStream();
        }

        public static WaveStream ToWaveStream(this IWaveProvider provider) {
            var str = new System.IO.MemoryStream();
            var buffer = new byte[provider.WaveFormat.AverageBytesPerSecond];
            while (true)
            {
                if (2147483591 - str.Position < buffer.Length)
                {
                    buffer = new byte[2147483591 - str.Position - 1];
                    var bytesRead1 = provider.Read(buffer, 0, buffer.Length);
                    if (bytesRead1 == 0)
                    {
                        // end of source provider
                        str.Flush();
                        break;
                    }
                    str.Write(buffer, 0, bytesRead1);
                    break;
                }
                var bytesRead = provider.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // end of source provider
                    str.Flush();
                    break;
                }
                str.Write(buffer, 0, bytesRead);
            }
            str.Position = 0;
            return new RawSourceWaveStream(str, provider.WaveFormat);
        }
    }
}
