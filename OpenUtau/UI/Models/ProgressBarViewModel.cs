using OpenUtau.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OpenUtau.UI.Models
{
    class ProgressBarViewModel : INotifyPropertyChanged, ICmdSubscriber
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        object lockObject = new object();
        Brush _foreground;
        public Brush Foreground { set { _foreground = value; OnPropertyChanged("Foreground"); } get => _foreground;
        }
        public int Progress { set; get; }
        public string Info { set; get; }
        public bool IsIndeterminate { get; set; }

        public void Update(ProgressBarNotification cmd)
        {
            lock (lockObject)
            {
                Info = cmd.Info;
                Progress = cmd.Progress;
                IsIndeterminate = cmd.Progress < 0;
            }
            OnPropertyChanged("Progress");
            OnPropertyChanged("IsIndeterminate");
            OnPropertyChanged("Info");
        }

        public void Subscribe(ICmdPublisher publisher) { if (publisher != null) publisher.Subscribe(this); }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is ProgressBarNotification) Update((ProgressBarNotification)cmd);
        }
    }
}
