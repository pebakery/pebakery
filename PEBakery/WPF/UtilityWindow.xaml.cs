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
using System.ComponentModel;
using System.Diagnostics;
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

        private readonly UtilityViewModel _m;

        public UtilityWindow(FontHelper.WPFFont monoFont)
        {
            Interlocked.Increment(ref Count);

            _m = new UtilityViewModel(monoFont);

            InitializeComponent();
            DataContext = _m;

            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow w = Application.Current.MainWindow as MainWindow;
                Debug.Assert(w != null, "MainWindow != null");

                List<Project> projList = Global.Projects.ProjectList;
                for (int i = 0; i < projList.Count; i++)
                {
                    Project proj = projList[i];

                    _m.CodeBox_Projects.Add(new Tuple<string, Project>(proj.ProjectName, proj));

                    if (proj.ProjectName.Equals(Global.MainViewModel.CurMainTree.Script.Project.ProjectName, StringComparison.Ordinal))
                        _m.CodeBox_SelectedProjectIndex = i;
                }
            });
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

        private void EscapeButton_Click(object sender, RoutedEventArgs e)
        {
            _m.Escaper_ConvertedString = StringEscaper.QuoteEscape(_m.Escaper_StringToConvert, false, _m.Escaper_EscapePercent);
        }

        private void UnescapeButton_Click(object sender, RoutedEventArgs e)
        {
            string str = StringEscaper.QuoteUnescape(_m.Escaper_StringToConvert);
            _m.Escaper_ConvertedString = _m.Escaper_EscapePercent ? StringEscaper.UnescapePercent(str) : str;
        }

        private void EscapeSequenceLegend_Click(object sender, RoutedEventArgs e)
        {
            _m.Escaper_ConvertedString = StringEscaper.Legend;
        }

        private void CodeBoxSaveButton_Click(object sender, RoutedEventArgs e)
        {
            Encoding encoding = Encoding.UTF8;
            if (File.Exists(_m.CodeFile))
                encoding = EncodingHelper.DetectBom(_m.CodeFile);

            using (StreamWriter writer = new StreamWriter(_m.CodeFile, false, encoding))
            {
                writer.Write(_m.CodeBox_Input);
                writer.Close();
            }
        }

        private async void CodeBoxRunButton_Click(object sender, RoutedEventArgs e)
        {
            Encoding encoding = Encoding.UTF8;
            if (File.Exists(_m.CodeFile))
                encoding = EncodingHelper.DetectBom(_m.CodeFile);

            using (StreamWriter writer = new StreamWriter(_m.CodeFile, false, encoding))
            {
                writer.Write(_m.CodeBox_Input);
                writer.Close();
            }

            if (Engine.WorkingLock == 0)  // Start Build
            {
                Interlocked.Increment(ref Engine.WorkingLock);

                Project project = _m.CodeBox_CurrentProject;
                Script sc = project.LoadScriptRuntime(_m.CodeFile, new LoadScriptRuntimeOptions());

                SettingViewModel setting = Global.Setting;
                MainViewModel mainModel = Global.MainViewModel;

                mainModel.BuildTreeItems.Clear();
                mainModel.SwitchNormalBuildInterface = false;
                mainModel.WorkInProgress = true;

                EngineState s = new EngineState(sc.Project, Global.Logger, mainModel, EngineMode.RunMainAndOne, sc);
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
                mainModel.StatusBarText = $"CodeBox took {s.Elapsed:h\\:mm\\:ss}";

                mainModel.WorkInProgress = false;
                mainModel.SwitchNormalBuildInterface = true;
                mainModel.BuildTreeItems.Clear();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = Application.Current.MainWindow as MainWindow;
                    Debug.Assert(w != null, "MainWindow != null");

                    s.MainViewModel.DisplayScript(Global.MainViewModel.CurMainTree.Script);

                    if (Global.Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
                    { // Open BuildLogWindow
                        w.LogDialog = new LogWindow(1);
                        w.LogDialog.Show();
                    }
                });

                Engine.WorkingEngine = null;
                Interlocked.Decrement(ref Engine.WorkingLock);
            }
            else
            {
                MessageBox.Show("Engine is already running", "Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SyntaxCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_m.Syntax_InputCode.Equals(string.Empty, StringComparison.Ordinal))
            {
                _m.Syntax_Output = "No Code";
                return;
            }

            Project p = _m.CodeBox_CurrentProject;

            Script sc = p.MainScript;
            ScriptSection section;
            if (p.MainScript.Sections.ContainsKey("Process"))
                section = sc.Sections["Process"];
            else
                section = new ScriptSection(sc, "Process", SectionType.Code, new string[0], 1);

            string[] lines = _m.Syntax_InputCode.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToArray();
            CodeParser parser = new CodeParser(section, Global.Setting.ExportCodeParserOptions());
            (CodeCommand[] cmds, List<LogInfo> errorLogs) = parser.ParseStatements(lines);

            // Check Macros
            Macro macro = new Macro(p, p.Variables, out _);
            if (macro.MacroEnabled)
            {
                foreach (CodeCommand cmd in cmds)
                {
                    if (cmd.Type != CodeType.Macro)
                        continue;

                    Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Macro), "Invalid CodeInfo");
                    CodeInfo_Macro info = cmd.Info as CodeInfo_Macro;
                    Debug.Assert(info != null, "Invalid CodeInfo");

                    if (!macro.GlobalDict.ContainsKey(info.MacroType))
                        errorLogs.Add(new LogInfo(LogState.Error, $"Invalid CodeType or Macro [{info.MacroType}]", cmd));
                }
            }

            if (0 < errorLogs.Count)
            {
                StringBuilder b = new StringBuilder();
                for (int i = 0; i < errorLogs.Count; i++)
                {
                    LogInfo log = errorLogs[i];
                    b.AppendLine($"[{i + 1}/{errorLogs.Count}] {log.Message} ({log.Command})");
                }
                _m.Syntax_Output = b.ToString();
            }
            else
            {
                _m.Syntax_Output = "No Error";
            }
        }
        #endregion

        #region InputBinding Event
        public static RoutedUICommand CodeBoxSaveCommand { get; } = new RoutedUICommand("Save", "Save", typeof(UtilityWindow));
        public static RoutedUICommand CodeBoxRunCommand { get; } = new RoutedUICommand("Run", "Run", typeof(UtilityWindow),
            new InputGestureCollection { new KeyGesture(Key.F5, ModifierKeys.Control) });

        private void CodeBox_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _m.TabIndex == 0;
        }

        private void CodeBoxSave_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CodeBoxSaveButton.Focus();
            CodeBoxSaveButton_Click(sender, e);
        }

        private void CodeBoxRun_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CodeBoxRunButton.Focus();
            CodeBoxRunButton_Click(sender, e);
        }

        #endregion
    }

    #region UtilityViewModel
    public class UtilityViewModel : INotifyPropertyChanged
    {
        public FontHelper.WPFFont MonoFont { get; }
        public FontFamily MonoFontFamily => MonoFont.FontFamily;
        public FontWeight MonoFontWeight => MonoFont.FontWeight;
        public double MonoFontSize => MonoFont.FontSizeInDIP;

        public UtilityViewModel(FontHelper.WPFFont monoFont)
        {
            MonoFont = monoFont;
        }

        #region Tab Index
        private int _tabIndex = 0;
        public int TabIndex
        {
            get => _tabIndex;
            set
            {
                _tabIndex = value;
                OnPropertyUpdate(nameof(TabIndex));
            }
        }
        #endregion

        #region CodeBox
        public string CodeFile { get; private set; }

        private int codeBox_SelectedProjectIndex;
        public int CodeBox_SelectedProjectIndex
        {
            get => codeBox_SelectedProjectIndex;
            set
            {
                if (0 <= value && value < codeBox_Projects.Count)
                {
                    codeBox_SelectedProjectIndex = value;

                    Project proj = codeBox_Projects[value].Item2;
                    CodeFile = System.IO.Path.Combine(proj.ProjectDir, "CodeBox.txt");
                    if (File.Exists(CodeFile))
                    {
                        Encoding encoding = EncodingHelper.DetectBom(CodeFile);
                        using (StreamReader reader = new StreamReader(CodeFile, encoding))
                        {
                            CodeBox_Input = reader.ReadToEnd();
                            OnPropertyUpdate("CodeBox_Input");
                        }
                    }
                    else
                    {
                        CodeBox_Input = @"[Main]
Title=CodeBox
Description=Test Commands

[Variables]

[Process]
// Write Commands Here
//--------------------

";
                    }
                }

                OnPropertyUpdate("CodeBox_SelectedProjectIndex");
            }
        }

        private ObservableCollection<Tuple<string, Project>> codeBox_Projects = new ObservableCollection<Tuple<string, Project>>();
        public ObservableCollection<Tuple<string, Project>> CodeBox_Projects
        {
            get => codeBox_Projects;
            set
            {
                codeBox_Projects = value;
                OnPropertyUpdate("CodeBox_Projects");
            }
        }
        public Project CodeBox_CurrentProject
        {
            get
            {
                int i = codeBox_SelectedProjectIndex;
                if (0 <= i && i < codeBox_Projects.Count)
                    return codeBox_Projects[i].Item2;
                else
                    return null;
            }
        }

        private string codeBox_Input = string.Empty;
        public string CodeBox_Input
        {
            get => codeBox_Input;
            set
            {
                codeBox_Input = value;
                OnPropertyUpdate("CodeBox_Input");
            }
        }
        #endregion

        #region String Escaper
        private string escaper_StringToConvert = string.Empty;
        public string Escaper_StringToConvert
        {
            get => escaper_StringToConvert;
            set
            {
                escaper_StringToConvert = value;
                OnPropertyUpdate("Escaper_StringToConvert");
            }
        }

        private string escaper_ConvertedString = string.Empty;
        public string Escaper_ConvertedString
        {
            get => escaper_ConvertedString;
            set
            {
                escaper_ConvertedString = value;
                OnPropertyUpdate("Escaper_ConvertedString");
            }
        }

        private bool escaper_EscapePercent = false;
        public bool Escaper_EscapePercent
        {
            get => escaper_EscapePercent;
            set
            {
                escaper_EscapePercent = value;
                OnPropertyUpdate("Escaper_EscapePercent");
            }
        }
        #endregion

        #region Syntax Checker
        private string syntax_InputCode = string.Empty;
        public string Syntax_InputCode
        {
            get => syntax_InputCode;
            set
            {
                syntax_InputCode = value;
                OnPropertyUpdate("Syntax_InputCode");
            }
        }

        private string syntax_Output = string.Empty;
        public string Syntax_Output
        {
            get => syntax_Output;
            set
            {
                syntax_Output = value;
                OnPropertyUpdate("Syntax_Output");
            }
        }
        #endregion

        #region Utility
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion

    #region WindowCommand
    public class WindowCommand : ICommand
    {
        private readonly Action<object> _onExecute;
        private readonly Predicate<object> _onCanExecute;

        public WindowCommand(Action<object> onExecute, Predicate<object> onCanExecuteDelegate)
        {
            _onExecute = onExecute;
            _onCanExecute = onCanExecuteDelegate;
        }

        public void Execute(object parameter) => _onExecute?.Invoke(parameter);

        public bool CanExecute(object parameter) => _onCanExecute == null || _onCanExecute(parameter);

        public event EventHandler CanExecuteChanged;
        /*
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        */

        public void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    }
    #endregion
}
