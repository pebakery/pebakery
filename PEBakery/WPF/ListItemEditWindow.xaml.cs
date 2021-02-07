/*
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
        private void ItemValue_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Prohibit '|', ','
            if (e.Text.Contains('|'))
                e.Handled = true;
            if (e.Text.Contains(','))
                e.Handled = true;

            OnPreviewTextInput(e);
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
            _banner = $"Item list of Control [{uiCtrl.Key}]";
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

        #region Commands for ListItemBox
        private ICommand _listItemAddCommand;
        private ICommand _listItemEditCommand;
        private ICommand _listItemDeleteCommand;
        public ICommand ListItemBoxAddCommand => GetRelayCommand(ref _listItemAddCommand, "Add item", ListItemBoxAddCommand_Execute, ListItemBoxAddCommand_CanExecuteFunc);
        public ICommand ListItemBoxEditCommand => GetRelayCommand(ref _listItemEditCommand, "Edit item", ListItemEditCommand_Execute, ListItemBoxEditCommand_CanExecuteFunc);
        public ICommand ListItemBoxDeleteCommand => GetRelayCommand(ref _listItemDeleteCommand, "Delete item", ListItemDeleteCommand_Execute);

        private ICommand _listItemBoxMoveUpCommand;
        private ICommand _listItemBoxMoveDownCommand;
        public ICommand ListItemBoxMoveUpCommand => GetRelayCommand(ref _listItemBoxMoveUpCommand, "Move item one step up", ListItemBoxMoveUpCommand_Execute);
        public ICommand ListItemBoxMoveDownCommand => GetRelayCommand(ref _listItemBoxMoveDownCommand, "Move item one step down", ListItemBoxMoveDownCommand_Execute);

        private bool _canExecuteCommand;
        public bool CanExecuteCommand
        {
            get => _canExecuteCommand;
            set => SetProperty(ref _canExecuteCommand, value);
        }

        private bool ListItemBoxAddCommand_CanExecuteFunc(object parameter)
        {
            return CanExecuteCommand;
        }

        private bool ListItemBoxEditCommand_CanExecuteFunc(object parameter)
        {
            return CanExecuteCommand && SelectedIndex != -1 && SelectedIndex < Items.Count;
        }

        private void ListItemBoxAddCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                string internalErrorMsg = $"Internal Logic Error at {nameof(ListItemBoxAddCommand_Execute)}";

                Debug.Assert(_uiCtrl != null, internalErrorMsg);
                Debug.Assert(SelectedIndex < Items.Count, internalErrorMsg);

                ListViewEditItem item = Items[SelectedIndex];


                /*
                string newItem = Items;
                switch (_uiCtrl.Type)
                {
                    case UIControlType.ComboBox:
                        Debug.Assert(UICtrlComboBoxInfo != null, internalErrorMsg);
                        UICtrlComboBoxInfo.Items.Add(newItem);
                        break;
                    case UIControlType.RadioGroup:
                        Debug.Assert(UICtrlRadioGroupInfo != null, internalErrorMsg);
                        UICtrlRadioGroupInfo.Items.Add(newItem);
                        break;
                    default:
                        throw new InvalidOperationException(internalErrorMsg);
                }
                UICtrlListItemBoxItems.Add(newItem);
                */
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }

            
        }

        private void ListItemEditCommand_Execute(object parameter)
        {
            /*
            switch (_uiCtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(_uiCtrlComboBoxInfo != null, internalErrorMsg);
                    Debug.Assert(UICtrlComboBoxInfo.Items.Count == UICtrlListItemBoxItems.Count, internalErrorMsg);
                    UICtrlComboBoxInfo.Index = UICtrlListItemBoxSelectedIndex;
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(UICtrlRadioGroupInfo != null, internalErrorMsg);
                    Debug.Assert(UICtrlRadioGroupInfo.Items.Count == UICtrlListItemBoxItems.Count, internalErrorMsg);
                    UICtrlRadioGroupInfo.Selected = UICtrlListItemBoxSelectedIndex;
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }
            */

            CanExecuteCommand = false;
            try
            {
                string internalErrorMsg = $"Internal Logic Error at {nameof(ListItemEditCommand_Execute)}";

                Debug.Assert(_uiCtrl != null, internalErrorMsg);
                Debug.Assert(SelectedIndex < Items.Count, internalErrorMsg);

                ListViewEditItem item = Items[SelectedIndex];

                // Switch to edit mode
                item.ViewModeSwitch = ItemViewEditSwitch.ItemEdit;



                /*
                string newItem = Items;
                switch (_uiCtrl.Type)
                {
                    case UIControlType.ComboBox:
                        Debug.Assert(UICtrlComboBoxInfo != null, internalErrorMsg);
                        UICtrlComboBoxInfo.Items.Add(newItem);
                        break;
                    case UIControlType.RadioGroup:
                        Debug.Assert(UICtrlRadioGroupInfo != null, internalErrorMsg);
                        UICtrlRadioGroupInfo.Items.Add(newItem);
                        break;
                    default:
                        throw new InvalidOperationException(internalErrorMsg);
                }
                UICtrlListItemBoxItems.Add(newItem);
                */
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ListItemDeleteCommand_Execute(object parameter)
        {
            /*
            string internalErrorMsg = $"Internal Logic Error at {nameof(ListItemDeleteCommand_Execute)}";

            List<string> items;
            switch (SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(UICtrlComboBoxInfo != null, internalErrorMsg);
                    items = UICtrlComboBoxInfo.Items;
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(UICtrlRadioGroupInfo != null, internalErrorMsg);
                    items = UICtrlRadioGroupInfo.Items;
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }
            Debug.Assert(items.Count == UICtrlListItemBoxItems.Count, internalErrorMsg);

            if (items.Count < 2)
            {
                string errMsg = $"{SelectedUICtrl.Type} [{SelectedUICtrl.Key}] must contain at least one item";
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Cannot Delete Value: {errMsg}"));
                MessageBox.Show(errMsg + '.', "Cannot Delete Value", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int idx = UICtrlListItemBoxSelectedIndex;

            items.RemoveAt(idx);
            UICtrlListItemBoxItems.RemoveAt(idx);

            switch (SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    if (UICtrlComboBoxInfo.Index == idx)
                        UICtrlComboBoxInfo.Index = 0;
                    break;
                case UIControlType.RadioGroup:
                    if (UICtrlRadioGroupInfo.Selected == idx)
                        UICtrlRadioGroupInfo.Selected = 0;
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }

            UICtrlListItemBoxSelectedIndex = 0;
            InvokeUIControlEvent(false);
            */
        }

        private void ListItemBoxMoveUpCommand_Execute(object parameter)
        {
            /*
            string internalErrorMsg = $"Internal Logic Error at {nameof(ListItemBoxMoveUpCommand_Execute)}";

            Debug.Assert(SelectedUICtrl != null, internalErrorMsg);
            List<string> items;
            switch (SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(UICtrlComboBoxInfo != null, internalErrorMsg);
                    items = UICtrlComboBoxInfo.Items;
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(UICtrlRadioGroupInfo != null, internalErrorMsg);
                    items = UICtrlRadioGroupInfo.Items;
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }
            Debug.Assert(items.Count == UICtrlListItemBoxItems.Count, internalErrorMsg);

            int idx = UICtrlListItemBoxSelectedIndex;
            if (0 < idx)
            {
                string item = items[idx];
                items.RemoveAt(idx);
                items.Insert(idx - 1, item);

                string editItem = UICtrlListItemBoxItems[idx];
                UICtrlListItemBoxItems.RemoveAt(idx);
                UICtrlListItemBoxItems.Insert(idx - 1, editItem);

                switch (SelectedUICtrl.Type)
                {
                    case UIControlType.ComboBox:
                        if (UICtrlComboBoxInfo.Index == idx)
                            UICtrlComboBoxInfo.Index = idx - 1;
                        break;
                    case UIControlType.RadioGroup:
                        if (UICtrlRadioGroupInfo.Selected == idx)
                            UICtrlRadioGroupInfo.Selected = idx - 1;
                        break;
                    default:
                        throw new InvalidOperationException(internalErrorMsg);
                }

                UICtrlListItemBoxSelectedIndex = idx - 1;
                InvokeUIControlEvent(false);
            }
            */
        }

        private void ListItemBoxMoveDownCommand_Execute(object parameter)
        {
            /*
            string internalErrorMsg = $"Internal Logic Error at {nameof(ListItemBoxMoveDownCommand_Execute)}";

            Debug.Assert(SelectedUICtrl != null, internalErrorMsg);
            List<string> items;
            switch (SelectedUICtrl.Type)
            {
                case UIControlType.ComboBox:
                    Debug.Assert(UICtrlComboBoxInfo != null, internalErrorMsg);
                    items = UICtrlComboBoxInfo.Items;
                    Debug.Assert(items.Count == UICtrlListItemBoxItems.Count, internalErrorMsg);
                    break;
                case UIControlType.RadioGroup:
                    Debug.Assert(UICtrlRadioGroupInfo != null, internalErrorMsg);
                    items = UICtrlRadioGroupInfo.Items;
                    Debug.Assert(items.Count == UICtrlListItemBoxItems.Count, internalErrorMsg);
                    break;
                default:
                    throw new InvalidOperationException(internalErrorMsg);
            }

            int idx = UICtrlListItemBoxSelectedIndex;
            if (idx + 1 < items.Count)
            {
                string item = items[idx];
                items.RemoveAt(idx);
                items.Insert(idx + 1, item);

                string editItem = UICtrlListItemBoxItems[idx];
                UICtrlListItemBoxItems.RemoveAt(idx);
                UICtrlListItemBoxItems.Insert(idx + 1, editItem);

                switch (SelectedUICtrl.Type)
                {
                    case UIControlType.ComboBox:
                        if (UICtrlComboBoxInfo.Index == idx)
                            UICtrlComboBoxInfo.Index = idx + 1;
                        break;
                    case UIControlType.RadioGroup:
                        if (UICtrlRadioGroupInfo.Selected == idx)
                            UICtrlRadioGroupInfo.Selected = idx + 1;
                        break;
                    default:
                        throw new InvalidOperationException(internalErrorMsg);
                }

                UICtrlListItemBoxSelectedIndex = idx + 1;
                InvokeUIControlEvent(false);
            }
            */
        }
        #endregion
    }
    #endregion

    #region ListViewEditItem
    public class ListViewEditItem : ViewModelBase
    {
        #region Constructor
        public ListViewEditItem()
        {

        }
        #endregion

        #region Property
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
            set => SetProperty(ref _value, value);
        }

        private ItemViewEditSwitch _viewModeSwitch = ItemViewEditSwitch.ItemView;
        public ItemViewEditSwitch ViewModeSwitch
        {
            get => _viewModeSwitch;
            set
            {
                SetProperty(ref _viewModeSwitch, value);
                OnPropertyUpdate(nameof(ItemViewVisibility));
                OnPropertyUpdate(nameof(ItemEditVisibility));
            }
        }

        public Visibility ItemViewVisibility
        {
            get
            {
                switch (ViewModeSwitch)
                {
                    case ItemViewEditSwitch.ItemView:
                    default:
                        return Visibility.Visible;
                    case ItemViewEditSwitch.ItemEdit:
                        return Visibility.Hidden;
                }
            }
        }

        public Visibility ItemEditVisibility
        {
            get
            {
                switch (ViewModeSwitch)
                {
                    case ItemViewEditSwitch.ItemView:
                    default:
                        return Visibility.Hidden;
                    case ItemViewEditSwitch.ItemEdit:
                        return Visibility.Visible;
                }
            }
        }
        #endregion

        #region Commands for ListViewEditItem
        private ICommand _editToViewCommand;
        private ICommand _viewToEditCommand;
        /// <summary>
        /// Switch to view mode from edit mode
        /// </summary>
        public ICommand EditToViewCommand => GetRelayCommand(ref _editToViewCommand, "Add item", EditToViewCommand_Execute, Command_CanExecuteFunc);
        /// <summary>
        /// Switch to edit mode from view mode
        /// </summary>
        public ICommand ViewToEditCommand => GetRelayCommand(ref _viewToEditCommand, "Add item", ViewToEditCommand_Execute, Command_CanExecuteFunc);

        private bool _canExecuteCommand;
        public bool CanExecuteCommand
        {
            get => _canExecuteCommand;
            set => SetProperty(ref _canExecuteCommand, value);
        }

        private bool Command_CanExecuteFunc(object parameter)
        {
            return CanExecuteCommand;
        }

        private void EditToViewCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                ViewModeSwitch = ItemViewEditSwitch.ItemView;
                // 
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ViewToEditCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                ViewModeSwitch = ItemViewEditSwitch.ItemEdit;
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion
    }
    #endregion 
}
