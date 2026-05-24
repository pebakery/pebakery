/*
    Copyright (C) 2018-present Hajin Jang
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
        public ProjectTreeItemModel? Parent { get; }

        public ProjectTreeItemModel(ProjectTreeItemModel? root, ProjectTreeItemModel? parent, Script script)
        {
            ProjectRoot = root ?? this;
            Parent = parent;
            _sc = script;

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
        /// <summary>
        /// Tri-state checkbox value.
        ///   true  = checked (all selectable descendants checked)
        ///   false = unchecked
        ///   null  = indeterminate (directory with a mix of checked/unchecked children)
        ///
        /// For directory nodes the value is always DERIVED from children so the
        /// UI stays consistent without any extra bookkeeping.
        /// For script/link nodes it is read from _sc.Selected.
        /// </summary>
        public bool? Checked
        {
            get
            {
                if (_sc.Type == ScriptType.Directory)
                    return GetDirectoryCheckedState();

                // Mandatory scripts are always checked regardless of _sc.Selected,
                // which may be SelectedState.None (not user-selectable).
                return _sc.Mandatory || _sc.Selected == SelectedState.True;
            }
            set
            {
                // For directory nodes we cannot trust the value WPF sends here.
                // With IsThreeState="False", when IsChecked is null (indeterminate)
                // WPF has no real null state internally, so it evaluates null=false.
                // This causes a click on an indeterminate directory to call
                // SetChecked(false) again instead of checking all children.
                // We need to derive the toggle direction from the current computed state 
                // instead, the same way the keyboard handler does.
                //
                // For regular scripts the value sent by WPF is never indeterminate so we can use it as-is.
                bool targetValue;

                if (_sc.Type == ScriptType.Directory)
                {
                    targetValue = (Checked != true);
                }
                else
                {
                    targetValue = value ?? false;
                }
                Task.Run(() => SetChecked(targetValue, true));
            }
        }

        /// <summary>
        /// Derives the tri-state for a directory node by inspecting its children.
        /// Returns null (indeterminate) when children have mixed selection.
        /// </summary>
        private bool? GetDirectoryCheckedState()
        {
            // Only children that can actually be selected (or directories containing them) participate in the vote.
            List<ProjectTreeItemModel> voters = Children
                .Where(c => c.IsSelectableVoter)
                .ToList();

            if (voters.Count == 0)
                return false;

            bool anyChecked = voters.Any(c => c.Checked == true);
            bool anyUnchecked = voters.Any(c => c.Checked == false);
            bool anyIndeterminate = voters.Any(c => c.Checked == null);

            if (anyIndeterminate || (anyChecked && anyUnchecked))
                return null;  // indeterminate
            return anyChecked; // all true = true, all false = false
        }

        /// <summary>
        /// Determines if this item can participate in determining a directory's tri-state.
        /// Regular scripts vote if they are mandatory (always selected) or selectable.
        /// Directories vote if they contain at least one voting descendant.
        /// </summary>
        public bool IsSelectableVoter
        {
            get
            {
                if (_sc.Type != ScriptType.Directory)
                    return _sc.Mandatory || _sc.Selected != SelectedState.None;

                // A directory is only a voter if it contains at least one voter.
                // This prevents a deadlock when a subfolder that contains only non-selectable
                // scripts is present under a parent folder.
                //
                // Details: 
                //  If a subfolder contains only a non-selectable script(s), it inherently evaluates to false
                //  because it has nothing inside it to check. When its parent folder checks its children,
                //  it sees regular scripts turning true, but that subfolder returning false.
                //  This forces the parent folder to remain indeterminate (null). Because the parent is stuck at null,
                //  clicking it keeps triggering targetValue = (Checked != true) to evaluate to true.
                //  The result is that it constantly tells its children to turn on, never realizing it's allowed to turn them off.
                //
                // Note: .Any() short-circuits, making this highly efficient.
                return Children.Any(c => c.IsSelectableVoter);
            }
        }

        public void SetChecked(bool value, bool first)
        {
            // Directory nodes have _sc.Selected == SelectedState.None, so they must
            // bypass the guard below as their job is purely to propagate to children.
            // Their own Checked value is computed live from children, not stored.
            if (_sc.Type != ScriptType.Directory)
            {
                if (_sc.Mandatory || _sc.Selected == SelectedState.None)
                    return;
            }

            if (first && Global.MainViewModel != null)
            {
                Global.MainViewModel.WorkInProgress = true;
                Global.MainViewModel.EnableTreeItems = false;
            }

            // Only write _sc.Selected for scripts.
            if (_sc.Type != ScriptType.Directory)
            {
                if (value)
                {
                    _sc.Selected = SelectedState.True;
                    DisableScripts(ProjectRoot, _sc);
                }
                else
                {
                    _sc.Selected = SelectedState.False;
                }
            }

            // Do not propagate in main script
            if (!_sc.IsMainScript)
            {
                // Set child scripts (Top-down propagation)
                if (0 < Children.Count)
                {
                    foreach (ProjectTreeItemModel child in Children)
                        child.SetChecked(value, false);
                }

                if (first)
                    ParentCheckedPropagation();
            }

            OnPropertyUpdate(nameof(Checked));

			// No point in using try-finally, if any exception is thrown, the program just dies.
            if (first && Global.MainViewModel != null)
            {
                Global.MainViewModel.EnableTreeItems = true;
                Global.MainViewModel.WorkInProgress = false;
                Application.Current?.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
            }
        }

        /// <summary>
        /// Bottom-up propagation: after a node changes, notify every ancestor
        /// directory to re-evaluate its computed Checked value.
        /// Directory nodes no longer need their _sc.Selected written here because
        /// their Checked getter derives the value live from children.
        /// </summary>
        public void ParentCheckedPropagation()
        {
            if (Parent == null)
                return;

            Parent.OnPropertyUpdate(nameof(Checked));
            Parent.ParentCheckedPropagation();
        }

        /// <summary>
        /// Called when a sibling's state changes and the parent directory needs
        /// to reflect the change. The directory's Checked is computed, so we only
        /// need to update _sc.Selected for the build engine and then notify.
        /// </summary>
        public void SetParentChecked(bool value)
        {
            if (Parent == null)
                return;

            // Keep _sc.Selected in sync for the build engine (directory scripts
            // still need a Selected state so they appear correctly in build trees).
            if (!_sc.Mandatory && _sc.Selected != SelectedState.None)
                _sc.Selected = value ? SelectedState.True : SelectedState.False;

            // Raise PropertyChanged — the getter will derive the tri-state from children.
            OnPropertyUpdate(nameof(Checked));
            ParentCheckedPropagation();
        }

        private void DisableScripts(ProjectTreeItemModel root, Script sc)
        {
            if (root == null || sc == null)
                return;

            string[]? paths = Script.GetDisableScriptPaths(sc, out List<LogInfo> errorLogs);
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
                ProjectTreeItemModel? found = FindScriptByRealPath(path);
                if (found == null)
                    continue;
                if (sc.Type != ScriptType.Directory && !sc.Mandatory && sc.Selected != SelectedState.None)
                    found.SetChecked(false, false);
            }
        }
        #endregion

        #region Find Script
        public ProjectTreeItemModel? FindScriptByRealPath(string realPath)
        {
            return RecursiveFindScriptByRealPath(ProjectRoot, realPath);
        }

        public static ProjectTreeItemModel? FindScriptByRealPath(ProjectTreeItemModel root, string realPath)
        {
            return RecursiveFindScriptByRealPath(root, realPath);
        }

        private static ProjectTreeItemModel? RecursiveFindScriptByRealPath(ProjectTreeItemModel cur, string fullPath)
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
                    ProjectTreeItemModel? found = RecursiveFindScriptByRealPath(next, fullPath);
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
