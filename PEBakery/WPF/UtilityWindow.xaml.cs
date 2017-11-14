using PEBakery.Core;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PEBakery.WPF
{
    /// <summary>
    /// UtilityWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class UtilityWindow : Window
    {
        #region Field and Constructor
        public static int Count = 0;

        UtilityViewModel m;

        public UtilityWindow(FontHelper.WPFFont monoFont)
        {
            Interlocked.Increment(ref UtilityWindow.Count);

            m = new UtilityViewModel(monoFont);

            InitializeComponent();
            DataContext = m;

            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow w = (Application.Current.MainWindow as MainWindow);
                List<Project> projList = w.Projects.Projects;
                for (int i = 0; i < projList.Count; i++)
                {
                    Project proj = projList[i];

                    m.CodeBox_Projects.Add(new Tuple<string, Project>(proj.ProjectName, proj));

                    if (proj.ProjectName.Equals(w.CurMainTree.Plugin.Project.ProjectName, StringComparison.Ordinal))
                        m.CodeBox_SelectedProjectIndex = i;
                }
            });
        }
        #endregion

        #region Window Event
        private void Window_Closed(object sender, EventArgs e)
        {
            Interlocked.Decrement(ref UtilityWindow.Count);
        }
        #endregion

        #region Button Event
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void EscapeButton_Click(object sender, RoutedEventArgs e)
        {
            m.Escaper_ConvertedString = StringEscaper.QuoteEscape(m.Escaper_StringToConvert, false, m.Escaper_EscapePercent, m.Escaper_EscapeSharpDollar);
        }

        private void UnescapeButton_Click(object sender, RoutedEventArgs e)
        {
            string str = StringEscaper.QuoteUnescape(m.Escaper_StringToConvert);
            if (m.Escaper_EscapePercent)
                m.Escaper_ConvertedString = StringEscaper.UnescapePercent(str);
            else
                m.Escaper_ConvertedString = str;
        }

        private void EscapeSequenceLegend_Click(object sender, RoutedEventArgs e)
        {
            m.Escaper_ConvertedString = StringEscaper.Legend;
        }

        private void CodeBoxSaveButton_Click(object sender, RoutedEventArgs e)
        {
            Encoding encoding = Encoding.UTF8;
            if (File.Exists(m.CodeFile))
                encoding = FileHelper.DetectTextEncoding(m.CodeFile);

            using (StreamWriter writer = new StreamWriter(m.CodeFile, false, encoding))
            {
                writer.Write(m.CodeBox_Input);
                writer.Close();
            }
        }

        private async void CodeBoxRunButton_Click(object sender, RoutedEventArgs e)
        {
            Encoding encoding = Encoding.UTF8;
            if (File.Exists(m.CodeFile))
                encoding = FileHelper.DetectTextEncoding(m.CodeFile);

            using (StreamWriter writer = new StreamWriter(m.CodeFile, false, encoding))
            {
                writer.Write(m.CodeBox_Input);
                writer.Close();
            }

            if (Engine.WorkingLock == 0)  // Start Build
            {
                Interlocked.Increment(ref Engine.WorkingLock);

                Project project = m.CodeBox_CurrentProject;
                Plugin p = project.LoadPluginMonkeyPatch(m.CodeFile);

                Logger logger = null;
                SettingViewModel setting = null;
                MainViewModel mainModel = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = (Application.Current.MainWindow as MainWindow);
                    logger = w.Logger;
                    setting = w.Setting;
                    mainModel = w.Model;
                });

                mainModel.SwitchNormalBuildInterface = false;
                mainModel.ProgressRingActive = true;

                EngineState s = new EngineState(p.Project, logger, mainModel, p);
                s.SetOption(setting);

                Engine.WorkingEngine = new Engine(s);

                await Engine.WorkingEngine.Run($"CodeBox - {project.ProjectName}");

                mainModel.ProgressRingActive = false;
                mainModel.SwitchNormalBuildInterface = true;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow w = Application.Current.MainWindow as MainWindow;

                    w.DrawPlugin(w.CurMainTree.Plugin);

                    if (w.Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
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
            if (m.Syntax_InputCode.Equals(string.Empty, StringComparison.Ordinal))
            {
                m.Syntax_Output = "No Code";
                return;
            }

            Project project = m.CodeBox_CurrentProject;

            Plugin p = project.MainPlugin;
            PluginSection section;
            if (project.MainPlugin.Sections.ContainsKey("Process"))
                section = p.Sections["Process"];
            else
                section = new PluginSection(p, "Process", SectionType.Code, new List<string>());
            SectionAddress addr = new SectionAddress(p, section);

            List<string> lines = m.Syntax_InputCode.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
            List<CodeCommand> cmds = CodeParser.ParseRawLines(lines, addr, out List<LogInfo> errorLogs);

            // Check Macros
            Macro macro = new Macro(project, project.Variables, out List<LogInfo> macroLogs);

            if (macro.MacroEnabled)
            {
                foreach (CodeCommand cmd in cmds)
                {
                    if (cmd.Type == CodeType.Macro)
                    {
                        Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Macro));
                        CodeInfo_Macro info = cmd.Info as CodeInfo_Macro;

                        if (!macro.MacroDict.ContainsKey(info.MacroType))
                            errorLogs.Add(new LogInfo(LogState.Error, $"Invalid CodeType or Macro [{info.MacroType}]", cmd));
                    }
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
                m.Syntax_Output = b.ToString();
            }
            else
            {
                m.Syntax_Output = "No Error";
            }
        }
        #endregion
    }

    #region UtiltiyViewModel
    public class UtilityViewModel : INotifyPropertyChanged
    {
        public FontHelper.WPFFont MonoFont { get; private set; }
        public FontFamily MonoFontFamily { get => MonoFont.FontFamily; }
        public FontWeight MonoFontWeight { get => MonoFont.FontWeight; }
        public double MonoFontSize { get => MonoFont.FontSizeInDIP; }

        public UtilityViewModel(FontHelper.WPFFont monoFont)
        {
            MonoFont = monoFont;
        }

        #region CodeBox
        private string codeFile;
        public string CodeFile { get => codeFile; }

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
                    codeFile = System.IO.Path.Combine(proj.ProjectDir, "CodeBox.txt");
                    if (File.Exists(codeFile))
                    {
                        Encoding encoding = FileHelper.DetectTextEncoding(codeFile);
                        using (StreamReader reader = new StreamReader(codeFile, encoding))
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

        private bool escaper_EscapeSharpDollar = false;
        public bool Escaper_EscapeSharpDollar
        {
            get => escaper_EscapeSharpDollar;
            set
            {
                escaper_EscapeSharpDollar = value;
                OnPropertyUpdate("Escaper_EscapeSharpDollar");
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
}
