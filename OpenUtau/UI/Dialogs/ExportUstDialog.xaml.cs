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
    /// ExportUstDialog.xaml 的互動邏輯
    /// </summary>
    public partial class ExportUstDialog : Window
    {
        public bool ExportTrack => comboExportU.SelectedIndex == 1;

        public ExportUstDialog()
        {
            InitializeComponent();
        }

        private void chkboxPosAsRest_Click(object sender, RoutedEventArgs e)
        {
            ChangePartsCanvas();
        }

        private void comboExportU_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChangePartsCanvas();
        }

        private void ChangePartsCanvas() {
            if (!IsInitialized) return;
            if (chkboxPosAsRest.IsChecked.Value || comboExportU.SelectedIndex == 1)
            {
                Canvas.SetLeft(rectT2P1, 10);
                rectT2P1.Width = 130;
                Canvas.SetLeft(rectT2P2, 10);
                rectT2P2.Width = 220;
                Canvas.SetLeft(rectT3P1, 10);
                rectT3P1.Width = 230;
                Canvas.SetLeft(rectT4P2, 10);
                rectT4P2.Width = 220;
            }
            else
            {
                Canvas.SetLeft(rectT2P1, 85);
                rectT2P1.Width = 55;
                Canvas.SetLeft(rectT2P2, 160);
                rectT2P2.Width = 70;
                Canvas.SetLeft(rectT3P1, 80);
                rectT3P1.Width = 160;
                Canvas.SetLeft(rectT4P2, 170);
                rectT4P2.Width = 60;
            }
            if (comboExportU.SelectedIndex == 1)
            {
                rectT2P1.Visibility = Visibility.Hidden;
                rectT4P1.Visibility = Visibility.Hidden;
            }
            else
            {
                rectT2P1.Visibility = Visibility.Visible;
                rectT4P1.Visibility = Visibility.Visible;
            }
        }

        private void butCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void butOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ChangePartsCanvas();
        }
    }
}
