using OpenUtau.Core;
using OpenUtau.Core.Render;
using System;
using System.Collections.Generic;
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
    /// ClearCacheDialog.xaml 的互動邏輯
    /// </summary>
    public partial class ClearCacheDialog : Window
    {
        public ClearCacheDialog()
        {
            InitializeComponent();
        }

        private void butCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void butOk_Click(object sender, RoutedEventArgs e)
        {
            if (chkboxRamCache.IsChecked.Value) {
                RenderCache.Inst.Clear();
                RenderDispatcher.Inst.trackCache.ForEach(pair => pair.Baked?.Close());
                RenderDispatcher.Inst.trackCache.Clear();
                RenderDispatcher.Inst.partCache.Clear();
            }
            if (chkboxdiskCache.IsChecked.Value) {
                if (!string.IsNullOrEmpty(DocManager.Inst.Project.FilePath)) {
                    string path = Path.Combine(Path.GetDirectoryName(DocManager.Inst.Project.FilePath), "UCache");
                    if(Directory.Exists(path))
                    Directory.Delete(path, true);
                }
            }
            if (chkboxVbCache.IsChecked.Value) {
                if(Directory.Exists(SoundbankCache.CachePath))
                Directory.Delete(SoundbankCache.CachePath, true);
            }
            DialogResult = true;
        }
    }
}
