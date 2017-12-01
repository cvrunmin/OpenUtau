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
        public void LoadSinger(USinger singer) {
            Singer = singer;
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn() { Caption = "Vowels", ColumnName = "Vowels" });
            table.Columns.Add(new DataColumn() { Caption = "-", ColumnName = "-" });
            foreach (var item in singer.ConsonentMap.Keys)
            {
                if(!table.Columns.Contains(item) && !string.IsNullOrEmpty(item))
                    table.Columns.Add(new DataColumn() { Caption = item, ColumnName = item });
            }
            foreach (var vowel in singer.VowelMap)
            {
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
    }
}
