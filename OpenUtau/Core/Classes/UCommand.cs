using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;
using System.Windows.Input;

namespace OpenUtau.Core
{
    public abstract class UCommand
    {
        public abstract void Execute();
        public abstract void Unexecute();

        public override abstract string ToString();
    }

    public class UCommandGroup
    {
        public List<UCommand> Commands;
        public UCommandGroup() { Commands = new List<UCommand>(); }
        public override string ToString() { return Commands.Count == 0 ? "No op" : Commands.First().ToString(); }
    }

    public class DelegateCommand : ICommand
    {
        private readonly Predicate<object> _canExecute;
        private readonly Action<object> _execute;

        public event EventHandler CanExecuteChanged;

        public DelegateCommand(Action<object> execute)
            : this(execute, null)
        {
        }

        public DelegateCommand(Action<object> execute,
                       Predicate<object> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            return _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public interface ICmdPublisher
    {
        void Subscribe(ICmdSubscriber subscriber);
        void UnSubscribe(ICmdSubscriber subscriber);
        void Publish(UCommand cmd, bool isUndo);
    }

    public interface ICmdSubscriber
    {
        void Subscribe(ICmdPublisher publisher);
        void OnNext(UCommand cmd, bool isUndo);
    }
}
