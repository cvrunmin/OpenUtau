using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using NAudio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using OpenUtau.Core.USTx;
using OpenUtau.Core.Render;

namespace OpenUtau.Core.Formats
{
    static class Wave
    {
        public static UWavePart CreatePart(string filepath)
        {
            foreach (var part in DocManager.Inst.Project.Parts)
            {
                var _part = part as UWavePart;
                if (_part != null && _part.FilePath == filepath)
                {
                    return new UWavePart()
                    {
                        FilePath = filepath,
                        PartNo = _part.PartNo,
                        FileDurTick = _part.FileDurTick,
                        DurTick = _part.DurTick,
                        Channels = _part.Channels,
                        Peaks = _part.Peaks
                    };
                }
            }
            WaveStream stream = null;
            try
            {
                stream = new AudioFileReaderExt(filepath);
            }
            catch(Exception e)
            {
                return new UWavePart() {
                    FilePath = filepath,
                    PartNo = DocManager.Inst.Project.Parts.Count,
                    Error = true
                };
            }
            var ms = 1000.0 * stream.Length / stream.WaveFormat.AverageBytesPerSecond;
            int durTick = DocManager.Inst.Project.MillisecondToTick(ms);
            UWavePart uwavepart = new UWavePart()
            {
                FilePath = filepath,
                FileDurTick = durTick,
                FileDurMillisecond = ms,
                DurTick = durTick,
                PartNo = DocManager.Inst.Project.Parts.Count,
                Channels = stream.WaveFormat.Channels
            };
            stream.Close();
            return uwavepart;
        }
        public static float[] BuildPeaks(UWavePart part, System.ComponentModel.BackgroundWorker worker) {
            return BuildPeaks(part.FilePath, part.Channels, worker, new TimeSpan(0, 0, 0, 0, (int)DocManager.Inst.Project.TickToMillisecond(part.HeadTrimTick)), new TimeSpan(0, 0, 0, 0, (int)DocManager.Inst.Project.TickToMillisecond(part.DurTick)));
        }
        public static float[] BuildPeaks(string path, int channels, System.ComponentModel.BackgroundWorker worker) {
            return BuildPeaks(path, channels, worker, TimeSpan.Zero, TimeSpan.MinValue);
        }
        public static float[] BuildPeaks(string path, int channels, System.ComponentModel.BackgroundWorker worker, TimeSpan startPos, TimeSpan durPos)
        {
            if (!File.Exists(path)) return new float[0];
            const double peaksRate = 4000;
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            float[] peaks;
            using (var stream = new AudioFileReaderExt(path))
            {
                using (var offset = new WaveOffsetStream(new Wave32To16Stream(stream), TimeSpan.Zero, startPos, durPos != TimeSpan.MinValue ? durPos : stream.TotalTime))
                {
                    double peaksSamples = (int)((double)offset.Length / offset.WaveFormat.BlockAlign / offset.WaveFormat.SampleRate * peaksRate);
                    if (channels != offset.WaveFormat.Channels) channels = offset.WaveFormat.Channels;
                    peaks = new float[(int)(peaksSamples + 1) * channels];
                    double blocksPerPixel = offset.Length / offset.WaveFormat.BlockAlign / peaksSamples;

                    var converted =  (offset.ToSampleProvider());

                    float[] buffer = new float[4096];

                    int readed;
                    int readPos = 0;
                    int peaksPos = 0;
                    double bufferPos = 0;
                    float lmax = 0, lmin = 0, rmax = 0, rmin = 0;
                    while ((readed = converted.Read(buffer, 0, 4096)) != 0)
                    {
                        if (offset.Position > offset.Length) break; //over-run
                        readPos += readed;
                        for (int i = 0; i < readed; i += channels)
                        {
                            lmax = Math.Max(lmax, buffer[i]);
                            lmin = Math.Min(lmin, buffer[i]);
                            if (channels > 1)
                            {
                                rmax = Math.Max(rmax, buffer[i + 1]);
                                rmin = Math.Min(rmin, buffer[i + 1]);
                            }
                            if (i > bufferPos)
                            {
                                lmax = -lmax; lmin = -lmin; rmax = -rmax; rmin = -rmin; // negate peaks to fipped waveform
                                peaks[peaksPos * channels] = lmax == 0 ? lmin : lmin == 0 ? lmax : (lmin + lmax) / 2;
                                peaks[peaksPos * channels + 1] = rmax == 0 ? rmin : rmin == 0 ? rmax : (rmin + rmax) / 2;
                                peaksPos++;
                                lmax = lmin = rmax = rmin = 0;
                                bufferPos += blocksPerPixel * stream.WaveFormat.Channels;
                            }
                        }
                        bufferPos -= readed;
                        worker.ReportProgress((int)((double)readPos * sizeof(float) * 100 / stream.Length));
                    }
                }
            }
            sw.Stop();
            System.Diagnostics.Debug.WriteLine("Build peaks {0} ms", sw.Elapsed.TotalMilliseconds);
            return peaks;
        }
    }
}
