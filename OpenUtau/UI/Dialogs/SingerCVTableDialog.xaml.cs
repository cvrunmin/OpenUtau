using OpenUtau.Core.USTx;
using System;
using System.Collections.Generic;
using System.Data;
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

namespace OpenUtau.UI.Dialogs
{
    /// <summary>
    /// SingerCVTableDialog.xaml 的互動邏輯
    /// </summary>
    public partial class SingerCVTableDialog : Window
    {
        public SingerCVTableDialog()
        {
            InitializeComponent();
        }
        USinger Singer;

        private void LoadSinger(bool noset = false)
        {
            listVowel.Items.Clear();
            listConsonents.Items.Clear();
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn() { Caption = "Vowels", ColumnName = "Vowels" });
            table.Columns.Add(new DataColumn() { Caption = "-", ColumnName = "-" });
            foreach (var item in Singer.ConsonentMap.Keys)
            {
                listConsonents.Items.Add(item);
                if (!table.Columns.Contains(item) && !string.IsNullOrEmpty(item))
                    table.Columns.Add(new DataColumn() { Caption = item, ColumnName = item });
            }
            foreach (var vowel in Singer.VowelMap)
            {
                listVowel.Items.Add(vowel.Key);
                var row = table.NewRow();
                row.SetField("Vowels", vowel.Key);
                var map = new Dictionary<string, SortedSet<string>>();
                foreach (var pho in vowel.Value)
                {
                    string consonant = Core.Util.LyricsHelper.GetConsonant(pho);
                    consonant = string.IsNullOrEmpty(consonant) ? "-" : consonant;
                    if (!map.ContainsKey(consonant)) map.Add(consonant, new SortedSet<string>());
                    map[consonant].Add(pho);
                }
                foreach (var pair in map)
                {
                    if (table.Columns.Contains(pair.Key))
                        row.SetField(pair.Key, string.Join(", ", pair.Value));
                }
                table.Rows.Add(row);
            }
            dataGridCV.DataContext = table.DefaultView;
            if (!noset)
            {
                listVowel.SelectedIndex = 0;
                listConsonents.SelectedIndex = 0;
            }
        }

        public void LoadSinger(USinger singer) {
            Singer = singer;
            LoadSinger();
        }

        private void dataGridCV_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {

            }
        }

        private void dataGridCV_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                DataRow row = ((DataRowView)e.Row.Item).Row;
                if (row.Table.Rows.IndexOf(row) > 0)
                {
                    var post = ((TextBox)e.EditingElement).Text;
                    var pre = row.Field<string>(e.Column.Header as string);
                    if (row.Table.Columns.IndexOf(e.Column.Header as string) == 0) {
                        
                    }
                }
            }
        }

        private void listVowel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listVowel.SelectedIndex == -1) return;
            if (listVowel.SelectedItem is string key && Singer.VowelMap.ContainsKey(key))
            {
                txtVowel.Text = key;
                var pho = Singer.VowelMap[key];
                var s = "";
                var i = 0;
                foreach (var p in pho)
                {
                    i++;
                    s += p;
                    if (i != pho.Count) s += ",";
                }
                txtPhoV.Text = s;
            }
        }

        private void listConsonents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(listConsonents.SelectedIndex == -1) return;
            if (listConsonents.SelectedItem is string key && Singer.ConsonentMap.ContainsKey(key))
            {
                txtConsonent.Text = key;
                var pho = Singer.ConsonentMap[key];
                var s = "";
                var i = 0;
                foreach (var p in pho)
                {
                    i++;
                    s += p;
                    if (i != pho.Count) s += ",";
                }
                txtPhoC.Text = s;
            }
        }

        private void butAddC_Click(object sender, RoutedEventArgs e)
        {
            var key = txtConsonent.Text;
            if (true)
            {
                if (!Singer.ConsonentMap.ContainsKey(key))
                {
                    var phos = new SortedSet<string>();
                    foreach (var s in txtPhoC.Text.Split(','))
                    {
                        phos.Add(s);
                    }
                    Singer.ConsonentMap.Add(txtConsonent.Text, phos);
                    var s1 = txtConsonent.Text;
                    LoadSinger(true);
                    listConsonents.SelectedItem = s1;
                }
                else
                {
                    butSaveC_Click(sender,e);
                }
            }
        }

        private void butAddV_Click(object sender, RoutedEventArgs e)
        {
            var key = txtVowel.Text;
            if (true)
            {
                if (!Singer.VowelMap.ContainsKey(key))
                {
                    var phos = new SortedSet<string>();
                    foreach (var s in txtPhoV.Text.Split(','))
                    {
                        phos.Add(s);
                    }
                    Singer.VowelMap.Add(txtVowel.Text, phos);
                    var s1 = txtVowel.Text;
                    LoadSinger(true);
                    listVowel.SelectedItem = s1;
                }
                else
                {
                    butSaveV_Click(sender,e);
                }
            }
        }

        private void butRemoveC_Click(object sender, RoutedEventArgs e)
        {
            if (listConsonents.SelectedIndex == -1) return;
            if (listConsonents.SelectedItem is string key && Singer.ConsonentMap.ContainsKey(key) && MessageBox.Show($"Are you sure to delete {key}?", "Warning", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Singer.ConsonentMap.Remove(listConsonents.SelectedItem as string);
                var i = listConsonents.SelectedIndex;
                LoadSinger(true);
                listConsonents.SelectedIndex = i;
            }
        }

        private void butRemoveV_Click(object sender, RoutedEventArgs e)
        {
            if (listVowel.SelectedIndex == -1) return;
            if (listVowel.SelectedItem is string key && Singer.VowelMap.ContainsKey(key) && MessageBox.Show($"Are you sure to delete {key}?", "Warning", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Singer.VowelMap.Remove(listVowel.SelectedItem as string);
                var i = listVowel.SelectedIndex;
                LoadSinger(true);
                listVowel.SelectedIndex = i;
            }
        }

        private void butSaveC_Click(object sender, RoutedEventArgs e)
        {
            if (listConsonents.SelectedIndex == -1) return;
            if (listConsonents.SelectedItem is string key && Singer.ConsonentMap.ContainsKey(key))
            {
                Singer.ConsonentMap.Remove(key);
                var phos = new SortedSet<string>();
                foreach (var s in txtPhoC.Text.Split(','))
                {
                    phos.Add(s);
                }
                Singer.ConsonentMap.Add(txtConsonent.Text, phos);
                var ind = listConsonents.SelectedIndex;
                LoadSinger(true);
                listConsonents.SelectedIndex = ind;
            }
        }

        private void butSaveV_Click(object sender, RoutedEventArgs e)
        {
            if (listVowel.SelectedIndex == -1) return;
            if (listVowel.SelectedItem is string key && Singer.VowelMap.ContainsKey(key))
            {
                Singer.VowelMap.Remove(key);
                var phos = new SortedSet<string>();
                foreach (var s in txtPhoV.Text.Split(','))
                {
                    phos.Add(s);
                }
                Singer.VowelMap.Add(txtVowel.Text, phos);
                var ind = listVowel.SelectedIndex;
                LoadSinger(true);
                listVowel.SelectedIndex = ind;
            }
        }
    }
}
