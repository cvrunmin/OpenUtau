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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace OpenUtau.UI.Controls
{
    /// <summary>
    /// Interaction logic for ExpComboBox.xaml
    /// </summary>
    public partial class ExpComboBox : UserControl
    {
        public event EventHandler Click;
        public event EventHandler SelectionChanged;

        public int SelectedIndex { set => SetValue(SelectedIndexProperty, value);
            get => (int)GetValue(SelectedIndexProperty);
        }
        public ObservableCollection<string> ItemsSource { set => SetValue(ItemsSourceProperty, value);
            get => (ObservableCollection<string>)GetValue(ItemsSourceProperty);
        }
        public Brush TagBrush { set => SetValue(TagBrushProperty, value);
            get => (Brush)GetValue(TagBrushProperty);
        }
        public Brush Highlight { set => SetValue(HighlightProperty, value);
            get => (Brush)GetValue(HighlightProperty);
        }
        public string Text { set => SetValue(TextProperty, value);
            get => (string)GetValue(TextProperty);
        }

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register("SelectedIndex", typeof(int), typeof(ExpComboBox), new PropertyMetadata(0));
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(ObservableCollection<string>), typeof(ExpComboBox));
        public static readonly DependencyProperty TagBrushProperty = DependencyProperty.Register("TagBrush", typeof(Brush), typeof(ExpComboBox), new PropertyMetadata(Brushes.Black));
        public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register("Highlight", typeof(Brush), typeof(ExpComboBox), new PropertyMetadata(Brushes.Black));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(ExpComboBox), new PropertyMetadata(""));

        public ExpComboBox()
        {
            InitializeComponent();
        }

        private void mainGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Click?.Invoke(this, e);
        }

        private void dropList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedIndex < 0 || SelectedIndex >= ItemsSource.Count) return;
            string name = ItemsSource[SelectedIndex];
            string abbr = OpenUtau.Core.DocManager.Inst.Project.ExpressionTable[name].Abbr;
            Text = abbr.Substring(0, Math.Min(3, abbr.Length));
            SelectionChanged?.Invoke(this, e);
        }
    }
}
