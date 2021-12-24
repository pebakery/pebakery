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
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;

// ReSharper disable RedundantAssignment

namespace PEBakery.Core.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        #region OnPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void OnPropertyUpdate([CallerMemberName] string propertyName = null)
        {
            Debug.Assert(propertyName != null);
            Debug.Assert(GetType().GetRuntimeProperty(propertyName) != null);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region SetProperty
        protected virtual void SetProperty<T>(ref T fieldRef, T newValue, [CallerMemberName] string propertyName = null)
        {
            fieldRef = newValue;
            OnPropertyUpdate(propertyName);
        }

        protected virtual void SetCollectionProperty<T>(ref T fieldRef, object lockObject, T newValue, [CallerMemberName] string propertyName = null)
            where T : IEnumerable
        {
            fieldRef = newValue;
            BindingOperations.EnableCollectionSynchronization(fieldRef, lockObject);
            OnPropertyUpdate(propertyName);
        }
        #endregion

        #region RelayCommand
        protected virtual ICommand GetRelayCommand(ref ICommand cmdRef, Action<object> executeFunc, Func<object, bool> canExecuteFunc = null)
        {
            return cmdRef ??= new RelayCommand(executeFunc, canExecuteFunc);
        }

        protected virtual ICommand GetRelayCommand(ref ICommand cmdRef, string text, Action<object> executeFunc, Func<object, bool> canExecuteFunc = null)
        {
            return cmdRef ??= new RelayCommand(text, executeFunc, canExecuteFunc);
        }
        #endregion
    }
}
