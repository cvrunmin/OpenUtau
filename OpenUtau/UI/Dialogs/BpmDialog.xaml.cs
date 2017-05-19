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
        public BpmDialog()
        {
            InitializeComponent();
        }

        private void butOk_Click(object sender, RoutedEventArgs e)
        {
            DocManager.Inst.ExecuteCmd(new UpdateProjectPropertiesNotification(DocManager.Inst.Project, Bpm, BeatPerBar, BeatUnit));
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
    }
}
