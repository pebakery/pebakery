/*
    Copyright (C) 2016-2020 Hajin Jang
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
    // ReSharper disable RedundantExtendsListEntry
    public partial class UtilityWindow : Window
    {
        #region Field and Constructor
        public static int Count = 0;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly UtilityViewModel _m;

        public UtilityWindow(FontHelper.FontInfo monoFont)
        {
            Interlocked.Increment(ref Count);

            _m = new UtilityViewModel(monoFont);

            InitializeComponent();
            DataContext = _m;

            // Populate projects
            List<Project> projects = Global.Projects.ProjectList;
            for (int i = 0; i < projects.Count; i++)
            {
                Project p = projects[i];

                _m.Projects.Add(new Tuple<string, Project>(p.ProjectName, p));

                if (p.ProjectName.Equals(Global.MainViewModel.CurMainTree.Script.Project.ProjectName, StringComparison.Ordinal))
                    _m.SelectedProjectIndex = i;
            }
        }
        #endregion

        #region Window Event
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Interlocked.Decrement(ref Count);
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
                        Script sc = project.LoadScriptRuntime(_m.CodeFile, new LoadScriptRuntimeOptions { IgnoreMain = true });

                        MainViewModel mainModel = Global.MainViewModel;
                        mainModel.BuildTreeItems.Clear();
                        mainModel.SwitchNormalBuildInterface = false;
                        mainModel.WorkInProgress = true;

                        EngineState s = new EngineState(sc.Project, Global.Logger, mainModel, EngineMode.RunMainAndOne, sc);
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
                        string haltReason = s.RunResultReport();
                        if (haltReason != null)
                            mainModel.StatusBarText = $"CodeBox took {s.Elapsed:h\\:mm\\:ss}, stopped by {haltReason}";
                        else
                            mainModel.StatusBarText = $"CodeBox took {s.Elapsed:h\\:mm\\:ss}";

                        s.MainViewModel.DisplayScript(mainModel.CurMainTree.Script);
                        if (Global.Setting.General.ShowLogAfterBuild && LogWindow.Count == 0)
                        { // Open BuildLogWindow
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                if (!(Application.Current.MainWindow is MainWindow w))
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
                _m.SyntaxCheckResult = "Please input code.";
                return;
            }

            SyntaxCheckButton.Focus();
            _m.CanExecuteCommand = false;
            try
            {
                _m.SyntaxCheckResult = "Checking...";

                await Task.Run(() =>
                {
                    Project p = _m.CurrentProject;

                    Script sc = p.MainScript;
                    ScriptSection section;
                    if (p.MainScript.Sections.ContainsKey(ScriptSection.Names.Process))
                        section = sc.Sections[ScriptSection.Names.Process];
                    else // Create dummy [Process] section instance
                        section = new ScriptSection(sc, ScriptSection.Names.Process, SectionType.Code, new string[0], 1);

                    // Split lines from SyntaxInputCode
                    List<string> lines = new List<string>();
                    using (StringReader r = new StringReader(_m.SyntaxInputCode))
                    {
                        string line;
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
                            CodeInfo_Macro info = cmd.Info.Cast<CodeInfo_Macro>();

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
                        _m.SyntaxCheckResult = "Error not found.";
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
        private int _selectedProjectIndex;
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
                    return null;
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
        public string CodeFile { get; private set; }

        private string _codeBoxInput = string.Empty;
        public string CodeBoxInput
        {
            get => _codeBoxInput;
            set => SetProperty(ref _codeBoxInput, value);
        }

        public void SaveCodeBox()
        {
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
    }
    #endregion
}
