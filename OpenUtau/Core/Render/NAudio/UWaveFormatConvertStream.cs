using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.Render.NAudio
{
    public class UWaveFormatConvertStream : WaveStream
    {
        public override WaveFormat WaveFormat { get; }

        public override long Length => (long)Math.Ceiling(sourceStream.Length / sourceStream.WaveFormat.BitsPerSample * 8 / sourceStream.WaveFormat.Channels * ((double)WaveFormat.SampleRate / sourceStream.WaveFormat.SampleRate) * WaveFormat.BitsPerSample / 8 * WaveFormat.Channels);

        private long position;

        public override long Position { get => position; set { position = value; sourceStream.Position = (long)((double)position / WaveFormat.BitsPerSample * 8 / WaveFormat.Channels / ((double)WaveFormat.SampleRate / sourceStream.WaveFormat.SampleRate) * sourceStream.WaveFormat.BitsPerSample / 8 * sourceStream.WaveFormat.Channels); } }

        private WaveStream sourceStream;

        public UWaveFormatConvertStream(WaveStream source, WaveFormat format)
        {
            sourceStream = source;
            WaveFormat = format;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var sampleRead = count / (WaveFormat.BitsPerSample / 8) / WaveFormat.Channels;
            var sampleSrc = (int)(sampleRead / ((float)WaveFormat.SampleRate / sourceStream.WaveFormat.SampleRate));
            byte[] rb = new byte[0];
            int bytesRequired = sampleSrc * (sourceStream.WaveFormat.BitsPerSample / 8) * sourceStream.WaveFormat.Channels;
            rb = BufferHelpers.Ensure(rb, bytesRequired);
            var actread = sourceStream.Read(rb, 0, bytesRequired);
            sampleSrc = actread / sourceStream.WaveFormat.BitsPerSample * 8 / sourceStream.WaveFormat.Channels;
            sampleRead = (int)(sampleSrc * ((float)WaveFormat.SampleRate / sourceStream.WaveFormat.SampleRate));
            var srdone = ConvertSampleRate(rb, sourceStream.WaveFormat, WaveFormat.SampleRate, sampleSrc, sampleRead);
            var bddone = ConvertBitDepth(srdone, sourceStream.WaveFormat, WaveFormat);
            var cdone = ConvertChannels(bddone, sourceStream.WaveFormat, WaveFormat);
            buffer = BufferHelpers.Ensure(buffer, sampleRead * WaveFormat.BitsPerSample / 8 * WaveFormat.Channels);
            for (int i = 0; i < cdone.Length; i++)
            {
                buffer[i] = cdone[i];
            }
            return sampleRead * WaveFormat.BitsPerSample / 8 * WaveFormat.Channels;
        }

        private byte[] ConvertSampleRate(byte[] src, WaveFormat old, int mod, int sampleO, int sampleN)
        {
            if (old.SampleRate == mod) return src;
            var ratio = (float)mod / old.SampleRate;
            var sampleS = src.Length / old.BitsPerSample * 8;
            var sampleSC = sampleS / old.Channels;
            var sampleC = (int)(sampleSC * ratio);
            var sample = sampleC * old.Channels;
            var bytesPsample = old.BitsPerSample / 8;
            var bs = new byte[sample * bytesPsample];
            if (old.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                var warps = new WaveBuffer(src);
                var warpsc = new WaveBuffer[old.Channels];
                for (int i = 0; i < warpsc.Length; i++)
                {
                    warpsc[i] = new WaveBuffer(sampleSC * bytesPsample);
                }
                for (int i = 0; i < sampleSC; i++)
                {
                    for (int j = 0; j < old.Channels; j++)
                    {
                        warpsc[j].FloatBuffer[i] = warps.FloatBuffer[i * old.Channels + j];
                    }
                }
                var warpc = new WaveBuffer[old.Channels];
                for (int i = 0; i < warpsc.Length; i++)
                {
                    warpc[i] = new WaveBuffer(sampleC * bytesPsample);
                }
                for (int i = 0; i < sampleC; i++)
                {
                    var pos = i / ratio;
                    for (int j = 0; j < old.Channels; j++)
                    {
                        warpc[j].FloatBuffer[i] = warpsc[j].FloatBuffer[(int)Math.Floor(pos)] * (1 - (pos - (float)Math.Truncate(pos))) + warpsc[j].FloatBuffer[(int)Math.Ceiling(pos)] * (pos - (float)Math.Truncate(pos));
                    }
                }
                var warp = new WaveBuffer(bs);
                for (int i = 0; i < sampleC; i++)
                {
                    for (int j = 0; j < old.Channels; j++)
                    {
                        warp.FloatBuffer[i * old.Channels + j] = warpc[j].FloatBuffer[i];
                    }
                }
            }
            else if (old.Encoding == WaveFormatEncoding.Pcm)
            {
                var isc = new int[old.Channels, sampleSC * bytesPsample];
                var ic = new int[old.Channels, sampleC * bytesPsample];
                switch (old.BitsPerSample)
                {
                    case 16:
                        for (int i = 0; i < sampleS; i++)
                        {
                            int a = BitConverter.ToInt16(src, i * 2);
                            isc[i % old.Channels,i / old.Channels] = a;
                        }
                        for (int i = 0; i < sample; i++)
                        {
                            var pos = i / old.Channels / ratio;
                            ic[i % old.Channels,i / old.Channels] = (int)(isc[i % old.Channels,(int)Math.Floor(pos)] * (1 - (pos - (float)Math.Truncate(pos))) + isc[i % old.Channels,(int)Math.Ceiling(pos)] * (pos - (float)Math.Truncate(pos)));
                        }
                        var warp = new WaveBuffer(bs);
                        for (int i = 0; i < sample; i++)
                        {
                            warp.ShortBuffer[i] = (short)(ic[i % old.Channels,i / old.Channels]);
                        }
                        break;
                    case 24:
                        for (int i = 0; i < sampleS; i++)
                        {
                            int a = (((sbyte)src[i * 3 + 2] << 16) | (src[i * 3 + 1] << 8) | src[i * 3]);
                            isc[i % old.Channels,i / old.Channels] = a;
                        }
                        for (int i = 0; i < sample; i++)
                        {
                            var pos = i / old.Channels / ratio;
                            ic[i % old.Channels,i / old.Channels] = (int)(isc[i % old.Channels,(int)Math.Floor(pos)] * (1 - (pos - (float)Math.Truncate(pos))) + isc[i % old.Channels,(int)Math.Ceiling(pos)] * (pos - (float)Math.Truncate(pos)));
                        }
                        for (int i = 0; i < sample; i++)
                        {
                            var sample24 = ic[i % old.Channels,i / old.Channels];
                            bs[i * 3] = (byte)(sample24);
                            bs[i * 3 + 1] = (byte)(sample24 >> 8);
                            bs[i * 3 + 2] = (byte)(sample24 >> 16);
                        }
                        break;
                    case 32:
                        for (int i = 0; i < sampleS; i++)
                        {
                            int a = (((sbyte)src[i * 4 + 3] << 24 | src[i * 4 + 2] << 16) | (src[i * 4 + 1] << 8) | src[i * 4]);
                            isc[i % old.Channels,i / old.Channels] = a;
                        }
                        for (int i = 0; i < sample; i++)
                        {
                            var pos = i / old.Channels / ratio;
                            ic[i % old.Channels,i / old.Channels] = (int)(isc[i % old.Channels,(int)Math.Floor(pos)] * (1 - (pos - (float)Math.Truncate(pos))) + isc[i % old.Channels,(int)Math.Ceiling(pos)] * (pos - (float)Math.Truncate(pos)));
                        }
                        for (int i = 0; i < sample; i++)
                        {
                            var sample32i = ic[i % old.Channels,i / old.Channels];
                            bs[i * 4] = (byte)(sample32i);
                            bs[i * 4 + 1] = (byte)(sample32i >> 8);
                            bs[i * 4 + 2] = (byte)(sample32i >> 16);
                            bs[i * 4 + 3] = (byte)(sample32i >> 24);
                        }
                        break;
                    default:
                        break;
                }
            }
            return bs;
        }

        private byte[] ConvertBitDepth(byte[] src, WaveFormat old, WaveFormat mod)
        {
            if (old.BitsPerSample == mod.BitsPerSample && old.Encoding == mod.Encoding) return src;
            var samples = src.Length / (old.BitsPerSample / 8);
            var bd = new byte[samples * (mod.BitsPerSample / 8)];
            if (old.Encoding == WaveFormatEncoding.Pcm && mod.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                PcmToIeeeFloat(src, old, samples, bd);
            }
            else if (old.Encoding == WaveFormatEncoding.IeeeFloat && mod.Encoding == WaveFormatEncoding.Pcm)
            {
                IeeeFloatToPcm(src, mod, samples, bd);
            }
            else if (old.Encoding == WaveFormatEncoding.Pcm && mod.Encoding == WaveFormatEncoding.Pcm)
            {
                var tbd = new byte[samples * 4];
                PcmToIeeeFloat(src, old, samples, tbd);
                IeeeFloatToPcm(tbd, mod, samples, bd);
            }
            return bd;
        }

        private byte[] ConvertChannels(byte[] src, WaveFormat old, WaveFormat mod)
        {
            if (old.Channels == mod.Channels) return src;
            var samples = src.Length / (old.BitsPerSample / 8) / old.Channels;
            var bc = new byte[samples * mod.BitsPerSample / 8 * mod.Channels];
            var bytepersample = old.BitsPerSample / 8;
            switch (old.Channels)
            {
                case 1:
                    if (mod.Channels == 2)
                    {
                        for (int i = 0; i < samples; i++)
                        {
                            for (int j = 0; j < bytepersample; j++)
                            {
                                bc[i * 2 * bytepersample + j] = src[i * bytepersample + j];
                                bc[(i * 2 + 1) * bytepersample + j] = src[i * bytepersample + j];
                            }
                        }
                    }
                    break;
                case 2:
                    if (mod.Channels == 1)
                    {
                        if (old.Encoding == WaveFormatEncoding.IeeeFloat) {
                            var warp = new WaveBuffer(src);
                            var warpd = new WaveBuffer(bc);
                            var i = 0;
                            for (var sourceSample = 0; sourceSample < samples; sourceSample += 2)
                            {
                                var left = warp.FloatBuffer[sourceSample];
                                var right = warp.FloatBuffer[sourceSample + 1];
                                var outSample = (left * 0.5f) + (right * 0.5f);

                                warpd.FloatBuffer[i++] = outSample;
                            }
                        }
                    }
                    break;
            }
            return bc;
        }

        private static void IeeeFloatToPcm(byte[] src, WaveFormat mod, int samples, byte[] bd)
        {
            var warp = new WaveBuffer(bd);
            var warps = new WaveBuffer(src);
            for (int i = 0; i < samples; i++)
            {
                // adjust volume
                float sample32 = warps.FloatBuffer[i] * 1.0f;
                // clip
                if (sample32 > 1.0f)
                    sample32 = 1.0f;
                if (sample32 < -1.0f)
                    sample32 = -1.0f;
                if (mod.BitsPerSample == 16)
                    warp.ShortBuffer[i] = (short)(sample32 * 32767);
                else if (mod.BitsPerSample == 24)
                {
                    var sample24 = (int)Math.Round(sample32 * 8388607.0);
                    bd[i * 3] = (byte)(sample24);
                    bd[i * 3 + 1] = (byte)(sample24 >> 8);
                    bd[i * 3 + 2] = (byte)(sample24 >> 16);
                }
                else if (mod.BitsPerSample == 32)
                {
                    var sample32i = (int)(sample32 * 2147483647.0);
                    bd[i * 4] = (byte)(sample32i);
                    bd[i * 4 + 1] = (byte)(sample32i >> 8);
                    bd[i * 4 + 2] = (byte)(sample32i >> 16);
                    bd[i * 4 + 3] = (byte)(sample32i >> 24);
                }
            }
        }

        private static void PcmToIeeeFloat(byte[] src, WaveFormat old, int samples, byte[] bd)
        {
            var warp = new WaveBuffer(bd);
            for (int i = 0; i < samples; i++)
            {
                if (old.BitsPerSample == 8)
                    warp.FloatBuffer[samples] = src[i] / 128f - 1.0f;
                else if (old.BitsPerSample == 16)
                    warp.FloatBuffer[samples] = BitConverter.ToInt16(src, i * 2) / 32768f;
                else if (old.BitsPerSample == 24)
                    warp.FloatBuffer[samples] = (((sbyte)src[i * 3 + 2] << 16) | (src[i * 3 + 1] << 8) | src[i * 3]) / 8388608f;
                else if (old.BitsPerSample == 32)
                    warp.FloatBuffer[samples] = (((sbyte)src[i * 4 + 3] << 24 | src[i * 4 + 2] << 16) | (src[i * 4 + 1] << 8) | src[i * 4]) / 2147483648f;
            }
        }
    }
}
