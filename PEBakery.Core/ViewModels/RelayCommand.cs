/*
    Copyright (C) 2018-2022 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System;
using System.Windows.Input;

namespace PEBakery.Core.ViewModels
{
    public class RelayCommand : ICommand
    {
        #region Fields and Properties
        private readonly Action<object?> _executeAction;
        private readonly Func<object?, bool>? _canExecuteFunc;

        /// <summary>
        /// Description for a command
        /// </summary>
        public string Text { get; set; }
        #endregion

        #region Constructor
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _executeAction = execute;
            _canExecuteFunc = canExecute;
            Text = string.Empty;
        }

        public RelayCommand(string text, Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _executeAction = execute;
            _canExecuteFunc = canExecute;
            Text = text;
        }
        #endregion

        #region ICommand Methods
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecuteFunc == null || _canExecuteFunc(parameter);
        }

        public void Execute(object? parameter)
        {
            _executeAction(parameter);
        }
        #endregion
    }
}
