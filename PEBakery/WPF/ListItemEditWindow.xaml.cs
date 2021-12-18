﻿/*
    Copyright (C) 2020 Hajin Jang
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

using PEBakery.Core;
using PEBakery.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace PEBakery.WPF
{
    #region ListItemEditWindow
    public partial class ListItemEditWindow : Window
    {
        #region Constructor
        public ListItemEditWindow()
        {
            InitializeComponent();
        }
        #endregion

        #region Event Handlers
        private void ItemValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Prohibit '|', ','
            if (e.Text.Contains('|'))
                e.Handled = true;
            if (e.Text.Contains(','))
                e.Handled = true;

            OnPreviewTextInput(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // DialogResult = false;
            if (DataContext is ListItemEditViewModel vm)
            {
                vm.OnRequestClose += ViewModel_OnRequestClose;
            }
        }

        private void ViewModel_OnRequestClose(object sender, bool e)
        {
            // Setting DialogResult closes the Window
            DialogResult = e;
        }
        #endregion
    }
    #endregion

    #region ListItemEditViewModel
    public class ListItemEditViewModel : ViewModelBase
    {
        #region Constructor
        public ListItemEditViewModel(UIControl uiCtrl)
        {
            _uiCtrl = uiCtrl;
            _banner = $"Item list of {uiCtrl.Type} [{uiCtrl.Key}]";

            // Read list items
            ReadListItems();
        }
        #endregion

        #region Fields and Properties
        private readonly UIControl _uiCtrl;

        private string _banner;
        public string Banner
        {
            get => _banner;
            set => SetProperty(ref _banner, value);
        }

        private ObservableCollection<ListViewEditItem> _items = new ObservableCollection<ListViewEditItem>();
        public ObservableCollection<ListViewEditItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetProperty(ref _selectedIndex, value);
        }
        #endregion

        #region UIControl Read and Write
        public void ReadListItems()
        {
            string internalErrorMsg = $"Internal Logic Error at {nameof(ReadListItems)}";

            List<string> ctrlItems = null;
            int ctrlItemDefault = -1;
            switch (_uiCtrl.Type)
            {
                case UIControlType.ComboBox:
                    {
                        UIInfo_ComboBox info = _uiCtrl.Info.Cast<UIInfo_ComboBox>();
                        Debug.Assert(info != null, internalErrorMsg);

                        ctrlItems = info.Items;
                        ctrlItemDefault = info.Index;
                    }
                    break;
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = _uiCtrl.Info.Cast<UIInfo_RadioGroup>();
                        Debug.Assert(info != null, internalErrorMsg);

                        ctrlItems = info.Items;
                        ctrlItemDefault = info.Selected;
                    }
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(ListItemEditWindow)} does not support [{_uiCtrl.Type}]");
            }

            Debug.Assert(ctrlItems != null, internalErrorMsg);
            Debug.Assert(ctrlItemDefault != -1, internalErrorMsg);

            for (int i = 0; i < ctrlItems.Count; i++)
            {
                string ctrlItem = ctrlItems[i];

                ListViewEditItem editItem = new ListViewEditItem(Items, ctrlItem, i == ctrlItemDefault);
                Items.Add(editItem);
            }
            SelectedIndex = ctrlItemDefault;
        }

        public void WriteListItems()
        {
            string internalErrorMsg = $"Internal Logic Error at {nameof(WriteListItems)}";

            List<string> ctrlItems = Items.Select(x => x.Value).ToList();
            int ctrlItemDefault = SelectedIndex;

            switch (_uiCtrl.Type)
            {
                case UIControlType.ComboBox:
                    {
                        UIInfo_ComboBox info = _uiCtrl.Info.Cast<UIInfo_ComboBox>();
                        Debug.Assert(info != null, internalErrorMsg);

                        info.Items = ctrlItems;
                        info.Index = ctrlItemDefault;
                    }
                    break;
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = _uiCtrl.Info.Cast<UIInfo_RadioGroup>();
                        Debug.Assert(info != null, internalErrorMsg);

                        info.Items = ctrlItems;
                        info.Selected = ctrlItemDefault;
                    }
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(ListItemEditWindow)} does not support [{_uiCtrl.Type}]");
            }
        }
        #endregion

        #region Commands for ListItemBox
        private ICommand _listItemAddCommand;
        private ICommand _listItemDeleteCommand;
        public ICommand ListItemAddCommand => GetRelayCommand(ref _listItemAddCommand, "Add item", ListItemAddCommand_Execute, ListItemAddCommand_CanExecuteFunc);
        public ICommand ListItemDeleteCommand => GetRelayCommand(ref _listItemDeleteCommand, "Delete item", ListItemDeleteCommand_Execute, ListItemDeleteCommand_CanExecuteFunc);

        private ICommand _listItemMoveUpCommand;
        private ICommand _listItemMoveDownCommand;
        public ICommand ListItemMoveUpCommand => GetRelayCommand(ref _listItemMoveUpCommand, "Move item one step up", ListItemMoveUpCommand_Execute, ListItemMoveUpCommand_CanExecuteFunc);
        public ICommand ListItemMoveDownCommand => GetRelayCommand(ref _listItemMoveDownCommand, "Move item one step down", ListItemMoveDownCommand_Execute, ListItemMoveDownCommand_CanExecuteFunc);

        

        private bool _canExecuteCommand = true;
        public bool CanExecuteCommand
        {
            get => _canExecuteCommand;
            set => SetProperty(ref _canExecuteCommand, value);
        }

        private bool ListItemAddCommand_CanExecuteFunc(object parameter)
        {
            return CanExecuteCommand;
        }

        private bool ListItemDeleteCommand_CanExecuteFunc(object parameter)
        {
            return CanExecuteCommand && 2 <= Items.Count;
        }

        private bool ListItemMoveUpCommand_CanExecuteFunc(object arg)
        {
            return CanExecuteCommand && 1 <= SelectedIndex && SelectedIndex < Items.Count;
        }

        private bool ListItemMoveDownCommand_CanExecuteFunc(object arg)
        {
            return CanExecuteCommand && 0 <= SelectedIndex && SelectedIndex < Items.Count - 1;
        }

        private void ListItemAddCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                // Prevent duplicated value
                int postfix = Items.Count + 1;
                string newValue;
                do
                {
                    newValue = $"Item{postfix:00}";
                    if (Items.Any(x => x.Value.Equals(newValue, StringComparison.OrdinalIgnoreCase)) == false)
                        break;
                    
                    postfix += 1;
                }
                while (true);

                ListViewEditItem newItem = new ListViewEditItem(Items, newValue, false);
                Items.Add(newItem);
                SelectedIndex = Items.Count - 1;
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ListItemDeleteCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                // Indexing fail-safe code
                if (!(0 <= SelectedIndex && SelectedIndex < Items.Count))
                    return;
                if (Items.Count <= 1)
                    return;

                int indexToDelete = SelectedIndex;
                
                // Delete selected item
                // If RemoveAt deletes current selected index, SelectedIndex becomes -1
                Items.RemoveAt(indexToDelete);

                // Change default selected item
                if (SelectedIndex == -1) // deleted by RemoveAt
                    SelectedIndex = 0;
                else if (SelectedIndex == Items.Count - 1) // last one
                    SelectedIndex -= 1;

                // Set new default index
                Items[SelectedIndex].IsDefault = true;
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ListItemMoveUpCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                // Indexing fail-safe code
                if (!(1 <= SelectedIndex && SelectedIndex < Items.Count))
                    return;

                Items.Move(SelectedIndex, SelectedIndex - 1);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ListItemMoveDownCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                // Indexing fail-safe code
                if (!(0 <= SelectedIndex && SelectedIndex < Items.Count - 1))
                    return;

                Items.Move(SelectedIndex, SelectedIndex + 1);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        #endregion

        #region Command/Event for Apply Button
        private ICommand _applyCommand;
        public ICommand ApplyCommand => GetRelayCommand(ref _applyCommand, "Apply changes", ApplyCommand_Execute, ApplyCommand_CanExecuteFunc);

        private EventHandler<bool> _onRequestClose;
        public event EventHandler<bool> OnRequestClose
        {
            add => _onRequestClose += value;
            remove => _onRequestClose -= value;
        }

        private bool ApplyCommand_CanExecuteFunc(object parameter)
        {
            return CanExecuteCommand && 1 <= Items.Count;
        }

        private void ApplyCommand_Execute(object parameter)
        {
            WriteListItems();
            _onRequestClose?.Invoke(this, true);
        }
        #endregion
    }
    #endregion

    #region ListViewEditItem
    public class ListViewEditItem : ViewModelBase
    {
        public ListViewEditItem(ObservableCollection<ListViewEditItem> itemList, string value, bool isDefault = false)
        {
            _itemList = itemList;
            Value = value;
            IsDefault = isDefault;
        }

        private ObservableCollection<ListViewEditItem> _itemList;

        private bool _isDefault = false;
        public bool IsDefault
        {
            get => _isDefault;
            set => SetProperty(ref _isDefault, value);
        }

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            { // This code is only called when the TextBox lost focus.
                // Check for duplicated value
                string setValue = PreventDuplicateValue(value);
                SetProperty(ref _value, setValue);
            }
        }

        private string PreventDuplicateValue(string setValue)
        {
            // Prevent duplicated value
            int postfix = 1;
            string newValue = setValue;
            do
            {
                if (_itemList.Any(x => x.Value.Equals(newValue, StringComparison.OrdinalIgnoreCase)) == false)
                    break;

                postfix += 1;
                newValue = $"{setValue} ({postfix})";
            }
            while (true);

            return newValue;
        }
    }
    #endregion 
}
