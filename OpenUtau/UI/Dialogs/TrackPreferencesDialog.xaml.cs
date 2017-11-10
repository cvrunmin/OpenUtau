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

            var singers = new Dictionary<string, USinger>
            {
                { "No Singer", new USinger(){ Name = "No Singer", Loaded = true } }
            };
            comboSinger.ItemsSource = singers.Concat(DocManager.Inst.Singers).ToDictionary(pair=>pair.Key,pair=>pair.Value).Values;
            comboSinger.SelectedItem = Track.Singer;
            generalItem.IsSelected = true;
            UpdateEngines();
        }


        private void treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (treeView.SelectedItem == renderingItem) SelectedGrid = renderingGrid;
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
                if (string.IsNullOrWhiteSpace(Track.OverrideRenderEngine))
                {
                    previewRatioInternal.IsChecked = true;
                }
                else
                {
                    previewRatioExternal.IsChecked = true;
                    previewEngineCombo.SelectedIndex = Math.Max(0, engines.IndexOf(Track.OverrideRenderEngine));
                }
            }

        }

        private void previewEngine_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == this.previewRatioInternal) {
                Track.OverrideRenderEngine = "";
            }
            else if(previewEngineCombo.SelectedIndex != -1)
            {
                Track.OverrideRenderEngine = engines[this.previewEngineCombo.SelectedIndex];
            }
        }

        private void previewEngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Track.OverrideRenderEngine = engines[this.previewEngineCombo.SelectedIndex];
        }

        #endregion

        private void comboSinger_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DocManager.Inst.StartUndoGroup();
            DocManager.Inst.ExecuteCmd(new TrackChangeSingerCommand(DocManager.Inst.Project, Track, comboSinger.SelectedIndex == 0 ? null : comboSinger.SelectedItem as USinger));
            DocManager.Inst.EndUndoGroup();
        }

        private void butTrackColor_Click(object sender, RoutedEventArgs e)
        {
            var color = new System.Windows.Forms.ColorDialog();
            color.ShowDialog();
            Track.Color = color.Color.ToMediaColor();
        }
    }
}
