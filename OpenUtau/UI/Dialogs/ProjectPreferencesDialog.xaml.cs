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

            txtboxMin = new TextBox() { Name = "txtboxMin", Width = 70 };
            txtboxMax = new TextBox() { Name = "txtboxMax", Width = 70 };
            gridBound = new Grid();
            gridBound.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            gridBound.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            gridBound.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(5, GridUnitType.Pixel) });
            gridBound.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            gridBound.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            var lbl1 = new Label() { Content = "Min" };
            Grid.SetColumn(lbl1, 0);
            Grid.SetColumn(txtboxMin, 1);
            var lbl2 = new Label() { Content = "Max" };
            Grid.SetColumn(lbl2, 3);
            Grid.SetColumn(txtboxMax, 4);
            gridBound.Children.Add(lbl1);
            gridBound.Children.Add(lbl2);
            gridBound.Children.Add(txtboxMin);
            gridBound.Children.Add(txtboxMax);

            LoadExpList();

            comboType.SelectedIndex = 0;

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
            if (Project.ExpressionTable.ContainsKey(txtboxName.Text)) return;
            var exp = CreateExpression();
            if (exp == null) return;
            Project.ExpressionTable.Add(txtboxName.Text, exp);
            Reload();
        }

        private UExpression CreateExpression()
        {
            switch ((comboType.SelectedItem as ComboBoxItem).Tag as string)
            {
                case "flag_int":
                    {
                        if (!int.TryParse(txtboxMin.Text, out var min)) return null;
                        if (!int.TryParse(txtboxMax.Text, out var max)) return null;
                        if (!int.TryParse(txtboxDefault.Text, out var de)) return null;
                        return new FlagIntExpression(null, txtboxName.Text, txtboxAbbr.Text) { Flag = txtboxFlag.Text, Default = de, Data = de, Max = max, Min = min };
                    }
                case "flag_float":
                    {
                        if (!float.TryParse(txtboxMin.Text, out var min)) return null;
                        if (!float.TryParse(txtboxMax.Text, out var max)) return null;
                        if (!float.TryParse(txtboxDefault.Text, out var de)) return null;
                        return new FlagFloatExpression(null, txtboxName.Text, txtboxAbbr.Text) { Flag = txtboxFlag.Text, Default = de, Data = de, Max = max, Min = min };
                    }
                case "flag_bool":
                    {
                        return new FlagBoolExpression(null, txtboxName.Text, txtboxAbbr.Text) { Flag = txtboxFlag.Text, Default = chkboxDefault.IsChecked.Value, Data = chkboxDefault.IsChecked.Value };
                    }
                case "int":
                    {
                        if (!int.TryParse(txtboxMin.Text, out var min)) return null;
                        if (!int.TryParse(txtboxMax.Text, out var max)) return null;
                        if (!int.TryParse(txtboxDefault.Text, out var de)) return null;
                        return new IntExpression(null, txtboxName.Text, txtboxAbbr.Text) { Default = de, Data = de, Max = max, Min = min };
                    }
                case "float":
                    {
                        if (!float.TryParse(txtboxMin.Text, out var min)) return null;
                        if (!float.TryParse(txtboxMax.Text, out var max)) return null;
                        if (!float.TryParse(txtboxDefault.Text, out var de)) return null;
                        return new FloatExpression(null, txtboxName.Text, txtboxAbbr.Text) { Default = de, Data = de, Max = max, Min = min };
                    }
                case "bool":
                    {
                        return new BoolExpression(null, txtboxName.Text, txtboxAbbr.Text) { Default = chkboxDefault.IsChecked.Value, Data = chkboxDefault.IsChecked.Value };
                    }
                default:
                    return null;
            }
        }

        private void butSet_Click(object sender, RoutedEventArgs e)
        {
            if (txtboxName.Text.Equals((listExp.SelectedItem as DataRowView).Row.Field<string>("Name"))) {
                Project.ExpressionTable.Remove((listExp.SelectedItem as DataRowView).Row.Field<string>("Name"));
                var exp = CreateExpression();
                Project.ExpressionTable.Add(txtboxName.Text, exp);
            }
            else
            {
                Project.ExpressionTable[txtboxName.Text] = CreateExpression();
            }
            Reload();
        }

        private void butRemove_Click(object sender, RoutedEventArgs e)
        {
            Project.ExpressionTable.Remove((listExp.SelectedItem as DataRowView).Row.Field<string>("Name"));
            Reload();
        }

        private void Reload()
        {
            int selInd = listExp.SelectedIndex;
            LoadExpList();
            listExp.SelectedIndex = Math.Min(selInd, listExp.SelectedIndex);
        }

        static readonly string[] FixedExp = new string[]{ "volume", "velocity", "accent", "decay", "release" };
        private void listExp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listExp.SelectedItem == null) {
                butSet.IsEnabled = false;
                return;
            }
            butSet.IsEnabled = true;
            string name = (listExp.SelectedItem as DataRowView).Row.Field<string>("Name");
            if (FixedExp.Contains(name))
            {
                butRemove.IsEnabled = false;
            }
            else
            {
                butRemove.IsEnabled = true;
            }
            var exp = Project.ExpressionTable[name];
            comboType.SelectedValue = exp.Type;
            switch (exp)
            {
                case FlagBoolExpression exp1:
                    txtboxFlag.Text = exp1.Flag;
                    chkboxDefault.IsChecked = exp1.Default;
                    break;
                case BoolExpression exp1:
                    chkboxDefault.IsChecked = exp1.Default;
                    break;
                case FlagFloatExpression exp1:
                    txtboxFlag.Text = exp1.Flag;
                    txtboxDefault.Text = exp1.Default.ToString();
                    txtboxMin.Text = exp1.Min.ToString();
                    txtboxMax.Text = exp1.Max.ToString();
                    break;
                case FloatExpression exp1:
                    txtboxDefault.Text = exp1.Default.ToString();
                    txtboxMin.Text = exp1.Min.ToString();
                    txtboxMax.Text = exp1.Max.ToString();
                    break;
                case FlagIntExpression exp1:
                    txtboxFlag.Text = exp1.Flag;
                    txtboxDefault.Text = exp1.Default.ToString();
                    txtboxMin.Text = exp1.Min.ToString();
                    txtboxMax.Text = exp1.Max.ToString();
                    break;
                case IntExpression exp1:
                    txtboxDefault.Text = exp1.Default.ToString();
                    txtboxMin.Text = exp1.Min.ToString();
                    txtboxMax.Text = exp1.Max.ToString();
                    break;
                default:
                    break;
            }
            txtboxAbbr.Text = exp.Abbr;
            txtboxName.Text = exp.Name;
        }
        CheckBox chkboxDefault = new CheckBox() { Name = "chkboxDefault", VerticalAlignment = VerticalAlignment.Center };
        TextBox txtboxDefault = new TextBox() { Name = "txtboxDefault", Width=70 };
        Grid gridBound;
        TextBox txtboxMin, txtboxMax;


        private void comboType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tag = (comboType.SelectedItem as ComboBoxItem).Tag as string;
            if (tag.Contains("flag")) {
                gridFlag.Visibility = Visibility.Visible;
            }
            else
            {
                gridFlag.Visibility = Visibility.Collapsed;
            }
            gridDefault.Children.Remove(chkboxDefault);
            gridDefault.Children.Remove(txtboxDefault);
            gridValues.Children.Remove(gridBound);
            switch (tag.Replace("flag_", ""))
            {
                case "bool":
                    Grid.SetColumn(chkboxDefault, 1);
                    gridDefault.Children.Add(chkboxDefault);
                        break;
                case "int":
                case "float":
                    Grid.SetColumn(txtboxDefault, 1);
                    gridDefault.Children.Add(txtboxDefault);
                    Grid.SetColumn(gridBound, 1);
                    gridValues.Children.Add(gridBound);
                    break;
                default:
                    break;
            }
        }
    }
}
