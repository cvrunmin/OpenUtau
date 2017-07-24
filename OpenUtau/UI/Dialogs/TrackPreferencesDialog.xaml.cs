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

namespace OpenUtau.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for Preferences.xaml
    /// </summary>
    public partial class TrackPreferencesDialog : Window
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
        public UTrack Track { get; set; }
        List<string> singerPaths;
        public TrackPreferencesDialog(UTrack track)
        {
            Track = track;
            InitializeComponent();

            generalItem.IsSelected = true;
            UpdateEngines();
        }


        private void treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (treeView.SelectedItem == playbackItem) SelectedGrid = playbackGrid;
            else if (treeView.SelectedItem == renderingItem) SelectedGrid = renderingGrid;
            else if (treeView.SelectedItem == generalItem) SelectedGrid = generalGrid;
            else SelectedGrid = null;
        }

        # region Engine select

        List<string> engines;

        private void UpdateEngines()
        {

            var enginesInfo = Core.ResamplerDriver.ResamplerDriver.SearchEngines(PathManager.Inst.GetEngineSearchPath());
            engines = enginesInfo.Select(x => x.Name).ToList();
            if (engines.Count == 0)
            {
                this.previewRatioInternal.IsChecked = true;
                this.previewRatioExternal.IsEnabled = false;
                this.previewEngineCombo.IsEnabled = false;
            }
            else
            {
                this.previewEngineCombo.ItemsSource = engines;
                previewEngineCombo.SelectedIndex = Math.Max(0, engines.IndexOf(Track.OverrideRenderEngine));
            }
            if (string.IsNullOrWhiteSpace(Track.OverrideRenderEngine)) this.previewRatioInternal.IsChecked = true;
            else this.previewRatioExternal.IsChecked = true;
        }

        private void previewEngine_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == this.previewRatioInternal) {
                Track.OverrideRenderEngine = "";
            }
            else
            {
                Track.OverrideRenderEngine = engines[this.previewEngineCombo.SelectedIndex];
            }
        }

        private void previewEngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Track.OverrideRenderEngine = engines[this.previewEngineCombo.SelectedIndex];
        }

        #endregion

    }
}
