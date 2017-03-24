using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenUtau.Core;
using OpenUtau.Core.USTx;
using OpenUtau.UI.Controls;
using System.ComponentModel;
using System.Windows.Controls;

namespace OpenUtau.UI.Models
{
    class OtoViewModel : INotifyPropertyChanged, ICmdSubscriber
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Canvas otoCanvas;

        public UOto oto;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            throw new NotImplementedException();
        }

        public void Subscribe(ICmdPublisher publisher)
        {
            if (publisher != null) publisher.Subscribe(this);
        }
    }
}
