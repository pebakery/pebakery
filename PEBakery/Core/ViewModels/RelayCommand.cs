using System;
using System.Windows.Input;

namespace PEBakery.Core.ViewModels
{
    public class RelayCommand : ICommand
    {
        #region Fields
        private readonly Action<object> _executeAction;
        private readonly Func<object, bool> _canExecuteFunc;
        #endregion

        #region Constructor
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _executeAction = execute;
            _canExecuteFunc = canExecute;
        }
        #endregion

        #region ICommand
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecuteFunc == null || _canExecuteFunc(parameter);
        }

        public void Execute(object parameter)
        {
            _executeAction(parameter);
        }
        #endregion
    }
}
