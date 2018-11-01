using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
// ReSharper disable RedundantAssignment

namespace PEBakery.Core.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        #region OnPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void OnPropertyUpdate([CallerMemberName] string propertyName = null)
        {
            Debug.Assert(propertyName != null);
            Debug.Assert(GetType().GetRuntimeProperty(propertyName) != null);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void SetProperty<T>(ref T fieldRef, T newValue, [CallerMemberName] string propertyName = null)
        {
            fieldRef = newValue;
            OnPropertyUpdate(propertyName);
        }
        #endregion
    }
}
