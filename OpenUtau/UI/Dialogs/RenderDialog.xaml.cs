using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.Wave;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.Render.NAudio;
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
    public partial class RenderDialog : Window
    {
        public RenderDialog()
        {
            InitializeComponent();
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
                taskprogress.State = TaskDialogProgressBarState.Marquee;
                var sampler = await RenderDispatcher.Inst.GetMixingStream(DocManager.Inst.Project, cancel.Token);
                taskprogress.State = TaskDialogProgressBarState.Normal;
                taskdialog.Text = GenTextInfo(task: "Writing into files", status:"Writing track 0/" + DocManager.Inst.Project.Tracks.Count);
                if (!shouldMix)
                {
                    taskprogress.Value = 0;
                    var tracks = sampler.InputStreams.Cast<TrackWaveChannel>().ToList();
                    int i = 0;
                    foreach (var track in tracks)
                    {
                        if(track != null && !skipped.Contains(track.TrackNo))
                        {
                            var p = track.Pan;
                            var pv = track.PlainVolume;
                            var mute = track.Muted;
                            var pad = track.PadWithZeroes;
                            track.Pan = 0;
                            track.PlainVolume = MusicMath.DecibelToVolume(0);
                            track.Muted = false;
                            track.PadWithZeroes = false;
                            WaveFileWriter.CreateWaveFile(System.IO.Path.Combine(path, System.IO.Path.GetFileNameWithoutExtension(DocManager.Inst.Project.FilePath) + "_Track-" + (track.TrackNo + 1) + ".wav"), track);
                            track.Pan = p;
                            track.PlainVolume = pv;
                            track.Muted = mute;
                            track.PadWithZeroes = pad;
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
                    WaveFileWriter.CreateWaveFile(path, sampler);
                    Dispatcher.Invoke(()=>taskprogress.Value = 1000);
                }
            }, cancel.Token).ContinueWith(task=>taskdialog.Close());
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
                        listboxGenFiles.Items.Add(new CheckBox() { Content = "Track " + item.DisplayTrackNo, IsChecked = true, Tag = item.TrackNo });
                    }
                }
                else if (radio == radioRenderSplitted)
                {
                    foreach (var item in DocManager.Inst.Project.Tracks)
                    {
                        listboxGenFiles.Items.Add(new Label() { Content = "Track " + item.DisplayTrackNo, Tag = item.TrackNo, Padding = new Thickness(0) });
                    }
                }
            }
        }

        private void txtboxPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            /*if(listboxGenFiles != null)
            foreach (var item in listboxGenFiles.Items)
            {
                if (item is ContentControl control) {
                    control.Content = System.IO.Path.Combine(txtboxPath.Text, System.IO.Path.GetFileNameWithoutExtension(DocManager.Inst.Project.FilePath) + "_Track-" + control.Tag + ".wav");
                }
            }*/
        }

        private string GenTextInfo(string task = "", string status = "") {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Generating wave file...").AppendLine(string.Format("{0,10}: {1}", "Task", task)).AppendLine(string.Format("{0,10}: {1}", "Status", status));
            return sb.ToString();
        }
    }
}
