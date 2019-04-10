/*
    Copyright (C) 2018-2019 Hajin Jang
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

using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PEBakery.Core;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using PEBakery.Ini;
using PEBakery.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// ReSharper disable InconsistentNaming
namespace PEBakery.WPF
{
    #region ScriptEditWindow
    // ReSharper disable once RedundantExtendsListEntry
    public partial class ScriptEditWindow : Window
    {
        #region Field and Property
        public static int Count = 0;

        private readonly ScriptEditViewModel m;
        #endregion

        #region Constructor
        public ScriptEditWindow(Script sc, MainViewModel mainViewModel)
        {
            Interlocked.Increment(ref Count);

            try
            {
                DataContext = m = new ScriptEditViewModel(sc, this, mainViewModel);

                InitializeComponent();

                m.InterfaceCanvas.UIControlSelected += InterfaceCanvas_UIControlSelected;
                m.InterfaceCanvas.UIControlMoved += InterfaceCanvas_UIControlDragged;
                m.InterfaceCanvas.UIControlResized += InterfaceCanvas_UIControlDragged;
                m.UIControlModified += ViewModel_UIControlModified;

                m.ReadScriptGeneral();
                m.ReadScriptInterface(true);
                m.ReadScriptAttachment();
            }
            catch (Exception e)
            { // Rollback Count to 0
                Interlocked.Decrement(ref Count);

                Global.Logger.SystemWrite(new LogInfo(LogState.CriticalError, e));
                MessageBox.Show(this, $"[Error Message]\r\n{Logger.LogExceptionMessage(e)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Window Event
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            bool scriptSaved = false;
            if (m.ScriptHeaderNotSaved)
            {
                switch (MessageBox.Show(this, "The script header was modified.\r\n\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation))
                {
                    case MessageBoxResult.Yes:
                        if (m.WriteScriptGeneral(false))
                        {
                            scriptSaved = true;
                        }
                        else
                        {
                            e.Cancel = true; // Error while saving, do not close ScriptEditWindow
                            return;
                        }
                        break;
                    case MessageBoxResult.No:
                        // Cancel updated changes
                        m.ScriptHeaderUpdated = false;
                        break;
                    default:
                        throw new InvalidOperationException("Internal Logic Error at ScriptEditWindow.CloseButton_Click");
                }
            }

            if (m.InterfaceNotSaved)
            {
                switch (MessageBox.Show(this, "The interface was modified.\r\n\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation))
                {
                    case MessageBoxResult.Yes:
                        // Do not use e.Cancel here, when script file is moved the method will always fail
                        if (m.WriteScriptInterface(m.SelectedInterfaceSectionName, false))
                            scriptSaved = true;
                        break;
                    case MessageBoxResult.No:
                        // Cancel updated changes
                        m.InterfaceUpdated = false;
                        break;
                    default:
                        throw new InvalidOperationException("Internal Logic Error at ScriptEditWindow.CloseButton_Click");
                }
            }

            if (scriptSaved)
                m.RefreshMainWindow();

            // If script was updated, force MainWindow to refresh script
            DialogResult = m.ScriptHeaderUpdated || m.ScriptLogoUpdated || m.InterfaceUpdated || m.ScriptAttachUpdated;

            Tag = m.Script;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            m.InterfaceCanvas.UIControlSelected -= InterfaceCanvas_UIControlSelected;
            m.InterfaceCanvas.UIControlMoved -= InterfaceCanvas_UIControlDragged;
            m.InterfaceCanvas.UIControlResized -= InterfaceCanvas_UIControlDragged;
            m.UIControlModified -= ViewModel_UIControlModified;

            m.Renderer.Clear();
            Interlocked.Decrement(ref Count);
            CommandManager.InvalidateRequerySuggested();
        }
        #endregion

        #region Event Handler - Interface
        #region For Editor
        private void ScaleFactor_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            m.DrawScript();
        }

        private async void ActiveSectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Run only if selected interface section is different from active interface section
            if (m.SelectedInterfaceSectionName == null ||
                m.SelectedInterfaceSectionName.Equals(m.InterfaceSectionName, StringComparison.OrdinalIgnoreCase))
                return;

            m.CanExecuteCommand = false;
            try
            {
                // Must save current edits to switch active interface section
                MessageBoxResult result = MessageBox.Show(this, "The script must be saved before switching to another interface.\r\n\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                {
                    await m.WriteScriptInterfaceAsync(m.SelectedInterfaceSectionName, false);
                }
                else
                {
                    // Keep current active interface section in ComboBox
                    m.SelectedInterfaceSectionName = m.InterfaceSectionName;
                    return;
                }

                m.ReadScriptInterface(false);
            }
            finally
            {
                m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void UIControlComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m.InterfaceUICtrlIndex < 0 || m.Renderer.UICtrls.Count <= m.InterfaceUICtrlIndex)
                return;

            m.SelectedUICtrl = m.Renderer.UICtrls[m.InterfaceUICtrlIndex];
            m.InterfaceCanvas.ClearSelectedElements(true);
            m.InterfaceCanvas.DrawSelectedElement(m.SelectedUICtrl);
        }

        private void InterfaceCanvas_UIControlSelected(object sender, DragCanvas.UIControlSelectedEventArgs e)
        {
            if (e.UIControl == null && e.UIControls == null)
            { // Reset
                m.SelectedUICtrl = null;
                m.SelectedUICtrls = null;
                m.InterfaceUICtrlIndex = -1;
            }
            else if (e.MultiSelect)
            {
                m.SelectedUICtrls = e.UIControls;
                m.InterfaceUICtrlIndex = -1;
            }
            else
            {
                m.SelectedUICtrl = e.UIControl;
                m.ReadUIControlInfo(m.SelectedUICtrl);

                int idx = m.Renderer.UICtrls.FindIndex(x => x.Key.Equals(e.UIControl.Key));
                Debug.Assert(idx != -1, "Internal Logic Error at ViewModel_UIControlSelected");
                m.InterfaceUICtrlIndex = idx;
            }
        }

        private void InterfaceCanvas_UIControlDragged(object sender, DragCanvas.UIControlDraggedEventArgs e)
        {
            if (!e.MultiSelect)
            {
                // m.SelectedUICtrl should have been set to e.UIControl by InterfaceCanvas_UIControlSelected
                Debug.Assert(m.SelectedUICtrl == e.UIControl, "Incorrect m.SelectedUICtrl");
            }

            if (e.ForceUpdate || 5 <= Math.Abs(e.DeltaX) || 5 <= Math.Abs(e.DeltaY))
                m.InvokeUIControlEvent(true);
        }

        private void ViewModel_UIControlModified(object sender, ScriptEditViewModel.UIControlModifiedEventArgs e)
        {
            if (e.MultiSelect)
            {
                m.InterfaceCanvas.ClearSelectedElements(true);
                m.Renderer.Render();
                m.InterfaceCanvas.DrawSelectedElements(e.UIControls);
            }
            else
            {
                UIControl uiCtrl = e.UIControl;

                if (!e.InfoNotUpdated)
                    m.WriteUIControlInfo(uiCtrl);

                m.InterfaceCanvas.ClearSelectedElements(true);
                m.Renderer.Render();
                m.InterfaceCanvas.DrawSelectedElement(uiCtrl);
            }
        }

        private void InterfaceScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            InterfaceScrollViewer.Focus();

            // Clicked outside of the viewport (== clicked scrollbar) -> Do nothing to let event propagate to scrollbar
            Point svCursor = e.GetPosition(InterfaceScrollViewer);
            if (InterfaceScrollViewer.ViewportWidth < svCursor.X ||
                InterfaceScrollViewer.ViewportHeight < svCursor.Y)
                return;

            // Clicked outside of DragCanvas -> Route OnPreviewMouseLeftButtonDown event to DragCanvas
            Point cvCursor = e.GetPosition(m.InterfaceCanvas);
            if (cvCursor.X < 0 || m.InterfaceCanvas.Width < cvCursor.X ||
                cvCursor.Y < 0 || m.InterfaceCanvas.Height < cvCursor.Y)
                m.InterfaceCanvas.TriggerPreviewMouseLeftButtonDown(e);
        }
        #endregion  
        #region For Interface Move/Resize via Keyboard
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m.TabIndex == 1)
                InterfaceScrollViewer.Focus();
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            IInputElement focusedControl = Keyboard.FocusedElement;
            if (Equals(focusedControl, InterfaceScrollViewer))
            {
                int delta;
                bool move;
                switch (e.KeyboardDevice.Modifiers)
                {
                    case ModifierKeys.None:
                        move = true;
                        delta = 5;
                        break;
                    case ModifierKeys.Control:
                        move = true;
                        delta = 1;
                        break;
                    case ModifierKeys.Shift:
                        move = false;
                        delta = 5;
                        break;
                    case ModifierKeys.Shift | ModifierKeys.Control:
                        move = false;
                        delta = 1;
                        break;
                    default:
                        return;
                }

                // UIControl should have position/size of int
                int deltaX = 0;
                int deltaY = 0;
                switch (e.Key)
                {
                    case Key.Left:
                        deltaX = -1 * delta;
                        break;
                    case Key.Right:
                        deltaX = delta;
                        break;
                    case Key.Up:
                        deltaY = -1 * delta;
                        break;
                    case Key.Down:
                        deltaY = delta;
                        break;
                    default:
                        return;
                }

                switch (m.SelectMode)
                {
                    case ScriptEditViewModel.ControlSelectMode.SingleSelect:
                        if (move)
                        {
                            DragCanvas.ApplyUIControlPosition(m.SelectedUICtrl, deltaX, deltaY);
                            m.InvokeUIControlEvent(true);
                        }
                        else // Resize
                        {
                            DragCanvas.ApplyUIControlSize(m.SelectedUICtrl, deltaX, deltaY);
                            m.InvokeUIControlEvent(true);
                        }
                        break;
                    case ScriptEditViewModel.ControlSelectMode.MultiSelect:
                        if (move)
                        {
                            DragCanvas.ApplyUIControlPositions(m.SelectedUICtrls, deltaX, deltaY);
                            m.InvokeUIControlEvent(true);
                        }
                        else // Resize
                        {
                            DragCanvas.ApplyUIControlSizes(m.SelectedUICtrls, deltaX, deltaY);
                            m.InvokeUIControlEvent(true);
                        }
                        break;
                }
            }
        }
        #endregion
        #region For (Common) ListItemBox
        private void ListNewItem_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Prohibit '|'
            if (e.Text.Contains('|'))
                e.Handled = true;

            OnPreviewTextInput(e);
        }
        #endregion
        #region For (Common) RunOptional
        private void SectionName_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Prohibit invalid path characters
            if (!StringEscaper.IsFileNameValid(e.Text, new char[] { '[', ']', '\t' }))
                e.Handled = true;

            OnPreviewTextInput(e);
        }
        #endregion
        #endregion

        #region Command - Save
        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (m == null)
            {
                e.CanExecute = false;
            }
            else
            {
                // Only in Tab [General] or [Interface]
                e.CanExecute = m.CanExecuteCommand && (m.TabIndex == 0 || m.TabIndex == 1);
            }
        }

        private void SaveCommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            switch (m.TabIndex)
            {
                case 0:
                    // Changing focus is required to make sure changes in UI updated to ViewModel
                    MainSaveButton.Focus();
                    m.WriteScriptGeneral();
                    break;
                case 1:
                    m.WriteScriptInterface();
                    break;
            }
        }
        #endregion
    }
    #endregion

    #region ScriptEditViewModel
    public class ScriptEditViewModel : ViewModelBase
    {
        #region Constructor
        public ScriptEditViewModel(Script sc, Window window, MainViewModel mainViewModel)
        {
            Script = sc ?? throw new ArgumentNullException(nameof(sc));
            _window = window;
            MainViewModel = mainViewModel;
            InterfaceScaleFactor = Global.Setting.Interface.ScaleFactor;

            // Init ObservableCollection
            AttachedFolders = new ObservableCollection<AttachFolderItem>();
            AttachedFiles = new ObservableCollection<AttachFileItem>();
            // SelectedAttachedFiles = new List<AttachFileItem>();

            // InterfaceCanvas
            DragCanvas canvas = new DragCanvas
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 10, 10, 10),
            };
            Grid.SetRow(canvas, 0);
            Grid.SetColumn(canvas, 0);
            Panel.SetZIndex(canvas, -1);
            InterfaceCanvas = canvas;
        }
        #endregion

        #region Events
        public class UIControlModifiedEventArgs : EventArgs
        {
            public UIControl UIControl { get; set; }
            public List<UIControl> UIControls { get; set; }
            public bool MultiSelect => UIControls != null;
            public bool InfoNotUpdated { get; set; }

            public UIControlModifiedEventArgs(UIControl uiCtrl, bool infoNotUpdated)
            {
                UIControl = uiCtrl;
                InfoNotUpdated = infoNotUpdated;
            }
            public UIControlModifiedEventArgs(List<UIControl> uiCtrls, bool infoNotUpdated)
            {
                UIControls = uiCtrls;
                InfoNotUpdated = infoNotUpdated;
            }
        }
        public delegate void UIControlModifiedHandler(object sender, UIControlModifiedEventArgs e);
        public event UIControlModifiedHandler UIControlModified;
        #endregion

        #region Property - Basic
        public Script Script;
        private readonly Window _window;
        public MainViewModel MainViewModel { get; }
        public UIRenderer Renderer { get; private set; }
        public string InterfaceSectionName { get; private set; }

        /// <summary>
        /// Temp variable to disable buttons while working.
        /// Will be replaced by Commands.
        /// </summary>
        private bool _enableButtons = true;
        public bool CanExecuteCommand
        {
            get => _enableButtons;
            set => SetProperty(ref _enableButtons, value);
        }
        #endregion

        #region Property - Tab Index
        private int _tabIndex = 0;
        public int TabIndex
        {
            get => _tabIndex;
            set
            {
                _tabIndex = value;
                OnPropertyUpdate(nameof(TabIndex));

                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Property - General - Script Header
        public bool ScriptHeaderNotSaved { get; set; } = false;
        public bool ScriptHeaderUpdated { get; set; } = false;

        private string _scriptTitle = string.Empty;
        public string ScriptTitle
        {
            get => _scriptTitle;
            set
            {
                _scriptTitle = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptTitle));
            }
        }

        private string _scriptAuthor = string.Empty;
        public string ScriptAuthor
        {
            get => _scriptAuthor;
            set
            {
                _scriptAuthor = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptAuthor));
            }
        }

        private string _scriptVersion = "0";
        public string ScriptVersion
        {
            get => _scriptVersion;
            set
            {
                _scriptVersion = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptVersion));
            }
        }

        private string _scriptDate = string.Empty;
        public string ScriptDate
        {
            get => _scriptDate;
            set
            {
                _scriptDate = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptDate));
            }
        }

        private decimal _scriptLevel = 0;
        public decimal ScriptLevel
        {
            get => _scriptLevel;
            set
            {
                _scriptLevel = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptLevel));
            }
        }

        private string _scriptDescription = string.Empty;
        public string ScriptDescription
        {
            get => _scriptDescription;
            set
            {
                _scriptDescription = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptDescription));
            }
        }

        private SelectedState _scriptSelectedState = SelectedState.False;
        public SelectedState ScriptSelectedState
        {
            get => _scriptSelectedState;
            set
            {
                _scriptSelectedState = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptSelected));
            }
        }

        [SuppressMessage("ReSharper", "RedundantCaseLabel")]
        public bool? ScriptSelected
        {
            get
            {
                switch (_scriptSelectedState)
                {
                    case SelectedState.True:
                        return true;
                    default:
                    case SelectedState.False:
                        return false;
                    case SelectedState.None:
                        return null;
                }
            }
            set
            {
                switch (value)
                {
                    case true:
                        _scriptSelectedState = SelectedState.True;
                        break;
                    default:
                    case false:
                        _scriptSelectedState = SelectedState.False;
                        break;
                    case null:
                        _scriptSelectedState = SelectedState.None;
                        break;
                }
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
            }
        }

        private bool _scriptMandatory = false;
        public bool ScriptMandatory
        {
            get => _scriptMandatory;
            set
            {
                _scriptMandatory = value;
                ScriptHeaderNotSaved = true;
                ScriptHeaderUpdated = true;
                OnPropertyUpdate(nameof(ScriptMandatory));
            }
        }
        #endregion

        #region Property - General - Script Logo
        public bool ScriptLogoUpdated { get; set; } = false;

        public Color ScriptPanelBackground => MainViewModel.ScriptPanelBackground;

        #region ScriptLogo
        private PackIconMaterialKind? _scriptLogoIcon;
        public PackIconMaterialKind? ScriptLogoIcon
        {
            get => _scriptLogoIcon;
            set
            {
                _scriptLogoIcon = value;
                _scriptLogoImage = null;
                _scriptLogoSvg = null;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private ImageSource _scriptLogoImage;
        public ImageSource ScriptLogoImage
        {
            get => _scriptLogoImage;
            set
            {
                _scriptLogoIcon = null;
                _scriptLogoImage = value;
                _scriptLogoSvg = null;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private DrawingBrush _scriptLogoSvg;
        public DrawingBrush ScriptLogoSvg
        {
            get => _scriptLogoSvg;
            set
            {
                _scriptLogoIcon = null;
                _scriptLogoImage = null;
                _scriptLogoSvg = value;
                OnPropertyUpdate(nameof(ScriptLogoIcon));
                OnPropertyUpdate(nameof(ScriptLogoImage));
                OnPropertyUpdate(nameof(ScriptLogoSvg));
            }
        }

        private double _scriptLogoSvgWidth;
        public double ScriptLogoSvgWidth
        {
            get => _scriptLogoSvgWidth;
            set
            {
                _scriptLogoSvgWidth = value;
                OnPropertyUpdate(nameof(ScriptLogoSvgWidth));
            }
        }

        private double _scriptLogoSvgHeight;
        public double ScriptLogoSvgHeight
        {
            get => _scriptLogoSvgHeight;
            set
            {
                _scriptLogoSvgHeight = value;
                OnPropertyUpdate(nameof(ScriptLogoSvgHeight));
            }
        }
        #endregion

        private EncodedFileInfo _scriptLogoInfo;
        public EncodedFileInfo ScriptLogoInfo
        {
            get => _scriptLogoInfo;
            set
            {
                _scriptLogoInfo = value;
                OnPropertyUpdate(nameof(ScriptLogoLoaded));
                OnPropertyUpdate(nameof(ScriptLogoName));
                OnPropertyUpdate(nameof(ScriptLogoRawSize));
                OnPropertyUpdate(nameof(ScriptLogoEncodedSize));
                OnPropertyUpdate(nameof(ScriptLogoCompression));
                OnPropertyUpdate(nameof(ScriptLogoInfoVisibility));
            }
        }

        public Visibility ScriptLogoInfoVisibility => ScriptLogoInfo == null ? Visibility.Collapsed : Visibility.Visible;
        public bool ScriptLogoLoaded => ScriptLogoInfo != null;

        public string ScriptLogoName => ScriptLogoInfo == null ? string.Empty : ScriptLogoInfo.FileName;

        public string ScriptLogoRawSize
        {
            get
            {
                if (ScriptLogoInfo == null)
                    return string.Empty; // Invalid value

                string str = NumberHelper.NaturalByteSizeToSIUnit(ScriptLogoInfo.RawSize);
                return $"{str} ({ScriptLogoInfo.RawSize})";
            }
        }

        public string ScriptLogoEncodedSize
        {
            get
            {
                if (ScriptLogoInfo == null)
                    return string.Empty; // Invalid value

                string str = NumberHelper.NaturalByteSizeToSIUnit(ScriptLogoInfo.EncodedSize);
                return $"{str} ({ScriptLogoInfo.EncodedSize})";
            }
        }

        public string ScriptLogoCompression
        {
            get
            {
                if (ScriptLogoInfo == null)
                    return string.Empty; // Empty value

                return ScriptLogoInfo.EncodeMode == null ? string.Empty : EncodedFile.EncodeModeStr(ScriptLogoInfo.EncodeMode, false);
            }
        }
        #endregion

        #region Property - Interface - Panels
        public bool InterfaceNotSaved { get; set; } = false;
        public bool InterfaceUpdated { get; set; } = false;

        // Canvas
        private DragCanvas _interfaceCanvas;
        public DragCanvas InterfaceCanvas
        {
            get => _interfaceCanvas;
            set => SetProperty(ref _interfaceCanvas, value);
        }

        private int _interfaceScaleFactor = 100;
        public int InterfaceScaleFactor
        {
            get => _interfaceScaleFactor;
            set => SetProperty(ref _interfaceScaleFactor, value);
        }

        // InterfaceSection
        private ObservableCollection<string> _interfaceSectionNames;
        private readonly object _interfaceSectionNamesLock = new object();
        public ObservableCollection<string> InterfaceSectionNames
        {
            get => _interfaceSectionNames;
            set => SetCollectionProperty(ref _interfaceSectionNames, _interfaceSectionNamesLock, value);
        }

        private string _selectedInterfaceSectionName;
        public string SelectedInterfaceSectionName
        {
            get => _selectedInterfaceSectionName;
            set => SetProperty(ref _selectedInterfaceSectionName, value);
        }

        // Add Control
        private int _uiCtrlAddTypeIndex = -1;
        public int UICtrlAddTypeIndex
        {
            get => _uiCtrlAddTypeIndex;
            set
            {
                _uiCtrlAddTypeIndex = value;
                OnPropertyUpdate(nameof(UICtrlAddTypeIndex));

                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Delete Control (not for UI, for code-behind use)
        public List<UIControl> UICtrlToBeDeleted = new List<UIControl>();
        public List<string> UICtrlKeyChanged = new List<string>();
        #endregion

        #region Property - Interface - Editor
        // Editor
        private bool _interfaceLoaded = false;
        public bool InterfaceLoaded
        {
            get => _interfaceLoaded;
            set
            {
                _interfaceLoaded = value;
                OnPropertyUpdate(nameof(InterfaceLoaded));
            }
        }
        private ObservableCollection<string> _interfaceUICtrls = new ObservableCollection<string>();
        public ObservableCollection<string> InterfaceUICtrls
        {
            get => _interfaceUICtrls;
            set
            {
                _interfaceUICtrls = value;
                OnPropertyUpdate(nameof(InterfaceUICtrls));
            }
        }
        private int _interfaceUICtrlIndex;
        public int InterfaceUICtrlIndex
        {
            get => _interfaceUICtrlIndex;
            set
            {
                _interfaceUICtrlIndex = value;
                OnPropertyUpdate(nameof(InterfaceUICtrlIndex));
            }
        }
        // Select mode
        public enum ControlSelectMode
        {
            None = 0,
            SingleSelect = 1,
            MultiSelect = 2,
        }
        public ControlSelectMode SelectMode
        {
            get
            {
                bool singleSelect = _selectedUICtrl != null;
                bool multiSelect = _selectedUICtrls != null;
                Debug.Assert(!(singleSelect && multiSelect), "SelectedUICtrl and SelectedUICtrls cannot be activated at the same time");

                if (singleSelect)
                    return ControlSelectMode.SingleSelect;
                else if (multiSelect)
                    return ControlSelectMode.MultiSelect;
                else
                    return ControlSelectMode.None;
            }
        }
        // Single-select
        private UIControl _selectedUICtrl = null;
        public UIControl SelectedUICtrl
        {
            get => _selectedUICtrl;
            set
            {
                _selectedUICtrl = value;
                _selectedUICtrls = null;

                // UIControl Shared Argument
                OnPropertyUpdate(nameof(UICtrlEditEnabled));
                OnPropertyUpdate(nameof(UICtrlKey));
                OnPropertyUpdate(nameof(UICtrlText));
                OnPropertyUpdate(nameof(UICtrlVisible));
                OnPropertyUpdate(nameof(UICtrlX));
                OnPropertyUpdate(nameof(UICtrlY));
                OnPropertyUpdate(nameof(UICtrlWidth));
                OnPropertyUpdate(nameof(UICtrlHeight));
                OnPropertyUpdate(nameof(UICtrlToolTip));

                // UIControl Visibility
                OnPropertyUpdate(nameof(IsUICtrlTextBox));
                OnPropertyUpdate(nameof(IsUICtrlTextLabel));
                OnPropertyUpdate(nameof(IsUICtrlNumberBox));
                OnPropertyUpdate(nameof(IsUICtrlCheckBox));
                OnPropertyUpdate(nameof(IsUICtrlComboBox));
                OnPropertyUpdate(nameof(IsUICtrlImage));
                OnPropertyUpdate(nameof(IsUICtrlTextFile));
                OnPropertyUpdate(nameof(IsUICtrlButton));
                OnPropertyUpdate(nameof(IsUICtrlWebLabel));
                OnPropertyUpdate(nameof(IsUICtrlRadioButton));
                OnPropertyUpdate(nameof(IsUICtrlBevel));
                OnPropertyUpdate(nameof(IsUICtrlFileBox));
                OnPropertyUpdate(nameof(IsUICtrlRadioGroup));
                OnPropertyUpdate(nameof(ShowUICtrlListItemBox));
                OnPropertyUpdate(nameof(ShowUICtrlRunOptional));

                CommandManager.InvalidateRequerySuggested();
            }
        }
        // Multi-select
        private List<UIControl> _selectedUICtrls = null;
        public List<UIControl> SelectedUICtrls
        {
            get => _selectedUICtrls;
            set
            {
                _selectedUICtrl = null;
                _selectedUICtrls = value;

                // UIControl Shared Argument
                OnPropertyUpdate(nameof(UICtrlEditEnabled));
                OnPropertyUpdate(nameof(UICtrlKey));
                OnPropertyUpdate(nameof(UICtrlText));
                OnPropertyUpdate(nameof(UICtrlVisible));
                OnPropertyUpdate(nameof(UICtrlX));
                OnPropertyUpdate(nameof(UICtrlY));
                OnPropertyUpdate(nameof(UICtrlWidth));
                OnPropertyUpdate(nameof(UICtrlHeight));
                OnPropertyUpdate(nameof(UICtrlToolTip));

                // UIControl Visibility
                OnPropertyUpdate(nameof(IsUICtrlTextBox));
                OnPropertyUpdate(nameof(IsUICtrlTextLabel));
                OnPropertyUpdate(nameof(IsUICtrlNumberBox));
                OnPropertyUpdate(nameof(IsUICtrlCheckBox));
                OnPropertyUpdate(nameof(IsUICtrlComboBox));
                OnPropertyUpdate(nameof(IsUICtrlImage));
                OnPropertyUpdate(nameof(IsUICtrlTextFile));
                OnPropertyUpdate(nameof(IsUICtrlButton));
                OnPropertyUpdate(nameof(IsUICtrlWebLabel));
                OnPropertyUpdate(nameof(IsUICtrlRadioButton));
                OnPropertyUpdate(nameof(IsUICtrlBevel));
                OnPropertyUpdate(nameof(IsUICtrlFileBox));
                OnPropertyUpdate(nameof(IsUICtrlRadioGroup));
                OnPropertyUpdate(nameof(ShowUICtrlListItemBox));
                OnPropertyUpdate(nameof(ShowUICtrlRunOptional));

                CommandManager.InvalidateRequerySuggested();
            }
        }

        #region Shared Arguments
        public bool UICtrlEditEnabled => _selectedUICtrl != null;
        public string UICtrlKey
        {
            get => _selectedUICtrl != null ? _selectedUICtrl.Key : string.Empty;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                // Prevent other key being overwritten
                if (_interfaceUICtrls.Contains(value, StringComparer.OrdinalIgnoreCase))
                    return; // Ignore

                // Remove this key later
                UICtrlKeyChanged.Add(_selectedUICtrl.Key);

                _selectedUICtrl.Key = value;
                InvokeUIControlEvent(true);
            }
        }
        public string UICtrlText
        {
            get => _selectedUICtrl != null ? _selectedUICtrl.Text : string.Empty;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Text = value;
                InvokeUIControlEvent(true);
            }
        }
        public bool UICtrlVisible
        {
            get => _selectedUICtrl != null && _selectedUICtrl.Visibility;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Visibility = value;
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlX
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.X : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.X = value;
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlY
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Y : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Y = value;
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlWidth
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Width : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Width = value;
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlHeight
        {
            get => _selectedUICtrl != null ? (int)_selectedUICtrl.Rect.Height : 0;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Height = value;
                InvokeUIControlEvent(true);
            }
        }
        public string UICtrlToolTip
        {
            get => _selectedUICtrl != null ? _selectedUICtrl.Info.ToolTip : string.Empty;
            set
            {
                if (_selectedUICtrl == null)
                    return;

                _selectedUICtrl.Info.ToolTip = value;
                InvokeUIControlEvent(true);
            }
        }
        #endregion

        #region IsUICtrl Visibility Series
        public Visibility IsUICtrlTextBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.TextBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlTextLabel => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.TextLabel ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlNumberBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.NumberBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlCheckBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.CheckBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlComboBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.ComboBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlImage => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.Image ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlTextFile => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.TextFile ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlButton => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.Button ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlWebLabel => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.WebLabel ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlRadioButton => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.RadioButton ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlBevel => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.Bevel ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlFileBox => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.FileBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsUICtrlRadioGroup => _selectedUICtrl != null && _selectedUICtrl.Type == UIControlType.RadioGroup ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ShowUICtrlListItemBox
        {
            get
            {
                if (_selectedUICtrl == null)
                    return Visibility.Collapsed;

                switch (_selectedUICtrl.Type)
                {
                    case UIControlType.ComboBox:
                    case UIControlType.RadioGroup:
                        return Visibility.Visible;
                    default:
                        return Visibility.Collapsed;
                }
            }
        }
        public Visibility ShowUICtrlRunOptional
        {
            get
            {
                if (_selectedUICtrl == null)
                    return Visibility.Collapsed;

                switch (_selectedUICtrl.Type)
                {
                    case UIControlType.CheckBox:
                    case UIControlType.ComboBox:
                    case UIControlType.Button:
                    case UIControlType.RadioButton:
                    case UIControlType.RadioGroup:
                        return Visibility.Visible;
                    default:
                        return Visibility.Collapsed;
                }
            }
        }
        #endregion

        #region For TextBox
        private UIInfo_TextBox _uiCtrlTextBoxInfo;
        public UIInfo_TextBox UICtrlTextBoxInfo
        {
            get => _uiCtrlTextBoxInfo;
            set
            {
                _uiCtrlTextBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlTextBoxValue));
            }
        }
        public string UICtrlTextBoxValue
        {
            get => _uiCtrlTextBoxInfo?.Value ?? string.Empty;
            set
            {
                if (_uiCtrlTextBoxInfo == null)
                    return;

                _uiCtrlTextBoxInfo.Value = value;
                OnPropertyUpdate(nameof(UICtrlTextBoxValue));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For TextLabel
        private UIInfo_TextLabel _uiCtrlTextLabelInfo;
        public UIInfo_TextLabel UICtrlTextLabelInfo
        {
            get => _uiCtrlTextLabelInfo;
            set
            {
                _uiCtrlTextLabelInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlTextLabelFontSize));
                OnPropertyUpdate(nameof(UICtrlTextLabelFontWeightIndex));
                OnPropertyUpdate(nameof(UICtrlTextLabelFontStyleIndex));
            }
        }
        public int UICtrlTextLabelFontSize
        {
            get => _uiCtrlTextLabelInfo?.FontSize ?? 8;
            set
            {
                if (_uiCtrlTextLabelInfo == null)
                    return;

                _uiCtrlTextLabelInfo.FontSize = value;
                OnPropertyUpdate(nameof(UICtrlTextLabelFontSize));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlTextLabelFontWeightIndex
        {
            get => (int)(_uiCtrlTextLabelInfo?.FontWeight ?? UIFontWeight.Normal);
            set
            {
                if (_uiCtrlTextLabelInfo == null)
                    return;

                _uiCtrlTextLabelInfo.FontWeight = (UIFontWeight)value;
                OnPropertyUpdate(nameof(UICtrlTextLabelFontWeightIndex));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlTextLabelFontStyleIndex
        {
            get
            {
                if (_uiCtrlTextLabelInfo?.FontStyle == null)
                    return 0;
                return (int)_uiCtrlTextLabelInfo.FontStyle + 1;
            }
            set
            {
                if (_uiCtrlTextLabelInfo == null)
                    return;

                if (value == 0)
                    _uiCtrlTextLabelInfo.FontStyle = null;
                else
                    _uiCtrlTextLabelInfo.FontStyle = (UIFontStyle)(value - 1);
                OnPropertyUpdate(nameof(UICtrlTextLabelFontStyleIndex));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For NumberBox
        private UIInfo_NumberBox _uiCtrlNumberBoxInfo;
        public UIInfo_NumberBox UICtrlNumberBoxInfo
        {
            get => _uiCtrlNumberBoxInfo;
            set
            {
                _uiCtrlNumberBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlNumberBoxValue));
                OnPropertyUpdate(nameof(UICtrlNumberBoxMin));
                OnPropertyUpdate(nameof(UICtrlNumberBoxMax));
                OnPropertyUpdate(nameof(UICtrlNumberBoxTick));
            }
        }
        public int UICtrlNumberBoxValue
        {
            get => _uiCtrlNumberBoxInfo?.Value ?? 0;
            set
            {
                if (_uiCtrlNumberBoxInfo == null)
                    return;

                _uiCtrlNumberBoxInfo.Value = value;
                OnPropertyUpdate(nameof(UICtrlNumberBoxValue));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlNumberBoxMin
        {
            get => _uiCtrlNumberBoxInfo?.Min ?? 0;
            set
            {
                if (_uiCtrlNumberBoxInfo == null)
                    return;

                _uiCtrlNumberBoxInfo.Min = value;
                OnPropertyUpdate(nameof(UICtrlNumberBoxMin));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlNumberBoxMax
        {
            get => _uiCtrlNumberBoxInfo?.Max ?? 100;
            set
            {
                if (_uiCtrlNumberBoxInfo == null)
                    return;

                _uiCtrlNumberBoxInfo.Max = value;
                OnPropertyUpdate(nameof(UICtrlNumberBoxMax));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlNumberBoxTick
        {
            get => _uiCtrlNumberBoxInfo?.Tick ?? 1;
            set
            {
                if (_uiCtrlNumberBoxInfo == null)
                    return;

                _uiCtrlNumberBoxInfo.Tick = value;
                OnPropertyUpdate(nameof(UICtrlNumberBoxTick));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For CheckBox
        private UIInfo_CheckBox _uiCtrlCheckBoxInfo;
        public UIInfo_CheckBox UICtrlCheckBoxInfo
        {
            get => _uiCtrlCheckBoxInfo;
            set
            {
                _uiCtrlCheckBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlCheckBoxValue));
            }
        }
        public bool UICtrlCheckBoxValue
        {
            get => _uiCtrlCheckBoxInfo?.Value ?? false;
            set
            {
                if (_uiCtrlCheckBoxInfo == null)
                    return;

                _uiCtrlCheckBoxInfo.Value = value;
                OnPropertyUpdate(nameof(UICtrlCheckBoxValue));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For ComboBox
        private UIInfo_ComboBox _uiCtrlComboBoxInfo;
        public UIInfo_ComboBox UICtrlComboBoxInfo
        {
            get => _uiCtrlComboBoxInfo;
            set
            {
                _uiCtrlComboBoxInfo = value;
                if (value == null)
                    return;

                UICtrlListItemBoxItems = new ObservableCollection<string>(_uiCtrlComboBoxInfo.Items);
                UICtrlListItemBoxSelectedIndex = _uiCtrlComboBoxInfo.Index;
                UICtrlListItemBoxNewItem = string.Empty;
            }
        }
        #endregion
        #region For Image
        private UIInfo_Image _uiCtrlImageInfo;
        public UIInfo_Image UICtrlImageInfo
        {
            get => _uiCtrlImageInfo;
            set
            {
                _uiCtrlImageInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlImageUrl));
            }
        }
        private bool _uiCtrlImageSet = false;
        public bool UICtrlImageSet
        {
            get => _uiCtrlImageSet;
            set
            {
                _uiCtrlImageSet = value;
                OnPropertyUpdate(nameof(UICtrlImageLoaded));
                OnPropertyUpdate(nameof(UICtrlImageUnloaded));
            }
        }
        public Visibility UICtrlImageLoaded => _uiCtrlImageSet ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UICtrlImageUnloaded => !_uiCtrlImageSet ? Visibility.Visible : Visibility.Collapsed;
        public string UICtrlImageUrl
        {
            get => _uiCtrlImageInfo?.Url ?? string.Empty;
            set
            {
                if (_uiCtrlImageInfo == null)
                    return;

                _uiCtrlImageInfo.Url = string.IsNullOrWhiteSpace(value) ? null : value;
                OnPropertyUpdate(nameof(UICtrlImageInfo.Url));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For TextFile
        private bool _uiCtrlTextFileSet = false;
        public bool UICtrlTextFileSet
        {
            get => _uiCtrlTextFileSet;
            set
            {
                _uiCtrlTextFileSet = value;
                OnPropertyUpdate(nameof(UICtrlTextFileLoaded));
                OnPropertyUpdate(nameof(UICtrlTextFileUnloaded));
            }
        }
        public Visibility UICtrlTextFileLoaded => _uiCtrlTextFileSet ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UICtrlTextFileUnloaded => !_uiCtrlTextFileSet ? Visibility.Visible : Visibility.Collapsed;
        #endregion
        #region For Button
        public UIInfo_Button UICtrlButtonInfo { get; set; }
        private bool _uiCtrlButtonPictureSet = false;
        public bool UICtrlButtonPictureSet
        {
            get => _uiCtrlButtonPictureSet;
            set
            {
                _uiCtrlButtonPictureSet = value;
                OnPropertyUpdate(nameof(UICtrlButtonPictureLoaded));
                OnPropertyUpdate(nameof(UICtrlButtonPictureUnloaded));
            }
        }
        public Visibility UICtrlButtonPictureLoaded => _uiCtrlButtonPictureSet ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UICtrlButtonPictureUnloaded => !_uiCtrlButtonPictureSet ? Visibility.Visible : Visibility.Collapsed;
        #endregion
        #region For WebLabel
        private UIInfo_WebLabel _uiCtrlWebLabelInfo;
        public UIInfo_WebLabel UICtrlWebLabelInfo
        {
            get => _uiCtrlWebLabelInfo;
            set
            {
                _uiCtrlWebLabelInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlWebLabelUrl));
            }
        }
        public string UICtrlWebLabelUrl
        {
            get => _uiCtrlWebLabelInfo?.Url ?? string.Empty;
            set
            {
                if (_uiCtrlWebLabelInfo == null)
                    return;

                _uiCtrlWebLabelInfo.Url = string.IsNullOrWhiteSpace(value) ? null : value;
                OnPropertyUpdate(nameof(UICtrlWebLabelInfo.Url));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For RadioButton
        public List<UIControl> UICtrlRadioButtonList { get; set; }
        private UIInfo_RadioButton _uiCtrlRadioButtonInfo;
        public UIInfo_RadioButton UICtrlRadioButtonInfo
        {
            get => _uiCtrlRadioButtonInfo;
            set
            {
                _uiCtrlRadioButtonInfo = value;
                if (value == null)
                    return;
                OnPropertyUpdate(nameof(UICtrlRadioButtonInfo));
                OnPropertyUpdate(nameof(UICtrlRadioButtonSelectEnable));
                CommandManager.InvalidateRequerySuggested();
            }
        }
        public bool UICtrlRadioButtonSelectEnable => !_uiCtrlRadioButtonInfo?.Selected ?? false;
        #endregion
        #region For Bevel
        private UIInfo_Bevel _uiCtrlBevelInfo;
        public UIInfo_Bevel UICtrlBevelInfo
        {
            get => _uiCtrlBevelInfo;
            set
            {
                _uiCtrlBevelInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlBevelCaptionEnabled));
                OnPropertyUpdate(nameof(UICtrlBevelFontSize));
                OnPropertyUpdate(nameof(UICtrlBevelFontWeightIndex));
                OnPropertyUpdate(nameof(UICtrlBevelFontStyleIndex));
            }
        }
        public bool UICtrlBevelCaptionEnabled
        {
            get
            {
                if (_selectedUICtrl == null || _selectedUICtrl.Type != UIControlType.Bevel)
                    return false;

                return UICtrlBevelInfo.CaptionEnabled;
            }
            set
            {
                UICtrlBevelInfo.CaptionEnabled = value;
                OnPropertyUpdate(nameof(UICtrlBevelCaptionEnabled));
                InvokeUIControlEvent(true);
            }
        }
        public string UICtrlBevelCaption
        {
            get
            {
                if (_selectedUICtrl == null || _selectedUICtrl.Type != UIControlType.Bevel)
                    return string.Empty;

                return UICtrlBevelInfo.CaptionEnabled ? _selectedUICtrl.Text : string.Empty;
            }
            set
            {
                if (_selectedUICtrl == null || _selectedUICtrl.Type != UIControlType.Bevel)
                    return;

                _selectedUICtrl.Text = value;
                OnPropertyUpdate(nameof(UICtrlBevelCaption));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlBevelFontSize
        {
            get => _uiCtrlBevelInfo?.FontSize ?? UIControl.DefaultFontPoint;
            set
            {
                if (_uiCtrlBevelInfo == null)
                    return;

                UICtrlBevelInfo.CaptionEnabled = true;

                _uiCtrlBevelInfo.FontSize = value;
                OnPropertyUpdate(nameof(UICtrlBevelFontSize));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlBevelFontWeightIndex
        {
            get => (int?)_uiCtrlBevelInfo?.FontWeight ?? 0;
            set
            {
                if (_uiCtrlBevelInfo == null)
                    return;

                UICtrlBevelInfo.CaptionEnabled = true;

                _uiCtrlBevelInfo.FontWeight = (UIFontWeight)value;
                OnPropertyUpdate(nameof(UICtrlBevelFontWeightIndex));
                InvokeUIControlEvent(true);
            }
        }
        public int UICtrlBevelFontStyleIndex
        {
            get
            {
                if (_uiCtrlBevelInfo?.FontStyle == null)
                    return 0;
                return (int)_uiCtrlBevelInfo.FontStyle + 1;
            }
            set
            {
                if (_uiCtrlBevelInfo == null)
                    return;

                UICtrlBevelInfo.CaptionEnabled = true;

                if (value == 0)
                    _uiCtrlBevelInfo.FontStyle = null;
                else
                    _uiCtrlBevelInfo.FontStyle = (UIFontStyle)(value - 1);

                OnPropertyUpdate(nameof(UICtrlBevelFontStyleIndex));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For FileBox
        private UIInfo_FileBox _uiCtrlFileBoxInfo;
        public UIInfo_FileBox UICtrlFileBoxInfo
        {
            get => _uiCtrlFileBoxInfo;
            set
            {
                _uiCtrlFileBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlFileBoxFileChecked));
                OnPropertyUpdate(nameof(UICtrlFileBoxDirChecked));
            }
        }
        public bool UICtrlFileBoxFileChecked
        {
            get => _uiCtrlFileBoxInfo?.IsFile ?? false;
            set
            {
                if (_uiCtrlFileBoxInfo == null)
                    return;

                _uiCtrlFileBoxInfo.IsFile = value;
                OnPropertyUpdate(nameof(UICtrlFileBoxFileChecked));
                OnPropertyUpdate(nameof(UICtrlFileBoxDirChecked));
                InvokeUIControlEvent(true);
            }
        }
        public bool UICtrlFileBoxDirChecked
        {
            get => !_uiCtrlFileBoxInfo?.IsFile ?? false;
            set
            {
                if (_uiCtrlFileBoxInfo == null)
                    return;

                _uiCtrlFileBoxInfo.IsFile = !value;
                OnPropertyUpdate(nameof(UICtrlFileBoxFileChecked));
                OnPropertyUpdate(nameof(UICtrlFileBoxDirChecked));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For RadioGroup
        private UIInfo_RadioGroup _uiCtrlRadioGroupInfo;
        public UIInfo_RadioGroup UICtrlRadioGroupInfo
        {
            get => _uiCtrlRadioGroupInfo;
            set
            {
                _uiCtrlRadioGroupInfo = value;
                if (value == null)
                    return;

                UICtrlListItemBoxItems = new ObservableCollection<string>(_uiCtrlRadioGroupInfo.Items);
                UICtrlListItemBoxSelectedIndex = _uiCtrlRadioGroupInfo.Selected;
                UICtrlListItemBoxNewItem = string.Empty;
            }
        }
        #endregion
        #region For (Common) ListItemBox
        public ObservableCollection<string> _uiCtrlListItemBoxItems;
        public ObservableCollection<string> UICtrlListItemBoxItems
        {
            get => _uiCtrlListItemBoxItems;
            set
            {
                _uiCtrlListItemBoxItems = value;
                OnPropertyUpdate(nameof(UICtrlListItemBoxItems));
                InvokeUIControlEvent(true);
            }
        }
        private int _uiCtrlListItemBoxSelectedIndex;
        public int UICtrlListItemBoxSelectedIndex
        {
            get => _uiCtrlListItemBoxSelectedIndex;
            set
            {
                _uiCtrlListItemBoxSelectedIndex = value;
                OnPropertyUpdate(nameof(UICtrlListItemBoxSelectedIndex));
            }
        }
        private string _uiCtrlListItemBoxNewItem;
        public string UICtrlListItemBoxNewItem
        {
            get => _uiCtrlListItemBoxNewItem;
            set
            {
                _uiCtrlListItemBoxNewItem = value;
                OnPropertyUpdate(nameof(UICtrlListItemBoxNewItem));
            }
        }
        #endregion
        #region For (Common) RunOptional
        private bool _uiCtrlRunOptionalEnabled;
        public bool UICtrlRunOptionalEnabled
        {
            get => _uiCtrlRunOptionalEnabled;
            set
            {
                _uiCtrlRunOptionalEnabled = value;
                OnPropertyUpdate(nameof(UICtrlRunOptionalEnabled));
                InvokeUIControlEvent(false);
            }
        }
        private string _uiCtrlSectionToRun;
        public string UICtrlSectionToRun
        {
            get => _uiCtrlSectionToRun;
            set
            {
                _uiCtrlSectionToRun = value;
                OnPropertyUpdate(nameof(UICtrlSectionToRun));
                InvokeUIControlEvent(false);
            }
        }
        private bool _uiCtrlHideProgress;
        public bool UICtrlHideProgress
        {
            get => _uiCtrlHideProgress;
            set
            {
                _uiCtrlHideProgress = value;
                OnPropertyUpdate(nameof(UICtrlHideProgress));
                InvokeUIControlEvent(false);
            }
        }
        #endregion
        #endregion

        #region Property - Attachment
        public bool ScriptAttachUpdated { get; set; } = false;

        private readonly object _attachedFoldersLock = new object();
        private ObservableCollection<AttachFolderItem> _attachedFolders;
        public ObservableCollection<AttachFolderItem> AttachedFolders
        {
            get => _attachedFolders;
            set => SetCollectionProperty(ref _attachedFolders, _attachedFoldersLock, value);
        }

        private AttachFolderItem _selectedAttachedFolder;
        public AttachFolderItem SelectedAttachedFolder
        {
            get => _selectedAttachedFolder;
            set
            {
                _selectedAttachedFolder = value;
                OnPropertyUpdate();

                if (value != null)
                    AttachedFiles = SelectedAttachedFolder.Children;
                else
                    AttachedFiles.Clear();
            }
        }

        private readonly object _attachedFilesLock = new object();
        private ObservableCollection<AttachFileItem> _attachedFiles;
        public ObservableCollection<AttachFileItem> AttachedFiles
        {
            get => _attachedFiles;
            set => SetCollectionProperty(ref _attachedFiles, _attachedFilesLock, value);
        }

        public AttachFileItem[] SelectedAttachedFiles => AttachedFiles.Where(x => x.IsSelected).ToArray();

        private double _attachProgressValue = -1;
        public double AttachProgressValue
        {
            get => _attachProgressValue;
            set => SetProperty(ref _attachProgressValue, value);
        }

        private bool _attachProgressIndeterminate = false;
        public bool AttachProgressIndeterminate
        {
            get => _attachProgressIndeterminate;
            set => SetProperty(ref _attachProgressIndeterminate, value);
        }

        private bool _attachEnableAdvancedView = false;
        public bool AttachEnableAdvancedView
        {
            get => _attachEnableAdvancedView;
            set => SetProperty(ref _attachEnableAdvancedView, value);
        }

        private bool _attachIncludeInterfaceEncoded = false;
        public bool AttachIncludeInterfaceEncoded
        {
            get => _attachIncludeInterfaceEncoded;
            set => SetProperty(ref _attachIncludeInterfaceEncoded, value);
        }

        private bool _attachIncludeAuthorEncoded = false;
        public bool AttachIncludeAuthorEncoded
        {
            get => _attachIncludeAuthorEncoded;
            set => SetProperty(ref _attachIncludeAuthorEncoded, value);
        }
        #endregion

        #region InvokeUIControlEvent
        public bool UIControlModifiedEventToggle = false;
        public void InvokeUIControlEvent(bool infoNotUpdated)
        {
            if (UIControlModifiedEventToggle)
                return;

            InterfaceNotSaved = true;
            InterfaceUpdated = true;
            switch (SelectMode)
            {
                case ControlSelectMode.SingleSelect:
                    UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl, infoNotUpdated));
                    break;
                case ControlSelectMode.MultiSelect:
                    UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrls, true));
                    break;
            }
        }
        #endregion

        #region Command - Logo
        public ICommand ScriptLogoAttachCommand => new RelayCommand(ScriptLogoAttachCommand_Execute, CanExecuteFunc);
        public ICommand ScriptLogoExtractCommand => new RelayCommand(ScriptLogoExtractCommand_Execute, ScriptLogoLoadedFunc);
        public ICommand ScriptLogoDeleteCommand => new RelayCommand(ScriptLogoDeleteCommand_Execute, ScriptLogoLoadedFunc);

        private bool ScriptLogoLoadedFunc(object parameter)
        {
            return CanExecuteCommand && ScriptLogoLoaded;
        }

        private void ScriptLogoAttachCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Filter = "Image Files|*.bmp;*.jpg;*.png;*.gif;*.ico;*.svg",
                };

                if (dialog.ShowDialog() != true)
                    return;

                string srcFile = dialog.FileName;
                long fileSize = new FileInfo(srcFile).Length;
                if (EncodedFile.DecodeInMemorySizeLimit <= fileSize)
                {
                    string sizeLimitStr = NumberHelper.NaturalByteSizeToSIUnit(EncodedFile.DecodeInMemorySizeLimit);
                    MessageBoxResult result = MessageBox.Show(_window, $"You are attaching a file that is larger than {sizeLimitStr}.\r\n\r\nLarge files are supported, but PEBakery would be unresponsive when rendering a script.\r\n\r\nDo you want to continue?",
                        "Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation);
                    if (result == MessageBoxResult.No)
                        return;
                }

                try
                {
                    string srcFileName = Path.GetFileName(srcFile);
                    EncodedFile.AttachLogo(Script, srcFileName, srcFile);
                    MessageBox.Show(_window, "Logo successfully attached.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    ScriptLogoUpdated = true;
                    ReadScriptGeneral();
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show(_window, $"Unable to attach logo.\r\n- {Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ScriptLogoExtractCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                if (!EncodedFile.ContainsLogo(Script))
                {
                    MessageBox.Show(_window, $"Script [{Script.Title}] does not have a logo attached", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (MemoryStream ms = EncodedFile.ExtractLogo(Script, out ImageHelper.ImageFormat type, out string filename))
                {
                    SaveFileDialog dialog = new SaveFileDialog
                    {
                        OverwritePrompt = true,
                        FileName = filename,
                        Filter = $"{type.ToString().ToUpper().Replace(".", string.Empty)} Image|*.{type}",
                        DefaultExt = $".{type}",
                        AddExtension = true,
                    };

                    if (dialog.ShowDialog() != true)
                        return;

                    string destPath = dialog.FileName;
                    try
                    {
                        using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            ms.CopyTo(fs);
                        }

                        MessageBox.Show(_window, "Logo successfully extracted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                        MessageBox.Show(_window, $"Logo extraction failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ScriptLogoDeleteCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                if (!EncodedFile.ContainsLogo(Script))
                {
                    MessageBox.Show(_window, "Logo does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string errMsg;
                (Script, errMsg) = EncodedFile.DeleteLogo(Script);
                if (errMsg == null)
                {
                    MessageBox.Show(_window, "Logo successfully deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    ScriptLogoUpdated = true;
                    ReadScriptGeneral();
                }
                else
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                    MessageBox.Show(_window, $"There was an issue with deleting the logo.\r\n\r\n[Message]\r\n{errMsg}", "Warning", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Command - Interface Editor
        #region For Add, Delete of Interface Section
        private ICommand _interfaceSectionAddCommand;
        private ICommand _interfaceSectionDeleteCommand;
        public ICommand InterfaceSectionAddCommand => GetRelayCommand(ref _interfaceSectionAddCommand, "Add interface section", InterfaceSectionAddCommand_Execute, InterfaceSectionAddCommand_CanExecute);
        public ICommand InterfaceSectionDeleteCommand => GetRelayCommand(ref _interfaceSectionDeleteCommand, "Delete interface section", InterfaceSectionDeleteCommand_Execute, InterfaceSectionDeleteCommand_CanExecute);

        private bool InterfaceSectionAddCommand_CanExecute(object sender)
        {
            return CanExecuteCommand;
        }

        private async void InterfaceSectionAddCommand_Execute(object sender)
        {
            CanExecuteCommand = false;
            try
            {
                // Must save current edits to switch active interface section
                if (InterfaceNotSaved)
                {
                    MessageBoxResult result = MessageBox.Show(_window, "The script must be saved before adding a new interface.\r\n\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                    if (result == MessageBoxResult.Yes)
                        await WriteScriptInterfaceAsync(null, false);
                    else
                        return;
                }

                string newInterfaceSectionName = StringEscaper.GetUniqueKey(ScriptSection.Names.Interface + "_", Script.Sections.Select(x => x.Key), 2);
                TextBoxDialog dialog = new TextBoxDialog(_window,
                    "New Interface Section",
                    "Please enter a name for the new Interface section",
                    newInterfaceSectionName,
                    PackIconMaterialKind.PlaylistPlus);
                if (dialog.ShowDialog() == true)
                {
                    newInterfaceSectionName = dialog.InputText;
                    if (Script.Sections.ContainsKey(newInterfaceSectionName))
                    { // Section name conflict
                        MessageBox.Show(_window, $"Section [{newInterfaceSectionName}] already exists", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    await Task.Run(() =>
                    {
                        // Tried to implement auto-generation of multi-interface button, but doing this the right way is very hard.
                        // Current implementation would cause a lot of duplicated sections, because PEBakery cannot sense whether a button for
                        // switching info another section is already present. Let's leave this to script developer.

                        // [Main] -> Interface, InterfaceList
                        List<IniKey> switchButtons = new List<IniKey>();
                        InterfaceSectionNames.Add(newInterfaceSectionName);
                        switchButtons.Add(new IniKey(ScriptSection.Names.Main, Script.Const.Interface, newInterfaceSectionName));
                        string newInterfaceList = string.Join(",", InterfaceSectionNames.Select(StringEscaper.DoubleQuote));
                        switchButtons.Add(new IniKey(ScriptSection.Names.Main, Script.Const.InterfaceList, newInterfaceList));

                        // Write section info to file
                        IniReadWriter.WriteKeys(Script.RealPath, switchButtons);
                        IniReadWriter.AddSection(Script.RealPath, newInterfaceSectionName);

                        // Read from script
                        Script = Script.Project.RefreshScript(Script);
                    });

                    ReadScriptInterface(true);
                    InterfaceUpdated = true;
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool InterfaceSectionDeleteCommand_CanExecute(object sender)
        {
            return CanExecuteCommand &&
                   InterfaceSectionNames != null && SelectedInterfaceSectionName != null &&
                   1 < InterfaceSectionNames.Count && !SelectedInterfaceSectionName.Equals(ScriptSection.Names.Interface);
        }

        private async void InterfaceSectionDeleteCommand_Execute(object sender)
        {
            CanExecuteCommand = false;
            try
            {
                Debug.Assert(1 < InterfaceSectionNames.Count);
                Debug.Assert(!SelectedInterfaceSectionName.Equals(ScriptSection.Names.Interface));
                Debug.Assert(InterfaceSectionNames.Contains(SelectedInterfaceSectionName, StringComparer.OrdinalIgnoreCase));

                if (InterfaceSectionNames.Count == 1)
                { // Cannot delete default interface section
                    MessageBox.Show(_window, $"You cannot delete the default interface section [{SelectedInterfaceSectionName}].", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (SelectedInterfaceSectionName.Equals(ScriptSection.Names.Interface))
                { // Cannot delete default interface section
                    MessageBox.Show(_window, "Cannot delete [Interface] section.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Must save current edits to switch active interface section
                MessageBoxResult result = MessageBox.Show(_window,
                    "The script must be saved before deleting an interface.\r\n\r\nWarning: Deleted interface sections cannot be recovered!\r\n\r\nAre you sure you want to delete?",
                    "Delete Confirmation",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                    await WriteScriptInterfaceAsync(null, false);
                else
                    return;

                await Task.Run(() =>
                {
                    // Set InterfaceSectionNames and SelectedInterfaceSectionName
                    string sectionToDelete = SelectedInterfaceSectionName;
                    Debug.Assert(InterfaceSectionNames.Contains(sectionToDelete, StringComparer.OrdinalIgnoreCase));
                    Debug.Assert(1 < InterfaceSectionNames.Count);
                    InterfaceSectionNames.Remove(sectionToDelete);
                    string defaultInterface = InterfaceSectionNames[0];
                    Debug.Assert(0 < defaultInterface.Length, "New default interface is empty");

                    // Prepare to delete [sectionToDelete]
                    // Remove control's encoded file so we don't have orphaned Interface-Encoded attachments
                    foreach (UIControl uiCtrl in Renderer.UICtrls)
                    {
                        DeleteInterfaceEncodedFile(uiCtrl);
                    }
                    SelectedUICtrl = null;

                    // [Main] -> Interface, InterfaceList
                    List<IniKey> setKeys = new List<IniKey>();
                    List<IniKey> delKeys = new List<IniKey>();

                    // If only one interface is left, delete InterfaceList.
                    // If don't, overwrite InterfaceList with new section names.
                    if (InterfaceSectionNames.Count == 1)
                    {
                        delKeys.Add(new IniKey(ScriptSection.Names.Main, Script.Const.InterfaceList));
                    }
                    else
                    {
                        string newInterfaceList = string.Join(",", InterfaceSectionNames.Select(StringEscaper.DoubleQuote));
                        setKeys.Add(new IniKey(ScriptSection.Names.Main, Script.Const.InterfaceList, newInterfaceList));
                    }

                    if (defaultInterface.Equals(ScriptSection.Names.Interface, StringComparison.OrdinalIgnoreCase))
                        delKeys.Add(new IniKey(ScriptSection.Names.Main, Script.Const.Interface));
                    else
                        setKeys.Add(new IniKey(ScriptSection.Names.Main, Script.Const.Interface, defaultInterface));

                    // Write to script
                    if (0 < setKeys.Count)
                        IniReadWriter.WriteKeys(Script.RealPath, setKeys);
                    if (0 < delKeys.Count)
                        IniReadWriter.DeleteKeys(Script.RealPath, delKeys);
                    IniReadWriter.DeleteSection(Script.RealPath, sectionToDelete);

                    // Read from script
                    Script = Script.Project.RefreshScript(Script);
                });

                // Rendering must be done in ui thread.
                ReadScriptInterface(true);
                InterfaceUpdated = true;
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region For Add, Delete, Reload of UIControl
        private ICommand _uiCtrlAddCommand;
        private ICommand _uiCtrlDeleteCommand;
        private ICommand _uiCtrlReloadCommand;
        public ICommand UICtrlAddCommand => GetRelayCommand(ref _uiCtrlAddCommand, "Add UIControl", UICtrlAddCommand_Execute, UICtrlAddCommand_CanExecute);
        public ICommand UICtrlDeleteCommand => GetRelayCommand(ref _uiCtrlDeleteCommand, "Delete UIControl", UICtrlDeleteCommand_Execute, UICtrlDeleteCommand_CanExecute);
        public ICommand UICtrlReloadCommand => GetRelayCommand(ref _uiCtrlReloadCommand, "Reload UIControl", UICtrlReloadCommand_Execute, UICtrlReloadCommand_CanExecute);

        private bool UICtrlAddCommand_CanExecute(object sender)
        {
            return CanExecuteCommand && (int)UIControlType.None < UICtrlAddTypeIndex;
        }

        private void UICtrlAddCommand_Execute(object sender)
        {
            CanExecuteCommand = false;
            try
            {
                UIControlType type = UIControl.UIControlZeroBasedDict[UICtrlAddTypeIndex];
                if (type == UIControlType.None)
                {
                    MessageBox.Show(_window, "You must specify a control type", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string newControlKey = StringEscaper.GetUniqueKey(type.ToString(), Renderer.UICtrls.Select(x => x.Key));
                TextBoxDialog dialog = new TextBoxDialog(_window,
                    "New Interface Control",
                    "Please enter a name for the new control",
                    newControlKey,
                    PackIconMaterialKind.PlaylistPlus);
                if (dialog.ShowDialog() != true)
                    return;

                newControlKey = dialog.InputText;
                if (string.IsNullOrWhiteSpace(newControlKey))
                {
                    MessageBox.Show(_window, "The control's name cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string key = newControlKey.Trim();
                if (Renderer.UICtrls.Select(x => x.Key).Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    MessageBox.Show(_window, $"The control [{key}] already exists", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Script.Sections.ContainsKey(InterfaceSectionName))
                { // No [Interface] section, so add it
                    IniReadWriter.AddSection(Script.DirectRealPath, InterfaceSectionName);
                    Script = Script.Project.RefreshScript(Script);
                }

                ScriptSection ifaceSection = Script.Sections[InterfaceSectionName];
                string line = UIControl.GetUIControlTemplate(type, key);

                UIControl uiCtrl = UIParser.ParseStatement(line, ifaceSection, out List<LogInfo> errorLogs);
                Debug.Assert(uiCtrl != null, "Internal Logic Error at UICtrlAddButton_Click");
                Debug.Assert(errorLogs.Count == 0, "Internal Logic Error at UICtrlAddButton_Click");

                Renderer.UICtrls.Add(uiCtrl);
                InterfaceUICtrls = new ObservableCollection<string>(Renderer.UICtrls.Select(x => x.Key));
                InterfaceUICtrlIndex = 0;

                InterfaceCanvas.ClearSelectedElements(true);
                Renderer.Render();
                SelectedUICtrl = uiCtrl;
                InterfaceCanvas.DrawSelectedElement(uiCtrl);

                InterfaceNotSaved = true;
                InterfaceUpdated = true;
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool UICtrlDeleteCommand_CanExecute(object sender)
        {
            return CanExecuteCommand && UICtrlEditEnabled;
        }

        private void UICtrlDeleteCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                if (SelectedUICtrl == null)
                    return;

                UIControl uiCtrl = SelectedUICtrl;
                UICtrlToBeDeleted.Add(uiCtrl);

                // Remove control's encoded file so we don't have orphaned Interface-Encoded attachments
                DeleteInterfaceEncodedFile(uiCtrl);

                Renderer.UICtrls.Remove(uiCtrl);
                InterfaceUICtrls = new ObservableCollection<string>(Renderer.UICtrls.Select(x => x.Key));
                InterfaceUICtrlIndex = 0;

                Renderer.Render();
                SelectedUICtrl = null;

                WriteScriptInterface(null, true);
                InterfaceNotSaved = true;
                InterfaceUpdated = true;
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool UICtrlReloadCommand_CanExecute(object sender)
        {
            // Only in Tab [Interface]
            return CanExecuteCommand && TabIndex == 1;
        }

        private void UICtrlReloadCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                ReadScriptInterface(true);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion
        #region For Image
        private ICommand _uiCtrlImageAutoResizeCommand;
        public ICommand UICtrlImageAutoResizeCommand => GetRelayCommand(ref _uiCtrlImageAutoResizeCommand, "Auto resize Image control", UICtrlImageAutoResizeCommand_Execute, CanExecuteFunc);

        private async void UICtrlImageAutoResizeCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlImageAutoResizeButton_Click";

                Debug.Assert(SelectedUICtrl != null, internalErrorMsg);
                Debug.Assert(SelectedUICtrl.Type == UIControlType.Image, internalErrorMsg);

                UIControl uiCtrl = SelectedUICtrl;
                string fileName = uiCtrl.Text;
                string ext = Path.GetExtension(uiCtrl.Text);

                Debug.Assert(fileName != null, internalErrorMsg);
                Debug.Assert(ext != null, internalErrorMsg);

                if (InterfaceNotSaved)
                {
                    MessageBoxResult result = MessageBox.Show(_window,
                        "The interface must be saved before editing an image.\r\nSave changes?",
                        "Save Confirmation",
                        MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                    if (result == MessageBoxResult.Yes)
                        WriteScriptInterface(null, false);
                    else
                        return;
                }

                if (!ImageHelper.GetImageFormat(fileName, out ImageHelper.ImageFormat type))
                {
                    MessageBox.Show(_window, $"[{fileName}] is an unsupported image format", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (MemoryStream ms = await EncodedFile.ExtractFileInMemAsync(Script, ScriptSection.Names.InterfaceEncoded, fileName))
                {
                    int width, height;
                    if (type == ImageHelper.ImageFormat.Svg)
                        (width, height) = ImageHelper.GetSvgSizeInt(ms);
                    else
                        (width, height) = ImageHelper.GetImageSize(ms);

                    uiCtrl.Width = width;
                    uiCtrl.Height = height;
                    InvokeUIControlEvent(false);
                    WriteScriptInterface(null, false);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion
        #region For RadioButton
        private ICommand _uiCtrlRadioButtonSelectCommand;
        public ICommand UICtrlRadioButtonSelectCommand => GetRelayCommand(ref _uiCtrlRadioButtonSelectCommand, "Select RadioButton", UICtrlRadioButtonSelectCommand_Execute, UICtrlRadioButtonSelectCommand_CanExecute);

        private bool UICtrlRadioButtonSelectCommand_CanExecute(object parameter)
        {
            return CanExecuteCommand && UICtrlRadioButtonSelectEnable;
        }

        private void UICtrlRadioButtonSelectCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlRadioButtonSelectCommand_Execute";

                Debug.Assert(SelectedUICtrl != null, internalErrorMsg);
                Debug.Assert(SelectedUICtrl.Type == UIControlType.RadioButton, internalErrorMsg);
                Debug.Assert(UICtrlRadioButtonInfo != null, internalErrorMsg);
                Debug.Assert(UICtrlRadioButtonInfo.Selected == false, internalErrorMsg);
                Debug.Assert(UICtrlRadioButtonList != null, internalErrorMsg);

                foreach (UIControl uncheck in UICtrlRadioButtonList.Where(x => !x.Key.Equals(SelectedUICtrl.Key)))
                {
                    UIInfo_RadioButton subInfo = uncheck.Info.Cast<UIInfo_RadioButton>();
                    subInfo.Selected = false;
                }
                UICtrlRadioButtonInfo.Selected = true;

                OnPropertyUpdate(nameof(UICtrlRadioButtonSelectEnable));
                InvokeUIControlEvent(true);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion
        #region For (Common) ListItemBox
        private ICommand _uiCtrlListItemBoxUpCommand;
        private ICommand _uiCtrlListItemBoxDownCommand;
        private ICommand _uiCtrlListItemBoxSelectCommand;
        private ICommand _uiCtrlListItemBoxDeleteCommand;
        private ICommand _uiCtrlListItemBoxAddCommand;
        public ICommand UICtrlListItemBoxUpCommand => GetRelayCommand(ref _uiCtrlListItemBoxUpCommand, "Move item one step up", UICtrlListItemBoxUpCommand_Execute, CanExecuteFunc);
        public ICommand UICtrlListItemBoxDownCommand => GetRelayCommand(ref _uiCtrlListItemBoxDownCommand, "Move item one step down", UICtrlListItemBoxDownCommand_Execute, CanExecuteFunc);
        public ICommand UICtrlListItemBoxSelectCommand => GetRelayCommand(ref _uiCtrlListItemBoxSelectCommand, "Select default item", UICtrlListItemBoxSelectCommand_Execute, CanExecuteFunc);
        public ICommand UICtrlListItemBoxDeleteCommand => GetRelayCommand(ref _uiCtrlListItemBoxDeleteCommand, "Delete item", UICtrlListItemBoxDeleteCommand_Execute, CanExecuteFunc);
        public ICommand UICtrlListItemBoxAddCommand => GetRelayCommand(ref _uiCtrlListItemBoxAddCommand, "Add item", UICtrlListItemBoxAddCommand_Execute, CanExecuteFunc);

        private void UICtrlListItemBoxUpCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxUpCommand_Execute";

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
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void UICtrlListItemBoxDownCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxDownCommand_Execute";

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
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void UICtrlListItemBoxSelectCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxSelectCommand_Execute";

                Debug.Assert(SelectedUICtrl != null, internalErrorMsg);
                switch (SelectedUICtrl.Type)
                {
                    case UIControlType.ComboBox:
                        Debug.Assert(UICtrlComboBoxInfo != null, internalErrorMsg);
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

                InvokeUIControlEvent(false);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void UICtrlListItemBoxDeleteCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxDelete_Click";

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
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void UICtrlListItemBoxAddCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlListItemBoxAddCommand";

                Debug.Assert(SelectedUICtrl != null, internalErrorMsg);

                string newItem = UICtrlListItemBoxNewItem;
                switch (SelectedUICtrl.Type)
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

                InvokeUIControlEvent(false);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion
        #region For (Common) InterfaceEncoded 
        private ICommand _uiCtrlInterfaceAttachCommand;
        private ICommand _uiCtrlInterfaceExtractCommand;
        private ICommand _uiCtrlInterfaceResetCommand;
        public ICommand UICtrlInterfaceAttachCommand => GetRelayCommand(ref _uiCtrlInterfaceAttachCommand, "Attach file", UICtrlInterfaceAttachCommand_Execute, CanExecuteFunc);
        public ICommand UICtrlInterfaceExtractCommand => GetRelayCommand(ref _uiCtrlInterfaceExtractCommand, "Extract attached file", UICtrlInterfaceExtractCommand_Execute, CanExecuteFunc);
        public ICommand UICtrlInterfaceResetCommand => GetRelayCommand(ref _uiCtrlInterfaceResetCommand, "Delete attached file", UICtrlInterfaceResetCommand_Execute, CanExecuteFunc);

        private async void UICtrlInterfaceAttachCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceAttachButton_Click";

                if (!(parameter is string sender))
                    throw new InvalidOperationException(internalErrorMsg);

                UIControlType selectedType;
                string saveConfirmMsg;
                string extFilter;
                if (sender.Equals("ImageAttach", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.Image;
                    saveConfirmMsg = "The interface must be saved before editing an image.\r\n\r\nSave changes?";
                    extFilter = "Image Files|*.bmp;*.jpg;*.png;*.gif;*.ico;*.svg";
                }
                else if (sender.Equals("TextFileAttach", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.TextFile;
                    saveConfirmMsg = "The interface must be saved before editing a text file.\r\n\r\nSave changes?";
                    extFilter = "Text Files|*.txt;*.rtf|All Files|*.*";
                }
                else if (sender.Equals("ButtonPictureAttach", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.Button;
                    saveConfirmMsg = "The interface must be saved before editing an image.\r\n\r\nSave changes?";
                    extFilter = "Image Files|*.bmp;*.jpg;*.png;*.gif;*.ico;*.svg";
                }
                else
                {
                    throw new InvalidOperationException(internalErrorMsg);
                }

                Debug.Assert(SelectedUICtrl != null, internalErrorMsg);
                Debug.Assert(SelectedUICtrl.Type == selectedType, internalErrorMsg);

                if (InterfaceNotSaved)
                {
                    MessageBoxResult result = MessageBox.Show(saveConfirmMsg, "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                    if (result == MessageBoxResult.Yes)
                        WriteScriptInterface(null, false);
                    else
                        return;
                }

                OpenFileDialog dialog = new OpenFileDialog
                {
                    Filter = extFilter,
                };

                if (dialog.ShowDialog() != true)
                    return;

                string srcFilePath = dialog.FileName;
                string srcFileName = Path.GetFileName(srcFilePath);
                string srcFileExt = Path.GetExtension(srcFileName);

                // Check if srcFilePath is a text or binary (only for TextFile)
                // Check is skipped if file format is .txt or .rtf
                if (sender.Equals("TextFileAttach", StringComparison.Ordinal) &&
                    !srcFileExt.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
                    !srcFileExt.Equals(".rtf", StringComparison.OrdinalIgnoreCase))
                {
                    if (!EncodingHelper.IsText(srcFilePath, 16 * 1024))
                    { // File is expected to be binary
                        MessageBoxResult result = MessageBox.Show(_window, $"{srcFileName} appears to be a binary file.\r\n\r\nBinary files may not display correctly and can negatively impact rendering performance.\r\n\r\nDo you want to continue?",
                            "Warning",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Exclamation);
                        // Abort
                        if (result != MessageBoxResult.Yes)
                            return;
                    }
                }

                if (EncodedFile.ContainsInterface(Script, srcFileName))
                {
                    (List<EncodedFileInfo> infos, string errMsg) = EncodedFile.GetFolderInfo(Script, ScriptSection.Names.InterfaceEncoded, false);
                    if (errMsg != null)
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                        MessageBox.Show(_window, $"Attach failed.\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    srcFileName = StringEscaper.GetUniqueFileName(srcFileName, infos.Select(x => x.FileName));
                }

                // PEBakery can handle large encoded files.
                // -> But large file in interface requires lots of memory to decompress and can cause long unresponsive time.
                // -> Threshold is debatable.
                // Surprised to see render performance of text in TextFile is quite low. Keep lower threshold for text files.
                long fileLen = new FileInfo(srcFilePath).Length;
                long sizeLimit;
                if (sender.Equals("TextFileAttach", StringComparison.Ordinal) &&
                    !srcFileExt.Equals(".rtf", StringComparison.OrdinalIgnoreCase)) // rtf file can include image, so do not use lower limit
                    sizeLimit = EncodedFile.InterfaceTextSizeLimit;
                else
                    sizeLimit = EncodedFile.DecodeInMemorySizeLimit;
                if (sizeLimit <= fileLen)
                {
                    string sizeLimitStr = NumberHelper.NaturalByteSizeToSIUnit(sizeLimit);
                    MessageBoxResult result = MessageBox.Show(_window, $"You are attaching a file that is larger than {sizeLimitStr}.\r\n\r\nLarge files are supported, but PEBakery would be unresponsive when rendering a script.\r\n\r\nDo you want to continue?",
                        "Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation);
                    if (result == MessageBoxResult.No)
                        return;
                }

                try
                {
                    await EncodedFile.AttachInterfaceAsync(Script, srcFileName, srcFilePath, null);

                    UIControl.ReplaceAddress(Renderer.UICtrls, Script);

                    switch (selectedType)
                    {
                        case UIControlType.Image:
                            SelectedUICtrl.Text = srcFileName;
                            UICtrlImageSet = true;
                            break;
                        case UIControlType.TextFile:
                            SelectedUICtrl.Text = srcFileName;
                            UICtrlTextFileSet = true;
                            break;
                        case UIControlType.Button:
                            UICtrlButtonInfo.Picture = srcFileName;
                            UICtrlButtonPictureSet = true;
                            break;
                    }

                    InvokeUIControlEvent(false);
                    WriteScriptInterface(null, true);
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show(_window, $"Attach failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private async void UICtrlInterfaceExtractCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceExtractCommand_Execute";

                if (!(parameter is string sender))
                    throw new InvalidOperationException(internalErrorMsg);

                UIControlType selectedType;
                string cannotFindFile;
                if (sender.Equals("ImageExtract", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.Image;
                    cannotFindFile = "Unable to find the encoded image";
                }
                else if (sender.Equals("TextFileExtract", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.TextFile;
                    cannotFindFile = "Unable to find the encoded text file";
                }
                else if (sender.Equals("ButtonPictureExtract", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.Button;
                    cannotFindFile = "Unable to find the encoded image";
                }
                else
                {
                    throw new InvalidOperationException(internalErrorMsg);
                }

                Debug.Assert(SelectedUICtrl != null, internalErrorMsg);
                Debug.Assert(SelectedUICtrl.Type == selectedType, internalErrorMsg);

                UIControl uiCtrl = SelectedUICtrl;
                string fileName = uiCtrl.Text;
                if (selectedType == UIControlType.Button)
                {
                    Debug.Assert(UICtrlButtonInfo != null, internalErrorMsg);
                    fileName = UICtrlButtonInfo.Picture;
                }
                string ext = Path.GetExtension(fileName);

                Debug.Assert(fileName != null, internalErrorMsg);
                Debug.Assert(ext != null, internalErrorMsg);

                string extFilter;
                if (selectedType == UIControlType.TextFile)
                    extFilter = $"Text File|*{ext}";
                else
                    extFilter = $"{ext.ToUpper().Replace(".", String.Empty)} Image|*{ext}";

                if (!EncodedFile.ContainsInterface(Script, fileName))
                {
                    MessageBox.Show(_window, $"{cannotFindFile} [{fileName}]", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SaveFileDialog dialog = new SaveFileDialog
                {
                    OverwritePrompt = true,
                    FileName = fileName,
                    Filter = extFilter,
                    DefaultExt = ext,
                    AddExtension = true,
                };
                if (dialog.ShowDialog() != true)
                    return;

                string destPath = dialog.FileName;
                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    try
                    {
                        await EncodedFile.ExtractFileAsync(Script, ScriptSection.Names.InterfaceEncoded, fileName, fs, null);
                    }
                    catch (Exception ex)
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                        MessageBox.Show(_window, $"Extraction failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        /// <summary>
        /// Only for Image, TextFile, Button
        /// </summary>
        private void UICtrlInterfaceResetCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceResetButton_Click";

                if (!(parameter is string sender))
                    throw new InvalidOperationException(internalErrorMsg);

                UIControlType selectedType;
                string saveConfirmMsg;
                if (sender.Equals("ImageReset", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.Image;
                    saveConfirmMsg = "The interface must be saved before editing an image.\r\n\r\nSave changes?";
                }
                else if (sender.Equals("TextFileReset", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.TextFile;
                    saveConfirmMsg = "The interface must be saved before editing text a file.\r\n\r\nSave changes?";
                }
                else if (sender.Equals("ButtonPictureReset", StringComparison.Ordinal))
                {
                    selectedType = UIControlType.Button;
                    saveConfirmMsg = "The interface must be saved before editing an image.\r\n\r\nSave changes?";
                }
                else
                {
                    throw new InvalidOperationException(internalErrorMsg);
                }

                Debug.Assert(SelectedUICtrl != null, internalErrorMsg);
                Debug.Assert(SelectedUICtrl.Type == selectedType, internalErrorMsg);

                // If interface was not saved, save it with confirmation
                if (InterfaceNotSaved)
                {
                    MessageBoxResult result = MessageBox.Show(saveConfirmMsg, "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                    if (result == MessageBoxResult.Yes)
                        WriteScriptInterface(null, false);
                    else
                        return;
                }

                // Delete interface encoded file to prevent orphaned Interface-Encoded attachments
                string fileName = DeleteInterfaceEncodedFile(SelectedUICtrl);
                Debug.Assert(fileName != null, internalErrorMsg);

                // Clear encoded file information from uiCtrl
                switch (selectedType)
                {
                    case UIControlType.Image:
                        SelectedUICtrl.Text = UIInfo_Image.NoResource;
                        UICtrlImageSet = false;
                        break;
                    case UIControlType.TextFile:
                        SelectedUICtrl.Text = UIInfo_TextFile.NoResource;
                        UICtrlTextFileSet = false;
                        break;
                    case UIControlType.Button:
                        UICtrlButtonInfo.Picture = null;
                        UICtrlButtonPictureSet = false;
                        break;
                }
                InvokeUIControlEvent(false);
                WriteScriptInterface(null, true);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private string DeleteInterfaceEncodedFile(UIControl uiCtrl)
        {
            async void InternalDeleteInterfaceEncodedFile(string delFileName)
            {
                (Script sc, _) = await EncodedFile.DeleteFileAsync(Script, ScriptSection.Names.InterfaceEncoded, delFileName);
                if (sc != null)
                    Script = sc;
                else
                    MessageBox.Show(_window, $"Unable to delete encoded file [{delFileName}].", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // If two or more controls are referencing encoded file, do not delete it.
            Dictionary<string, int> fileRefCountDict = EncodedFile.GetInterfaceFileRefCount(Script);

            string fileName = null;
            switch (uiCtrl.Type)
            {
                case UIControlType.Image:
                    {
                        fileName = uiCtrl.Text;
                        if (fileName.Equals(UIInfo_Image.NoResource) || fileRefCountDict.ContainsKey(fileName))
                        {
                            if (EncodedFile.ContainsInterface(Script, fileName) && fileRefCountDict[fileName] == 1)
                                InternalDeleteInterfaceEncodedFile(fileName);
                        }
                    }
                    break;
                case UIControlType.TextFile:
                    {
                        fileName = uiCtrl.Text;
                        if (fileName.Equals(UIInfo_TextFile.NoResource) || fileRefCountDict.ContainsKey(fileName))
                        {
                            if (EncodedFile.ContainsInterface(Script, fileName) && fileRefCountDict[fileName] == 1)
                                InternalDeleteInterfaceEncodedFile(fileName);
                        }
                    }
                    break;
                case UIControlType.Button:
                    {
                        UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();
                        if (info.Picture != null)
                        {
                            fileName = info.Picture;
                            if (fileRefCountDict.ContainsKey(fileName))
                            {
                                if (EncodedFile.ContainsInterface(Script, fileName) && fileRefCountDict[fileName] == 1)
                                    InternalDeleteInterfaceEncodedFile(fileName);
                            }
                        }
                    }
                    break;
            }
            return fileName;
        }
        #endregion 
        #endregion

        #region Command - IsWorking
        private bool CanExecuteFunc(object parameter)
        {
            return CanExecuteCommand;
        }
        #endregion

        #region Command - Attachment (Folder)
        private ICommand _addFolderCommand;
        private ICommand _renameFolderCommand;
        private ICommand _extractFolderCommand;
        private ICommand _deleteFolderCommand;
        private ICommand _attachFileCommand;
        public ICommand AddFolderCommand => GetRelayCommand(ref _addFolderCommand, "Add folder", AddFolderCommand_Execute, CanExecuteFunc);
        public ICommand RenameFolderCommand => GetRelayCommand(ref _renameFolderCommand, "Rename folder", RenameFolderCommand_Execute, FolderSelected_CanExecute);
        public ICommand ExtractFolderCommand => GetRelayCommand(ref _extractFolderCommand, "Extract folder", ExtractFolderCommand_Execute, FolderSelected_CanExecute);
        public ICommand DeleteFolderCommand => GetRelayCommand(ref _deleteFolderCommand, "Delete folder", DeleteFolderCommand_Execute, FolderSelected_CanExecute);
        public ICommand AttachFileCommand => GetRelayCommand(ref _attachFileCommand, "Attach file", AttachFileCommand_Execute, FolderSelected_CanExecute);

        private bool FolderSelected_CanExecute(object parameter)
        {
            return CanExecuteCommand && SelectedAttachedFolder != null;
        }

        private void AddFolderCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                TextBoxDialog dialog = new TextBoxDialog(_window, "Add Folder", "Please enter a name for the new folder.", PackIconMaterialKind.FolderPlus);
                if (dialog.ShowDialog() != true)
                    return;

                string folderName = dialog.InputText;
                if (folderName.Length == 0)
                {
                    MessageBox.Show(_window, "The folder name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AttachProgressIndeterminate = true;
                try
                {
                    if (EncodedFile.ContainsFolder(Script, folderName))
                    {
                        MessageBox.Show(_window, $"The folder [{folderName}] already exists!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    Script = EncodedFile.AddFolder(Script, folderName, false);
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show(_window,
                        $"Unable to add folder.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    AttachProgressIndeterminate = false;
                }

                ScriptAttachUpdated = true;
                ReadScriptAttachment();

                SelectScriptAttachedFolder(folderName);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void RenameFolderCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                AttachFolderItem folder = SelectedAttachedFolder;
                Debug.Assert(folder != null);

                TextBoxDialog dialog = new TextBoxDialog(_window,
                        "Rename folder",
                        "Please input new folder name.",
                        PackIconMaterialKind.Pencil)
                {
                    InputText = folder.FolderName
                };

                if (dialog.ShowDialog() != true)
                    return;

                string newFolderName = dialog.InputText;
                if (newFolderName.Length == 0)
                {
                    MessageBox.Show(_window, "New folder name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AttachProgressIndeterminate = true;

                string errMsg;
                (Script, errMsg) = await EncodedFile.RenameFolderAsync(Script, folder.FolderName, newFolderName);
                if (errMsg == null)
                {
                    ScriptAttachUpdated = true;
                    ReadScriptAttachment();

                    SelectScriptAttachedFolder(newFolderName);
                }
                else // Failure
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                    MessageBox.Show(_window, $"Delete failed.\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                AttachProgressIndeterminate = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void ExtractFolderCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                AttachFolderItem folder = SelectedAttachedFolder;
                Debug.Assert(folder != null);
                EncodedFileInfo[] fileInfos = folder.Children.Select(x => x.Info).ToArray();
                if (fileInfos.Length == 0)
                {
                    MessageBox.Show(_window, $"Unable to extract files. The folder [{folder.FolderName}] is empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
                if (dialog.ShowDialog(_window) == true)
                {
                    string destDir = dialog.SelectedPath;

                    StringBuilder b = new StringBuilder();
                    bool fileOverwrote = false;
                    for (int i = 0; i < fileInfos.Length; i++)
                    {
                        EncodedFileInfo info = fileInfos[i];

                        string destFile = Path.Combine(destDir, info.FileName);
                        if (File.Exists(destFile))
                        {
                            fileOverwrote = true;

                            b.Append(destFile);
                            if (i + 1 < fileInfos.Length)
                                b.Append(", ");
                        }
                    }

                    bool proceedExtract = false;
                    if (fileOverwrote)
                    {
                        MessageBoxResult owResult = MessageBox.Show(_window, $"The file [{b}] will be overwritten.\r\n\r\nWould you like to proceed?",
                                                                    "Confirm Overwrite",
                                                                    MessageBoxButton.YesNo,
                                                                    MessageBoxImage.Information);

                        if (owResult == MessageBoxResult.Yes)
                            proceedExtract = true;
                        else if (owResult != MessageBoxResult.No)
                            throw new InternalException($"Internal Logic Error at {nameof(ExtractFolderCommand_Execute)}");
                    }
                    else
                    {
                        proceedExtract = true;
                    }

                    if (!proceedExtract)
                        return;

                    List<string> successFiles = new List<string>();
                    List<(string, Exception)> failureFiles = new List<(string, Exception)>();
                    AttachProgressValue = 0;
                    try
                    {
                        int idx = 0;
                        IProgress<double> progress = new Progress<double>(x =>
                        {
                            // ReSharper disable once AccessToModifiedClosure
                            AttachProgressValue = (x + idx) / fileInfos.Length;
                        });

                        foreach (EncodedFileInfo fi in fileInfos)
                        {
                            try
                            {
                                string destFile = Path.Combine(destDir, fi.FileName);
                                using (FileStream fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                                {
                                    await EncodedFile.ExtractFileAsync(Script, fi.FolderName, fi.FileName, fs, progress);
                                }

                                successFiles.Add(fi.FileName);
                            }
                            catch (Exception ex)
                            {
                                failureFiles.Add((fi.FileName, ex));
                            }

                            idx += 1;
                        }
                    }
                    finally
                    {
                        AttachProgressValue = -1;
                    }

                    // Success Report
                    b.Clear();
                    if (1 < successFiles.Count)
                    {
                        b.AppendLine($"{successFiles.Count} files were successfully extracted.");
                        foreach (string fileName in successFiles)
                            b.AppendLine($"- {fileName}");
                    }
                    else if (successFiles.Count == 1)
                    {
                        b.AppendLine($"File [{successFiles[0]}] was successfully extracted.");
                    }
                    MessageBox.Show(_window, b.ToString(), "Extraction Success Report", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Failure Report
                    if (1 <= failureFiles.Count)
                    {
                        b.Clear();
                        b.AppendLine($"Unable to extract {successFiles.Count} files.");
                        foreach ((string fileName, Exception ex) in failureFiles)
                        {
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                            b.AppendLine($"- {fileName} ({Logger.LogExceptionMessage(ex)})");
                        }
                        MessageBox.Show(_window, b.ToString(), "Extraction Failure Report", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void DeleteFolderCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                AttachFolderItem folder = SelectedAttachedFolder;
                Debug.Assert(folder != null);

                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to delete [{folder.FolderName}]?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;

                AttachProgressIndeterminate = true;

                string errMsg;
                (Script, errMsg) = await EncodedFile.DeleteFolderAsync(Script, folder.FolderName);
                if (errMsg == null)
                {
                    ScriptAttachUpdated = true;
                    ReadScriptAttachment();
                }
                else // Failure
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                    MessageBox.Show(_window, $"Delete failed.\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                AttachProgressIndeterminate = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void AttachFileCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                AttachFolderItem folder = SelectedAttachedFolder;
                Debug.Assert(folder != null);

                ScriptAttachFileDialog dialog = new ScriptAttachFileDialog { Owner = _window };
                if (dialog.ShowDialog() != true)
                    return;
                string srcFilePath = dialog.FilePath;
                string srcFileName = dialog.FileName;
                EncodedFile.EncodeMode mode = dialog.EncodeMode;

                if (srcFilePath.Length == 0)
                {
                    MessageBox.Show(_window, "You must choose a file to attach!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!File.Exists(srcFilePath))
                {
                    MessageBox.Show(_window, $"Invalid path:\r\n[{srcFilePath}]\r\n\r\nThe file does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(srcFileName))
                {
                    MessageBox.Show(_window, "The file name cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    if (EncodedFile.ContainsFile(Script, folder.FolderName, srcFileName))
                    {
                        MessageBoxResult result = MessageBox.Show(
                            $"The attached file [{srcFileName}] will be overwritten.\r\n\r\nWould you like to proceed?",
                            "Confirm Overwrite",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Error);
                        if (result == MessageBoxResult.No)
                            return;
                    }

                    try
                    {
                        CanExecuteCommand = false;
                        AttachProgressValue = 0;
                        IProgress<double> progress = new Progress<double>(x => { AttachProgressValue = x; });
                        await EncodedFile.AttachFileAsync(Script, folder.FolderName, srcFileName, srcFilePath, mode, progress);
                    }
                    finally
                    {
                        AttachProgressValue = -1;
                        CanExecuteCommand = true;
                    }
                    MessageBox.Show(_window, "File successfully attached.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    ScriptAttachUpdated = true;
                    ReadScriptAttachment();

                    SelectScriptAttachedFolder(folder.FolderName);
                    SelectScriptAttachedFile(srcFileName);
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show(_window, $"Attach failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Command - Attachment (File)
        private ICommand _renameFileCommand;
        private ICommand _extractFileCommand;
        private ICommand _deleteFileCommand;
        private ICommand _openFileCommand;
        private ICommand _inspectFileCommand;
        public ICommand RenameFileCommand => GetRelayCommand(ref _renameFileCommand, "Rename file", RenameFileCommand_Execute, FileSingleSelected_CanExecute);
        public ICommand ExtractFileCommand => GetRelayCommand(ref _extractFileCommand, "Extract file", ExtractFileCommand_Execute, FileSelected_CanExecute);
        public ICommand DeleteFileCommand => GetRelayCommand(ref _deleteFileCommand, "Delete file", DeleteFileCommand_Execute, FileSelected_CanExecute);
        public ICommand OpenFileCommand => GetRelayCommand(ref _openFileCommand, "Open file", OpenFileCommand_Execute, FileSingleSelected_CanExecute);
        public ICommand InspectFileCommand => GetRelayCommand(ref _inspectFileCommand, "Inspect file", InspectFileCommand_Execute, FileSelected_CanExecute);

        private bool FileSelected_CanExecute(object parameter)
        {
            return CanExecuteCommand && SelectedAttachedFolder != null && 0 < SelectedAttachedFiles.Length;
        }

        private bool FileSingleSelected_CanExecute(object parameter)
        {
            return CanExecuteCommand && SelectedAttachedFolder != null && SelectedAttachedFiles.Length == 1;
        }

        private async void RenameFileCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                AttachFileItem file = SelectedAttachedFiles[0];
                Debug.Assert(file != null);
                EncodedFileInfo fi = file.Info;
                Debug.Assert(fi != null);

                TextBoxDialog dialog = new TextBoxDialog(_window,
                    "Rename file",
                    "Please input new file name.",
                    PackIconMaterialKind.Pencil)
                {
                    InputText = fi.FileName
                };
                if (dialog.ShowDialog() != true)
                    return;

                string newFileName = dialog.InputText;
                if (newFileName.Length == 0)
                {
                    MessageBox.Show(_window, "New file name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Debug.Assert(fi.FileName != null);
                Debug.Assert(newFileName != null);
                string oldExt = Path.GetExtension(fi.FileName);
                string newExt = Path.GetExtension(newFileName);
                if (!oldExt.Equals(newExt, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBoxResult result = MessageBox.Show(_window,
                        $"File's extension is being changed to [{newExt}] from [{oldExt}].\r\nAre you sure to continue?",
                        "Extension Changed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (result != MessageBoxResult.Yes)
                        return;
                }

                CanExecuteCommand = false;
                AttachProgressIndeterminate = true;

                string errorMsg;
                (Script, errorMsg) = await EncodedFile.RenameFileAsync(Script, fi.FolderName, fi.FileName, newFileName);
                if (errorMsg == null)
                {
                    ScriptAttachUpdated = true;
                    ReadScriptAttachment();

                    SelectScriptAttachedFolder(fi.FolderName);
                    SelectScriptAttachedFile(newFileName);
                }
                else
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errorMsg));
                    MessageBox.Show(_window,
                        $"Unable to rename [{fi.FileName}] to [{newFileName}].\r\n- {errorMsg}",
                        "Rename Failure",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                AttachProgressIndeterminate = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void ExtractFileCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                if (SelectedAttachedFiles.Length == 1)
                {
                    AttachFileItem file = SelectedAttachedFiles[0];
                    Debug.Assert(file != null);
                    EncodedFileInfo fi = file.Info;
                    Debug.Assert(fi != null);

                    Debug.Assert(fi.FileName != null);
                    string ext = Path.GetExtension(fi.FileName);
                    SaveFileDialog dialog = new SaveFileDialog
                    {
                        OverwritePrompt = true,
                        Title = "Extract file",
                        FileName = fi.FileName,
                        Filter = $"{ext.ToUpper().TrimStart('.')} file|*{ext}"
                    };

                    if (dialog.ShowDialog() != true)
                        return;

                    string destFile = dialog.FileName;
                    try
                    {
                        CanExecuteCommand = false;
                        AttachProgressValue = 0;
                        IProgress<double> progress = new Progress<double>(x => { AttachProgressValue = x; });
                        using (FileStream fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                        {
                            await EncodedFile.ExtractFileAsync(Script, fi.FolderName, fi.FileName, fs, progress);
                        }

                        MessageBox.Show(_window, $"File [{fi.FileName}] successfully extracted.", "Extract Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                        MessageBox.Show(_window, $"Extraction failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Extract Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        AttachProgressValue = -1;
                        CanExecuteCommand = true;
                    }
                }
                else if (1 < SelectedAttachedFiles.Length)
                {
                    VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog
                    {
                        UseDescriptionForTitle = true,
                        Description = "Extract files",
                    };

                    if (dialog.ShowDialog() != true)
                        return;

                    string destDir = dialog.SelectedPath;

                    List<string> successFiles = new List<string>();
                    List<(string, Exception)> failureFiles = new List<(string, Exception)>();
                    AttachProgressValue = 0;
                    CanExecuteCommand = false;
                    try
                    {
                        int idx = 0;
                        IProgress<double> progress = new Progress<double>(x =>
                        {
                            // ReSharper disable once AccessToModifiedClosure
                            AttachProgressValue = (x + idx) / SelectedAttachedFiles.Length;
                        });

                        foreach (AttachFileItem file in SelectedAttachedFiles)
                        {
                            EncodedFileInfo fi = file.Info;
                            Debug.Assert(fi != null);

                            try
                            {
                                string destFile = Path.Combine(destDir, fi.FileName);
                                using (FileStream fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                                {
                                    await EncodedFile.ExtractFileAsync(Script, fi.FolderName, fi.FileName, fs, progress);
                                }

                                successFiles.Add(fi.FileName);
                            }
                            catch (Exception ex)
                            {
                                failureFiles.Add((fi.FileName, ex));
                            }

                            idx += 1;
                        }
                    }
                    finally
                    {
                        AttachProgressValue = -1;
                        CanExecuteCommand = true;
                    }

                    // Success Report
                    StringBuilder b = new StringBuilder();
                    if (1 < successFiles.Count)
                    {
                        b.AppendLine($"{successFiles.Count} files were successfully extracted.");
                        foreach (string fileName in successFiles)
                            b.AppendLine($"- {fileName}");
                    }
                    else if (successFiles.Count == 1)
                    {
                        b.AppendLine($"File [{successFiles[0]}] was successfully extracted.");
                    }
                    MessageBox.Show(_window, b.ToString(), "Extraction Success Report", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Failure Report
                    if (1 <= failureFiles.Count)
                    {
                        b.Clear();
                        b.AppendLine($"Unable to extract {successFiles.Count} files.");
                        foreach ((string fileName, Exception ex) in failureFiles)
                        {
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                            b.AppendLine($"- {fileName} ({Logger.LogExceptionMessage(ex)})");
                        }
                        MessageBox.Show(_window, b.ToString(), "Extraction Failure Report", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void DeleteFileCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                if (SelectedAttachedFiles.Length == 1)
                {
                    AttachFileItem file = SelectedAttachedFiles[0];
                    Debug.Assert(file != null);
                    EncodedFileInfo fi = file.Info;
                    Debug.Assert(fi != null);

                    MessageBoxResult result = MessageBox.Show(
                        $"Are you sure you want to delete [{fi.FileName}]?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                        return;

                    AttachProgressIndeterminate = true;

                    string errMsg;
                    (Script, errMsg) = await EncodedFile.DeleteFileAsync(Script, fi.FolderName, fi.FileName);
                    if (errMsg == null)
                    {
                        ScriptAttachUpdated = true;
                        ReadScriptAttachment();

                        SelectScriptAttachedFolder(fi.FolderName);
                    }
                    else // Failure
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                        MessageBox.Show(_window, $"Unable to delete file [{fi.FileName}]\r\n- {errMsg}", "Delete Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (1 < SelectedAttachedFiles.Length)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"Are you sure you want to delete {SelectedAttachedFiles.Length} files?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                        return;

                    AttachProgressIndeterminate = true;
                    string folderName = SelectedAttachedFolder.FolderName;
                    string[] fileNames = SelectedAttachedFiles.Select(fi => fi.FileName).ToArray();

                    List<string> errorMessages;
                    (Script, errorMessages) = await EncodedFile.DeleteFilesAsync(Script, folderName, fileNames);

                    if (errorMessages.Count == 0)
                    {
                        ScriptAttachUpdated = true;
                        ReadScriptAttachment();

                        SelectScriptAttachedFolder(folderName);
                    }
                    else
                    {
                        // Failure Report
                        StringBuilder b = new StringBuilder();
                        b.AppendLine($"Unable to delete {errorMessages.Count} files.");
                        foreach (string errMsg in errorMessages)
                        {
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                            b.AppendLine($"- {errMsg}");
                        }
                        MessageBox.Show(_window, b.ToString(), "Delete Failure Report", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            finally
            {
                CanExecuteCommand = true;
                AttachProgressIndeterminate = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void OpenFileCommand_Execute(object parameter)
        {
            Debug.Assert(SelectedAttachedFiles.Length == 1);
            CanExecuteCommand = false;
            AttachProgressValue = 0;
            try
            {
                AttachFileItem file = SelectedAttachedFiles[0];
                Debug.Assert(file != null);
                EncodedFileInfo fi = file.Info;
                Debug.Assert(fi != null);

                IProgress<double> progress = new Progress<double>(x => { AttachProgressValue = x; });

                // Do not clear tempDir right after calling OpenPath(). Doing this will trick the opened process.
                // Instead, leave it to Global.Cleanup() when program is exited.
                string tempDir = FileHelper.GetTempDir();
                try
                {
                    string tempFile = Path.Combine(tempDir, fi.FileName);
                    using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                    {
                        await EncodedFile.ExtractFileAsync(Script, fi.FolderName, fi.FileName, fs, progress);
                    }

                    FileHelper.OpenPath(tempFile);
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show(_window,
                        $"Unable to open file [{fi.FileName}].\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                AttachProgressValue = -1;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void InspectFileCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            AttachProgressIndeterminate = true;
            try
            {
                List<string> failures = new List<string>();

                foreach (AttachFileItem file in SelectedAttachedFiles)
                {
                    EncodedFileInfo fi = file.Info;
                    Debug.Assert(fi != null);

                    string dirName = fi.FolderName;
                    string fileName = fi.FileName;

                    if (fi.EncodeMode != null)
                        return;

                    string errMsg;
                    (fi, errMsg) = await EncodedFile.GetFileInfoAsync(Script, dirName, fileName, true);
                    if (errMsg == null)
                    { // Success
                        file.Info = fi;
                        file.PropertyUpdate();
                    }
                    else
                    { // Failure
                        failures.Add(fi.FileName);
                    }
                }

                if (0 < failures.Count)
                {
                    StringBuilder b = new StringBuilder();
                    if (failures.Count == 1)
                        b.AppendLine("Unable to inspect 1 file.");
                    else
                        b.AppendLine($"Unable to inspect {failures.Count} files.");
                    foreach (string failure in failures)
                    {
                        b.AppendLine($"- {failure}");
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, $"Unable to inspect [{failure}]"));
                    }
                    MessageBox.Show(_window, b.ToString(), "Inspect Failure Report", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                AttachProgressIndeterminate = false;
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Command - Attachment (Advanced)
        private ICommand _enableAdvancedViewCommand;
        private ICommand _enableAuthorEncodedCommand;
        private ICommand _enableInterfaceEncodedCommand;
        public ICommand EnableAdvancedViewCommand => GetRelayCommand(ref _enableAdvancedViewCommand, "Enable advanced view", EnableAdvancedViewCommand_Execute, CanExecuteFunc);
        public ICommand EnableAuthorEncodedCommand => GetRelayCommand(ref _enableAuthorEncodedCommand, "Enable [AuthorEncoded]", EnableAuthorEncodedCommand_Execute, AdvancedView_CanExecute);
        public ICommand EnableInterfaceEncodedCommand => GetRelayCommand(ref _enableInterfaceEncodedCommand, "Enable [InterfaceEncoded]", EnableInterfaceEncodedCommand_Execute, AdvancedView_CanExecute);

        private bool AdvancedView_CanExecute(object parameter)
        {
            return CanExecuteCommand && AttachEnableAdvancedView;
        }

        private void EnableAdvancedViewCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                if (AttachEnableAdvancedView)
                {
                    MessageBoxResult result = MessageBox.Show(_window,
                        "Advanced view allows access resources embedded in the script's interface and is intended for expert users only!\r\nIf you do not understand the inner workings of PEBakery's interface and attachment handling you can easily corrupt your script!\r\n\r\nAre you sure you want to enable advanced view?",
                        "Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                        AttachEnableAdvancedView = false;
                }
                else
                { // Turn off all advanced view
                    AttachIncludeAuthorEncoded = false;
                    AttachIncludeInterfaceEncoded = false;
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void EnableAuthorEncodedCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                ReadScriptAttachment();

                if (AttachIncludeAuthorEncoded)
                    SelectScriptAttachedFolder(ScriptSection.Names.AuthorEncoded);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void EnableInterfaceEncodedCommand_Execute(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                ReadScriptAttachment();

                if (AttachIncludeInterfaceEncoded)
                    SelectScriptAttachedFolder(ScriptSection.Names.InterfaceEncoded);
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Methods - ReadScript
        public void ReadScriptGeneral()
        {
            // Nested Function
            string GetStringValue(string key, string defaultValue = "") => Script.MainInfo.ContainsKey(key) ? Script.MainInfo[key] : defaultValue;

            // General
            if (EncodedFile.ContainsLogo(Script))
            {
                (EncodedFileInfo info, string errMsg) = EncodedFile.GetLogoInfo(Script, true);
                if (errMsg != null)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                    MessageBox.Show(_window, $"Unable to read script logo\r\n\r\n[Message]\r\n\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (MemoryStream ms = EncodedFile.ExtractLogo(Script, out ImageHelper.ImageFormat type, out _))
                {
                    switch (type)
                    {
                        case ImageHelper.ImageFormat.Svg:
                            DrawingGroup svgDrawing = ImageHelper.SvgToDrawingGroup(ms);
                            Rect svgSize = svgDrawing.Bounds;
                            (double width, double height) = ImageHelper.StretchSizeAspectRatio(svgSize.Width, svgSize.Height, 90, 90);
                            ScriptLogoSvg = new DrawingBrush { Drawing = svgDrawing };
                            ScriptLogoSvgWidth = width;
                            ScriptLogoSvgHeight = height;
                            break;
                        default:
                            ScriptLogoImage = ImageHelper.ImageToBitmapImage(ms);
                            break;
                    }
                }
                ScriptLogoInfo = info;
            }
            else
            { // No script logo
                ScriptLogoIcon = PackIconMaterialKind.BorderNone;
                ScriptLogoInfo = null;
            }

            ScriptTitle = Script.Title;
            ScriptAuthor = Script.Author;
            ScriptVersion = Script.Version;
            ScriptDate = GetStringValue("Date");
            ScriptLevel = Script.Level;
            ScriptDescription = StringEscaper.Unescape(Script.Description);
            ScriptSelectedState = Script.Selected;
            ScriptMandatory = Script.Mandatory;

            ScriptHeaderNotSaved = false;
            ScriptHeaderUpdated = false;
        }

        /// <summary>
        /// Read script interface from a script instance.
        /// </summary>
        /// <param name="refreshSectionNames">
        /// Refresh section list and auto detect active section if true. Do not update section list and keep current active section if false.
        /// </param>
        public void ReadScriptInterface(bool refreshSectionNames)
        {
            // Refresh interface section names
            if (refreshSectionNames)
            {
                InterfaceSectionNames = new ObservableCollection<string>(Script.GetInterfaceSectionNames(true));
                SelectedInterfaceSectionName = InterfaceSectionName = Script.InterfaceSectionName;
            }
            else
            {
                InterfaceSectionName = SelectedInterfaceSectionName;
            }

            // Make a copy of uiCtrls, to prevent change in interface should not affect script file immediately.
            (List<UIControl> uiCtrls, List<LogInfo> errLogs) = UIRenderer.LoadInterfaces(Script, InterfaceSectionName);
            if (uiCtrls == null) // No Interface -> empty list
            {
                if (0 < errLogs.Count)
                {
                    Global.Logger.SystemWrite(errLogs);

                    StringBuilder b = new StringBuilder();
                    b.AppendLine("[Error Messages]");
                    foreach (LogInfo log in errLogs)
                        b.AppendLine(log.Message);
                    MessageBox.Show(b.ToString(), "Interface Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                uiCtrls = new List<UIControl>();
            }

            Renderer = new UIRenderer(InterfaceCanvas, _window, Script, uiCtrls.ToList(), false, Script.Project.Compat.IgnoreWidthOfWebLabel);

            InterfaceUICtrls = new ObservableCollection<string>(uiCtrls.Select(x => x.Key));
            InterfaceUICtrlIndex = -1;

            InterfaceNotSaved = false;
            InterfaceUpdated = false;

            DrawScript();
            ResetSelectedUICtrl();
        }

        public void ReadScriptAttachment()
        {
            // Attachment
            AttachedFolders.Clear();
            AttachedFiles.Clear();

            EncodedFile.GetFileInfoOptions opts = new EncodedFile.GetFileInfoOptions
            {
                IncludeAuthorEncoded = AttachIncludeAuthorEncoded,
                IncludeInterfaceEncoded = AttachIncludeInterfaceEncoded,
                InspectEncodeMode = false,
            };

            (Dictionary<string, List<EncodedFileInfo>> fileDict, string errMsg) = EncodedFile.GetAllFilesInfo(Script, opts);
            if (errMsg != null)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show(_window, $"Unable to read script attachments\r\n\r\n[Message]\r\n{errMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            List<EncodedFileInfo> files = new List<EncodedFileInfo>();
            foreach (var kv in fileDict.OrderBy(kv => kv.Key, StringComparer.InvariantCulture))
            {
                AttachedFolders.Add(new AttachFolderItem(kv.Key, kv.Value));
                files.AddRange(kv.Value);
            }

            Parallel.ForEach(files, fi =>
            {
                if (fi.EncodedSize <= EncodedFile.DecodeInMemorySizeLimit)
                    fi.EncodeMode = EncodedFile.GetEncodeMode(Script, fi.FolderName, fi.FileName, true);
            });
        }

        public void SelectScriptAttachedFolder(string folderName)
        {
            foreach (AttachFolderItem fi in AttachedFolders)
            {
                if (fi.FolderName.Equals(folderName, StringComparison.Ordinal))
                {
                    SelectedAttachedFolder = fi;
                    foreach (AttachFileItem item in AttachedFiles)
                        item.IsSelected = false;
                    break;
                }

            }
        }

        public void SelectScriptAttachedFile(string fileName)
        {
            foreach (AttachFileItem fi in AttachedFiles)
            {
                if (fi.FileName.Equals(fileName, StringComparison.Ordinal))
                {
                    fi.IsSelected = true;
                    break;
                }
                else
                {
                    fi.IsSelected = false;
                }
            }
        }

        public void ReadUIControlInfo(UIControl uiCtrl)
        {
            UIControlModifiedEventToggle = true;

            switch (uiCtrl.Type)
            {
                case UIControlType.TextBox:
                    {
                        UIInfo_TextBox info = uiCtrl.Info.Cast<UIInfo_TextBox>();

                        UICtrlTextBoxInfo = info;
                        break;
                    }
                case UIControlType.TextLabel:
                    {
                        UIInfo_TextLabel info = uiCtrl.Info.Cast<UIInfo_TextLabel>();

                        UICtrlTextLabelInfo = info;
                        break;
                    }
                case UIControlType.NumberBox:
                    {
                        UIInfo_NumberBox info = uiCtrl.Info.Cast<UIInfo_NumberBox>();

                        UICtrlNumberBoxInfo = info;
                        break;
                    }
                case UIControlType.CheckBox:
                    {
                        UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();

                        UICtrlCheckBoxInfo = info;
                        UICtrlSectionToRun = info.SectionName;
                        UICtrlHideProgress = info.HideProgress;
                        break;
                    }
                case UIControlType.ComboBox:
                    {
                        UIInfo_ComboBox info = uiCtrl.Info.Cast<UIInfo_ComboBox>();

                        UICtrlComboBoxInfo = info;
                        UICtrlSectionToRun = info.SectionName;
                        UICtrlHideProgress = info.HideProgress;
                        break;
                    }
                case UIControlType.Image:
                    {
                        UIInfo_Image info = uiCtrl.Info.Cast<UIInfo_Image>();

                        UICtrlImageInfo = info;
                        UICtrlImageSet = EncodedFile.ContainsInterface(Script, uiCtrl.Text);
                        break;
                    }
                case UIControlType.TextFile:
                    {
                        Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_TextFile), "Invalid UIInfo");

                        UICtrlTextFileSet = EncodedFile.ContainsInterface(Script, uiCtrl.Text);
                        break;
                    }
                case UIControlType.Button:
                    {
                        UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();

                        UICtrlButtonInfo = info;
                        UICtrlSectionToRun = info.SectionName;
                        UICtrlHideProgress = info.HideProgress;
                        UICtrlButtonPictureSet = info.Picture != null && EncodedFile.ContainsInterface(Script, info.Picture);
                        break;
                    }
                case UIControlType.WebLabel:
                    {
                        UIInfo_WebLabel info = uiCtrl.Info.Cast<UIInfo_WebLabel>();

                        UICtrlWebLabelInfo = info;
                        break;
                    }
                case UIControlType.RadioButton:
                    {
                        UIInfo_RadioButton info = uiCtrl.Info.Cast<UIInfo_RadioButton>();

                        UICtrlRadioButtonList = Renderer.UICtrls.Where(x => x.Type == UIControlType.RadioButton).ToList();
                        UICtrlRadioButtonInfo = info;
                        UICtrlSectionToRun = info.SectionName;
                        UICtrlHideProgress = info.HideProgress;
                        break;
                    }
                case UIControlType.Bevel:
                    {
                        UIInfo_Bevel info = uiCtrl.Info.Cast<UIInfo_Bevel>();

                        UICtrlBevelInfo = info;
                        break;
                    }
                case UIControlType.FileBox:
                    {
                        UIInfo_FileBox info = uiCtrl.Info.Cast<UIInfo_FileBox>();

                        UICtrlFileBoxInfo = info;
                        break;
                    }
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                        UICtrlRadioGroupInfo = info;
                        UICtrlSectionToRun = info.SectionName;
                        UICtrlHideProgress = info.HideProgress;
                        break;
                    }
            }

            UIControlModifiedEventToggle = false;
        }
        #endregion

        #region Methods - WriteScript
        public bool WriteScriptGeneral(bool refresh = true)
        {
            if (Script == null)
                return false;

            // Check m.ScriptVersion
            string verStr = StringEscaper.ProcessVersionString(ScriptVersion);
            if (verStr == null)
            {
                string errMsg = $"Invalid version string [{ScriptVersion}], please check again";
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show(errMsg + '.', "Invalid Version String", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            IniKey[] keys =
            {
                new IniKey(ScriptSection.Names.Main, "Title", ScriptTitle),
                new IniKey(ScriptSection.Names.Main, "Author", ScriptAuthor),
                new IniKey(ScriptSection.Names.Main, "Version", ScriptVersion),
                new IniKey(ScriptSection.Names.Main, "Date", ScriptDate),
                new IniKey(ScriptSection.Names.Main, "Level", ((int)ScriptLevel).ToString()),
                new IniKey(ScriptSection.Names.Main, "Description", StringEscaper.Escape(ScriptDescription)),
                new IniKey(ScriptSection.Names.Main, "Selected", ScriptSelectedState.ToString()),
                new IniKey(ScriptSection.Names.Main, "Mandatory", ScriptMandatory.ToString()),
            };

            IniReadWriter.WriteKeys(Script.RealPath, keys);
            Script = Script.Project.RefreshScript(Script);

            if (refresh)
                RefreshMainWindow();

            ScriptHeaderNotSaved = false;
            return true;
        }

        public Task<bool> WriteScriptInterfaceAsync(string activeInterfaceSection = null, bool refreshMainWindow = true)
        {
            return Task.Run(() => WriteScriptInterface(activeInterfaceSection, refreshMainWindow));
        }

        public bool WriteScriptInterface(string activeInterfaceSection = null, bool refreshMainWindow = true)
        {
            if (Renderer == null)
                return false;

            try
            {
                if (SelectedUICtrl != null)
                    WriteUIControlInfo(SelectedUICtrl);

                UIControl.Update(Renderer.UICtrls);
                UIControl.Delete(UICtrlToBeDeleted);
                UICtrlToBeDeleted.Clear();
                IniReadWriter.DeleteKeys(Script.RealPath, UICtrlKeyChanged.Select(x => new IniKey(InterfaceSectionName, x)));
                UICtrlKeyChanged.Clear();

                if (activeInterfaceSection != null)
                {
                    IniReadWriter.WriteKey(Script.RealPath, ScriptSection.Names.Main, Script.Const.Interface, activeInterfaceSection);
                }

                Script = Script.Project.RefreshScript(Script);

                if (refreshMainWindow)
                    RefreshMainWindow();
            }
            catch (Exception e)
            {
                MessageBox.Show(_window, $"Unable to save the script interface.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(e)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            InterfaceNotSaved = false;
            return true;
        }

        public void RefreshMainWindow()
        {
            MainViewModel.DisplayScript(Script);
        }

        public void WriteUIControlInfo(UIControl uiCtrl)
        {
            switch (uiCtrl.Type)
            {
                case UIControlType.CheckBox:
                    {
                        UIInfo_CheckBox info = uiCtrl.Info.Cast<UIInfo_CheckBox>();

                        info.SectionName = string.IsNullOrWhiteSpace(UICtrlSectionToRun) ? null : UICtrlSectionToRun;
                        info.HideProgress = UICtrlHideProgress;
                        break;
                    }
                case UIControlType.ComboBox:
                    {
                        UIInfo_ComboBox info = uiCtrl.Info.Cast<UIInfo_ComboBox>();

                        uiCtrl.Text = info.Items[info.Index];
                        info.SectionName = string.IsNullOrWhiteSpace(UICtrlSectionToRun) ? null : UICtrlSectionToRun;
                        info.HideProgress = UICtrlHideProgress;
                        break;
                    }
                case UIControlType.Image:
                    {
                        Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_Image), "Invalid UIInfo");

                        UICtrlImageSet = EncodedFile.ContainsInterface(Script, uiCtrl.Text);
                        break;
                    }
                case UIControlType.TextFile:
                    {
                        Debug.Assert(uiCtrl.Info.GetType() == typeof(UIInfo_TextFile), "Invalid UIInfo");

                        UICtrlTextFileSet = EncodedFile.ContainsInterface(Script, uiCtrl.Text);
                        break;
                    }
                case UIControlType.Button:
                    {
                        UIInfo_Button info = uiCtrl.Info.Cast<UIInfo_Button>();

                        UICtrlButtonPictureSet = info.Picture != null && EncodedFile.ContainsInterface(Script, info.Picture);
                        info.SectionName = string.IsNullOrWhiteSpace(UICtrlSectionToRun) ? null : UICtrlSectionToRun;
                        info.HideProgress = UICtrlHideProgress;
                        break;
                    }
                case UIControlType.RadioButton:
                    {
                        UIInfo_RadioButton info = uiCtrl.Info.Cast<UIInfo_RadioButton>();

                        info.SectionName = string.IsNullOrWhiteSpace(UICtrlSectionToRun) ? null : UICtrlSectionToRun;
                        info.HideProgress = UICtrlHideProgress;
                        break;
                    }
                case UIControlType.RadioGroup:
                    {
                        UIInfo_RadioGroup info = uiCtrl.Info.Cast<UIInfo_RadioGroup>();

                        info.SectionName = string.IsNullOrWhiteSpace(UICtrlSectionToRun) ? null : UICtrlSectionToRun;
                        info.HideProgress = UICtrlHideProgress;
                        break;
                    }
            }
        }
        #endregion

        #region Methods - For Editor
        public void DrawScript()
        {
            if (Renderer == null)
                return;

            ScaleTransform transform;
            if (InterfaceScaleFactor == 100)
            {
                transform = new ScaleTransform(1, 1);
            }
            else
            {
                double scale = InterfaceScaleFactor / 100.0;
                transform = new ScaleTransform(scale, scale);
            }
            InterfaceCanvas.LayoutTransform = transform;

            Renderer.Render();
            InterfaceLoaded = true;
        }

        public void ResetSelectedUICtrl()
        {
            SelectedUICtrl = null;
            SelectedUICtrls = null;
            InterfaceCanvas.ClearSelectedElements(true);
        }
        #endregion
    }
    #endregion

    #region AttachFolderItem, AttachFileItem
    public class AttachFolderItem : ViewModelBase
    {
        #region Constructor
        public AttachFolderItem(string folderName, IReadOnlyList<EncodedFileInfo> infos)
        {
            FolderName = folderName;
            Children = new ObservableCollection<AttachFileItem>(infos.Select(x => new AttachFileItem(x)));
        }

        public AttachFolderItem(string folderName, IReadOnlyList<AttachFileItem> files)
        {
            FolderName = folderName;
            Children = new ObservableCollection<AttachFileItem>(files);
        }
        #endregion

        #region Property
        private string _folderName;
        public string FolderName
        {
            get => _folderName;
            set => SetProperty(ref _folderName, value);
        }

        private readonly object _childrenLock = new object();
        private ObservableCollection<AttachFileItem> _children;
        public ObservableCollection<AttachFileItem> Children
        {
            get => _children;
            set => SetCollectionProperty(ref _children, _childrenLock, value);
        }

        public int FileCount => Children.Count;
        #endregion
    }

    public class AttachFileItem : ViewModelBase
    {
        #region Constructor
        public AttachFileItem(EncodedFileInfo info)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
        }
        #endregion

        #region Property
        public EncodedFileInfo Info;
        public string FileName => Info.FileName;
        public string RawSize => $"{NumberHelper.NaturalByteSizeToSIUnit(Info.RawSize)} ({Info.RawSize:N0})";
        public string EncodedSize => $"{NumberHelper.NaturalByteSizeToSIUnit(Info.EncodedSize)} ({Info.EncodedSize:N0})";
        public string Compression
        {
            get
            {
                switch (Info.EncodeMode)
                {
                    case EncodedFile.EncodeMode.Raw:
                        return "None";
                    case EncodedFile.EncodeMode.ZLib:
                        return "Deflate";
                    case EncodedFile.EncodeMode.XZ:
                        return "LZMA2";
                    case null:
                        return "Not Inspected";
                    default:
                        return "Unknown";
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public void PropertyUpdate()
        {
            OnPropertyUpdate(nameof(FileName));
            OnPropertyUpdate(nameof(RawSize));
            OnPropertyUpdate(nameof(EncodedSize));
            OnPropertyUpdate(nameof(Compression));
        }
        #endregion
    }
    #endregion

    #region ScriptEditCommands
    public static class ScriptEditCommands
    {
        #region Command - Save
        public static readonly RoutedCommand Save = new RoutedUICommand(nameof(Save), nameof(Save), typeof(ScriptEditCommands), new InputGestureCollection
        {
            new KeyGesture(Key.S, ModifierKeys.Control),
        });
        #endregion   
    }
    #endregion
}
