using System;
using System.Collections.Generic;
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

using OpenUtau.UI.Controls;
using OpenUtau.Core;
using OpenUtau.Core.USTx;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using static OpenUtau.Core.Formats.UtauSoundbank;
using System.Data;
using OpenUtau.Core.Util;
using OpenUtau.UI.Models;

namespace OpenUtau.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for SingerViewDialog.xaml
    /// </summary>
    public partial class SingerViewDialog : Window
    {
        List<string> singerNames;
        public SingerViewDialog()
        {
            InitializeComponent();
            UpdateSingers();
        }

        private void UpdateSingers()
        {
            singerNames = new List<string>();
            var selindex = name.SelectedIndex;
            foreach (var pair in DocManager.Inst.Singers)
            {
                if (pair.Value == null) continue;
                singerNames.Add(pair.Value.Name);
            }
            this.name.ItemsSource = singerNames;
            if (singerNames.Count > 0)
            {
                this.name.SelectedIndex = Math.Max(0, selindex);
                SetSinger(singerNames[name.SelectedIndex]);
            }
        }
        USinger SelectedSinger;
        public void SetSinger(string singerName)
        {
            USinger singer = SelectedSinger = null;
            foreach (var pair in DocManager.Inst.Singers)
            {
                if (pair.Value == null) continue;
                if (pair.Value.Name == singerName)
                {
                    singer = pair.Value;
                }
            }

            if (singer == null) return;
            SelectedSinger = singer;
            //this.name.Text = singer.Name;
            this.avatar.Source = singer.Avatar;
            this.info.Text = "Author: " + singer.Author + "\nWebsite: " + singer.Website + "\nPath: " + singer.Path + "\n\n" + singer.Detail;
            RefreshOtoView(true);
            RefreshLyricsView();
            singerAmend = false;
        }

        private void RefreshOtoView(bool force = false)
        {
            if (force)
            {
                otoview.ItemsSource = null;
                var observable = new ObservableCollection<UOto>(SelectedSinger.AliasMap.Values);
                observable.CollectionChanged += (sender, e) =>
                {
                    CollectionViewSource.GetDefaultView(otoview.ItemsSource).Refresh();
                };
                otoview.ItemsSource = observable;
            }
            else
            {
                otoview.Items.Refresh();
            }
        }

        private void RefreshLyricsView()
        {

            lyricsview.ItemsSource = null;
            var table = new DataTable();
            table.Columns.Add("Lyrics", typeof(string));
            table.Columns.Add("Phonemes", typeof(string));
            foreach (var item in SelectedSinger.PresetLyricsMap)
            {
                var str = "{ ";
                foreach (var item1 in item.Value.Notes.Values.Select(note => note.Lyric))
                {
                    str += item1 + " , ";
                }
                var row = table.NewRow();
                row["Lyrics"] = item.Key;
                row["Phonemes"] = (str.Length == 2 ? str : str.Remove(str.Length - 2)) + "}";
                table.Rows.Add(row);
            }
            table.AcceptChanges();
            lyricsview.ItemsSource = table.AsDataView();
        }

        private void name_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (singerAmend)
            {
                SaveSinger(SelectedSinger);
                foreach (var track in DocManager.Inst.Project.Tracks)
                {
                    if (track.Singer.Equals(SelectedSinger))
                    {
                        track.Amended = true;
                    }
                }
            }
            if(name.SelectedIndex >= 0)
            SetSinger(singerNames[this.name.SelectedIndex]);
        }

        private void butRefresh_Click(object sender, RoutedEventArgs e)
        {
            DocManager.Inst.SearchAllSingers();
            UpdateSingers();
        }
        bool singerAmend = false;
        private void otoview_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext == null) return;
            var item = (UOto)((FrameworkElement)e.OriginalSource).DataContext;
            var dialog = new OtoEditDialog(SelectedSinger, item);
            dialog.Closing += (sender1, e1) =>
            {
                if (dialog.DialogResult == true)
                {
                    var result = dialog.EditingOto;
                    if (SelectedSinger.AliasMap.ContainsKey(result.Alias))
                    {
                        FixConflictedOto(e1, result, result.Alias);
                    }
                    else if (SelectedSinger.AliasMap.ContainsKey(dialog.aliasBak))
                    {
                        if (!dialog.aliasBak.Equals(result.Alias))
                        {
                            SelectedSinger.AliasMap.Remove(dialog.aliasBak);
                            SelectedSinger.AliasMap.Add(result.Alias, result);
                        }
                        else
                        {
                            FixConflictedOto(e1, result, dialog.aliasBak);
                        }
                    }
                    else
                    {
                        SelectedSinger.AliasMap.Add(result.Alias, result);
                    }
                    singerAmend = true;
                }
            };
            dialog.ShowDialog();
        }

        private void FixConflictedOto(System.ComponentModel.CancelEventArgs e1, UOto result, string alias)
        {
            UOto conflictedOto = SelectedSinger.AliasMap[alias];
            if (!otoview.SelectedItem.Equals(conflictedOto) && !conflictedOto.Equals(result))
            {
                MessageBoxManager.Yes = Lang.LanguageManager.GetLocalized("Replace");
                MessageBoxManager.No = Lang.LanguageManager.GetLocalized("Duplicate");
                MessageBoxManager.Register();
                var warningResult = System.Windows.Forms.MessageBox.Show(string.Format("Singer {0} already has alia {1} (old: {2}({3}) , new: {4}({5})), replace or duplicate?", SelectedSinger.Name, result.Alias, conflictedOto.Alias, conflictedOto.File, result.Alias, result.File), "Conflict", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                MessageBoxManager.Unregister();
                switch (warningResult)
                {
                    case System.Windows.Forms.DialogResult.Yes:
                        SelectedSinger.AliasMap[alias] = result;
                        break;
                    case System.Windows.Forms.DialogResult.No:
                        int i = 1;
                        for (; SelectedSinger.AliasMap.ContainsKey(result.Alias + " (" + i + ")"); ++i) { }
                        result.Alias += " (" + i + ")";
                        SelectedSinger.AliasMap.Add(result.Alias, result);
                        break;
                    case System.Windows.Forms.DialogResult.Cancel:
                        e1.Cancel = true;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                SelectedSinger.AliasMap[alias] = result;
            }
        }

        private void FixConflictedLyricsPreset(UDictionaryNote result, string newkey, string oldkey)
        {
            UDictionaryNote conflictedOto = SelectedSinger.PresetLyricsMap[newkey];
            if ((lyricsview.SelectedItem == null || !lyricsview.SelectedItem.Equals(conflictedOto)) && !conflictedOto.Equals(result))
            {
                MessageBoxManager.Yes = Lang.LanguageManager.GetLocalized("Replace");
                MessageBoxManager.No = Lang.LanguageManager.GetLocalized("Duplicate");
                MessageBoxManager.Register();
                var warningResult = System.Windows.Forms.MessageBox.Show(string.Format("Singer {0} already has lyrics {1}, replace or duplicate?", SelectedSinger.Name, newkey), "Conflict", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                MessageBoxManager.Unregister();
                switch (warningResult)
                {
                    case System.Windows.Forms.DialogResult.Yes:
                        SelectedSinger.PresetLyricsMap[newkey] = result;
                        break;
                    case System.Windows.Forms.DialogResult.No:
                        int i = 1;
                        for (; SelectedSinger.PresetLyricsMap.ContainsKey(newkey + " (" + i + ")"); ++i) { }
                        newkey += " (" + i + ")";
                        SelectedSinger.PresetLyricsMap.Remove(oldkey);
                        SelectedSinger.PresetLyricsMap.Add(newkey, result);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                SelectedSinger.PresetLyricsMap[newkey] = result;
            }
        }

        private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (singerAmend)
            {
                SaveSinger(SelectedSinger);
                foreach (var track in DocManager.Inst.Project.Tracks)
                {
                    if (SelectedSinger.Equals(track.Singer))
                    {
                        track.Amended = true;
                    }
                }
            }
        }

        private void butDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (otoview.SelectedItem != null && otoview.SelectedItem is UOto oto)
            {
                UOto newOto = new UOto() { Alias = oto.Alias, Consonant = oto.Consonant, Cutoff = oto.Cutoff, Duration = oto.Duration, File = oto.File, Offset = oto.Offset, Overlap = oto.Overlap, Preutter = oto.Preutter };
                if (string.IsNullOrWhiteSpace(oto.Alias))
                {

                    var i1 = newOto.File.LastIndexOf('\\');
                    newOto = newOto.SetAlias(newOto.File.Substring(i1 > -1 ? i1 : 0).Replace(".wav", ""));
                }
                int i = 1;
                for (; SelectedSinger.AliasMap.ContainsKey(newOto.Alias + " (" + i + ")"); ++i) { }
                newOto.Alias += " (" + i + ")";
                SelectedSinger.AliasMap.Add(newOto.Alias, newOto);
                RefreshOtoView(true);
                singerAmend = true;
            }
        }

        private void butRemove_Click(object sender, RoutedEventArgs e)
        {
            if (otoview.SelectedItem != null && otoview.SelectedItem is UOto oto)
            {
                SelectedSinger.AliasMap.Remove(oto.Alias);
                RefreshOtoView(true);
                singerAmend = true;
            }
        }

        private void toggleLyricsPs_Click(object sender, RoutedEventArgs e)
        {
            if (toggleLyricsPs.IsChecked.Value)
            {
                lyricsview.Visibility = Visibility.Visible;
                otoview.Visibility = Visibility.Collapsed;
                gridPhonemesTool.Visibility = Visibility.Visible;
                gridOtoTool.Visibility = Visibility.Collapsed;
            }
            else
            {
                otoview.Visibility = Visibility.Visible;
                lyricsview.Visibility = Visibility.Collapsed;
                gridOtoTool.Visibility = Visibility.Visible;
                gridPhonemesTool.Visibility = Visibility.Collapsed;
            }
        }

        private void lyricsview_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lyricsview.SelectedItem != null)
            {
                StartEditingLyricsPreset();
                RefreshLyricsView();
            }
        }

        private void StartEditingLyricsPreset(bool auto = false)
        {
            var selectedrow = (lyricsview.SelectedItem as DataRowView).Row as DataRow;
            var key = (string)selectedrow.ItemArray[0];
            var lyrics = SelectedSinger.PresetLyricsMap[key];
            var midiWindow = new MidiWindow();
            var project = DocManager.Inst.Project;
            var track = new UTrack() { TrackNo = project.Tracks.Count(), Singer = SelectedSinger, Name = "Temp", Color = Colors.Black };
            //Start preparing
            DocManager.Inst.StartUndoGroup();
            DocManager.Inst.ExecuteCmd(new AddTrackCommand(project, track), true);
            var part = DocManager.Inst.Project.CreateVoicePart(PosTick: 0, TrackNo: track.TrackNo);
            part.DurTick = 480;
            DocManager.Inst.ExecuteCmd(new AddPartCommand(project, part), true);
            midiWindow.LyricsPresetDedicate = true;
            DocManager.Inst.ExecuteCmd(new LoadPartNotification(part, DocManager.Inst.Project, true), true);
            DocManager.Inst.ExecuteCmd(new AddNoteCommand(part, lyrics.Notes.Values.ToList()), true);

            if (!auto)
            {
                midiWindow.ShowDialog();
            }
            else
            {
                midiWindow.Height = 0;
                midiWindow.Width = 0;
                midiWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                midiWindow.Left = 0; midiWindow.Top = 0;
                midiWindow.Topmost = false;
                midiWindow.ShowInTaskbar = false;
                midiWindow.Show();
                midiWindow.MidiVM.SelectNote(part.Notes.First());
                for (int i = 0; i < part.Notes.Count * 2; i++)
                {
                    midiWindow.RaiseEvent(new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromDependencyObject(midiWindow), 0, Key.Enter) { RoutedEvent = KeyDownEvent});
                }
                PartManager.UpdatePart(part);
                midiWindow.Close();
                midiWindow = null;
            }

            var notes = part.Notes;
            lyrics.Notes.Clear();
            for (int i = 0; i < notes.Count; i++)
            {
                lyrics.Notes.Add(i, notes.ElementAt(i).Clone());
            }
            SelectedSinger.PresetLyricsMap[key] = lyrics;
            DocManager.Inst.ExecuteCmd(new RemovePartCommand(project, project.Parts[part.PartNo]), true);
            DocManager.Inst.ExecuteCmd(new RemoveTrackCommand(project, project.Tracks[track.TrackNo]), true);
            DocManager.Inst.EndUndoGroup();
            singerAmend = true;
            track = null;
            part = null;
        }

        private void butNewPho_Click(object sender, RoutedEventArgs e)
        {
            var newkey = "";
            if (SelectedSinger.PresetLyricsMap.ContainsKey(newkey))
            {
                int i = 1;
                for (; SelectedSinger.PresetLyricsMap.ContainsKey(newkey + " (" + i + ")"); ++i) { }
                newkey += " (" + i + ")";
            }
            SelectedSinger.PresetLyricsMap.Add(newkey, new UDictionaryNote());
            RefreshLyricsView();
            singerAmend = true;
        }

        private void butDuplicatePho_Click(object sender, RoutedEventArgs e)
        {
            if (lyricsview.SelectedItem != null)
            {
                var selectedrow = (lyricsview.SelectedItem as DataRowView).Row as DataRow;
                var newkey = (string)selectedrow.ItemArray[0];
                int i = 1;
                for (; SelectedSinger.PresetLyricsMap.ContainsKey(newkey + " (" + i + ")"); ++i) { }
                newkey += " (" + i + ")";
                SelectedSinger.PresetLyricsMap.Add(newkey, SelectedSinger.PresetLyricsMap[(string)selectedrow.ItemArray[0]].Clone());
                RefreshLyricsView();
                singerAmend = true;
            }
        }

        private void butRemovePho_Click(object sender, RoutedEventArgs e)
        {
            if (lyricsview.SelectedItem != null)
            {
                var selectedrow = (lyricsview.SelectedItem as DataRowView).Row as DataRow;
                SelectedSinger.PresetLyricsMap.Remove((string)selectedrow.ItemArray[0]);
                RefreshLyricsView();
                singerAmend = true;
            }
        }

        private void butCheatCLP_Click(object sender, RoutedEventArgs e)
        {
            const int basetick = 480;
            var dialog = new CheatCLPDialog();
            dialog.ShowDialog();
            try
            {
            var consonants = dialog.txtConsonant.Text.Split(',');
            var vowels = dialog.txtVowel.Text.Split(',');
            var connect = dialog.txtConnect.Text;
            var percents = dialog.txtPercent.Text.Split(',').Select(str =>
            {
                var pure = new string(str.SkipWhile(ch => !char.IsDigit(ch) && ch != '.').ToArray());
                return double.Parse((string)pure);
            }).ToList();
            foreach (var item in SelectedSinger.AliasMap)
            {
                var result = item.Key.StartsWithAny(consonants);
                if (result.flag)
                {
                    var root = item.Key;
                    var vowel = root.Replace(result.match, "");
                    foreach (var item1 in SelectedSinger.AliasMap.Where(pair => pair.Key.StartsWith(connect) && pair.Key.Replace(connect, "").StartsWith(vowel) && !string.IsNullOrWhiteSpace(pair.Key.Remove(0, connect.Length + vowel.Length))))
                    {
                        var part = DocManager.Inst.Project.CreateVoicePart(-1, 0);
                            part.DurTick = (int)(basetick * 1.25);
                        var rootnote = DocManager.Inst.Project.CreateNote(60, 0, (int)Math.Round(basetick * percents[0]));
                        rootnote.Lyric = root;
                        rootnote.Phonemes[0].Phoneme = root;
                        var part1note = DocManager.Inst.Project.CreateNote(60, (int)Math.Round(basetick * percents[0]), (int)Math.Round(basetick * percents[1]));
                        part1note.Lyric = item1.Key;
                        part1note.Phonemes[0].Phoneme = item1.Key;
                        part.Notes.Add(rootnote);
                        part.Notes.Add(part1note);
                        PartManager.UpdatePart(part, SelectedSinger);
                        var dict = new UDictionaryNote();
                        for (int i = 0; i < part.Notes.Count; i++)
                        {
                            dict.Notes.Add(i, part.Notes.ElementAt(i));
                        }
                        try
                        {
                            string lyrics = root + item1.Key.Replace(connect, "").Replace(vowel, "");
                            if (SelectedSinger.PresetLyricsMap.ContainsKey(lyrics))
                            {
                                int i = 1;
                                for (; SelectedSinger.PresetLyricsMap.ContainsKey(lyrics + " (" + i + ")"); ++i) { }
                                lyrics += " (" + i + ")";
                            }
                            SelectedSinger.PresetLyricsMap.Add(lyrics, dict);
                        }
                        catch (Exception)
                        {
                            
                        }
                    }
                }
            }
            RefreshLyricsView();
            singerAmend = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Fail(e.GetType().Name + ": " + ex.Message);
            }
        }
        string editinglyrics, editinglyricsQueue;
        bool queue, editing;

        private void butCheatRLP_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("Refresh all lyrics preset?", "Cheat (Refresh Lyrics Preset)", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                for (int i = 0; i < lyricsview.Items.Count; i++)
                {
                    lyricsview.SelectedIndex = i;
                    StartEditingLyricsPreset(true);
                    if (i % 100 == 0) GC.Collect(1, GCCollectionMode.Optimized, false);
                }
                System.Windows.MessageBox.Show("Done!", "Cheat (Refresh Lyrics Preset)");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SingerCVTableDialog dialog = new SingerCVTableDialog();
            dialog.LoadSinger(SelectedSinger);
            dialog.ShowDialog();
        }

        private void comboEncoding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty((string)comboEncoding.SelectedItem)) return;
            var selIndex = name.SelectedIndex;
            var selsing = SelectedSinger;
            DocManager.Inst.Singers.Remove(selsing.Path);
            var nes = OpenUtau.Core.Formats.UtauSoundbank.LoadSinger(selsing.Path, Encoding.GetEncoding((string)comboEncoding.SelectedItem), Encoding.GetEncoding((string)comboEncoding.SelectedItem));
            DocManager.Inst.Singers.Add(nes.Path, nes);
            UpdateSingers();
            name.SelectedIndex = selIndex;
        }

        private void TextBoxLyrics_GotFocus(object sender, RoutedEventArgs e)
        {
            if (e.Source is System.Windows.Controls.TextBox)
            {
                var _txt = e.Source as System.Windows.Controls.TextBox;
                if (editing) {
                    editinglyricsQueue = _txt.Text;
                    queue = true;
                }
                else
                {
                    editinglyrics = _txt.Text;
                    editing = true;
                }
            }
        }

        private void TextBoxLyrics_LostFocus(object sender, RoutedEventArgs e)
        {

            if (e.Source is System.Windows.Controls.TextBox)
            {
                var _txt = e.Source as System.Windows.Controls.TextBox;
                if (_txt.Text != editinglyrics)
                {
                    if (SelectedSinger.PresetLyricsMap.ContainsKey(_txt.Text))
                    {
                        FixConflictedLyricsPreset(SelectedSinger.PresetLyricsMap[editinglyrics], _txt.Text, editinglyrics);
                    }
                    else
                    {
                        var l = SelectedSinger.PresetLyricsMap[editinglyrics];
                        SelectedSinger.PresetLyricsMap.Remove(editinglyrics);
                        SelectedSinger.PresetLyricsMap.Add(_txt.Text, l);
                    }
                }
                editinglyrics = "";
                editing = false;
                if (queue)
                {
                    editinglyrics = editinglyricsQueue;
                    editinglyricsQueue = "";
                    editing = true;
                    queue = false;
                }
                singerAmend = true;
            }
        }
    }
}
