using OpenUtau.Core.USTx;
using OpenUtau.Core.Util;
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
            }
            foreach (var vowel in Singer.VowelMap)
            {
                listVowel.Items.Add(vowel.Key);
            }
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
        

        private void listVowel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listVowel.SelectedIndex == -1) return;
            if (listVowel.SelectedItem is string key && Singer.VowelMap.ContainsKey(key))
            {
                txtVowel.Text = key;
                var pho = Singer.VowelMap.DeRedirect()[key];
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
                var pho = Singer.ConsonentMap.DeRedirect()[key];
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
                    Singer.ConsonentMap.Add(txtConsonent.Text, new Core.Formats.Presamp.VCContent() { Content = phos });
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
                    Singer.VowelMap.Add(txtVowel.Text, new Core.Formats.Presamp.VCContent() { Content = phos });
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
                Singer.ConsonentMap.Add(txtConsonent.Text, new Core.Formats.Presamp.VCContent() { Content = phos });
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
                Singer.VowelMap.Add(txtVowel.Text, new Core.Formats.Presamp.VCContent() { Content = phos });
                var ind = listVowel.SelectedIndex;
                LoadSinger(true);
                listVowel.SelectedIndex = ind;
            }
        }
    }
}
