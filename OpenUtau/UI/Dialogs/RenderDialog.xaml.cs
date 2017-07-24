using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.Wave;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OpenUtau.UI.Dialogs
{
    /// <summary>
    /// RenderDialog.xaml 的互動邏輯
    /// </summary>
    public partial class RenderDialog : Window, ICmdSubscriber
    {
        public RenderDialog()
        {
            InitializeComponent();
            Subscribe(DocManager.Inst);
        }
        TaskDialog taskdialog;
        TaskDialogProgressBar taskprogress;
        private void butOk_Click(object sender, RoutedEventArgs e)
        {
            if (!System.IO.Path.HasExtension(txtboxPath.Text) && radioRenderMaster.IsChecked.Value) {
                MessageBox.Show("Invalid path, please check if it is a file path");
                return;
            }
            DialogResult = true;
            taskdialog = new TaskDialog()
            {
                StandardButtons = TaskDialogStandardButtons.Cancel
            };
            taskprogress = new TaskDialogProgressBar(0, 1000, 0);
            taskdialog.ProgressBar = taskprogress;
            taskdialog.Cancelable = true;
            var skipped = new List<int>();
            if (radioRenderSelected.IsChecked.Value)
            {
                foreach (var item in listboxGenFiles.Items)
                {
                    if (item is CheckBox chkbox && !chkbox.IsChecked.Value) skipped.Add((int)chkbox.Tag);
                }
            }
            taskdialog.Text = GenTextInfo(task:"Rendering tracks");
            var shouldMix = radioRenderMaster.IsChecked.Value;
            var path = txtboxPath.Text;
            var cancel = new System.Threading.CancellationTokenSource();
            Task.Run(async () =>
            {

                var sampler = await RenderDispatcher.Inst.GetMixingSampleProvider(DocManager.Inst.Project, skipped.ToArray(), cancel.Token);
                taskdialog.Text = GenTextInfo(task: "Writing into files", status:"Writing track 0/" + DocManager.Inst.Project.Tracks.Count);
                if (!shouldMix)
                {
                    taskprogress.Value = 0;
                    var tracks = sampler.MixerInputs.Cast<TrackSampleProvider>().ToList();
                    int i = 0;
                    foreach (var track in tracks)
                    {
                        if(!skipped.Contains(i))
                        {
                            double elisimatedMs;
                            try
                            {
                                var project = DocManager.Inst.Project;
                                elisimatedMs = project.TickToMillisecond(project.Parts.Where(part => part.TrackNo == track.TrackNo).OrderByDescending(part => part.EndTick).First().EndTick);
                            }
                            catch (Exception)
                            {
                                elisimatedMs = 60000;
                            }
                            track.Pan = 0;
                            track.PlainVolume = MusicMath.DecibelToVolume(0);
                            track.Muted = false;
                            int limit = track.WaveFormat.AverageBytesPerSecond * (int)Math.Ceiling(elisimatedMs / 1000);
                            using (var str = new WaveFileWriter(System.IO.Path.Combine(path, System.IO.Path.GetFileNameWithoutExtension(DocManager.Inst.Project.FilePath) + "_Track-" + i + ".wav"), track.WaveFormat))
                            {
                                var wave = track.ToWaveProvider();
                                var buffer = new byte[track.WaveFormat.AverageBytesPerSecond * 4];
                                while (str.Position < limit)
                                {
                                    var bytesRead = wave.Read(buffer, 0, buffer.Length);
                                    if (bytesRead == 0)
                                    {
                                        // end of source provider
                                        break;
                                    }
                                    await str.WriteAsync(buffer, 0, bytesRead);
                                }
                            }
                        }

                        ++i;
                        Dispatcher.Invoke(()=>
                        {
                            taskprogress.Value = (int)(i / (float)DocManager.Inst.Project.Tracks.Count * 1000);
                            taskdialog.Text = GenTextInfo(task: "Writing into files", status: "Writing track " + i + "/" + DocManager.Inst.Project.Tracks.Count);
                        }) ;
                    }
                }
                else
                {
                    taskdialog.Text = GenTextInfo("Writing into files", "Writing master track");
                    WaveFileWriter.CreateWaveFile(path, sampler.ToWaveProvider());
                    Dispatcher.Invoke(()=>taskprogress.Value = 1000);
                }
            }).ContinueWith(task=>taskdialog.Close());
            if (taskdialog.Show() == TaskDialogResult.Cancel) {
                cancel.Cancel(true);
            }
            Close();
        }

        private void butCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void butBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (radioRenderMaster.IsChecked == true) {
                var dialog = new CommonSaveFileDialog() { OverwritePrompt = true, AlwaysAppendDefaultExtension = true, EnsurePathExists = true, EnsureValidNames = true};
                dialog.Filters.Add(new CommonFileDialogFilter("Wave file", "*.wav"));
                dialog.DefaultExtension = "wav";
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok) {
                    txtboxPath.Text = dialog.FileName;
                }
            }
            else
            {
                var dialog = new CommonOpenFileDialog() { IsFolderPicker = true, EnsurePathExists = true };
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    txtboxPath.Text = dialog.FileName;
                }
            }
            this.Focus();
        }

        private void radioRenderOption_Checked(object sender, RoutedEventArgs e)
        {
            
            if (sender is RadioButton radio && radio.IsChecked.Value && listboxGenFiles != null)
            {
                listboxGenFiles.Items.Clear();
                if (radio == radioRenderSelected)
                {
                    foreach (var item in DocManager.Inst.Project.Tracks)
                    {
                        listboxGenFiles.Items.Add(new CheckBox() { Content = System.IO.Path.Combine(txtboxPath.Text, System.IO.Path.GetFileNameWithoutExtension(DocManager.Inst.Project.FilePath) + "_Track-" + item.TrackNo + ".wav"), IsChecked = true, Tag = item.TrackNo });
                    }
                }
                else if (radio == radioRenderSplitted)
                {
                    foreach (var item in DocManager.Inst.Project.Tracks)
                    {
                        listboxGenFiles.Items.Add(new Label() { Content = System.IO.Path.Combine(txtboxPath.Text, System.IO.Path.GetFileNameWithoutExtension(DocManager.Inst.Project.FilePath) + "_Track-" + item.TrackNo + ".wav"), Tag = item.TrackNo, Padding = new Thickness(0) });
                    }
                }
            }
        }

        private void txtboxPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(listboxGenFiles != null)
            foreach (var item in listboxGenFiles.Items)
            {
                if (item is ContentControl control) {
                    control.Content = System.IO.Path.Combine(txtboxPath.Text, System.IO.Path.GetFileNameWithoutExtension(DocManager.Inst.Project.FilePath) + "_Track-" + control.Tag + ".wav");
                }
            }
        }

        public void Subscribe(ICmdPublisher publisher)
        {
            publisher?.Subscribe(this);
        }

        private int RequiredRenderItem => DocManager.Inst.Project.Parts.OfType<Core.USTx.UVoicePart>().SelectMany(part => part.Notes).Count();
        private double decimals;
        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is ProgressBarNotification pbn && taskprogress != null) {
                double progress = 1f / (RequiredRenderItem + 10) * 1000 + decimals;
                int pi = (int)progress;
                if(taskprogress.Value + pi <= taskprogress.Maximum)taskprogress.Value += pi;
                decimals = progress - (int)progress;
                taskdialog.Text = GenTextInfo("Rendering Tracks", pbn.Info);
            }
        }

        private string GenTextInfo(string task = "", string status = "") {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Generating wave file...").AppendLine(string.Format("{0,10}: {1}", "Task", task)).AppendLine(string.Format("{0,10}: {1}", "Status", status));
            return sb.ToString();
        }
    }
}
