using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Vorbis;
using System.IO;

namespace OpenUtau.Core.Render
{
    class AudioFileReaderExt : WaveStream, ISampleProvider
    {
        private WaveStream readerStream; 
        private readonly SampleChannel sampleChannel; 
        private readonly int destBytesPerSample;
        private readonly int sourceBytesPerSample;
        private readonly long length;
        private readonly object lockObject;

        public AudioFileReaderExt(string filename)
        {
            lockObject = new object();
            CreateReaderStream(filename);
            try
            {
                sourceBytesPerSample = (readerStream.WaveFormat.BitsPerSample / 8) * readerStream.WaveFormat.Channels;
                sampleChannel = new SampleChannel(readerStream, false);
                destBytesPerSample = 4 * sampleChannel.WaveFormat.Channels;
                length = SourceToDest(readerStream.Length);
            }
            catch (Exception)
            {
            }
        }

        public override WaveFormat WaveFormat => sampleChannel?.WaveFormat;

        public override long Length => length;

        public override long Position
        {
            get => SourceToDest(readerStream.Position);
            set { lock (lockObject) { readerStream.Position = DestToSource(value); } }
        }

        private void CreateReaderStream(string fileName)
        {
            try
            {
                if (fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    readerStream = new WaveFileReader(fileName);
                    if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm && readerStream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                    {
                        readerStream = WaveFormatConversionStream.CreatePcmStream(readerStream);
                        readerStream = new BlockAlignReductionStream(readerStream);
                    }
                }
                else if (fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    readerStream = new Mp3FileReader(fileName);
                }
                else if (fileName.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".aif", StringComparison.OrdinalIgnoreCase))
                {
                    readerStream = new AiffFileReader(fileName);
                }
                else if (fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                {
                    readerStream = new VorbisWaveReader(fileName);
                }
                else
                {
                    // fall back to media foundation reader, see if that can play it
                    readerStream = new MediaFoundationReader(fileName);
                }
            }
            catch (IOException)
            {
            }
        }


        /// <summary>
        /// Helper to convert source to dest bytes
        /// </summary>
        private long SourceToDest(long sourceBytes)
        {
            return destBytesPerSample * (sourceBytes / sourceBytesPerSample);
        }

        private long DestToSource(long destBytes)
        {
            return sourceBytesPerSample * (destBytes / destBytesPerSample);
        }

        /// <summary>
        /// Reads from this wave stream
        /// </summary>
        /// <param name="buffer">Audio buffer</param>
        /// <param name="offset">Offset into buffer</param>
        /// <param name="count">Number of bytes required</param>
        /// <returns>Number of bytes read</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var waveBuffer = new WaveBuffer(buffer);
            int samplesRequired = count / 4;
            int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
            return samplesRead * 4;
        }

        /// <summary>
        /// Reads audio from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <param name="offset">Offset into sample buffer</param>
        /// <param name="count">Number of samples required</param>
        /// <returns>Number of samples read</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            lock (lockObject)
            {
                return (sampleChannel?.Read(buffer, offset, count)).GetValueOrDefault(0);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (readerStream != null)
                {
                    readerStream.Dispose();
                    readerStream = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
