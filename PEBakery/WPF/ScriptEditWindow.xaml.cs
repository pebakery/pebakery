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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PEBakery.WPF
{
    #region UIControlModifiedEvent
    public class UIControlModifiedEventArgs : EventArgs
    {
        public UIControl UIControl => UIControls[0];
        public UIControl[] UIControls { get; private set; } = Array.Empty<UIControl>();
        public bool MultiSelect { get; private set; }
        public bool InfoNotUpdated { get; set; }

        public UIControlModifiedEventArgs(UIControl uiCtrl, bool infoNotUpdated)
        {
            MultiSelect = false;
            UIControls = new UIControl[1] { uiCtrl };
            InfoNotUpdated = infoNotUpdated;
        }
        public UIControlModifiedEventArgs(IEnumerable<UIControl> uiCtrls, bool infoNotUpdated)
        {
            MultiSelect = true;
            UIControls = uiCtrls.ToArray();
            InfoNotUpdated = infoNotUpdated;
        }
    }

    public delegate void UIControlModifiedHandler(object sender, UIControlModifiedEventArgs e);
    #endregion

    #region ScriptEditWindow
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
            m = new ScriptEditViewModel(sc, this, mainViewModel);

            try
            {
                DataContext = m;

                InitializeComponent();

                m.InterfaceCanvas.UIControlSelected += InterfaceCanvas_UIControlSelected;
                m.InterfaceCanvas.UIControlMoved += InterfaceCanvas_UIControlDragged;
                m.InterfaceCanvas.UIControlResized += InterfaceCanvas_UIControlDragged;
                m.UIControlModified += ViewModel_UIControlModified;
                m.SwitchInterfaceStatusProgressBar = StatusProgressSwitch.Status;

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
                switch (MessageBox.Show(this, "The script interface was modified.\r\n\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation))
                {
                    case MessageBoxResult.Yes:
                        // Do not use e.Cancel here, when script file is moved the method will always fail
                        if (m.WriteScriptInterface(m.SelectedInterfaceSectionName))
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

        private void Window_Closed(object? sender, EventArgs e)
        {
            m.InterfaceCanvas.UIControlSelected -= InterfaceCanvas_UIControlSelected;
            m.InterfaceCanvas.UIControlMoved -= InterfaceCanvas_UIControlDragged;
            m.InterfaceCanvas.UIControlResized -= InterfaceCanvas_UIControlDragged;
            m.UIControlModified -= ViewModel_UIControlModified;

            if (m.Renderer != null)
                m.Renderer.Clear();
            Interlocked.Decrement(ref Count);
            CommandManager.InvalidateRequerySuggested();
        }
        #endregion

        #region Event Handler - Interface
        #region For Editor
        private void ScaleFactor_ValueChanged(object? sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            m.DrawScript();
        }

        private void ActiveSectionComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (m.InterfaceSectionName == null)
                return;

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
                    m.WriteScriptInterface(m.SelectedInterfaceSectionName);
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

        private void UIControlComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (m.Renderer == null)
                return;

            if (m.InterfaceUICtrlIndex < 0 || m.Renderer.UICtrls.Count <= m.InterfaceUICtrlIndex)
                return;

            m.SelectedUICtrl = m.Renderer.UICtrls[m.InterfaceUICtrlIndex];
            m.InterfaceCanvas.ClearSelectedElements(true);
            m.InterfaceCanvas.DrawSelectedElement(m.SelectedUICtrl);
        }
        #endregion

        #region For Interface Move/Resize via Mouse
        /// <summary>
        /// Update modified UIControl(s).
        /// </summary>
        private void ViewModel_UIControlModified(object? sender, UIControlModifiedEventArgs e)
        {
            if (m.Renderer == null)
                return;

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

        /// <summary>
        /// Selected UIControl(s) from interface canvas.
        /// </summary>
        private void InterfaceCanvas_UIControlSelected(object? sender, UIControlSelectedEventArgs e)
        {
            if (e.IsReset)
            { // Reset
                m.SelectedUICtrl = null;
                m.SelectedUICtrls = null;
                m.InterfaceUICtrlIndex = -1;
            }
            else if (e.MultiSelect)
            { // Selected multiple controls
                m.SelectedUICtrl = null;
                m.SelectedUICtrls = e.UIControls.ToList();
                m.InterfaceUICtrlIndex = -1;
            }
            else
            { // Selected single control
                m.SelectedUICtrls = null;
                m.SelectedUICtrl = e.UIControl;
                m.ReadUIControlInfo(m.SelectedUICtrl);

                if (m.Renderer != null)
                {
                    int idx = m.Renderer.UICtrls.FindIndex(x => x.Key.Equals(e.UIControl.Key));
                    m.InterfaceUICtrlIndex = idx;
                }
            }
        }

        /// <summary>
        /// Dragged UIControl(s) from interface canvas.
        /// </summary>
        private void InterfaceCanvas_UIControlDragged(object? sender, UIControlDraggedEventArgs e)
        {
            switch (e.DragState)
            {
                case DragState.Start:
                    { // Started dragging, set 
                        m.InterfaceControlDragging = true;
                        m.InterfaceControlDragDelta = new Vector(0, 0);
                    }
                    break;
                case DragState.Dragging:
                    { // In the middle of dragging, update status bar
                        m.InterfaceControlDragDelta = e.Delta;
                    }
                    break;
                case DragState.Finished:
                    { // Dragging finished, refresh dragged UIControl
                        if (e.MultiSelect == false)
                        {
                            // m.SelectedUICtrl should have been set to e.UIControl by InterfaceCanvas_UIControlSelected
                            Debug.Assert(m.SelectedUICtrl == e.UIControl, "Incorrect m.SelectedUICtrl");
                        }

                        m.InterfaceControlDragging = false;
                        m.InterfaceControlDragDelta = new Vector(0, 0);

                        // UIControl will not be updated if delta too small, to prevent unintended 1px shift.
                        if (e.ForceUpdate || DragCanvas.IsDeltaRelevant(e.Delta))
                            m.InvokeUIControlEvent(true);
                    }
                    break;
            }
        }

        /// <summary>
        /// Event handler to start UIControl dragging
        /// </summary>
        private void InterfaceScrollViewer_PreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
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

            // Update current cursor position to the status bar
            m.InterfaceCursorPos = cvCursor;
            m.InterfaceControlDragDelta = new Vector(0, 0);
            m.UpdateCursorPosStatus(m.InterfaceControlDragging, true);
        }

        /// <summary>
        /// Event handler to show current position to the status bar
        /// </summary>
        private void InterfaceScrollViewer_PreviewMouseMove(object? sender, MouseEventArgs e)
        {
            m.InterfaceCursorPos = e.GetPosition(m.InterfaceCanvas);
            m.UpdateCursorPosStatus(m.InterfaceControlDragging, false);
        }
        #endregion 

        #region For Interface Move/Resize via Keyboard
        private void TabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (m.TabIndex == 1)
                InterfaceScrollViewer.Focus();
        }

        /// <summary>
        /// Handle moving UIControl via keyboard
        /// </summary>
        private void Window_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            IInputElement focusedControl = Keyboard.FocusedElement;
            if (!Equals(focusedControl, InterfaceScrollViewer))
                return;

            // [*] Delete UIControl
            if (e.Key == Key.Delete && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                if (m.SelectMode == ScriptEditViewModel.ControlSelectMode.SingleSelect ||
                    m.SelectMode == ScriptEditViewModel.ControlSelectMode.MultiSelect)
                    m.UICtrlDeleteCommand.Execute(null);
                return;
            }

            // [*] Move/Resize by keyboard
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
                    if (m.SelectedUICtrl == null)
                        throw new InvalidOperationException($"{nameof(m.SelectedUICtrl)} is null");
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
                    if (m.SelectedUICtrls == null)
                        throw new InvalidOperationException($"{nameof(m.SelectedUICtrls)} is null");
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

            // Update control delta to the status bar
            m.InterfaceCursorPos = new Point(-1, -1); // Do not display cursor position
            m.InterfaceControlDragDelta += new Vector(deltaX, deltaY); // Accumulate delta set by keyboard
            m.UpdateCursorPosStatus(true, true);
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
                case 0: // Script [General]
                    // Changing focus is required to make sure changes in UI updated to ViewModel
                    MainSaveButton.Focus();
                    m.WriteScriptGeneral();
                    break;
                case 1: // Script [Interface]
                    m.WriteScriptInterface(m.SelectedInterfaceSectionName);
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
            UICtrlAddTypeSource = new ObservableCollection<string>(UIControl.LexicalDict
                .Where(x => x.Value != UIControlType.None)
                .Select(x => x.Value.ToString()));

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

        #region Event
        public event UIControlModifiedHandler? UIControlModified;
        #endregion

        #region Property - Basic
        private readonly object _scriptLock = new object();
        public Script Script { get; private set; }
        private readonly Window _window;
        public MainViewModel MainViewModel { get; }
        public UIRenderer? Renderer { get; private set; }
        public string? InterfaceSectionName { get; private set; }

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

        private ImageSource? _scriptLogoImage;
        public ImageSource? ScriptLogoImage
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

        private DrawingBrush? _scriptLogoSvg;
        public DrawingBrush? ScriptLogoSvg
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

        private EncodedFileInfo? _scriptLogoInfo;
        public EncodedFileInfo? ScriptLogoInfo
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
        // InterfaceSection
        private ObservableCollection<string> _interfaceSectionNames = new ObservableCollection<string>();
        private readonly object _interfaceSectionNamesLock = new object();
        public ObservableCollection<string> InterfaceSectionNames
        {
            get => _interfaceSectionNames;
            set => SetCollectionProperty(ref _interfaceSectionNames, _interfaceSectionNamesLock, value);
        }

        private string? _selectedInterfaceSectionName;
        public string? SelectedInterfaceSectionName
        {
            get => _selectedInterfaceSectionName;
            set => SetProperty(ref _selectedInterfaceSectionName, value);
        }

        // Add Control
        // UIControl.UIControlLexiDict is converted to UiCtrlAddTypeSource on constructor
        private ObservableCollection<string> _uiCtrlAddTypeSource = new ObservableCollection<string>();
        private readonly object _uiCtrlAddTypeSourceLock = new object();
        public ObservableCollection<string> UICtrlAddTypeSource
        {
            get => _uiCtrlAddTypeSource;
            set => SetCollectionProperty(ref _uiCtrlAddTypeSource, _uiCtrlAddTypeSourceLock, value);
        }

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
        public bool InterfaceNotSaved { get; set; } = false;
        public bool InterfaceUpdated { get; set; } = false;

        // Interface View Canvas
        private DragCanvas? _interfaceCanvas;
        public DragCanvas InterfaceCanvas
        {
            get
            {
                if (_interfaceCanvas == null)
                    throw new InvalidOperationException($"{nameof(_interfaceCanvas)} is null");
                return _interfaceCanvas;
            }
            set => SetProperty(ref _interfaceCanvas, value);
        }

        private int _interfaceScaleFactor = 100;
        public int InterfaceScaleFactor
        {
            get => _interfaceScaleFactor;
            set => SetProperty(ref _interfaceScaleFactor, value);
        }

        // Interface cursor position
        public const int InterfaceCursorStatusUpdateFps = 20;

        private DateTimeOffset _interfaceCursorPosLastUpdate;
        public DateTimeOffset InterfaceCursorPosLastUpdate
        {
            get => _interfaceCursorPosLastUpdate;
            set => SetProperty(ref _interfaceCursorPosLastUpdate, value);
        }

        private Point _interfaceCursorPos = new Point(0, 0);
        public Point InterfaceCursorPos
        {
            get => _interfaceCursorPos;
            set => SetProperty(ref _interfaceCursorPos, value);
        }

        private bool _interfaceControlDragging = false;
        public bool InterfaceControlDragging
        {
            get => _interfaceControlDragging;
            set => SetProperty(ref _interfaceControlDragging, value);
        }

        private Vector _interfaceControlDragDelta = new Vector(0, 0);
        public Vector InterfaceControlDragDelta
        {
            get => _interfaceControlDragDelta;
            set => SetProperty(ref _interfaceControlDragDelta, value);
        }

        // Interface StatusBar & ProgressBar
        private string _interfaceStatusBarText = string.Empty;
        public string InterfaceStatusBarText
        {
            get => _interfaceStatusBarText;
            set => SetProperty(ref _interfaceStatusBarText, value);
        }

        private StatusProgressSwitch _switchInterfaceStatusProgressBar = StatusProgressSwitch.Status;
        public StatusProgressSwitch SwitchInterfaceStatusProgressBar
        {
            get => _switchInterfaceStatusProgressBar;
            set
            {
                _switchInterfaceStatusProgressBar = value;
                switch (value)
                {
                    case StatusProgressSwitch.Status:
                        InterfaceStatusBarVisibility = Visibility.Visible;
                        InterfaceProgressBarVisibility = Visibility.Collapsed;
                        break;
                    case StatusProgressSwitch.Progress:
                        InterfaceStatusBarVisibility = Visibility.Collapsed;
                        InterfaceProgressBarVisibility = Visibility.Visible;
                        break;
                }
            }
        }

        private Visibility _interfaceStatusBarVisibility = Visibility.Collapsed;
        public Visibility InterfaceStatusBarVisibility
        {
            get => _interfaceStatusBarVisibility;
            set => SetProperty(ref _interfaceStatusBarVisibility, value);
        }

        private bool _interfaceProgressBarIndeterminate;
        public bool InterfaceProgressBarIndeterminate
        {
            get => _interfaceProgressBarIndeterminate;
            set => SetProperty(ref _interfaceProgressBarIndeterminate, value);
        }

        private double _interfaceProgressBarMinimum = 0;
        public double InterfaceProgressBarMinimum
        {
            get => _interfaceProgressBarMinimum;
            set => SetProperty(ref _interfaceProgressBarMinimum, value);
        }

        private double _interfaceProgressBarMaximum = 100;
        public double InterfaceProgressBarMaximum
        {
            get => _interfaceProgressBarMaximum;
            set => SetProperty(ref _interfaceProgressBarMaximum, value);
        }

        private double _interfaceProgressBarValue = 0;
        public double InterfaceProgressBarValue
        {
            get => _interfaceProgressBarValue;
            set => SetProperty(ref _interfaceProgressBarValue, value);
        }

        private Visibility _interfaceProgressBarVisibility = Visibility.Visible;
        public Visibility InterfaceProgressBarVisibility
        {
            get => _interfaceProgressBarVisibility;
            set => SetProperty(ref _interfaceProgressBarVisibility, value);
        }

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
        private UIControl? _selectedUICtrl = null;
        public UIControl? SelectedUICtrl
        {
            get => _selectedUICtrl;
            set
            {
                _selectedUICtrl = value;
                _selectedUICtrls = null;

                // UIControl Shared Argument
                OnPropertyUpdate(nameof(UICtrlEditEnabled));
                OnPropertyUpdate(nameof(UICtrlType));
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
                OnPropertyUpdate(nameof(ShowUICtrlListItemButton));
                OnPropertyUpdate(nameof(ShowUICtrlRunOptional));

                CommandManager.InvalidateRequerySuggested();
            }
        }
        // Multi-select
        private List<UIControl>? _selectedUICtrls = null;
        public List<UIControl>? SelectedUICtrls
        {
            get => _selectedUICtrls;
            set
            {
                _selectedUICtrl = null;
                _selectedUICtrls = value;

                // UIControl Shared Argument
                OnPropertyUpdate(nameof(UICtrlEditEnabled));
                OnPropertyUpdate(nameof(UICtrlType));
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
                OnPropertyUpdate(nameof(ShowUICtrlListItemButton));
                OnPropertyUpdate(nameof(ShowUICtrlRunOptional));

                CommandManager.InvalidateRequerySuggested();
            }
        }

        #region Shared Arguments
        public bool UICtrlEditEnabled => _selectedUICtrl != null;
        public string UICtrlType
        {
            get => _selectedUICtrl != null ? _selectedUICtrl.Type.ToString() : "None";
        }
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
            get
            {
                if (_selectedUICtrl == null)
                    return 0;

                int x = _selectedUICtrl.X;
                if (InterfaceControlDragging)
                    x += (int)InterfaceControlDragDelta.X;
                return x;
            }
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
            get
            {
                if (_selectedUICtrl == null)
                    return 0;

                int y = _selectedUICtrl.Y;
                if (InterfaceControlDragging)
                    y += (int)InterfaceControlDragDelta.Y;
                return y;
            }
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
            get
            {
                if (_selectedUICtrl == null)
                    return string.Empty;
                return _selectedUICtrl.Info.ToolTip ?? string.Empty;
            }
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
        public Visibility ShowUICtrlListItemButton
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
        private UIInfo_TextBox? _uiCtrlTextBoxInfo;
        public UIInfo_TextBox? UICtrlTextBoxInfo
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
        private UIInfo_TextLabel? _uiCtrlTextLabelInfo;
        public UIInfo_TextLabel? UICtrlTextLabelInfo
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
        private UIInfo_NumberBox? _uiCtrlNumberBoxInfo;
        public UIInfo_NumberBox? UICtrlNumberBoxInfo
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
        private UIInfo_CheckBox? _uiCtrlCheckBoxInfo;
        public UIInfo_CheckBox? UICtrlCheckBoxInfo
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
        private UIInfo_ComboBox? _uiCtrlComboBoxInfo;
        public UIInfo_ComboBox? UICtrlComboBoxInfo
        {
            get => _uiCtrlComboBoxInfo;
            set
            {
                _uiCtrlComboBoxInfo = value;
            }
        }
        #endregion
        #region For Image
        private UIInfo_Image? _uiCtrlImageInfo;
        public UIInfo_Image? UICtrlImageInfo
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
        public UIInfo_Button? UICtrlButtonInfo { get; set; }
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
        private UIInfo_WebLabel? _uiCtrlWebLabelInfo;
        public UIInfo_WebLabel? UICtrlWebLabelInfo
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

                _uiCtrlWebLabelInfo.Url = value;
                OnPropertyUpdate(nameof(UICtrlWebLabelInfo.Url));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For RadioButton
        public List<UIControl> UICtrlRadioButtonList { get; set; } = new List<UIControl>();
        private UIInfo_RadioButton? _uiCtrlRadioButtonInfo;
        public UIInfo_RadioButton? UICtrlRadioButtonInfo
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
        private UIInfo_Bevel? _uiCtrlBevelInfo;
        public UIInfo_Bevel? UICtrlBevelInfo
        {
            get => _uiCtrlBevelInfo;
            set
            {
                _uiCtrlBevelInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlBevelCaptionEnabled));
                OnPropertyUpdate(nameof(UICtrlBevelCaption));
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
                if (UICtrlBevelInfo == null)
                    return false;

                return UICtrlBevelInfo.CaptionEnabled;
            }
            set
            {
                if (UICtrlBevelInfo == null)
                    return;
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
                if (UICtrlBevelInfo == null)
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
                if (UICtrlBevelInfo == null)
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
                if (UICtrlBevelInfo == null || _uiCtrlBevelInfo == null)
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
                if (UICtrlBevelInfo == null || _uiCtrlBevelInfo == null)
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
        private UIInfo_FileBox? _uiCtrlFileBoxInfo;
        public UIInfo_FileBox? UICtrlFileBoxInfo
        {
            get => _uiCtrlFileBoxInfo;
            set
            {
                _uiCtrlFileBoxInfo = value;
                if (value == null)
                    return;

                OnPropertyUpdate(nameof(UICtrlFileBoxFileChecked));
                OnPropertyUpdate(nameof(UICtrlFileBoxDirChecked));
                OnPropertyUpdate(nameof(UICtrlFileBoxTitle));
                OnPropertyUpdate(nameof(UICtrlFileBoxFilter));
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

        public string UICtrlFileBoxTitle
        {
            get => _uiCtrlFileBoxInfo?.Title ?? string.Empty;
            set
            {
                if (_uiCtrlFileBoxInfo == null)
                    return;

                _uiCtrlFileBoxInfo.Title = value;
                OnPropertyUpdate(nameof(UICtrlFileBoxTitle));
                InvokeUIControlEvent(true);
            }
        }
        public string UICtrlFileBoxFilter
        {
            get => _uiCtrlFileBoxInfo?.Filter ?? string.Empty;
            set
            {
                if (_uiCtrlFileBoxInfo == null)
                    return;

                _uiCtrlFileBoxInfo.Filter = value;
                OnPropertyUpdate(nameof(UICtrlFileBoxFilter));
                InvokeUIControlEvent(true);
            }
        }
        #endregion
        #region For RadioGroup
        private UIInfo_RadioGroup? _uiCtrlRadioGroupInfo;
        public UIInfo_RadioGroup? UICtrlRadioGroupInfo
        {
            get => _uiCtrlRadioGroupInfo;
            set
            {
                _uiCtrlRadioGroupInfo = value;
            }
        }
        #endregion
        #region For (Common) ListItemEdit
        public int UICtrlListItemCount
        {
            set
            {
                UICtrlListItemEditButtonText = $"Edit List ({value} item{(value == 1 ? string.Empty : "s")})";
            }
        }

        private string _uiCtrlListItemEditButtonText = string.Empty;
        public string UICtrlListItemEditButtonText
        {
            get => _uiCtrlListItemEditButtonText;
            set => SetProperty(ref _uiCtrlListItemEditButtonText, value);
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
        private string? _uiCtrlSectionToRun;
        public string? UICtrlSectionToRun
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

        private long _scriptFileSize;
        public long ScriptFileSize
        {
            get => _scriptFileSize;
            set
            {
                SetProperty(ref _scriptFileSize, value);
                ScriptFileSizeStr = NumberHelper.NaturalByteSizeToSIUnit(value);
            }
        }

        private string _scriptFileSizeStr = string.Empty;
        public string ScriptFileSizeStr
        {
            get => _scriptFileSizeStr;
            set => SetProperty(ref _scriptFileSizeStr, value);
        }

        private readonly object _attachedFoldersLock = new object();
        private ObservableCollection<AttachFolderItem> _attachedFolders = new ObservableCollection<AttachFolderItem>();
        public ObservableCollection<AttachFolderItem> AttachedFolders
        {
            get => _attachedFolders;
            set => SetCollectionProperty(ref _attachedFolders, _attachedFoldersLock, value);
        }

        private AttachFolderItem? _selectedAttachedFolder;
        public AttachFolderItem? SelectedAttachedFolder
        {
            get => _selectedAttachedFolder;
            set
            {
                _selectedAttachedFolder = value;
                OnPropertyUpdate();

                if (_selectedAttachedFolder != null)
                    AttachedFiles = _selectedAttachedFolder.Children;
                else
                    AttachedFiles.Clear();
            }
        }

        private readonly object _attachedFilesLock = new object();
        private ObservableCollection<AttachFileItem> _attachedFiles = new ObservableCollection<AttachFileItem>();
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

        private bool _attachAdvancedViewEnabled = false;
        public bool AttachAdvancedViewEnabled
        {
            get => _attachAdvancedViewEnabled;
            set => SetProperty(ref _attachAdvancedViewEnabled, value);
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
                    if (_selectedUICtrl != null)
                        UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrl, infoNotUpdated));
                    break;
                case ControlSelectMode.MultiSelect:
                    if (_selectedUICtrls != null)
                        UIControlModified?.Invoke(this, new UIControlModifiedEventArgs(_selectedUICtrls, true));
                    break;
            }
        }
        #endregion

        #region Command - Logo
        public ICommand ScriptLogoAttachCommand => new RelayCommand(ScriptLogoAttachCommand_Execute, CanExecuteFunc);
        public ICommand ScriptLogoExtractCommand => new RelayCommand(ScriptLogoExtractCommand_Execute, ScriptLogoLoadedFunc);
        public ICommand ScriptLogoDeleteCommand => new RelayCommand(ScriptLogoDeleteCommand_Execute, ScriptLogoLoadedFunc);

        private bool ScriptLogoLoadedFunc(object? parameter)
        {
            return CanExecuteCommand && ScriptLogoLoaded;
        }

        private void ScriptLogoAttachCommand_Execute(object? parameter)
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

        private void ScriptLogoExtractCommand_Execute(object? parameter)
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

        private void ScriptLogoDeleteCommand_Execute(object? parameter)
        {
            CanExecuteCommand = false;
            try
            {
                if (!EncodedFile.ContainsLogo(Script))
                {
                    MessageBox.Show(_window, "Logo does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ResultReport<Script> report = EncodedFile.DeleteLogo(Script);
                if (report.Success && report.Result != null)
                {
                    Script = report.Result;
                    MessageBox.Show(_window, "Logo successfully deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    ScriptLogoUpdated = true;
                    ReadScriptGeneral();
                }
                else
                {
                    string errMsg = report.Message;
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
        private ICommand? _interfaceSectionAddCommand;
        private ICommand? _interfaceSectionDeleteCommand;
        public ICommand InterfaceSectionAddCommand => GetRelayCommand(ref _interfaceSectionAddCommand, "Add interface section", InterfaceSectionAddCommand_Execute, InterfaceSectionAddCommand_CanExecute);
        public ICommand InterfaceSectionDeleteCommand => GetRelayCommand(ref _interfaceSectionDeleteCommand, "Delete interface section", InterfaceSectionDeleteCommand_Execute, InterfaceSectionDeleteCommand_CanExecute);

        private bool InterfaceSectionAddCommand_CanExecute(object? sender)
        {
            return CanExecuteCommand;
        }

        private async void InterfaceSectionAddCommand_Execute(object? sender)
        {
            CanExecuteCommand = false;
            try
            {
                // Must save current edits to switch active interface section
                if (InterfaceNotSaved)
                {
                    MessageBoxResult result = MessageBox.Show(_window, "The script must be saved before adding a new interface.\r\n\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                    if (result == MessageBoxResult.Yes)
                        WriteScriptInterface(null);
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
                        Script? newScript = Script.Project.RefreshScript(Script);
                        if (newScript == null)
                        {
                            string errMsg = $"Script [{Script.Title}] refresh error.";
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                            MessageBox.Show(_window, errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        Script = newScript;
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

        private bool InterfaceSectionDeleteCommand_CanExecute(object? sender)
        {
            return CanExecuteCommand &&
                   InterfaceSectionNames != null && SelectedInterfaceSectionName != null &&
                   1 < InterfaceSectionNames.Count && !SelectedInterfaceSectionName.Equals(ScriptSection.Names.Interface);
        }

        private async void InterfaceSectionDeleteCommand_Execute(object? sender)
        {
            if (Renderer == null)
                return;
            if (SelectedInterfaceSectionName == null)
                return;

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
                    WriteScriptInterface(null);
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
                    // If not, overwrite InterfaceList with new section names.
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
                    Script? newScript = Script.Project.RefreshScript(Script);
                    if (newScript == null)
                    {
                        string errMsg = $"Script [{Script.Title}] refresh error.";
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                        MessageBox.Show(_window, errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Script = newScript;
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
        private ICommand? _uiCtrlAddCommand;
        private ICommand? _uiCtrlDeleteCommand;
        private ICommand? _uiCtrlReloadCommand;
        public ICommand UICtrlAddCommand => GetRelayCommand(ref _uiCtrlAddCommand, "Add UIControl", UICtrlAddCommand_Execute, UICtrlAddCommand_CanExecute);
        public ICommand UICtrlDeleteCommand => GetRelayCommand(ref _uiCtrlDeleteCommand, "Delete UIControl", UICtrlDeleteCommand_Execute, UICtrlDeleteCommand_CanExecute);
        public ICommand UICtrlReloadCommand => GetRelayCommand(ref _uiCtrlReloadCommand, "Reload UIControl", UICtrlReloadCommand_Execute, UICtrlReloadCommand_CanExecute);

        private bool UICtrlAddCommand_CanExecute(object? sender)
        {
            return CanExecuteCommand && (0 <= UICtrlAddTypeIndex && UICtrlAddTypeIndex < UIControl.LexicalDict.Count);
        }

        private void UICtrlAddCommand_Execute(object? sender)
        {
            if (Renderer == null)
                return;
            if (InterfaceSectionName == null)
                return;

            CanExecuteCommand = false;
            try
            {
                Debug.Assert(UIControl.LexicalDict.ContainsKey(UICtrlAddTypeIndex), "Invalid UIControl AddType index");

                UIControlType type = UIControl.LexicalDict[UICtrlAddTypeIndex];
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
                    Script? newScript = Script.Project.RefreshScript(Script);
                    if (newScript == null)
                    {
                        string errMsg = $"Script [{Script.Title}] refresh error.";
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                        MessageBox.Show(_window, errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Script = newScript;
                }

                ScriptSection ifaceSection = Script.Sections[InterfaceSectionName];
                string line = UIControl.GetUIControlTemplate(type, key);

                UIControl? uiCtrl = UIParser.ParseStatement(line, ifaceSection, out List<LogInfo> errorLogs);
                if (uiCtrl == null)
                {
                    Global.Logger.SystemWrite(errorLogs);
                    return;
                }

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

        private bool UICtrlDeleteCommand_CanExecute(object? sender)
        {
            return CanExecuteCommand && ((SelectedUICtrls != null && SelectedUICtrls.Count > 0) || (SelectedUICtrl != null));
        }

        private void UICtrlDeleteCommand_Execute(object? parameter)
        {
            if (Renderer == null)
                return;

            CanExecuteCommand = false;
            try
            {
                List<UIControl> toScrubEncodedFile = new List<UIControl>();
                if (SelectMode == ControlSelectMode.SingleSelect)
                {
                    // Single-Select
                    if (SelectedUICtrl == null)
                        return;

                    UIControl uiCtrl = SelectedUICtrl;

                    // If uiCtrl to delete has encoded file, alert user to save
                    if (UIControl.HasInterfaceEncodedFile.Contains(uiCtrl.Type))
                    {
                        // Must save current edits to switch active interface section
                        MessageBoxResult result = MessageBox.Show(_window, $"The script must be saved when deleting [{uiCtrl.Type}].\r\n\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                        if (result == MessageBoxResult.Yes)
                            toScrubEncodedFile.Add(uiCtrl);
                        else
                            return;
                    }

                    UICtrlToBeDeleted.Add(uiCtrl);

                    Renderer.UICtrls.Remove(uiCtrl);
                }
                else if (SelectMode == ControlSelectMode.MultiSelect)
                {
                    // Multi-Select
                    if (SelectedUICtrls == null || SelectedUICtrls.Count == 0)
                        return;

                    UICtrlToBeDeleted.AddRange(SelectedUICtrls);

                    // If uiCtrl to delete has encoded file, alert user to save
                    UIControl[] hasEncodedFiles = SelectedUICtrls.Where(x => UIControl.HasInterfaceEncodedFile.Contains(x.Type)).ToArray();
                    if (0 < hasEncodedFiles.Length)
                    {
                        UIControlType[] msgTypes = hasEncodedFiles.Select(x => x.Type).Distinct().ToArray();
                        string msgTypeStr = string.Join(", ", msgTypes);

                        // Must save current edits to switch active interface section
                        MessageBoxResult result = MessageBox.Show(_window, $"The script must be saved when deleting [{msgTypeStr}].\r\n\r\nSave changes?", "Save Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                        if (result == MessageBoxResult.Yes)
                            toScrubEncodedFile.AddRange(hasEncodedFiles);
                        else
                            return;
                    }

                    foreach (UIControl uiCtrl in SelectedUICtrls)
                    {
                        Renderer.UICtrls.Remove(uiCtrl);
                    }
                }

                InterfaceUICtrls = new ObservableCollection<string>(Renderer.UICtrls.Select(x => x.Key));
                InterfaceUICtrlIndex = 0;

                Renderer.Render();
                SelectedUICtrl = null;
                SelectedUICtrls = null;

                if (0 < toScrubEncodedFile.Count)
                { // Includes Image, FileBox, Button - Interface-Encoded attachment should be deleted, interface should be saved
                    foreach (UIControl uiCtrl in toScrubEncodedFile)
                    {
                        // Remove control's encoded file so we don't have orphaned Interface-Encoded attachments.
                        DeleteInterfaceEncodedFile(uiCtrl);
                    }

                    // Save into script file (InterfaceNotSaved is set to false inside function)
                    WriteScriptInterface();
                }
                else
                { // No need to save
                    //// Update MainWindow only
                    //RefreshMainWindow();

                    // TODO
                    InterfaceNotSaved = true;
                }

                InterfaceUpdated = true;
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool UICtrlReloadCommand_CanExecute(object? sender)
        {
            // Only in Tab [Interface]
            return CanExecuteCommand && TabIndex == 1;
        }

        private void UICtrlReloadCommand_Execute(object? parameter)
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
        private ICommand? _uiCtrlImageAutoResizeCommand;
        public ICommand UICtrlImageAutoResizeCommand => GetRelayCommand(ref _uiCtrlImageAutoResizeCommand, "Auto resize Image control", UICtrlImageAutoResizeCommand_Execute, CanExecuteFunc);

        private async void UICtrlImageAutoResizeCommand_Execute(object? parameter)
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
                        WriteScriptInterface(null);
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
                    WriteScriptInterface(null);
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
        private ICommand? _uiCtrlRadioButtonSelectCommand;
        public ICommand UICtrlRadioButtonSelectCommand => GetRelayCommand(ref _uiCtrlRadioButtonSelectCommand, "Select RadioButton", UICtrlRadioButtonSelectCommand_Execute, UICtrlRadioButtonSelectCommand_CanExecute);

        private bool UICtrlRadioButtonSelectCommand_CanExecute(object? parameter)
        {
            return CanExecuteCommand && UICtrlRadioButtonSelectEnable;
        }

        private void UICtrlRadioButtonSelectCommand_Execute(object? parameter)
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
        #region For ListItemEditWindow
        private ICommand? _uiCtrlListItemEditCommand;
        public ICommand UICtrlListItemEditCommand => GetRelayCommand(ref _uiCtrlListItemEditCommand, "Edit ListItem of the selected control", UICtrlListItemEditCommand_Execute, CanExecuteFunc);

        private void UICtrlListItemEditCommand_Execute(object? parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlImageAutoResizeButton_Click";

                Debug.Assert(SelectedUICtrl != null, internalErrorMsg);

                UIControl uiCtrl = SelectedUICtrl;
                ListItemEditViewModel editViewModel = new ListItemEditViewModel(uiCtrl);
                ListItemEditDialog editDialog = new ListItemEditDialog
                {
                    DataContext = editViewModel,
                    Owner = _window
                };
                bool? result = editDialog.ShowDialog();
                if (result == true)
                {
                    UICtrlListItemCount = editViewModel.Items.Count;
                    InvokeUIControlEvent(false);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region For (Common) InterfaceEncoded 
        private ICommand? _uiCtrlInterfaceAttachCommand;
        private ICommand? _uiCtrlInterfaceExtractCommand;
        private ICommand? _uiCtrlInterfaceResetCommand;
        public ICommand UICtrlInterfaceAttachCommand => GetRelayCommand(ref _uiCtrlInterfaceAttachCommand, "Attach file", UICtrlInterfaceAttachCommand_Execute, CanExecuteFunc);
        public ICommand UICtrlInterfaceExtractCommand => GetRelayCommand(ref _uiCtrlInterfaceExtractCommand, "Extract attached file", UICtrlInterfaceExtractCommand_Execute, CanExecuteFunc);
        public ICommand UICtrlInterfaceResetCommand => GetRelayCommand(ref _uiCtrlInterfaceResetCommand, "Delete attached file", UICtrlInterfaceResetCommand_Execute, CanExecuteFunc);

        private async void UICtrlInterfaceAttachCommand_Execute(object? parameter)
        {
            if (Renderer == null)
                return;

            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceAttachButton_Click";

                if (parameter is not string sender)
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
                        WriteScriptInterface(null);
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
                    if (!Global.FileTypeDetector.IsText(srcFilePath))
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
                    ResultReport<EncodedFileInfo[]> report = EncodedFile.ReadFolderInfo(Script, ScriptSection.Names.InterfaceEncoded, false);
                    if (!report.Success || report.Result == null)
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, report.Message));
                        MessageBox.Show(_window, $"Attach failed.\r\n\r\n[Message]\r\n{report.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    EncodedFileInfo[] infos = report.Result;
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
                            if (UICtrlButtonInfo == null)
                                throw new InvalidOperationException($"{nameof(UICtrlButtonInfo)} is null");
                            UICtrlButtonInfo.Picture = srcFileName;
                            UICtrlButtonPictureSet = true;
                            break;
                    }

                    InvokeUIControlEvent(false);
                    WriteScriptInterface(null);
                    RefreshMainWindow();
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
        private async void UICtrlInterfaceExtractCommand_Execute(object? parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceExtractCommand_Execute";

                if (parameter is not string sender)
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
                    if (UICtrlButtonInfo == null)
                        throw new InvalidOperationException($"{nameof(UICtrlButtonInfo)} is null");
                    if (UICtrlButtonInfo.Picture == null)
                        throw new InvalidOperationException($"{nameof(UICtrlButtonInfo.Picture)} is null");
                    fileName = UICtrlButtonInfo.Picture;
                }
                string ext = Path.GetExtension(fileName);

                string extFilter;
                if (selectedType == UIControlType.TextFile)
                    extFilter = $"Text File|*{ext}";
                else
                    extFilter = $"{ext.ToUpper().Replace(".", string.Empty)} Image|*{ext}";

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
        private void UICtrlInterfaceResetCommand_Execute(object? parameter)
        {
            CanExecuteCommand = false;
            try
            {
                const string internalErrorMsg = "Internal Logic Error at UICtrlInterfaceResetButton_Click";

                if (parameter is not string sender)
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
                        WriteScriptInterface(null);
                    else
                        return;
                }

                // Delete interface encoded file to prevent orphaned Interface-Encoded attachments
                string? fileName = DeleteInterfaceEncodedFile(SelectedUICtrl);
                if (fileName == null)
                    throw new InvalidOperationException($"Deleting InterfaceEncodedFile of [{SelectedUICtrl.Key}] ({SelectedUICtrl.Type}) is not supported");
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
                        if (UICtrlButtonInfo == null)
                            throw new InvalidOperationException($"{nameof(UICtrlButtonInfo)} is null");
                        UICtrlButtonInfo.Picture = null;
                        UICtrlButtonPictureSet = false;
                        break;
                }
                InvokeUIControlEvent(false);
                WriteScriptInterface(null);
                RefreshMainWindow();
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private string? DeleteInterfaceEncodedFile(UIControl uiCtrl)
        {
            void InternalDeleteInterfaceEncodedFile(string delFileName)
            {
                ResultReport<Script> report = EncodedFile.DeleteFile(Script, ScriptSection.Names.InterfaceEncoded, delFileName);
                if (report.Success && report.Result != null)
                    Script = report.Result;
                else
                    MessageBox.Show(_window, $"Unable to delete encoded file [{delFileName}].", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // If two or more controls are referencing encoded file, do not delete it.
            Dictionary<string, int> fileRefCountDict = EncodedFile.GetInterfaceFileRefCount(Script);

            string? fileName = null;
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
        private bool CanExecuteFunc(object? parameter)
        {
            return CanExecuteCommand;
        }
        #endregion

        #region Command - Attachment (Folder)
        private ICommand? _addFolderCommand;
        private ICommand? _renameFolderCommand;
        private ICommand? _extractFolderCommand;
        private ICommand? _deleteFolderCommand;
        private ICommand? _attachFileCommand;
        public ICommand AddFolderCommand => GetRelayCommand(ref _addFolderCommand, "Add folder", AddFolderCommand_Execute, CanExecuteFunc);
        public ICommand RenameFolderCommand => GetRelayCommand(ref _renameFolderCommand, "Rename folder", RenameFolderCommand_Execute, FolderSelected_CanExecute);
        public ICommand ExtractFolderCommand => GetRelayCommand(ref _extractFolderCommand, "Extract folder", ExtractFolderCommand_Execute, FolderSelected_CanExecute);
        public ICommand DeleteFolderCommand => GetRelayCommand(ref _deleteFolderCommand, "Delete folder", DeleteFolderCommand_Execute, FolderSelected_CanExecute);
        public ICommand AttachFileCommand => GetRelayCommand(ref _attachFileCommand, "Attach file", AttachFileCommand_Execute, FolderSelected_CanExecute);

        private bool FolderSelected_CanExecute(object? parameter)
        {
            return CanExecuteCommand && SelectedAttachedFolder != null;
        }

        private async void AddFolderCommand_Execute(object? parameter)
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

                    Script = await EncodedFile.AddFolderAsync(Script, folderName, false);
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

        private async void RenameFolderCommand_Execute(object? parameter)
        {
            AttachFolderItem? folder = SelectedAttachedFolder;
            if (folder == null)
                return;

            CanExecuteCommand = false;
            try
            {
                TextBoxDialog dialog = new TextBoxDialog(_window, "Rename folder", "Please input new folder name.", PackIconMaterialKind.Pencil)
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

                string? errMsg;
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

        private async void ExtractFolderCommand_Execute(object? parameter)
        {
            AttachFolderItem? folder = SelectedAttachedFolder;
            if (folder == null)
                return;

            CanExecuteCommand = false;
            try
            {
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
                    PackIconMaterialKind msgBoxIcon = PackIconMaterialKind.Information;
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

                    // Failure Report
                    if (1 <= failureFiles.Count)
                    {
                        msgBoxIcon = PackIconMaterialKind.Alert;
                        b.AppendLine();
                        b.AppendLine($"Unable to extract {successFiles.Count} files.");
                        foreach ((string fileName, Exception ex) in failureFiles)
                        {
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                            b.AppendLine($"- {fileName} ({Logger.LogExceptionMessage(ex)})");
                        }
                    }

                    const string msgTitle = "Extraction Report";
                    TextViewDialog reportDialog = new TextViewDialog(_window, msgTitle, msgTitle, b.ToString(), msgBoxIcon);
                    reportDialog.ShowDialog();
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void DeleteFolderCommand_Execute(object? parameter)
        {
            AttachFolderItem? folder = SelectedAttachedFolder;
            if (folder == null)
                return;

            CanExecuteCommand = false;
            try
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to delete [{folder.FolderName}]?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;

                AttachProgressIndeterminate = true;

                ResultReport<Script> report = await EncodedFile.DeleteFolderAsync(Script, folder.FolderName);
                if (report.Success && report.Result != null)
                {
                    Script = report.Result;
                    ScriptAttachUpdated = true;
                    ReadScriptAttachment();
                }
                else // Failure
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, report.Message));
                    MessageBox.Show(_window, $"Delete failed.\r\n\r\n[Message]\r\n{report.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                AttachProgressIndeterminate = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void AttachFileCommand_Execute(object? parameter)
        {
            AttachFolderItem? folder = SelectedAttachedFolder;
            if (folder == null)
                return;

            CanExecuteCommand = false;
            try
            {
                ScriptAttachFileDialog dialog = new ScriptAttachFileDialog { Owner = _window };
                if (dialog.ShowDialog() != true)
                    return;

                (string Name, string Path)[] srcFiles;
                if (dialog.MultiSelect)
                {
                    srcFiles = dialog.FilePaths.Select(path => (Path.GetFileName(path), path)).ToArray();
                }
                else
                {
                    srcFiles = new (string, string)[] { (dialog.FileName, dialog.FilePath) };
                }
                EncodeMode mode = dialog.EncodeMode;

                // Check validity of srcFile
                foreach ((string srcFileName, string srcFilePath) in srcFiles)
                {
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
                        MessageBox.Show(_window, "File name cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // EncodedFile exception guard
                try
                {
                    // Confirm overwrite
                    string[] overwriteFileNames = srcFiles
                        .Where(tup => EncodedFile.ContainsFile(Script, folder.FolderName, tup.Name))
                        .Select(tup => tup.Name).ToArray();
                    if (1 <= overwriteFileNames.Length)
                    {
                        StringBuilder b = new StringBuilder(overwriteFileNames.Length + 3);
                        if (overwriteFileNames.Length == 1)
                        {
                            b.AppendLine($"Attached file [{overwriteFileNames[0]}] will be overwritten.");
                        }
                        else
                        {
                            b.AppendLine($"[{overwriteFileNames.Length}] attached file will be overwritten.");
                            foreach (string overwriteFileName in overwriteFileNames)
                                b.AppendLine($"- {overwriteFileName}");
                        }
                        b.AppendLine();
                        b.Append("Would you like to proceed?");

                        MessageBoxResult result = MessageBox.Show(
                            b.ToString(),
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
                        await EncodedFile.AttachFilesAsync(Script, folder.FolderName, srcFiles, mode, progress);
                    }
                    finally
                    {
                        AttachProgressValue = -1;
                        CanExecuteCommand = true;
                    }
                    MessageBox.Show(_window, "File(s) successfully attached.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Finalize and update interface
                    ScriptAttachUpdated = true;
                    ReadScriptAttachment();

                    SelectScriptAttachedFolder(folder.FolderName);
                    SelectScriptAttachedFile(srcFiles[0].Name);
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show(_window, $"File attachment failed.\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        private ICommand? _renameFileCommand;
        private ICommand? _extractFileCommand;
        private ICommand? _deleteFileCommand;
        private ICommand? _openFileCommand;
        private ICommand? _inspectFileCommand;
        public ICommand RenameFileCommand => GetRelayCommand(ref _renameFileCommand, "Rename file", RenameFileCommand_Execute, FileSingleSelected_CanExecute);
        public ICommand ExtractFileCommand => GetRelayCommand(ref _extractFileCommand, "Extract file", ExtractFileCommand_Execute, FileSelected_CanExecute);
        public ICommand DeleteFileCommand => GetRelayCommand(ref _deleteFileCommand, "Delete file", DeleteFileCommand_Execute, FileSelected_CanExecute);
        public ICommand OpenFileCommand => GetRelayCommand(ref _openFileCommand, "Open file", OpenFileCommand_Execute, FileSingleSelected_CanExecute);
        public ICommand InspectFileCommand => GetRelayCommand(ref _inspectFileCommand, "Inspect file", InspectFileCommand_Execute, FileSelected_CanExecute);

        private bool FileSelected_CanExecute(object? parameter)
        {
            return CanExecuteCommand && SelectedAttachedFolder != null && 0 < SelectedAttachedFiles.Length;
        }

        private bool FileSingleSelected_CanExecute(object? parameter)
        {
            return CanExecuteCommand && SelectedAttachedFolder != null && SelectedAttachedFiles.Length == 1;
        }

        private async void RenameFileCommand_Execute(object? parameter)
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
                        $"The file's extension is being changed from [{oldExt}] to [{newExt}].\r\nAre you sure you want to continue?",
                        "File Extension Changed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (result != MessageBoxResult.Yes)
                        return;
                }

                CanExecuteCommand = false;
                AttachProgressIndeterminate = true;

                string? errorMsg;
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

        private async void ExtractFileCommand_Execute(object? parameter)
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
                    // .Net Core's System.Windows.Forms.FolderBrowserDialog (WinForms) does support Vista-style dialog.
                    // But it requires HWND to be displayed properly.
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
                    PackIconMaterialKind msgBoxIcon = PackIconMaterialKind.Information;
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

                    // Failure Report
                    if (1 <= failureFiles.Count)
                    {
                        msgBoxIcon = PackIconMaterialKind.Alert;
                        b.AppendLine();
                        b.AppendLine($"Unable to extract {successFiles.Count} files.");
                        foreach ((string fileName, Exception ex) in failureFiles)
                        {
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                            b.AppendLine($"- {fileName} ({Logger.LogExceptionMessage(ex)})");
                        }
                    }

                    const string msgTitle = "Extraction Report";
                    TextViewDialog reportDialog = new TextViewDialog(_window, msgTitle, msgTitle, b.ToString(), msgBoxIcon);
                    reportDialog.ShowDialog();
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void DeleteFileCommand_Execute(object? parameter)
        {
            AttachFolderItem? folder = SelectedAttachedFolder;
            if (folder == null)
                return;

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

                    ResultReport<Script> report = await EncodedFile.DeleteFileAsync(Script, fi.FolderName, fi.FileName);
                    if (report.Success && report.Result != null)
                    {
                        Script = report.Result;
                        ScriptAttachUpdated = true;
                        ReadScriptAttachment();

                        SelectScriptAttachedFolder(fi.FolderName);
                    }
                    else // Failure
                    {
                        Global.Logger.SystemWrite(new LogInfo(LogState.Error, report.Message));
                        MessageBox.Show(_window, $"Unable to delete file [{fi.FileName}]\r\n- {report.Message}", "Delete Failure", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    string folderName = folder.FolderName;
                    string[] fileNames = SelectedAttachedFiles.Select(fi => fi.FileName).ToArray();


                    ResultReport<Script, string[]> report = await EncodedFile.DeleteFilesAsync(Script, folderName, fileNames);
                    if (report.Success)
                    {
                        Script = report.Result1;
                        ScriptAttachUpdated = true;
                        ReadScriptAttachment();

                        SelectScriptAttachedFolder(folderName);
                    }
                    else
                    {
                        string[] errorMessages = report.Result2;

                        // Failure Report
                        StringBuilder b = new StringBuilder();
                        b.AppendLine($"Unable to delete {errorMessages.Length} files.");
                        foreach (string errMsg in errorMessages)
                        {
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                            b.AppendLine($"- {errMsg}");
                        }
                        TextViewDialog reportDialog = new TextViewDialog(_window, "Delete Report", "Delete Failure", b.ToString(), PackIconMaterialKind.Alert);
                        reportDialog.ShowDialog();
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

        private async void OpenFileCommand_Execute(object? parameter)
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
                string tempFile = Path.Combine(tempDir, fi.FileName);
                try
                {
                    using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                    {
                        await EncodedFile.ExtractFileAsync(Script, fi.FolderName, fi.FileName, fs, progress);
                    }
                }
                catch (Exception ex)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, ex));
                    MessageBox.Show(_window,
                        $"Unable to open file [{fi.FileName}].\r\n\r\n[Message]\r\n{Logger.LogExceptionMessage(ex)}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                ResultReport result = FileHelper.OpenPath(tempFile);
                if (!result.Success)
                {
                    MessageBox.Show(_window,
                        $"Unable to open file [{fi.FileName}].\r\n\r\n[Message]\r\n{result.Message}",
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

        private async void InspectFileCommand_Execute(object? parameter)
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

                    ResultReport<EncodedFileInfo> report = await EncodedFile.ReadFileInfoAsync(Script, dirName, fileName, true);
                    if (report.Success && report.Result != null)
                    { // Success
                        EncodedFileInfo newInfo = report.Result;
                        file.Info = newInfo;
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
                    TextViewDialog reportDialog = new TextViewDialog(_window, "Inspection Report", "Inspection Failure", b.ToString(), PackIconMaterialKind.Alert);
                    reportDialog.ShowDialog();
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
        private ICommand? _enableAdvancedViewCommand;
        public ICommand EnableAdvancedViewCommand => GetRelayCommand(ref _enableAdvancedViewCommand, "Enable advanced view", EnableAdvancedViewCommand_Execute, CanExecuteFunc);

        private void EnableAdvancedViewCommand_Execute(object? parameter)
        {
            CanExecuteCommand = false;
            try
            {
                ReadScriptAttachment();
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
                ResultReport<EncodedFileInfo> report = EncodedFile.ReadLogoInfo(Script, true);
                if (!report.Success)
                {
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, report.Message));
                    MessageBox.Show(_window, $"Unable to read script logo\r\n\r\n[Message]\r\n\r\n{report.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                ScriptLogoInfo = report.Result;
            }
            else
            { // No script logo
                ScriptLogoIcon = PackIconMaterialKind.BorderNone;
                ScriptLogoInfo = null;
            }

            ScriptTitle = Script.Title;
            ScriptAuthor = Script.Author;
            ScriptVersion = Script.RawVersion;
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
                Debug.Assert(InterfaceSectionNames.Contains(ScriptSection.Names.Interface, StringComparer.OrdinalIgnoreCase), "Invalid interface section selected");

                if (InterfaceSectionNames.Contains(Script.InterfaceSectionName, StringComparer.OrdinalIgnoreCase))
                    SelectedInterfaceSectionName = InterfaceSectionName = Script.InterfaceSectionName;
                else
                    SelectedInterfaceSectionName = InterfaceSectionName = ScriptSection.Names.Interface;
            }
            else
            {
                InterfaceSectionName = SelectedInterfaceSectionName;
            }

            // Change made to interface should not affect script file immediately.
            // Make a copy of uiCtrls to prevent this.
            (List<UIControl>? uiCtrls, List<LogInfo> errLogs) = UIRenderer.LoadInterfaces(Script, InterfaceSectionName);
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

            InterfaceStatusBarText = $"Interface [{SelectedInterfaceSectionName}] loaded";

            DrawScript();
            ResetSelectedUICtrl();
        }

        public void ReadScriptAttachment()
        {
            // Attachment
            AttachedFolders.Clear();
            AttachedFiles.Clear();

            ReadFileInfoOptions opts = new ReadFileInfoOptions
            {
                IncludeAuthorEncoded = AttachAdvancedViewEnabled,
                IncludeInterfaceEncoded = AttachAdvancedViewEnabled,
                InspectEncodeMode = false,
            };

            ResultReport<Dictionary<string, List<EncodedFileInfo>>> report = EncodedFile.ReadAllFilesInfo(Script, opts);
            if (!report.Success || report.Result == null)
            {
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, report.Message));
                MessageBox.Show(_window, $"Unable to read script attachments\r\n\r\n[Message]\r\n{report.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Dictionary<string, List<EncodedFileInfo>> fileDict = report.Result;
            List<EncodedFileInfo> files = new List<EncodedFileInfo>();
            foreach (var kv in fileDict.OrderBy(kv => kv.Key, StringComparer.InvariantCulture))
            {
                AttachedFolders.Add(new AttachFolderItem(kv.Key, kv.Value));
                files.AddRange(kv.Value);
            }

            // Reading encode mode form encoded file requires decompression of footer.
            Parallel.ForEach(files, fi =>
            {
                if (fi.EncodedSize <= EncodedFile.DecodeInMemorySizeLimit)
                    fi.EncodeMode = EncodedFile.ReadEncodeMode(Script, fi.FolderName, fi.FileName, true);
            });

            // Attachment
            ScriptFileSize = new FileInfo(Script.RealPath).Length;
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
            if (Renderer == null)
                return;

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

                        UICtrlListItemCount = UICtrlComboBoxInfo.Items.Count;
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

                        UICtrlListItemCount = UICtrlRadioGroupInfo.Items.Count;
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
            string? verStr = StringEscaper.ProcessVersionString(ScriptVersion);
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
            Script? newScript = Script.Project.RefreshScript(Script);
            if (newScript == null)
            {
                string errMsg = $"Script [{Script.Title}] refresh error.";
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                MessageBox.Show(_window, errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Script = newScript;
            }

            if (refresh)
                RefreshMainWindow();

            ScriptHeaderNotSaved = false;
            return true;
        }

        public Task<bool> WriteScriptInterfaceAsync(string? activeInterfaceSection = null)
        {
            return Task.Run(() => WriteScriptInterface(activeInterfaceSection));
        }

        public bool WriteScriptInterface(string? activeInterfaceSection = null)
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
                if (InterfaceSectionName != null)
                    IniReadWriter.DeleteKeys(Script.RealPath, UICtrlKeyChanged.Select(x => new IniKey(InterfaceSectionName, x)));
                UICtrlKeyChanged.Clear();

                if (activeInterfaceSection == null ||
                    activeInterfaceSection.Equals(Script.InterfaceSectionName, StringComparison.OrdinalIgnoreCase))
                { // [Interface] is active -> Remove "Interface=" key
                    IniReadWriter.DeleteKey(Script.RealPath, ScriptSection.Names.Main, Script.Const.Interface);
                }
                else
                { // Other than [Interface] is active -> Set "Interface=" key
                    IniReadWriter.WriteKey(Script.RealPath, ScriptSection.Names.Main, Script.Const.Interface, activeInterfaceSection);
                }

                InterfaceStatusBarText = $"Interface [{SelectedInterfaceSectionName}] loaded";
                Script? newScript = Script.Project.RefreshScript(Script);
                if (newScript == null)
                {
                    string errMsg = $"Script [{Script.Title}] refresh error.";
                    Global.Logger.SystemWrite(new LogInfo(LogState.Error, errMsg));
                    MessageBox.Show(_window, errMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    Script = newScript;
                }
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
                        info.SectionName = string.IsNullOrWhiteSpace(UICtrlSectionToRun) ? string.Empty : UICtrlSectionToRun;
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

        #region Methods - For Editor / Canvas
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

        /// <summary>
        /// Check if cursor position status bar should be updated
        /// </summary>
        /// <returns>Return true if update is necessary</returns>
        public bool CheckCursorPosStatusUpdate()
        {
            // Update only 20 times per second to reduce performance hit
            DateTimeOffset now = DateTime.UtcNow;
            if (now - InterfaceCursorPosLastUpdate < TimeSpan.FromMilliseconds(1000 / InterfaceCursorStatusUpdateFps))
                return false;

            InterfaceCursorPosLastUpdate = now;
            return true;
        }

        public void UpdateCursorPosStatus(bool dragging, bool forceUpdate)
        {
            string GetDeltaStatus(Vector d)
            {
                // Force update
                OnPropertyUpdate(nameof(UICtrlX));
                OnPropertyUpdate(nameof(UICtrlY));

                // Add delta info to the status
                return $"d: ({(int)d.X:+#;-#;0}, {(int)d.Y:+#;-#;0})";
            }
            // Update at specific interval
            if (!forceUpdate && !CheckCursorPosStatusUpdate())
                return;

            // Check if InterfaceCursorPos is in the range of canvas
            if (InterfaceCursorPos.X < 0 || InterfaceCursorPos.Y < 0)
            {
                if (dragging)
                    InterfaceStatusBarText = GetDeltaStatus(InterfaceControlDragDelta);

                return;
            }

            // Display current cursor position
            string status = $"Cursor: ({(int)InterfaceCursorPos.X}, {(int)InterfaceCursorPos.Y})";

            // Interface Control is being dragged
            if (dragging)
                status += " / " + GetDeltaStatus(InterfaceControlDragDelta);

            InterfaceStatusBarText = status;
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
        private string _folderName = string.Empty;
        public string FolderName
        {
            get => _folderName;
            set => SetProperty(ref _folderName, value);
        }

        private readonly object _childrenLock = new object();
        private ObservableCollection<AttachFileItem> _children = new ObservableCollection<AttachFileItem>();
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
        public EncodedFileInfo Info { get; set; }
        public string FileName => Info.FileName;
        public string RawSize => NumberHelper.NaturalByteSizeToSIUnit(Info.RawSize);
        public string EncodedSize => NumberHelper.NaturalByteSizeToSIUnit(Info.EncodedSize);
        public string Compression
        {
            get
            {
                switch (Info.EncodeMode)
                {
                    case EncodeMode.Raw:
                        return "None";
                    case EncodeMode.ZLib:
                        return "Deflate";
                    case EncodeMode.XZ:
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
