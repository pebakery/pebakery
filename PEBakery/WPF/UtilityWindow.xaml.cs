/*
    Copyright (C) 2016-present Hajin Jang
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
using PEBakery.Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PEBakery.WPF
{
    public partial class UtilityWindow : Window
    {
        #region Field and Constructor
        private static int _count = 0;

        private readonly UtilityViewModel _m;

        /// <summary>
        /// True when RegConverterOutput currently holds .reg-format text (from
        /// ScriptToRegCommand), false when it holds PEBakery script text (from
        /// RegToScriptCommand) or is otherwise stale/empty. Save must force UTF-16 BOM
        /// whenever this is true, regardless of the extension the user picks in the save
        /// dialog -- Windows' own regedit.exe will refuse or mis-handle a .reg file that
        /// isn't UTF-16 LE with BOM (or the legacy ASCII REGEDIT4 form, which this
        /// converter never emits on output), so the save path can't rely on the file
        /// extension alone.
        /// </summary>
        private bool _regConverterOutputIsRegFile = false;

        public UtilityWindow(FontHelper.FontInfo monoFont)
        {
            Acquire();

            _m = new UtilityViewModel(monoFont);

            InitializeComponent();
            DataContext = _m;

            // Populate projects
            if (Global.Projects == null)
            {
                DialogResult = false;
                Close();
                return;
            }

            List<Project> projects = Global.Projects.ProjectList;
            for (int i = 0; i < projects.Count; i++)
            {
                Project p = projects[i];

                _m.Projects.Add(new Tuple<string, Project>(p.ProjectName, p));

                ProjectTreeItemModel? curMainTree = Global.MainViewModel.CurMainTree;
                if (curMainTree != null)
                {
                    if (p.ProjectName.Equals(curMainTree.Script.Project.ProjectName, StringComparison.Ordinal))
                        _m.SelectedProjectIndex = i;
                }
            }
        }
        #endregion

        #region Reference Count
        public static int Acquire()
        {
            return Interlocked.Increment(ref _count);
        }

        public static int Release()
        {
            return Interlocked.Decrement(ref _count);
        }

        public static bool IsRunning()
        {
            return 0 < _count;
        }
        #endregion

        #region Window Event
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Interlocked.Decrement(ref _count);
            CommandManager.InvalidateRequerySuggested();
        }
        #endregion

        #region Button Event
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion

        #region Commands - CodeBox
        private void CodeBoxCommands_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.TabIndex == 0 && _m.CanExecuteCommand && !Engine.IsRunning;
        }

        private void CodeBoxSaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CodeBoxSaveButton.Focus();

            _m.CanExecuteCommand = false;
            Global.MainViewModel.WorkInProgress = true;
            try
            {
                _m.SaveCodeBox();
            }
            finally
            {
                _m.CanExecuteCommand = true;
                Global.MainViewModel.WorkInProgress = false;
                CommandManager.InvalidateRequerySuggested();

                CodeBoxInputTextBox.Focus();
            }
        }

        private async void CodeBoxRunCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // CodeBox not initialized
            if (_m.CodeFile == null)
                return;

            CodeBoxRunButton.Focus();

            _m.CanExecuteCommand = false;
            try
            {
                // Save CodeBox first
                _m.SaveCodeBox();

                // Run Engine
                if (Engine.TryEnterLock())
                {
                    try
                    {
                        Project project = _m.CurrentProject;
                        Script? sc = project.LoadScriptRuntime(_m.CodeFile, new LoadScriptRuntimeOptions { IgnoreMain = true });
                        if (sc == null)
                        {
                            Global.Logger.SystemWrite(new LogInfo(LogState.Error, "[CodeBox] script load failed"));
                            return;
                        }

                        MainViewModel mainModel = Global.MainViewModel;
                        mainModel.BuildTreeItems.Clear();
                        mainModel.SwitchNormalBuildInterface = false;
                        mainModel.WorkInProgress = true;

                        EngineState s = new EngineState(sc.Project, Global.Logger, mainModel, this, EngineMode.RunMainAndOne, sc);
                        s.SetOptions(Global.Setting);
                        s.SetCompat(sc.Project.Compat);

                        Engine.WorkingEngine = new Engine(s);

                        // Set StatusBar Text
                        using (CancellationTokenSource ct = new CancellationTokenSource())
                        {
                            Task printStatus = MainViewModel.PrintBuildElapsedStatus("Running CodeBox...", s, ct.Token);

                            await Engine.WorkingEngine.Run($"CodeBox - {project.ProjectName}");

                            // Cancel and Wait until PrintBuildElapsedStatus stops
                            ct.Cancel();
                            await printStatus;
                        }

                        // Turn off progress ring
                        mainModel.WorkInProgress = false;

                        // Build ended, Switch to Normal View
                        mainModel.SwitchNormalBuildInterface = true;
                        mainModel.BuildTreeItems.Clear();

                        // Report elapsed build time
                        string? haltReason = s.RunResultReport();
                        if (haltReason != null)
                            mainModel.StatusBarText = $"CodeBox took {s.Elapsed:h\\:mm\\:ss}, stopped by {haltReason}";
                        else
                            mainModel.StatusBarText = $"CodeBox took {s.Elapsed:h\\:mm\\:ss}";

                        if (mainModel.CurMainTree is ProjectTreeItemModel curMainTree)
                            s.MainViewModel.DisplayScript(curMainTree.Script);

                        if (Global.Setting.General.ShowLogAfterBuild && LogWindow.IsRunning() == false)
                        { // Open BuildLogWindow
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                if (Application.Current.MainWindow is not MainWindow w)
                                    return;

                                w.LogDialog = new LogWindow(1);
                                w.LogDialog.Show();
                            });
                        }
                    }
                    finally
                    {
                        Engine.WorkingEngine = null;
                        Engine.ExitLock();
                    }
                }
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();

                CodeBoxInputTextBox.Focus();
            }
        }
        #endregion

        #region Commands - String Escaper
        private void StringEscaperCommands_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.TabIndex == 1;
        }

        private void EscapeStringCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            EscapeButton.Focus();

            _m.EscaperConvertedString = StringEscaper.QuoteEscape(_m.EscaperStringToConvert, false, _m.EscaperEscapePercentFlag);

            ConvertedStringTextBox.Focus();
        }

        private void UnescapeStringCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            UnescapeButton.Focus();

            string str = StringEscaper.QuoteUnescape(_m.EscaperStringToConvert);
            _m.EscaperConvertedString = _m.EscaperEscapePercentFlag ? StringEscaper.UnescapePercent(str) : str;

            ConvertedStringTextBox.Focus();
        }

        private void EscapeSequenceLegendCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // No need to focus button, since _m.EscaperStringToConvert is not required here
            _m.EscaperConvertedString = StringEscaper.Legend;
            ConvertedStringTextBox.Focus();
        }
        #endregion

        #region Commands - Syntax Checker
        private void SyntaxCheckCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.TabIndex == 2 && _m.CanExecuteCommand;
        }

        private async void SyntaxCheckCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (_m.SyntaxInputCode.Length == 0)
            {
                _m.SyntaxCheckResult = "Please input code to check.";
                return;
            }

            SyntaxCheckButton.Focus();
            _m.CanExecuteCommand = false;
            try
            {
                _m.SyntaxCheckResult = "Checking syntax...";

                await Task.Run(() =>
                {
                    Project p = _m.CurrentProject;

                    Script sc = p.MainScript;
                    ScriptSection section;
                    if (p.MainScript.Sections.ContainsKey(ScriptSection.Names.Process))
                        section = sc.Sections[ScriptSection.Names.Process];
                    else // Create dummy [Process] section instance
                        section = new ScriptSection(sc, ScriptSection.Names.Process, SectionType.CodeOrUnknown, Array.Empty<string>(), 1);

                    // Split lines from SyntaxInputCode
                    List<string> lines = new List<string>();
                    using (StringReader r = new StringReader(_m.SyntaxInputCode))
                    {
                        string? line;
                        while ((line = r.ReadLine()) != null)
                        {
                            line = line.Trim();
                            lines.Add(line);
                        }
                    }

                    // Run CodeParser to retrieve parsing errors
                    CodeParser parser = new CodeParser(section, Global.Setting, sc.Project.Compat);
                    (CodeCommand[] cmds, List<LogInfo> errorLogs) = parser.ParseStatements(lines);

                    // Check macro commands
                    Macro macro = new Macro(p, p.Variables, out _);
                    if (macro.MacroEnabled)
                    {
                        foreach (CodeCommand cmd in cmds.Where(x => x.Type == CodeType.Macro))
                        {
                            CodeInfo_Macro info = (CodeInfo_Macro)cmd.Info;

                            if (!macro.GlobalDict.ContainsKey(info.MacroType))
                                errorLogs.Add(new LogInfo(LogState.Error, $"Invalid CodeType or Macro [{info.MacroType}]", cmd));
                        }
                    }

                    // Print results
                    if (0 < errorLogs.Count)
                    {
                        StringBuilder b = new StringBuilder();
                        for (int i = 0; i < errorLogs.Count; i++)
                        {
                            LogInfo log = errorLogs[i];
                            b.AppendLine($"[{i + 1}/{errorLogs.Count}] {log.Message} ({log.Command})");
                        }
                        _m.SyntaxCheckResult = b.ToString();
                    }
                    else
                    {
                        _m.SyntaxCheckResult = "No errors were found.";
                    }
                });
            }
            finally
            {
                _m.CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();

                SyntaxCheckResultTextBox.Focus();
            }
        }
        #endregion

        #region Commands - Registry Converter
        private void RegConverterCommands_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m != null && _m.TabIndex == 3 && _m.CanExecuteCommand;
        }

        /// <summary>
        /// Toggles everything that should be locked down while a load or a convert is
        /// running on the Registry Converter tab.
        /// </summary>
        private void SetRegConverterBusy(bool busy)
        {
            _m.CanExecuteCommand = !busy;
            _m.RegConverterProgressVisibility = busy ? Visibility.Visible : Visibility.Collapsed;
            RegConverterInputTextBox.IsEnabled = !busy;
            RegConverterLoadFileButton.IsEnabled = !busy;
            RegConverterHivePrefixTextBox.IsEnabled = !busy;
            RegConverterCopyButton.IsEnabled = !busy;
            RegConverterSaveButton.IsEnabled = !busy;
            RegConverterClearButton.IsEnabled = !busy;
            CommandManager.InvalidateRequerySuggested();
        }

        private async void RegToScriptCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RegToScriptButton.Focus();

            SetRegConverterBusy(true);
            _m.RegConverterOutput = "Converting registry file to PEBakery script commands...";
            try
            {
                RegConvertOptions opt = new RegConvertOptions
                {
                    HivePrefix = _m.RegConverterHivePrefix,
                };
                string input = _m.RegConverterInput;
                ConversionResult result = await Task.Run(() => RegistryConverter.ConvertRegToScript(input, opt));
                _m.RegConverterOutput = FormatWithWarnings(result);
                _regConverterOutputIsRegFile = false;
            }
            finally
            {
                SetRegConverterBusy(false);
                RegConverterOutputTextBox.Focus();
            }
        }

        private async void ScriptToRegCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ScriptToRegButton.Focus();

            SetRegConverterBusy(true);
            _m.RegConverterOutput = "Converting PEBakery script commands to registry file...";
            try
            {
                // HivePrefix intentionally not passed here -- it only applies to reg -> script.
                string input = _m.RegConverterInput;
                ConversionResult result = await Task.Run(() => RegistryConverter.ConvertScriptToReg(input));
                _m.RegConverterOutput = FormatWithWarnings(result);
                _regConverterOutputIsRegFile = true;
            }
            finally
            {
                SetRegConverterBusy(false);
                RegConverterOutputTextBox.Focus();
            }
        }

        /// <summary>
        /// Prepends any parse warnings as comment lines:
        /// - "//" for PEBakery script output
        /// - ";" for .reg output
        /// </summary>
        private static string FormatWithWarnings(ConversionResult result)
        {
            if (result.Warnings.Count == 0)
                return result.Output;
            string warningBlock = string.Join(
                Environment.NewLine,
                result.Warnings.Select(w => $"{result.CommentPrefix} [WARNING] {w}"));
            return warningBlock + Environment.NewLine + Environment.NewLine + result.Output;
        }

        private static readonly string[] RegConverterFileFilters =
        {
            "Registry/Script files (*.reg;*.script;*.txt)|*.reg;*.script;*.txt",
            "Registry files (*.reg)|*.reg",
            "PEBakery script files (*.script)|*.script",
            "Text files (*.txt)|*.txt",
            "All files (*.*)|*.*",
        };

        private void RegConverterInputTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void RegConverterInputTextBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
                return;

            _ = LoadRegConverterInputFileAsync(paths[0]);
        }

        private async void RegConverterLoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = string.Join("|", RegConverterFileFilters),
                Multiselect = false,
            };
            if (dialog.ShowDialog() == true)
                await LoadRegConverterInputFileAsync(dialog.FileName);
        }

        private async Task LoadRegConverterInputFileAsync(string path)
        {
            SetRegConverterBusy(true);
            _m.RegConverterOutput = "Loading File...";
            try
            {
                Encoding encoding = EncodingHelper.DetectEncoding(path);
                _m.RegConverterInput = await File.ReadAllTextAsync(path, encoding);
                _m.RegConverterOutput = string.Empty;
                _regConverterOutputIsRegFile = false; // stale output, no longer matches the new input
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to read [{path}]:\r\n{ex.Message}", "Load Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _m.RegConverterOutput = string.Empty;
            }
            finally
            {
                SetRegConverterBusy(false);
            }
        }

        private void RegConverterCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_m.RegConverterOutput))
                return;

            try
            {
                Clipboard.SetText(_m.RegConverterOutput);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to copy to clipboard:\r\n{ex.Message}", "Copy Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegConverterSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_m.RegConverterOutput))
                return;

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = string.Join("|", RegConverterFileFilters),
                FileName = "RegistryConverterOutput.txt",
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                Encoding encoding = GetEncodingForSaveFile(_regConverterOutputIsRegFile);
                File.WriteAllText(dialog.FileName, _m.RegConverterOutput, encoding);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to save [{dialog.FileName}]:\r\n{ex.Message}", "Save Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// .reg files must be UTF-16 LE with BOM or Windows may fail to recognize it as a valid reg file,
        /// or worse mangle non-ASCII values. This converter never outputs legacy ASCII REGEDIT4 files,
        /// so whenever the current output is detected as .reg format, UTF-16 BOM is forced regardless 
        /// of the extension the user picked in the save dialog.
        /// Everything else (PEBakery scripts, plain text) is saved as UTF-8 without a BOM.
        /// </summary>
        private static Encoding GetEncodingForSaveFile(bool isRegOutput)
        {
            if (isRegOutput)
                return Encoding.Unicode; // UTF-16 LE, includes BOM via GetPreamble()

            return new UTF8Encoding(false); // everything else as UTF-8, no BOM
        }

        private void RegConverterClearButton_Click(object sender, RoutedEventArgs e)
        {
            _m.RegConverterInput = string.Empty;
            _m.RegConverterOutput = string.Empty;
            _regConverterOutputIsRegFile = false;
            RegConverterInputTextBox.Focus();
        }
        #endregion
    }

    #region UtilityViewModel
    public class UtilityViewModel : ViewModelBase
    {
        #region Constructor
        public UtilityViewModel(FontHelper.FontInfo monoFont)
        {
            MonoFont = monoFont;
        }
        #endregion

        #region Monospaced Font Properties
        public FontHelper.FontInfo MonoFont { get; }
        public FontFamily MonoFontFamily => MonoFont.FontFamily;
        public FontWeight MonoFontWeight => MonoFont.FontWeight;
        public double MonoFontSize => MonoFont.DeviceIndependentPixelSize;
        #endregion

        #region CanExecuteCommand
        public bool CanExecuteCommand = true;
        #endregion

        #region Tab Index
        private int _tabIndex = 0;
        public int TabIndex
        {
            get => _tabIndex;
            set => SetProperty(ref _tabIndex, value);
        }
        #endregion

        #region Project Environment
        private int _selectedProjectIndex = 0;
        public int SelectedProjectIndex
        {
            get => _selectedProjectIndex;
            set
            {
                _selectedProjectIndex = value;
                LoadCodeBoxFile();
                OnPropertyUpdate(nameof(SelectedProjectIndex));
            }
        }

        private ObservableCollection<Tuple<string, Project>> _projects = new ObservableCollection<Tuple<string, Project>>();
        public ObservableCollection<Tuple<string, Project>> Projects
        {
            get => _projects;
            set => SetProperty(ref _projects, value);
        }

        public Project CurrentProject
        {
            get
            {
                int i = _selectedProjectIndex;
                if (0 <= i && i < _projects.Count)
                    return _projects[i].Item2;
                else
                    throw new InvalidOperationException("Project not selected");
            }
        }

        public async void LoadCodeBoxFile()
        {
            if (0 > _selectedProjectIndex || _projects.Count <= _selectedProjectIndex)
                return;

            Project p = _projects[_selectedProjectIndex].Item2;
            CodeFile = Path.Combine(p.ProjectDir, "CodeBox.txt");
            if (File.Exists(CodeFile))
            {
                Encoding encoding = EncodingHelper.DetectEncoding(CodeFile);
                using (StreamReader r = new StreamReader(CodeFile, encoding))
                {
                    CodeBoxInput = await r.ReadToEndAsync();
                }
            }
            else
            {
                CodeBoxInput = @"[Main]
Title=CodeBox
Description=Test Commands

[Variables]

[Process]
// Write Commands Here
//--------------------

";
            }
        }
        #endregion

        #region CodeBox
        public string? CodeFile { get; private set; }

        private string _codeBoxInput = string.Empty;
        public string CodeBoxInput
        {
            get => _codeBoxInput;
            set => SetProperty(ref _codeBoxInput, value);
        }

        public void SaveCodeBox()
        {
            // CodeBox not initialized
            if (CodeFile == null)
                return;

            // Detect encoding of text. 
            Encoding encoding;
            if (File.Exists(CodeFile))
                encoding = EncodingHelper.SmartDetectEncoding(CodeFile, CodeBoxInput);
            else
                encoding = Encoding.UTF8;

            using (StreamWriter w = new StreamWriter(CodeFile, false, encoding))
            {
                w.Write(CodeBoxInput);
            }
        }
        #endregion

        #region String Escaper
        private string _stringToConvert = string.Empty;
        public string EscaperStringToConvert
        {
            get => _stringToConvert;
            set => SetProperty(ref _stringToConvert, value);
        }

        private string _convertedString = string.Empty;
        public string EscaperConvertedString
        {
            get => _convertedString;
            set => SetProperty(ref _convertedString, value);
        }

        private bool _escapePercent = false;
        public bool EscaperEscapePercentFlag
        {
            get => _escapePercent;
            set => SetProperty(ref _escapePercent, value);
        }
        #endregion

        #region Syntax Checker
        private string _syntaxInputCode = string.Empty;
        public string SyntaxInputCode
        {
            get => _syntaxInputCode;
            set => SetProperty(ref _syntaxInputCode, value);
        }

        private string _syntaxCheckResult = string.Empty;
        public string SyntaxCheckResult
        {
            get => _syntaxCheckResult;
            set => SetProperty(ref _syntaxCheckResult, value);
        }
        #endregion

        #region Registry Converter
        private string _regConverterInput = string.Empty;
        public string RegConverterInput
        {
            get => _regConverterInput;
            set => SetProperty(ref _regConverterInput, value);
        }

        private string _regConverterOutput = string.Empty;
        public string RegConverterOutput
        {
            get => _regConverterOutput;
            set => SetProperty(ref _regConverterOutput, value);
        }

        private string _regConverterHivePrefix = "Tmp_";
        public string RegConverterHivePrefix
        {
            get => _regConverterHivePrefix;
            set => SetProperty(ref _regConverterHivePrefix, value);
        }

        private Visibility _regConverterProgressVisibility = Visibility.Collapsed;
        public Visibility RegConverterProgressVisibility
        {
            get => _regConverterProgressVisibility;
            set => SetProperty(ref _regConverterProgressVisibility, value);
        }
        #endregion
    }
    #endregion

    #region UtilityViewCommands
    public static class UtilityViewCommands
    {
        #region CodeBox
        public static readonly RoutedCommand CodeBoxSaveCommand = new RoutedUICommand("Save CodeBox", "CodeBoxSave", typeof(UtilityViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.S, ModifierKeys.Control),
            });
        public static readonly RoutedCommand CodeBoxRunCommand = new RoutedUICommand("Run CodeBox", "CodeBoxSave", typeof(UtilityViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F10),
            });
        #endregion

        #region String Escaper
        public static readonly RoutedCommand EscapeStringCommand = new RoutedUICommand("Escape string", "EscapeString", typeof(UtilityViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.E, ModifierKeys.Control),
            });
        public static readonly RoutedCommand UnescapeStringCommand = new RoutedUICommand("Unescape string", "UnescapeString", typeof(UtilityViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.E, ModifierKeys.Control | ModifierKeys.Shift),
            });
        public static readonly RoutedCommand EscapeSequenceLegendCommand = new RoutedUICommand("Print escape sequence legend", "EscapeSequenceLegend",
            typeof(UtilityViewCommands));
        #endregion

        #region Syntax Checker
        public static readonly RoutedCommand SyntaxCheckCommand = new RoutedUICommand("Run syntax check", "SyntaxCheck", typeof(UtilityViewCommands));
        #endregion

        #region Registry Converter
        public static readonly RoutedCommand RegToScriptCommand = new RoutedUICommand("Convert reg to Script", "RegToScript", typeof(UtilityViewCommands));
        public static readonly RoutedCommand ScriptToRegCommand = new RoutedUICommand("Convert Script to reg", "ScriptToReg", typeof(UtilityViewCommands));
        #endregion
    }
    #endregion
}
