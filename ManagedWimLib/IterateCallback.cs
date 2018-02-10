using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ManagedWimLib
{ 
    #region IterateDirTreeCallback
    /// <summary>
    /// Type of a callback function to wimlib_iterate_dir_tree().  Must return 0 on success.
    /// </summary>
    public delegate WimLibCallbackStatus IterateDirTreeCallback(DirEntry dentry, object userData);

    public class ManagedIterateDirTreeCallback
    {
        private readonly IterateDirTreeCallback _callback;
        private readonly object _userData;

        internal WimLibNative.NativeIterateDirTreeCallback NativeFunc { get; private set; }

        public ManagedIterateDirTreeCallback(IterateDirTreeCallback callback, object userData)
        {
            _callback = callback;
            _userData = userData;

            // Avoid GC by keeping ref here
            NativeFunc = NativeCallback;
        }

        private WimLibCallbackStatus NativeCallback(DirEntry dentry, IntPtr user_ctx)
        {
            if (_callback != null)
                return _callback(dentry, _userData);
            else
                return 0;
        }
    }
    #endregion

    #region IterateLookupTableCallback
    /// <summary>
    /// Type of a callback function to wimlib_iterate_lookup_table().  Must return 0 on success.
    /// </summary>
    public delegate WimLibCallbackStatus IterateLookupTableCallback(ResourceEntry resoure, object user_ctx);

    public class ManagedIterateLookupTableCallback
    {
        private readonly IterateLookupTableCallback _callback;
        private readonly object _userData;

        internal WimLibNative.NativeIterateLookupTableCallback NativeFunc { get; private set; }

        public ManagedIterateLookupTableCallback(IterateLookupTableCallback callback, object userData)
        {
            _callback = callback;
            _userData = userData;

            // Avoid GC by keeping ref here
            NativeFunc = NativeCallback;
        }

        private WimLibCallbackStatus NativeCallback(ResourceEntry resource, IntPtr user_ctx)
        {
            if (_callback != null)
                return _callback(resource, _userData);
            else
                return 0;
        }
    }
    #endregion


}
