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
    class RenderItemSampleProvider : ISampleProvider
    {
        private int firstSample;
        private int lastSample;
        private ISampleProvider signalChain;

        public RenderItemSampleProvider(RenderItem renderItem)
        {
            this.RenderItem = renderItem;
            var cachedSampleProvider = new CachedSoundSampleProvider(RenderItem.Sound);
            try
            {
                var offsetSampleProvider = new UOffsetSampleProvider(new EnvelopeSampleProvider(cachedSampleProvider, RenderItem.Envelope, RenderItem.SkipOver))
                {
                    DelayBySamples = (int)(RenderItem.PosMs * (cachedSampleProvider.WaveFormat?.SampleRate).GetValueOrDefault() / 1000),
                    TakeSamples = (int)(RenderItem.DurMs * (cachedSampleProvider.WaveFormat?.SampleRate).GetValueOrDefault() / 1000),
                    SkipOverSamples = (int)(RenderItem.SkipOver * (cachedSampleProvider.WaveFormat?.SampleRate).GetValueOrDefault() / 1000)
                };
                this.signalChain = offsetSampleProvider;
                this.firstSample = offsetSampleProvider.DelayBySamples + offsetSampleProvider.SkipOverSamples;
                this.lastSample = this.firstSample + offsetSampleProvider.TakeSamples;
            }
            catch (Exception)
            {
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
            return (signalChain?.Read(buffer, offset, count)).GetValueOrDefault();
        }

        public WaveFormat WaveFormat => signalChain?.WaveFormat;
    }
}
