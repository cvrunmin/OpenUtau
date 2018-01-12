using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

namespace OpenUtau.Core.Render
{
    class CachedSoundSampleProvider :  WaveStream, ISampleProvider
    {
        private readonly CachedSound cachedSound;
        private long position;

        public override long Position { set => position = value;
            get => position;
        }

        public CachedSoundSampleProvider(CachedSound cachedSound)
        {
            this.cachedSound = cachedSound;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var waveBuffer = new WaveBuffer(buffer);
            int samplesRequired = count / 4;
            int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
            return samplesRead * 4;
            /*var availableSamples = Length - Position;
            var samplesToCopy = Math.Min(availableSamples, count);
            Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);
            position += samplesToCopy;
            return (int)samplesToCopy;*/
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var availableSamples = cachedSound.AudioData.Length - position;
            var samplesToCopy = Math.Min(availableSamples, count);
            //Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy); //avoid using Array.Copy with WaveBuffer
            for (int i = 0; i < samplesToCopy; i++)
            {
                buffer[offset + i] = cachedSound.AudioData[position + i];
            }
            position += samplesToCopy;
            return (int)samplesToCopy;
        }

        public override WaveFormat WaveFormat => cachedSound.WaveFormat;

        public override long Length => cachedSound.AudioData.LongLength;

        public CachedSoundSampleProvider Clone() {
            return new CachedSoundSampleProvider(cachedSound.Clone());
        }

    }
}
