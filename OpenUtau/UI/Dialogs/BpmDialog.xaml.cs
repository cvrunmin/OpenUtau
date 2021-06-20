using OpenUtau.Core;
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

namespace OpenUtau.UI.Dialogs
{
    /// <summary>
    /// BpmDialog.xaml 的互動邏輯
    /// </summary>
    public partial class BpmDialog : Window
    {
        public double Bpm { get; set; }
        public int BeatPerBar { get; set; }
        public int BeatUnit { get; set; }
        public int TickLoc { get; set; }
        public bool SubBpm { get; set; }
        public BpmDialog()
        {
            InitializeComponent();
        }

        private void butOk_Click(object sender, RoutedEventArgs e)
        {
            if (SubBpm)
            {
                DocManager.Inst.ExecuteCmd(new UpdateProjectBpmsNotification(DocManager.Inst.Project, Bpm, TickLoc,
                    (Bpm == DocManager.Inst.Project.BPM &&
                    DocManager.Inst.Project.SubBPM.LastOrDefault(pair => pair.Key < TickLoc).Value == 0)
                    ^ (Bpm == DocManager.Inst.Project.SubBPM.LastOrDefault(pair => pair.Key < TickLoc).Value &&
                    DocManager.Inst.Project.SubBPM.LastOrDefault(pair => pair.Key < TickLoc).Value != 0)));
            }
            else
            {
                DocManager.Inst.ExecuteCmd(new UpdateProjectPropertiesNotification(DocManager.Inst.Project, Bpm, BeatPerBar, BeatUnit));
            }

            DialogResult = true;
            Close();
        }

        private void butCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        internal void ForceUpdateTextBox()
        {
            foreach (var item in gridProp.Children)
            {
                if (item is TextBox textbox)
                {
                    textbox.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                }
            }
        }

        private void window_Loaded(object sender, RoutedEventArgs e)
        {
            lblts.Visibility = SubBpm ? Visibility.Collapsed : Visibility.Visible;
            txtboxBeatPerBar.Visibility = SubBpm ? Visibility.Collapsed : Visibility.Visible;
            comboBeatUnit.Visibility = SubBpm ? Visibility.Collapsed : Visibility.Visible;
            ForceUpdateTextBox();
            comboBeatUnit.ItemsSource = new[] { 2,4,8,16 };
            comboBeatUnit.SelectedIndex = (int)Math.Log(BeatUnit, 2) - 1;
        }

        private void comboBeatUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BeatUnit = (int)comboBeatUnit.SelectedItem;
        }
    }
}
