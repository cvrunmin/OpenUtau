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

using OpenUtau.Core;
using OpenUtau.Core.USTx;
using OpenUtau.Core.Util;
using System.Data;

namespace OpenUtau.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for Preferences.xaml
    /// </summary>
    public partial class ProjectPreferencesDialog : Window
    {
        private Grid _selectedGrid = null;
        private Grid SelectedGrid
        {
            set
            {
                if (_selectedGrid == value) return;
                if (_selectedGrid != null) _selectedGrid.Visibility = System.Windows.Visibility.Hidden;
                _selectedGrid = value;
                if (_selectedGrid != null) _selectedGrid.Visibility = System.Windows.Visibility.Visible;
            }
            get
            {
                return _selectedGrid;
            }
        }
        public UProject Project { get; set; } = DocManager.Inst.Project;
        public ProjectPreferencesDialog()
        {
            InitializeComponent();

            LoadExpList();

            generalItem.IsSelected = true;
        }


        private void treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (treeView.SelectedItem == generalItem) SelectedGrid = generalGrid;
            else if (treeView.SelectedItem == bpmItem) SelectedGrid = bpmGrid;
            else if (treeView.SelectedItem == expItem) SelectedGrid = expGrid;
            else SelectedGrid = null;
        }

        private void LoadExpList() {
            string GenOtherData(UExpression exp) {
                switch (exp)
                {
                    case FlagIntExpression fie:
                        return $"{{ Default:{fie.Default}, Range:{fie.Min}-{fie.Max}, Flag:{fie.Flag} }}";
                    case IntExpression fie:
                        return $"{{ Default:{fie.Default}, Range:{fie.Min}-{fie.Max} }}";
                    case FlagFloatExpression fie:
                        return $"{{ Default:{fie.Default}, Range:{fie.Min}-{fie.Max}, Flag:{fie.Flag} }}";
                    case FloatExpression fie:
                        return $"{{ Default:{fie.Default}, Range:{fie.Min}-{fie.Max} }}";
                    case FlagBoolExpression fie:
                        return $"{{ Default:{fie.Default}, Flag:{fie.Flag} }}";
                    case BoolExpression fie:
                        return $"{{ Default:{fie.Default} }}";
                    default:
                        return "";
                }
            }
            var dt = new DataTable();
            dt.Columns.Add("Abbr");
            dt.Columns.Add("Name");
            dt.Columns.Add("Type");
            dt.Columns.Add("Other");
            foreach (var item in Project.ExpressionTable.Values)
            {
                dt.Rows.Add(item.Abbr, item.Name, item.Type, GenOtherData(item));
            }
            listExp.ItemsSource = dt.DefaultView;
        }

        private void butAddExp_Click(object sender, RoutedEventArgs e)
        {

        }
        private void butRemove_Click(object sender, RoutedEventArgs e)
        {

        }
        static readonly string[] FixedExp = new string[]{ "volume", "velocity", "accent", "decay", "release" };
        private void listExp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FixedExp.Contains((listExp.SelectedItem as DataRowView).Row.Field<string>("Name")))
            {
                butRemove.IsEnabled = false;
            }
            else
            {
                butRemove.IsEnabled = true;
            }
        }
    }
}
