using System;
using System.Threading;

namespace PEBakery.Core.ViewModels
{
    public class DebugWindowOpenEventArgs : EventArgs
    {
        public EngineState EngineState { get; set; }

        public DebugWindowOpenEventArgs(EngineState s)
        {
            EngineState = s;
        }
    }

    public delegate void DebugWindowOpenEventHandler(object sender, DebugWindowOpenEventArgs e);

    public class DebugViewModel : ViewModelBase
    {
        // public event DebugWindowOpenEventHandler DebugWindowOpened;

        public static AutoResetEvent AutoEvent { get; } = new AutoResetEvent(false);

        private bool _selectedTabIndex = false;
        public bool SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

    }
}
