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
    /// CheatCLPDialog.xaml 的互動邏輯
    /// </summary>
    public partial class CheatCLPDialog : Window
    {
        public CheatCLPDialog()
        {
            InitializeComponent();
        }

        private void butOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void butCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
