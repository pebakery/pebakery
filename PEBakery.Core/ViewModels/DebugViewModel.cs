using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

namespace PEBakery.Core.ViewModels
{
    public class DebugWindowOpenEventArgs : EventArgs
    {
        //[SuppressMessage("Style", "IDE1006:Style")]
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

        public static AutoResetEvent AutoEvent = new AutoResetEvent(false);

        private bool _selectedTabIndex = false;
        public bool SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

    }
}
