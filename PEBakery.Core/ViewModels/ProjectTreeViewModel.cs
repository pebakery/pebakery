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
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace PEBakery.Core.ViewModels
{
    #region ProjectTreeViewModel
    public class ProjectTreeItemModel : ViewModelBase
    {
        #region Basic Property and Constructor
        public ProjectTreeItemModel ProjectRoot { get; }
        public ProjectTreeItemModel Parent { get; }

        public ProjectTreeItemModel(ProjectTreeItemModel root, ProjectTreeItemModel parent)
        {
            ProjectRoot = root ?? this;
            Parent = parent;

            Children = new ObservableCollection<ProjectTreeItemModel>();
            BindingOperations.EnableCollectionSynchronization(Children, _childrenLock);
        }
        #endregion

        #region Shared Property
        private bool _isExpanded = false;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyUpdate(nameof(IsExpanded));
            }
        }

        private Script _sc;
        public Script Script
        {
            get => _sc;
            set
            {
                _sc = value;
                OnPropertyUpdate(nameof(Script));
                OnPropertyUpdate(nameof(Checked));
                // OnPropertyUpdate(nameof(MainViewModel.MainCanvas));
            }
        }

        private PackIconMaterialKind _icon;
        public PackIconMaterialKind Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private readonly object _childrenLock = new object();
        public ObservableCollection<ProjectTreeItemModel> Children { get; private set; }

        public void SortChildren()
        {
            IOrderedEnumerable<ProjectTreeItemModel> sorted = Children
                .OrderBy(x => x.Script.Level)
                .ThenBy(x => x.Script.Type)
                .ThenBy(x => x.Script.RealPath);
            Children = new ObservableCollection<ProjectTreeItemModel>(sorted);
        }
        #endregion

        #region Build Mode Property
        private bool _focus = false;
        public bool Focus
        {
            get => _focus;
            set
            {
                SetProperty(ref _focus, value);
                OnPropertyUpdate(nameof(Icon));
                OnPropertyUpdate(nameof(BuildFontWeight));
            }
        }
        public FontWeight BuildFontWeight => _focus ? FontWeights.SemiBold : FontWeights.Normal;
        #endregion

        #region Enabled CheckBox
        public bool Checked
        {
            get
            {
                switch (_sc.Selected)
                {
                    case SelectedState.True:
                        return true;
                    default:
                        return false;
                }
            }
            set
            {
                Task.Run(() =>
                {
                    SetChecked(value, true);
                });
            }
        }

        public void SetChecked(bool value, bool first)
        {
            if (_sc.Mandatory || _sc.Selected == SelectedState.None)
                return;

            if (first)
            {
                Global.MainViewModel.WorkInProgress = true;
                Global.MainViewModel.EnableTreeItems = false;
            }

            if (value)
            {
                _sc.Selected = SelectedState.True;

                // Run 'Disable' directive
                DisableScripts(ProjectRoot, _sc);
            }
            else
            {
                _sc.Selected = SelectedState.False;
            }

            // Do not propagate in main script
            if (!_sc.IsMainScript)
            {
                // Set also child scripts (Top-down propagation)
                if (0 < Children.Count)
                {
                    foreach (ProjectTreeItemModel child in Children)
                        child.SetChecked(value, false);
                }

                if (first)
                    ParentCheckedPropagation();
            }

            OnPropertyUpdate(nameof(Checked));

            // No meaning on using try-finally, if exception is thrown, the program just dies.
            if (first)
            {
                Global.MainViewModel.EnableTreeItems = true;
                Global.MainViewModel.WorkInProgress = false;
                Application.Current?.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
            }
        }

        public void ParentCheckedPropagation()
        { // Bottom-up propagation of Checked property
            if (Parent == null)
                return;

            bool setParentChecked = false;
            foreach (ProjectTreeItemModel sibling in Parent.Children)
            { // Siblings
                if (sibling.Checked)
                    setParentChecked = true;
            }

            Parent.SetParentChecked(setParentChecked);
        }

        public void SetParentChecked(bool value)
        {
            if (Parent == null)
                return;

            if (!_sc.Mandatory && _sc.Selected != SelectedState.None)
            {
                _sc.Selected = value ? SelectedState.True : SelectedState.False;
            }

            OnPropertyUpdate(nameof(Checked));
            ParentCheckedPropagation();
        }

        private void DisableScripts(ProjectTreeItemModel root, Script sc)
        {
            if (root == null || sc == null)
                return;

            string[] paths = Script.GetDisableScriptPaths(sc, out List<LogInfo> errorLogs);
            if (paths == null)
                return;
            Global.Logger.SystemWrite(errorLogs);

            foreach (string path in paths)
            {
                int exist = sc.Project.AllScripts.Count(x => x.RealPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (exist != 1)
                    continue;

                // Write to file
                IniReadWriter.WriteKey(path, "Main", "Selected", "False");

                // Write to in-memory script
                ProjectTreeItemModel found = FindScriptByRealPath(path);
                if (found == null)
                    continue;
                if (sc.Type != ScriptType.Directory && !sc.Mandatory && sc.Selected != SelectedState.None)
                    found.SetChecked(false, false);
            }
        }
        #endregion

        #region Find Script
        public ProjectTreeItemModel FindScriptByRealPath(string realPath)
        {
            return RecursiveFindScriptByRealPath(ProjectRoot, realPath);
        }

        public static ProjectTreeItemModel FindScriptByRealPath(ProjectTreeItemModel root, string realPath)
        {
            return RecursiveFindScriptByRealPath(root, realPath);
        }

        private static ProjectTreeItemModel RecursiveFindScriptByRealPath(ProjectTreeItemModel cur, string fullPath)
        {
            if (cur.Script != null)
            {
                if (fullPath.Equals(cur.Script.RealPath, StringComparison.OrdinalIgnoreCase))
                    return cur;
            }

            if (0 < cur.Children.Count)
            {
                foreach (ProjectTreeItemModel next in cur.Children)
                {
                    ProjectTreeItemModel found = RecursiveFindScriptByRealPath(next, fullPath);
                    if (found != null)
                        return found;
                }
            }

            // Not found in this path
            return null;
        }
        #endregion

        #region IsDirectoryUpdateable
        public bool IsDirectoryUpdateable()
        {
            if (Script.Type != ScriptType.Directory)
                return Script.IsUpdateable;

            Queue<ProjectTreeItemModel> itemQueue = new Queue<ProjectTreeItemModel>();
            itemQueue.Enqueue(this);
            while (0 < itemQueue.Count)
            {
                ProjectTreeItemModel item = itemQueue.Dequeue();
                Script sc = item.Script;

                if (sc.Type == ScriptType.Directory)
                {
                    foreach (ProjectTreeItemModel subItem in item.Children)
                        itemQueue.Enqueue(subItem);
                }
                else
                {
                    if (sc.IsUpdateable)
                        return true;
                }
            }

            return false;
        }
        #endregion

        #region ToString
        public override string ToString() => _sc.Title;
        #endregion
    }
    #endregion
}
