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
    public delegate CallbackStatus IterateDirTreeCallback(DirEntry dentry, object userData);

    public class ManagedIterateDirTreeCallback
    {
        private readonly IterateDirTreeCallback _callback;
        private readonly object _userData;

        internal NativeMethods.NativeIterateDirTreeCallback NativeFunc { get; private set; }

        public ManagedIterateDirTreeCallback(IterateDirTreeCallback callback, object userData)
        {
            _callback = callback;
            _userData = userData;

            // Avoid GC by keeping ref here
            NativeFunc = NativeCallback;
        }

        private CallbackStatus NativeCallback(IntPtr entry_ptr, IntPtr user_ctx)
        {
            CallbackStatus ret = CallbackStatus.CONTINUE;
            if (_callback != null)
            {
                DirEntryBase b = (DirEntryBase)Marshal.PtrToStructure(entry_ptr, typeof(DirEntryBase));
                DirEntry dentry = new DirEntry()
                {
                    FileName = b.FileName,
                    DosName = b.DosName,
                    FullPath = b.FullPath,
                    Depth = b.Depth,
                    SecurityDescriptor = b.SecurityDescriptor,
                    Attributes = b.Attributes,
                    ReparseTag = b.ReparseTag,
                    NumLinks = b.NumLinks,
                    NumNamedStreams = b.NumNamedStreams,
                    HardLinkGroupId = b.HardLinkGroupId,
                    CreationTime = b.CreationTime,
                    LastWriteTime = b.LastWriteTime,
                    LastAccessTime = b.LastAccessTime,
                    UnixUserId = b.UnixUserId,
                    UnixGroupId = b.UnixGroupId,
                    UnixMode = b.UnixMode,
                    UnixRootDevice = b.UnixRootDevice,
                    ObjectId = b.ObjectId,
                    Streams = new StreamEntry[b.NumNamedStreams + 1],
                };

                IntPtr baseOffset = IntPtr.Add(entry_ptr, Marshal.SizeOf(typeof(DirEntryBase)));
                for (int i = 0; i < dentry.Streams.Length; i++)
                {
                    IntPtr offset = IntPtr.Add(baseOffset, i * Marshal.SizeOf(typeof(StreamEntry)));
                    dentry.Streams[i] = (StreamEntry)Marshal.PtrToStructure(offset, typeof(StreamEntry));
                }

                ret = _callback(dentry, _userData);
            }

            return ret;
        }
    }
    #endregion

    #region IterateLookupTableCallback
    /// <summary>
    /// Type of a callback function to wimlib_iterate_lookup_table().  Must return 0 on success.
    /// </summary>
    public delegate CallbackStatus IterateLookupTableCallback(ResourceEntry resoure, object user_ctx);

    public class ManagedIterateLookupTableCallback
    {
        private readonly IterateLookupTableCallback _callback;
        private readonly object _userData;

        internal NativeMethods.NativeIterateLookupTableCallback NativeFunc { get; private set; }
        
        public ManagedIterateLookupTableCallback(IterateLookupTableCallback callback, object userData)
        {
            _callback = callback;
            _userData = userData;

            // Avoid GC by keeping ref here
            NativeFunc = NativeCallback;
        }

        private CallbackStatus NativeCallback(IntPtr entry_ptr, IntPtr user_ctx)
        {
            CallbackStatus ret = CallbackStatus.CONTINUE;
            if (_callback != null)
            {
                ResourceEntry resource = (ResourceEntry)Marshal.PtrToStructure(entry_ptr, typeof(ResourceEntry));
                ret = _callback(resource, _userData);
            }

            return ret;
        }
    }
    #endregion


}
