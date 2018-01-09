using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ManagedWimLib
{
    public delegate WimLibProgressStatus WimLibCallback(WimLibProgressMsg msg, object info, object progctx);

    public class ManagedWimLibCallback
    {
        private readonly WimLibCallback _callback;
        private readonly object _userData;

        public WimLibNative.ProgressFunc NativeFunc { get; private set; }

        public ManagedWimLibCallback(WimLibCallback callback, object userData)
        {
            _callback = callback ?? throw new ArgumentNullException("callback");
            _userData = userData;

            // Avoid GC by keeping ref here
            NativeFunc = Callback;
        }

        private WimLibProgressStatus Callback(WimLibProgressMsg msgType, IntPtr info, IntPtr progctx)
        {
            object pInfo = null;

            switch (msgType)
            {
                case WimLibProgressMsg.WRITE_STREAMS:
                    pInfo = (WimLibProgressInfoWriteStreams)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoWriteStreams));
                    break;
                case WimLibProgressMsg.SCAN_BEGIN:
                case WimLibProgressMsg.SCAN_DENTRY:
                case WimLibProgressMsg.SCAN_END:
                    pInfo = (WimLibProgressInfoScan)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoScan));
                    break;
                case WimLibProgressMsg.EXTRACT_SPWM_PART_BEGIN:
                case WimLibProgressMsg.EXTRACT_IMAGE_BEGIN:
                case WimLibProgressMsg.EXTRACT_TREE_BEGIN:
                case WimLibProgressMsg.EXTRACT_FILE_STRUCTURE:
                case WimLibProgressMsg.EXTRACT_STREAMS:
                case WimLibProgressMsg.EXTRACT_METADATA:
                case WimLibProgressMsg.EXTRACT_TREE_END:
                case WimLibProgressMsg.EXTRACT_IMAGE_END:
                    pInfo = (WimLibProgressInfoExtract) Marshal.PtrToStructure(info, typeof(WimLibProgressInfoExtract));
                    break;
                case WimLibProgressMsg.RENAME:
                    pInfo = (WimLibProgressInfoRename)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoRename));
                    break;
                case WimLibProgressMsg.UPDATE_BEGIN_COMMAND:
                case WimLibProgressMsg.UPDATE_END_COMMAND:
                    pInfo = (WimLibProgressInfoUpdate)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoUpdate));
                    break;
                case WimLibProgressMsg.VERIFY_INTEGRITY:
                case WimLibProgressMsg.CALC_INTEGRITY:
                    pInfo = (WimLibProgressInfoIntegrity)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoIntegrity));
                    break;
                case WimLibProgressMsg.SPLIT_BEGIN_PART:
                case WimLibProgressMsg.SPLIT_END_PART:
                    pInfo = (WimLibProgressInfoSplit)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoSplit));
                    break;
                case WimLibProgressMsg.REPLACE_FILE_IN_WIM:
                    pInfo = (WimLibProgressInfoReplace)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoReplace));
                    break;
                case WimLibProgressMsg.WIMBOOT_EXCLUDE:
                    pInfo = (WimLibProgressInfoWimbootExclude)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoWimbootExclude));
                    break;
                case WimLibProgressMsg.UNMOUNT_BEGIN:
                    pInfo = (WimLibProgressInfoUnmount)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoUnmount));
                    break;
                case WimLibProgressMsg.DONE_WITH_FILE:
                    pInfo = (WimLibProgressInfoDoneWithFile)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoDoneWithFile));
                    break;
                case WimLibProgressMsg.BEGIN_VERIFY_IMAGE:
                case WimLibProgressMsg.END_VERIFY_IMAGE:
                    pInfo = (WimLibProgressInfoVerifyImage)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoVerifyImage));
                    break;
                case WimLibProgressMsg.VERIFY_STREAMS:
                    pInfo = (WimLibProgressInfoVerifyStreams)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoVerifyStreams));
                    break;
                case WimLibProgressMsg.TEST_FILE_EXCLUSION:
                    pInfo = (WimLibProgressInfoTestFileExclusion)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoTestFileExclusion));
                    break;
                case WimLibProgressMsg.HANDLE_ERROR:
                    pInfo = (WimLibProgressInfoHandleError)Marshal.PtrToStructure(info, typeof(WimLibProgressInfoHandleError));
                    break;
                default:
                    throw new InvalidOperationException($"Invalid WimLibProgressMsg [{msgType}]");
            }

            return _callback(msgType, pInfo, _userData);
        }
    }

    #region WimLibProgressInfo
    /// <summary>
    /// Valid on the message ::WRITE_STREAMS.  This is
    /// the primary message for tracking the progress of writing a WIM file.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoWriteStreams
    {
        /// <summary>
        /// An upper bound on the number of bytes of file data that will
        /// be written.  This number is the uncompressed size; the actual
        /// size may be lower due to compression.  In addition, this
        /// number may decrease over time as duplicated file data is
        /// discovered.
        /// </summary>
        public ulong TotalBytes;
        /// <summary>
        /// An upper bound on the number of distinct file data "blobs"
        /// that will be written.  This will often be similar to the
        /// "number of files", but for several reasons (hard links, named
        /// data streams, empty files, etc.) it can be different.  In
        /// addition, this number may decrease over time as duplicated
        /// file data is discovered.
        /// </summary>
        public ulong TotalStreams;
        /// <summary>
        /// The number of bytes of file data that have been written so
        /// far.  This starts at 0 and ends at @p total_bytes.  This
        /// number is the uncompressed size; the actual size may be lower
        /// due to compression.
        /// </summary>
        public ulong CompletedBytes;
        /// <summary>
        /// The number of distinct file data "blobs" that have been
        /// written so far.  This starts at 0 and ends at @p
        /// total_streams.
        /// </summary>
        public ulong CompletedStreams;
        /// <summary>
        /// The number of threads being used for data compression; or,
        /// if no compression is being performed, this will be 1.
        /// </summary>
        public uint NumThreads;
        /// <summary>
        /// The compression type being used, as one of the
        /// ::wimlib_compression_type constants. 
        /// </summary>
        public int CompressionType;
        /// <summary>
        /// The number of on-disk WIM files from which file data is
        /// being exported into the output WIM file.  This can be 0, 1,
        /// or more than 1, depending on the situation.
        /// </summary>
        public uint TotalParts;
        /// <summary>
        /// This is currently broken and will always be 0. 
        /// </summary>
        public uint CompletedParts;
    }

    /// <summary>
    /// Valid on messages ::SCAN_BEGIN,
    /// ::SCAN_DENTRY, and
    /// ::SCAN_END.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoScan
    {
        /// <summary>
        /// Dentry scan status, valid on
        /// ::SCAN_DENTRY.
        /// </summary>
        public enum WimLibScanStatus : uint
        {
            /// <summary>
            /// File looks okay and will be captured.
            /// </summary>
            WIMLIB_SCAN_DENTRY_OK = 0,
            /// <summary>
            /// File is being excluded from capture due to the
            /// capture configuration.
            /// </summary>
            WIMLIB_SCAN_DENTRY_EXCLUDED = 1,
            /// <summary>
            /// File is being excluded from capture due to being of
            /// an unsupported type. 
            /// </summary>
            WIMLIB_SCAN_DENTRY_UNSUPPORTED = 2,
            /// <summary>
            /// The file is an absolute symbolic link or junction
            /// that points into the capture directory, and
            /// reparse-point fixups are enabled, so its target is
            /// being adjusted.  (Reparse point fixups can be
            /// disabled with the flag ::WIMLIB_ADD_FLAG_NORPFIX.)
            /// </summary>
            WIMLIB_SCAN_DENTRY_FIXED_SYMLINK = 3,
            /// <summary>
            /// Reparse-point fixups are enabled, but the file is an
            /// absolute symbolic link or junction that does
            /// <b>not</b> point into the capture directory, so its
            /// target is <b>not</b> being adjusted.
            /// </summary>
            WIMLIB_SCAN_DENTRY_NOT_FIXED_SYMLINK = 4,
        }

        /// <summary>
        /// Top-level directory being scanned; or, when capturing an NTFS
        /// volume with ::WIMLIB_ADD_FLAG_NTFS, this is instead the path
        /// to the file or block device that contains the NTFS volume
        /// being scanned. 
        /// </summary>
        private IntPtr ptrSource;
        public string Source => Marshal.PtrToStringUni(ptrSource);
        /// <summary>
        /// Path to the file (or directory) that has been scanned, valid
        /// on ::SCAN_DENTRY.  When capturing an NTFS
        /// volume with ::WIMLIB_ADD_FLAG_NTFS, this path will be
        /// relative to the root of the NTFS volume. 
        /// </summary>
        private IntPtr ptrCurPath;
        public string CurPath => Marshal.PtrToStringUni(ptrCurPath);
        /// <summary>
        /// Dentry scan status, valid on
        /// ::SCAN_DENTRY. 
        /// </summary>
        public WimLibScanStatus Status;
        /// <summary>
        /// - wim_target_path
        /// Target path in the image.  Only valid on messages
        /// ::SCAN_BEGIN and
        /// ::SCAN_END.
        /// 
        /// - symlink_target
        /// For ::SCAN_DENTRY and a status
        /// of @p WIMLIB_SCAN_DENTRY_FIXED_SYMLINK or @p
        /// WIMLIB_SCAN_DENTRY_NOT_FIXED_SYMLINK, this is the
        /// target of the absolute symbolic link or junction.
        /// </summary>
        private IntPtr ptrWimTargetPathSymlinkTarget;
        public string WimTargetPathSymlinkTarget => Marshal.PtrToStringUni(ptrWimTargetPathSymlinkTarget);
        /// <summary>
        /// The number of directories scanned so far, not counting
        /// excluded/unsupported files.
        /// </summary>
        public ulong NumDirsScanned;
        /// <summary>
        /// The number of non-directories scanned so far, not counting
        /// excluded/unsupported files.
        /// </summary>
        public ulong NumNonDirsScanned;
        /// <summary>
        /// The number of bytes of file data detected so far, not
        /// counting excluded/unsupported files.
        /// </summary>
        public ulong NumBytesScanned;
    }

    /// <summary>
    /// Valid on messages
    /// ::EXTRACT_SPWM_PART_BEGIN,
    /// ::EXTRACT_IMAGE_BEGIN,
    /// ::EXTRACT_TREE_BEGIN,
    /// ::EXTRACT_FILE_STRUCTURE,
    /// ::EXTRACT_STREAMS,
    /// ::EXTRACT_METADATA,
    /// ::EXTRACT_TREE_END, and
    /// ::EXTRACT_IMAGE_END.
    ///
    /// Note: most of the time of an extraction operation will be spent
    /// extracting file data, and the application will receive
    /// ::EXTRACT_STREAMS during this time.  Using @p
    /// completed_bytes and @p total_bytes, the application can calculate a
    /// percentage complete.  However, there is no way for applications to
    /// know which file is currently being extracted.  This is by design
    /// because the best way to complete the extraction operation is not
    /// necessarily file-by-file.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoExtract
    {
        /// <summary>
        /// The 1-based index of the image from which files are being
		/// extracted.
        /// </summary>
        public uint Image;
        /// <summary>
        /// Extraction flags being used. 
        /// </summary>
        public uint ExtractFlags;
        /// <summary>
        /// If the ::WIMStruct from which the extraction being performed
        /// has a backing file, then this is an absolute path to that
        /// backing file.  Otherwise, this is @c NULL.
        /// </summary>
        private IntPtr ptrWimFileName;    
        public string WimFileName => Marshal.PtrToStringUni(ptrWimFileName);
        /// <summary>
        /// Name of the image from which files are being extracted, or
        /// the empty string if the image is unnamed.
        /// </summary>
        private IntPtr ptrImageName;
        public string ImageName => Marshal.PtrToStringUni(ptrImageName);
        /// <summary>
        /// Path to the directory or NTFS volume to which the files are
        /// being extracted.
        /// </summary>
        private IntPtr ptrTarget;
        public string Target => Marshal.PtrToStringUni(ptrTarget);
        /// <summary>
        /// Reserved.
        /// </summary>
        private IntPtr ptrReserved;
        public string Reserved => Marshal.PtrToStringUni(ptrReserved);
        /// <summary>
        /// he number of bytes of file data that will be extracted. 
        /// </summary>
        public ulong TotalBytes;
        /// <summary>
        /// The number of bytes of file data that have been extracted so
        /// far.  This starts at 0 and ends at @p total_bytes.
        /// </summary>
        public ulong CompletedBytes;
        /// <summary>
        /// The number of file streams that will be extracted.  This
        /// will often be similar to the "number of files", but for
        /// several reasons (hard links, named data streams, empty files,
        /// etc.) it can be different.
        /// </summary>
        public ulong TotalStreams;
        /// <summary>
        /// The number of file streams that have been extracted so far.
        /// This starts at 0 and ends at @p total_streams.
        /// </summary>
        public ulong CompletedStreams;
        /// <summary>
        /// Currently only used for
        /// ::EXTRACT_SPWM_PART_BEGIN. 
        /// </summary>
        public uint PartNumber;
        /// <summary>
        /// Currently only used for
        /// ::EXTRACT_SPWM_PART_BEGIN.
        /// </summary>
        public uint TotalParts;
        /// <summary>
        /// Currently only used for
        /// ::EXTRACT_SPWM_PART_BEGIN.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Guid;
        /// <summary>
        /// For ::EXTRACT_FILE_STRUCTURE and
        /// ::EXTRACT_METADATA messages, this is the
        /// number of files that have been processed so far.  Once the
        /// corresponding phase of extraction is complete, this value
        /// will be equal to @c end_file_count. 
        /// </summary>
        public ulong CurrentFileCount;
        /// <summary>
        /// For ::EXTRACT_FILE_STRUCTURE and
        /// ::EXTRACT_METADATA messages, this is
        /// total number of files that will be processed.
        /// 
        /// This number is provided for informational purposes only, e.g.
        /// for a progress bar.  This number will not necessarily be
        /// equal to the number of files actually being extracted.  This
        /// is because extraction backends are free to implement an
        /// extraction algorithm that might be more efficient than
        /// processing every file in the "extract file structure" and
        /// "extract file metadata" phases.  For example, the current
        /// implementation of the UNIX extraction backend will create
        /// files on-demand during the "extract file data" phase.
        /// Therefore, when using that particular extraction backend, @p
        /// end_file_count will only include directories and empty files.
        /// </summary>
        public ulong EndFileCount;
    }

    /// <summary>
    /// Valid on messages ::RENAME.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoRename
    {
        /// <summary>
        /// Name of the temporary file that the WIM was written to.
        /// </summary>
        private IntPtr ptrFrom;
        public string From => Marshal.PtrToStringUni(ptrFrom);
        /// <summary>
        /// Name of the original WIM file to which the temporary file is
        /// being renamed.
        /// </summary>
        private IntPtr ptrTo;
        public string To => Marshal.PtrToStringUni(ptrTo);
    }

    /// <summary>
    /// Valid on messages ::UPDATE_BEGIN_COMMAND and
    /// ::UPDATE_END_COMMAND.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoUpdate
    {
        /// <summary>
        /// Specification of an update to perform on a WIM image.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public class WimLibUpdateCommand
        {
            public WimLibUpdateOp Op;
            private IntPtr ptrCmd;

            public WimLibCommand GetCommand()
            {
                switch (Op)
                {
                    case WimLibUpdateOp.WIMLIB_UPDATE_OP_ADD:
                        return (WimLibAddCommand) Marshal.PtrToStructure(ptrCmd, typeof(WimLibAddCommand));
                    case WimLibUpdateOp.WIMLIB_UPDATE_OP_DELETE:
                        return (WimLibDeleteCommand)Marshal.PtrToStructure(ptrCmd, typeof(WimLibDeleteCommand));
                    case WimLibUpdateOp.WIMLIB_UPDATE_OP_RENAME:
                        return (WimLibRenameCommand)Marshal.PtrToStructure(ptrCmd, typeof(WimLibRenameCommand));
                    default:
                        throw new InvalidOperationException("Wrong WimLibUpdateOp");
                }
            }
        }

        /// <summary>
        /// The specific type of update to perform.
        /// </summary>
        public enum WimLibUpdateOp : uint
        {
            /// <summary>
            /// Add a new file or directory tree to the image. 
            /// </summary>
            WIMLIB_UPDATE_OP_ADD = 0,
            /// <summary>
            /// Delete a file or directory tree from the image.
            /// </summary>
            WIMLIB_UPDATE_OP_DELETE = 1,
            /// <summary>
            /// Rename a file or directory tree in the image.
            /// </summary>
            WIMLIB_UPDATE_OP_RENAME = 2,
        };

        [StructLayout(LayoutKind.Sequential)]
        public class WimLibCommand { }

        /// <summary>
        /// Data for a ::WIMLIB_UPDATE_OP_ADD operation.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public class WimLibAddCommand : WimLibCommand
        {
            /// <summary>
            /// Filesystem path to the file or directory tree to add.
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string FsSourcePath;
            /// <summary>
            /// Destination path in the image.  To specify the root directory of the
            /// image, use ::WIMLIB_WIM_ROOT_PATH.
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string WimTargetPath;
            /// <summary>
            /// Path to capture configuration file to use, or @c NULL if not
            /// specified.
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string ConfigFile;
            /// <summary>
            /// Bitwise OR of WIMLIB_ADD_FLAG_* flags.
            /// </summary>
            public int AddFlags;
        };

        /// <summary>
        /// Data for a ::WIMLIB_UPDATE_OP_DELETE operation.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public class WimLibDeleteCommand : WimLibCommand
        {
            /// <summary>
            /// The path to the file or directory within the image to delete.
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string WimPath;

            /** Bitwise OR of WIMLIB_DELETE_FLAG_* flags.  */
            int DeleteFlags;
        };

        /// <summary>
        /// Data for a ::WIMLIB_UPDATE_OP_RENAME operation.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public class WimLibRenameCommand : WimLibCommand
        {
            /// <summary>
            /// The path to the source file or directory within the image.
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string WimSourcePath;
            /// <summary>
            /// The path to the destination file or directory within the image.
            /// </summary>
            [MarshalAs(UnmanagedType.LPWStr)]
            public string WimTargetPath;
            /// <summary>
            /// Reserved; set to 0.
            /// </summary>
            int RenameFlags;
        };

        /// <summary>
        /// Name of the temporary file that the WIM was written to.
        /// </summary>
        private IntPtr ptrCommand;
        public WimLibUpdateCommand Command => (WimLibUpdateCommand) Marshal.PtrToStructure(ptrCommand, typeof(WimLibUpdateCommand));
        /// <summary>
        /// Number of update commands that have been completed so far.
        /// 
        /// Use value of UIntPtr, this is not a memory address.
        /// </summary>
        public UIntPtr CompletedCommands;
        /// <summary>
        /// Number of update commands that are being executed as part of
        /// this call to wimlib_update_image().
        /// 
        /// Use value of UIntPtr, this is not a memory address.
        /// </summary>
        public UIntPtr TotalCommands;
    }

    /// <summary>
    /// Valid on messages ::VERIFY_INTEGRITY and
    /// ::CALC_INTEGRITY.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoIntegrity
    {
        /// <summary>
        /// The number of bytes in the WIM file that are covered by
        /// integrity checks.
        /// </summary>
        public ulong TotalBytes;
        /// <summary>
        /// The number of bytes that have been checksummed so far.  This
        /// starts at 0 and ends at @p total_bytes.
        /// </summary>
        public ulong CompletedBytes;
        /// <summary>
        /// The number of individually checksummed "chunks" the
        /// integrity-checked region is divided into.
        /// </summary>
        public uint TotalChunks;
        /// <summary>
        /// The number of chunks that have been checksummed so far.
        /// This starts at 0 and ends at @p total_chunks.
        /// </summary>
        public uint CompletedChunks;
        /// <summary>
        /// The size of each individually checksummed "chunk" in the
        /// integrity-checked region.
        /// </summary>
        public uint ChunkSize;
        /// <summary>
        /// For ::VERIFY_INTEGRITY messages, this is
        /// the path to the WIM file being checked.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FileName;
    }

    /// <summary>
    /// Valid on messages ::SPLIT_BEGIN_PART and
    /// ::SPLIT_END_PART.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoSplit
    {
        /// <summary>
        /// Total size of the original WIM's file and metadata resources
        /// (compressed).
        /// </summary>
        public ulong TotalBytes;
        /// <summary>
        /// Number of bytes of file and metadata resources that have
        /// been copied out of the original WIM so far.  Will be 0
        /// initially, and equal to @p total_bytes at the end.
        /// </summary>
        public ulong CompletedBytes;
        /// <summary>
        /// Number of the split WIM part that is about to be started
        /// (::SPLIT_BEGIN_PART) or has just been
        /// finished (::SPLIT_END_PART).
        /// </summary>
        public uint CurPartNumber;
        /// <summary>
        /// Total number of split WIM parts that are being written.
        /// </summary>
        public uint TotalParts;
        /// <summary>
        /// Name of the split WIM part that is about to be started
        /// (::SPLIT_BEGIN_PART) or has just been
        /// finished (::SPLIT_END_PART).  Since
        /// wimlib v1.7.0, the library user may change this when
        /// receiving ::SPLIT_BEGIN_PART in order to
        /// cause the next split WIM part to be written to a different
        /// location.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string PartName;
    }

    /// <summary>
    /// Valid on messages ::REPLACE_FILE_IN_WIM
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoReplace
    {
        /// <summary>
        /// Path to the file in the image that is being replaced
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string PathInWim;
    }

    /// <summary>
    /// Valid on messages ::WIMBOOT_EXCLUDE 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoWimbootExclude
    {
        /// <summary>
        /// Path to the file in the image
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string PathInWim;
        /// <summary>
        /// Path to which the file is being extracted 
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ExtractionInWim;
    }

    /// <summary>
    /// Valid on messages ::UNMOUNT_BEGIN.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoUnmount
    {
        /// <summary>
        /// Path to directory being unmounted
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string MountPoint;
        /// <summary>
        /// Path to WIM file being unmounted
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string MountedWim;
        /// <summary>
        /// 1-based index of image being unmounted.
        /// </summary>
        public uint MountedImage;
        /// <summary>
        /// Flags that were passed to wimlib_mount_image() when the
        /// mountpoint was set up.
        /// </summary>
        public uint MountFlags;
        /// <summary>
        /// Flags passed to wimlib_unmount_image().
        /// </summary>
        public uint UnmountFlags;
    }

    /// <summary>
    /// Valid on messages ::DONE_WITH_FILE.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoDoneWithFile
    {
        /// <summary>
        /// Path to the file whose data has been written to the WIM file,
        /// or is currently being asynchronously compressed in memory,
        /// and therefore is no longer needed by wimlib.
        ///
        /// WARNING: The file data will not actually be accessible in the
        /// WIM file until the WIM file has been completely written.
        /// Ordinarily you should <b>not</b> treat this message as a
        /// green light to go ahead and delete the specified file, since
        /// that would result in data loss if the WIM file cannot be
        /// successfully created for any reason.
        ///
        /// If a file has multiple names (hard links),
        /// ::DONE_WITH_FILE will only be received
        /// for one name.  Also, this message will not be received for
        /// empty files or reparse points (or symbolic links), unless
        /// they have nonempty named data streams.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string PathToFile;
    }

    /// <summary>
    /// Valid on messages ::BEGIN_VERIFY_IMAGE and
    /// ::END_VERIFY_IMAGE. 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoVerifyImage
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string WimFile;
        public uint TotalImages;
        public uint CurrentImage;
    }

    /// <summary>
    /// Valid on messages ::VERIFY_STREAMS.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoVerifyStreams
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string WimFile;
        public uint TotalStreams;
        public uint TotalBytes;
        public uint CurrentStreams;
        public uint CurrentBytes;
    }

    /// <summary>
    /// Valid on messages ::TEST_FILE_EXCLUSION.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoTestFileExclusion
    {
        /// <summary>
        /// Path to the file for which exclusion is being tested.
        ///
        /// UNIX capture mode:  The path will be a standard relative or
        /// absolute UNIX filesystem path.
        ///
        /// NTFS-3G capture mode:  The path will be given relative to the
        /// root of the NTFS volume, with a leading slash.
        ///
        /// Windows capture mode:  The path will be a Win32 namespace
        /// path to the file.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Path;
        /// <summary>
        /// Indicates whether the file or directory will be excluded from
        /// capture or not.  This will be <c>false</c> by default.  The
        /// progress function can set this to <c>true</c> if it decides
        /// that the file needs to be excluded.
        /// </summary>
        public bool WillExclude;
    }

    /// <summary>
    /// Valid on messages ::HANDLE_ERROR. 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WimLibProgressInfoHandleError
    {
        /// <summary>
        /// Path to the file for which the error occurred, or NULL if
        /// not relevant.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Path;
        /// <summary>
        /// The wimlib error code associated with the error.
        /// </summary>
        public int ErrorCode;
        /// <summary>
        /// Indicates whether the error will be ignored or not.  This
        /// will be <c>false</c> by default; the progress function may
        /// set it to <c>true</c>.
        /// </summary>
        public bool WillIgnore;
    }
    #endregion
}
