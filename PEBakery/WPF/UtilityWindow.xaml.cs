/*
    Copyright (C) 2016-2018 Hajin Jang
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

        #region Monospace Font Properties
        public FontHelper.FontInfo MonoFont { get; }
        public FontFamily MonoFontFamily => MonoFont.FontFamily;
        public FontWeight MonoFontWeight => MonoFont.FontWeight;
        public double MonoFontSize => MonoFont.FontSizeInDIP;
        #endregion

        #region Command - IsWorking
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
                Encoding encoding = EncodingHelper.DetectBom(CodeFile);
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

        private ICommand _codeBoxSaveCommand;
        private ICommand _codeBoxRunCommand;
        public ICommand CodeBoxSaveCommand => GetRelayCommand(ref _codeBoxSaveCommand, "Save CodeBox", ExecuteCodeBoxSaveCommand, CanExecuteCodeBoxSaveCommand);
        public ICommand CodeBoxRunCommand => GetRelayCommand(ref _codeBoxRunCommand, "Run CodeBox", ExecuteCodeBoxRunCommand, CanExecuteCodeBoxRunCommand);

        private async void SaveCodeBox()
        {
            Encoding encoding = Encoding.UTF8;
            if (File.Exists(CodeFile))
                encoding = EncodingHelper.DetectBom(CodeFile);
            using (StreamWriter w = new StreamWriter(CodeFile, false, encoding))
            {
                await w.WriteAsync(CodeBoxInput);
            }
        }

        private bool CanExecuteCodeBoxSaveCommand(object parameter)
        {
            return TabIndex == 0 && CanExecuteCommand && Engine.WorkingLock == 0;
        }

        private void ExecuteCodeBoxSaveCommand(object parameter)
        {
            CanExecuteCommand = false;
            Global.MainViewModel.WorkInProgress = true;
            try
            {
                SaveCodeBox();
            }
            finally
            {
                CanExecuteCommand = true;
                Global.MainViewModel.WorkInProgress = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool CanExecuteCodeBoxRunCommand(object parameter)
        {
            return TabIndex == 0 && CanExecuteCommand && Engine.WorkingLock == 0;
        }

        private async void ExecuteCodeBoxRunCommand(object parameter)
        {
            CanExecuteCommand = false;
            try
            {
                // Save CodeBox first
                SaveCodeBox();

                // Run Engine
                Interlocked.Increment(ref Engine.WorkingLock);
                try
                {
                    Project project = CurrentProject;
                    Script sc = project.LoadScriptRuntime(CodeFile, new LoadScriptRuntimeOptions());

                    SettingViewModel setting = Global.Setting;

                    Global.MainViewModel.BuildTreeItems.Clear();
                    Global.MainViewModel.SwitchNormalBuildInterface = false;
                    Global.MainViewModel.WorkInProgress = true;

                    EngineState s = new EngineState(sc.Project, Global.Logger, Global.MainViewModel, EngineMode.RunMainAndOne, sc);
                    s.SetOptions(setting);

                    Engine.WorkingEngine = new Engine(s);

                    // Set StatusBar Text
                    CancellationTokenSource ct = new CancellationTokenSource();
                    Task printStatus = MainViewModel.PrintBuildElapsedStatus("Running CodeBox...", s, ct.Token);

                    await Engine.WorkingEngine.Run($"CodeBox - {project.ProjectName}");

                    // Cancel and Wait until PrintBuildElapsedStatus stops
                    // Report elapsed build time
                    ct.Cancel();
                    await printStatus;
                    Global.MainViewModel.StatusBarText = $"CodeBox took {s.Elapsed:h\\:mm\\:ss}";

                    Global.MainViewModel.WorkInProgress = false;
                    Global.MainViewModel.SwitchNormalBuildInterface = true;
                    Global.MainViewModel.BuildTreeItems.Clear();

                    s.MainViewModel.DisplayScript(Global.MainViewModel.CurMainTree.Script);
                    if (Global.Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
                    { // Open BuildLogWindow
                        Application.Current?.Dispatcher.Invoke(() =>
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
                    Interlocked.Decrement(ref Engine.WorkingLock);
                }
            }
            finally
            {
                CanExecuteCommand = true;
                CommandManager.InvalidateRequerySuggested();
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

        private ICommand _escapeSequenceLegendCommand;
        private ICommand _escapeStringCommand;
        private ICommand _unescapeStringCommand;
        public ICommand EscapeSequenceLegendCommand => GetRelayCommand(ref _escapeSequenceLegendCommand, "Print escape sequence legend", ExecuteEscapeSequenceLegendCommand, CanExecuteEscaperCommands);
        public ICommand EscapeStringCommand => GetRelayCommand(ref _escapeStringCommand, "Escape string", ExecuteEscapeStringCommand, CanExecuteEscaperCommands);
        public ICommand UnescapeStringCommand => GetRelayCommand(ref _unescapeStringCommand, "Unescape string", ExecuteUnescapeStringCommand, CanExecuteEscaperCommands);

        private bool CanExecuteEscaperCommands(object parameter)
        {
            return TabIndex == 1;
        }

        private void ExecuteEscapeSequenceLegendCommand(object parameter)
        {
            EscaperConvertedString = StringEscaper.Legend;
        }

        private void ExecuteEscapeStringCommand(object parameter)
        {
            EscaperConvertedString = StringEscaper.QuoteEscape(EscaperStringToConvert, false, EscaperEscapePercentFlag);
        }

        private void ExecuteUnescapeStringCommand(object parameter)
        {
            string str = StringEscaper.QuoteUnescape(EscaperStringToConvert);
            EscaperConvertedString = EscaperEscapePercentFlag ? StringEscaper.UnescapePercent(str) : str;
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

        private ICommand _syntaxCheckCommand;
        public ICommand SyntaxCheckCommand => GetRelayCommand(ref _syntaxCheckCommand, "Run syntax check", ExecuteSyntaxCheck, CanExecuteSyntaxCheck);

        private bool CanExecuteSyntaxCheck(object parameter)
        {
            return TabIndex == 2 && CanExecuteCommand;
        }

        private async void ExecuteSyntaxCheck(object parameter)
        {
            if (SyntaxInputCode.Length == 0)
            {
                SyntaxCheckResult = "Please input code.";
                return;
            }

            CanExecuteCommand = false;
            try
            {
                SyntaxCheckResult = "Checking...";

                await Task.Run(() =>
                {
                    Project p = CurrentProject;

                    Script sc = p.MainScript;
                    ScriptSection section;
                    if (p.MainScript.Sections.ContainsKey(ScriptSection.Names.Process))
                        section = sc.Sections[ScriptSection.Names.Process];
                    else // Create dummy [Process] section instance
                        section = new ScriptSection(sc, ScriptSection.Names.Process, SectionType.Code, new string[0], 1);

                    // Split lines from SyntaxInputCode
                    List<string> lines = new List<string>();
                    using (StringReader r = new StringReader(SyntaxInputCode))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            line = line.Trim();
                            lines.Add(line);
                        }
                    }

                    // Run CodeParser to retrieve parsing errors
                    CodeParser parser = new CodeParser(section, Global.Setting.ExportCodeParserOptions());
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
                        SyntaxCheckResult = b.ToString();
                    }
                    else
                    {
                        SyntaxCheckResult = "Error not found.";
                    }
                });
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
