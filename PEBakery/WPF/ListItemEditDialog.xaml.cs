/*
    Copyright (C) 2020-2023 Hajin Jang
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
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace PEBakery.WPF
{
    #region ListItemEditDialog
    public partial class ListItemEditDialog : Window
    {
        #region Constructor
        public ListItemEditDialog()
        {
            InitializeComponent();
        }
        #endregion

        #region Event Handlers
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // DialogResult = false;
            if (DataContext is ListItemEditViewModel vm)
            {
                vm.PreRequestClose += ViewModel_PreRequestClose;
                vm.OnRequestClose += ViewModel_OnRequestClose;
            }
        }

        private void ViewModel_PreRequestClose(object? sender, EventArgs e)
        {
            // Get focus from ListViewEditItem, to trigger LostFocus data binding
            ApplyButton.Focus();
        }

        private void ViewModel_OnRequestClose(object? sender, bool e)
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
            _banner = $"[{uiCtrl.Type}] {uiCtrl.Key}";

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
            List<string>? ctrlItems = null;
            int ctrlItemDefault = -1;
            switch (_uiCtrl.Type)
            {
                case UIControlType.ComboBox:
                    {
                        UIInfo_ComboBox info = (UIInfo_ComboBox)_uiCtrl.Info;

                        ctrlItems = info.Items;
                        ctrlItemDefault = info.Index;
                    }
                    break;
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = (UIInfo_RadioGroup)_uiCtrl.Info;

                        ctrlItems = info.Items;
                        ctrlItemDefault = info.Selected;
                    }
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(ListItemEditDialog)} does not support editing [{_uiCtrl.Type}]");
            }

            if (ctrlItems == null)
                throw new InvalidOperationException($"{nameof(ctrlItems)} is null");
            if (ctrlItemDefault == -1)
                throw new InvalidOperationException($"Internal logic error of {nameof(ReadListItems)}");

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
            List<string> ctrlItems = Items.Select(x => x.Value).ToList();
            ListViewEditItem? ctrlItem = Items.Where(i => i.IsDefault == true).FirstOrDefault();
            if (ctrlItem == null)
                throw new InvalidOperationException($"{nameof(ctrlItem)} is null");
            int ctrlItemDefault = Items.IndexOf(ctrlItem);
            if (ctrlItemDefault == -1)
                throw new InvalidOperationException($"Internal logic error of {nameof(WriteListItems)}");

            switch (_uiCtrl.Type)
            {
                case UIControlType.ComboBox:
                    {
                        UIInfo_ComboBox info = (UIInfo_ComboBox)_uiCtrl.Info;

                        info.Items = ctrlItems;
                        info.Index = ctrlItemDefault;
                    }
                    break;
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = (UIInfo_RadioGroup)_uiCtrl.Info;

                        info.Items = ctrlItems;
                        info.Selected = ctrlItemDefault;
                    }
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(ListItemEditDialog)} does not support editing [{_uiCtrl.Type}]");
            }
        }
        #endregion

        #region Commands for ListItemBox
        private ICommand? _listItemAddCommand;
        private ICommand? _listItemInsertCommand;
        private ICommand? _listItemDeleteCommand;
        public ICommand ListItemAddCommand => GetRelayCommand(ref _listItemAddCommand, "Add item", ListItemAddCommand_Execute, ListItemAddCommand_CanExecuteFunc);
        public ICommand ListItemInsertCommand => GetRelayCommand(ref _listItemInsertCommand, "Insert item", ListItemInsertCommand_Execute, ListItemInsertCommand_CanExecuteFunc);
        public ICommand ListItemDeleteCommand => GetRelayCommand(ref _listItemDeleteCommand, "Delete item", ListItemDeleteCommand_Execute, ListItemDeleteCommand_CanExecuteFunc);

        private ICommand? _listItemMoveUpCommand;
        private ICommand? _listItemMoveDownCommand;
        public ICommand ListItemMoveUpCommand => GetRelayCommand(ref _listItemMoveUpCommand, "Move item one step up", ListItemMoveUpCommand_Execute, ListItemMoveUpCommand_CanExecuteFunc);
        public ICommand ListItemMoveDownCommand => GetRelayCommand(ref _listItemMoveDownCommand, "Move item one step down", ListItemMoveDownCommand_Execute, ListItemMoveDownCommand_CanExecuteFunc);



        private bool _canExecuteCommand = true;
        public bool CanExecuteCommand
        {
            get => _canExecuteCommand;
            set => SetProperty(ref _canExecuteCommand, value);
        }

        private bool ListItemAddCommand_CanExecuteFunc(object? parameter)
        {
            return CanExecuteCommand;
        }

        private bool ListItemInsertCommand_CanExecuteFunc(object? parameter)
        {
            return CanExecuteCommand;
        }

        private bool ListItemDeleteCommand_CanExecuteFunc(object? parameter)
        {
            return CanExecuteCommand && 2 <= Items.Count;
        }

        private bool ListItemMoveUpCommand_CanExecuteFunc(object? parameter)
        {
            return CanExecuteCommand && 1 <= SelectedIndex && SelectedIndex < Items.Count;
        }

        private bool ListItemMoveDownCommand_CanExecuteFunc(object? parameter)
        {
            return CanExecuteCommand && 0 <= SelectedIndex && SelectedIndex < Items.Count - 1;
        }

        private void ListItemAddCommand_Execute(object? parameter)
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

        private void ListItemInsertCommand_Execute(object? parameter)
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
                Items.Insert(SelectedIndex, newItem);
                SelectedIndex -= 1;
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ListItemDeleteCommand_Execute(object? parameter)
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

        private void ListItemMoveUpCommand_Execute(object? parameter)
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

        private void ListItemMoveDownCommand_Execute(object? parameter)
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
        private ICommand? _applyCommand;
        public ICommand ApplyCommand => GetRelayCommand(ref _applyCommand, "Apply changes", ApplyCommand_Execute, ApplyCommand_CanExecuteFunc);

        private EventHandler? _preRequestClose;
        public event EventHandler PreRequestClose
        {
            add => _preRequestClose += value;
            remove => _preRequestClose -= value;
        }

        private EventHandler<bool>? _onRequestClose;
        public event EventHandler<bool> OnRequestClose
        {
            add => _onRequestClose += value;
            remove => _onRequestClose -= value;
        }

        private bool ApplyCommand_CanExecuteFunc(object? parameter)
        {
            return CanExecuteCommand && 1 <= Items.Count;
        }

        private void ApplyCommand_Execute(object? parameter)
        {
            _preRequestClose?.Invoke(this, new EventArgs());
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

        private readonly ObservableCollection<ListViewEditItem> _itemList;

        private bool _isDefault = false;
        public bool IsDefault
        {
            get => _isDefault;
            set => SetProperty(ref _isDefault, value);
        }

        // Binding is set to UpdateSourceTrigger=LostFocus, to force dupliacte check happen in the last moment
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
