using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

namespace OpenUtau.Core.Render
{
    public class CachedSound
    {
        public int MemSize { get { return AudioData.Length * sizeof(float); } }
        public float[] AudioData { get; private set; }
        public WaveFormat WaveFormat { get; private set; }

        private CachedSound() { }

        public CachedSound(string audioFileName)
        {
            using (var audioFileReader = new AudioFileReader(audioFileName))
            {
                WaveFormat = audioFileReader.WaveFormat;
                var wholeFile = new List<float>((int)(audioFileReader.Length / 4));
                var readBuffer = new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
                int samplesRead;
                while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    wholeFile.AddRange(readBuffer.Take(samplesRead));
                }
                AudioData = wholeFile.ToArray();
            }
        }
        public CachedSound(System.IO.Stream WavStream)
        {
            if(WavStream.Length > 0)
            {
                using (var audioFileReader = new AudioStreamReader(WavStream))
                {
                    WaveFormat = audioFileReader.WaveFormat;
                    var wholeFile = new List<float>((int)(audioFileReader.Length / 4));
                    var readBuffer = new float[(WaveFormat?.SampleRate * audioFileReader.WaveFormat?.Channels).GetValueOrDefault(0)];
                    int samplesRead;
                    while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                    {
                        wholeFile.AddRange(readBuffer.Take(samplesRead));
                    }
                    AudioData = wholeFile.ToArray();
                }
            }
            else
            {
                WaveFormat = null;
                AudioData = new float[0];
            }
        }

        public CachedSound Clone() {
            return new CachedSound() { AudioData = AudioData, WaveFormat = WaveFormat};
        }
    }

}
