using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace ManagedWimLib
{
    #region SafeLibraryHandle
    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    public class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid, IDisposable
    {
        #region Managed Methods
        public SafeLibraryHandle() : base(true) { }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            return FreeLibrary(this.handle);
        }
        #endregion

        #region Windows API
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);
        #endregion
    }
    #endregion

    #region PinnedObject, PinnedArray
    internal class PinnedObject : IDisposable
    {
        internal GCHandle hObject;
        internal object _object;

        public PinnedObject(object _object)
        {
            this._object = _object;
            hObject = GCHandle.Alloc(_object, GCHandleType.Pinned);
        }

        ~PinnedObject()
        {
            hObject.Free();
        }

        public void Dispose()
        {
            hObject.Free();
            GC.SuppressFinalize(this);
        }
    }

    internal class PinnedArray : IDisposable
    {
        internal GCHandle hBuffer;
        internal Array buffer;

        public IntPtr this[int idx] => Marshal.UnsafeAddrOfPinnedArrayElement(buffer, idx);
        public static implicit operator IntPtr(PinnedArray fixedArray) => fixedArray[0];

        public PinnedArray(Array buffer)
        {
            this.buffer = buffer;
            hBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        }

        ~PinnedArray()
        {
            hBuffer.Free();
        }

        public void Dispose()
        {
            hBuffer.Free();
            GC.SuppressFinalize(this);
        }
    }
    #endregion

    #region WibLibNative
    public static class WimLibNative
    {
        #region Const and Fields
        internal const string InitFirstErrorMsg = "Please call WimLibNative.AssemblyInit() first!";
        internal static int WimStructSize;

        internal static SafeLibraryHandle hModule = null;
        public static bool Loaded => (hModule != null);
        #endregion

        #region AssemblyInit, AssemblyCleanup
        public static SafeLibraryHandle AssemblyInit(string dllPath, WimLibInitFlags initFlags = 0)
        {
            if (hModule == null)
            {
                if (dllPath == null) throw new ArgumentNullException("dllPath");
                else if (!File.Exists(dllPath)) throw new FileNotFoundException("Specified dll does not exist");

                hModule = LoadLibrary(dllPath);
                if (hModule.IsInvalid)
                    throw new ArgumentException($"Unable to load [{dllPath}]", new Win32Exception());

                // Check if dll is valid wimlib-15.dll
                if (GetProcAddress(hModule, "wimlib_open_wim") == IntPtr.Zero)
                {
                    AssemblyCleanup();
                    throw new ArgumentException($"[{dllPath}] is not a valid wimlib library");
                }

                try
                {
                    LoadFuntions(hModule);
                }
                catch (Exception e)
                {
                    AssemblyCleanup();
                    throw e;
                }

                GlobalInit(initFlags);
            }

            // Set WimStructSize, value of wimlib 1.12 (mingw)
            if (IntPtr.Size == 8) // 64
                WimStructSize = 0x1B8; 
            else if (IntPtr.Size == 4) // 32
                WimStructSize = 0x190;
            else
                throw new PlatformNotSupportedException();

            return hModule;
        }

        public static void AssemblyCleanup()
        {
            if (hModule != null)
            {
                GlobalCleanup();

                ResetFuntions();

                hModule.Close();
                hModule = null;
            }
        }
        #endregion

        #region LoadFunctions, ResetFunctions, GetFuncPtr
        private static Delegate GetFuncPtr(string exportFunc, Type delegateType)
        {
            IntPtr funcPtr = GetProcAddress(hModule, exportFunc);
            if (funcPtr == null || funcPtr == IntPtr.Zero)
                throw new ArgumentException($"Cannot import [{exportFunc}]", new Win32Exception());
            return Marshal.GetDelegateForFunctionPointer(funcPtr, delegateType);
        }

        private static void LoadFuntions(SafeLibraryHandle hModule)
        {
            GlobalInit = (wimlib_global_init)GetFuncPtr("wimlib_global_init", typeof(wimlib_global_init));
            GlobalCleanup = (wimlib_global_cleanup)GetFuncPtr("wimlib_global_cleanup", typeof(wimlib_global_cleanup));
            OpenWim = (wimlib_open_wim)GetFuncPtr("wimlib_open_wim", typeof(wimlib_open_wim));
            OpenWimWithProgress = (wimlib_open_wim_with_progress)GetFuncPtr("wimlib_open_wim_with_progress", typeof(wimlib_open_wim_with_progress));
            Free = (wimlib_free)GetFuncPtr("wimlib_free", typeof(wimlib_free));
            GetErrorString = (wimlib_get_error_string)GetFuncPtr("wimlib_get_error_string", typeof(wimlib_get_error_string));
            RegisterProgressFunction = (wimlib_register_progress_function_delegate)GetFuncPtr("wimlib_register_progress_function", typeof(wimlib_register_progress_function_delegate));
            ExtractImage = (wimlib_extract_image)GetFuncPtr("wimlib_extract_image", typeof(wimlib_extract_image));
            Overwrite = (wimlib_overwrite)GetFuncPtr("wimlib_overwrite", typeof(wimlib_overwrite));
        }

        private static void ResetFuntions()
        {
            GlobalInit = null;
            GlobalCleanup = null;
            OpenWim = null;
            OpenWimWithProgress = null;
            Free = null;
            GetErrorString = null;
            RegisterProgressFunction = null;
            ExtractImage = null;
            Overwrite = null;
        }
        #endregion

        #region Windows API
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeLibraryHandle LoadLibrary([MarshalAs(UnmanagedType.LPTStr)] string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);
        #endregion

        #region WimLib Function Pointer
        /// <summary>
        /// Initialization function for wimlib.  Call before using any other wimlib
        /// function (except possibly wimlib_set_print_errors()).  If not done manually,
        /// this function will be called automatically with a flags argument of 0.  This
        /// function does nothing if called again after it has already successfully run.
        /// </summary>
        /// <param name="init_flags">Bitwise OR of flags prefixed with WIMLIB_INIT_FLAG.</param>
        /// <returns>
        /// ::WIMLIB_ERR_INSUFFICIENT_PRIVILEGES
        /// ::WIMLIB_INIT_FLAG_STRICT_APPLY_PRIVILEGES and/or
        /// ::WIMLIB_INIT_FLAG_STRICT_CAPTURE_PRIVILEGES were specified in @p
        /// init_flags, but the corresponding privileges could not be acquired.
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate WimLibErrorCode wimlib_global_init(WimLibInitFlags init_flags);
        internal static wimlib_global_init GlobalInit;

        /// <summary>
        /// Cleanup function for wimlib.  You are not required to call this function, but
        /// it will release any global resources allocated by the library.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void wimlib_global_cleanup();
        internal static wimlib_global_cleanup GlobalCleanup;


        /// <summary>
        /// Open a WIM file and create a ::WIMStruct for it.
        /// </summary>
        /// <param name="wim_file">The path to the WIM file to open.</param>
        /// <param name="open_flags">Bitwise OR of flags prefixed with WIMLIB_OPEN_FLAG.</param>
        /// <param name="wim_ret">
        /// On success, a pointer to a new ::WIMStruct backed by the specified
        /// on-disk WIM file is written to the memory location pointed to by this
        /// parameter.  This ::WIMStruct must be freed using using wimlib_free()
        /// when finished with it.
        /// </param>
        /// <returns>0 on success; a ::wimlib_error_code value on failure.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate WimLibErrorCode wimlib_open_wim(
            [MarshalAs(UnmanagedType.LPWStr)] string wim_file,
            WimLibOpenFlags open_flags,
            out IntPtr wim_ret);
        internal static wimlib_open_wim OpenWim;

        /// <summary>
        /// @ingroup G_creating_and_opening_wims
        ///
        /// Same as wimlib_open_wim(), but allows specifying a progress function and
        /// progress context.  If successful, the progress function will be registered in
        /// the newly open ::WIMStruct, as if by an automatic call to
        /// wimlib_register_progress_function().  In addition, if
        /// ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY is specified in @p open_flags, then the
        /// progress function will receive ::WIMLIB_PROGRESS_MSG_VERIFY_INTEGRITY
        /// messages while checking the WIM file's integrity.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate WimLibErrorCode wimlib_open_wim_with_progress(
            [MarshalAs(UnmanagedType.LPWStr)] string wim_file,
            WimLibOpenFlags open_flags,
            out IntPtr wim_ret,
            [MarshalAs(UnmanagedType.FunctionPtr)] ProgressFunc progfunc,
            IntPtr progctx);
        internal static wimlib_open_wim_with_progress OpenWimWithProgress;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate WimLibProgressStatus ProgressFunc(
            WimLibProgressMsg msg_type,
            IntPtr info,
            IntPtr progctx);

        /// <summary>
        /// Register a progress function with a ::WIMStruct.
        /// </summary>
        /// <param name="wim">The ::WIMStruct for which to register the progress function.</param>
        /// <param name="progfunc">
        /// Pointer to the progress function to register.  If the WIM already has a
        /// progress function registered, it will be replaced with this one.  If @p
        /// NULL, the current progress function (if any) will be unregistered.
        /// </param>
        /// <param name="progctx">
        /// The value which will be passed as the third argument to calls to @p
        /// progfunc.
        /// </param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void wimlib_register_progress_function_delegate(
            IntPtr wim,
            [MarshalAs(UnmanagedType.FunctionPtr)] ProgressFunc progfunc,
            IntPtr progctx);
        internal static wimlib_register_progress_function_delegate RegisterProgressFunction;

        /// <summary>
        /// Release a reference to a ::WIMStruct.  If the ::WIMStruct is still referenced
        /// by other ::WIMStruct's (e.g. following calls to wimlib_export_image() or
        /// wimlib_reference_resources()), then the library will free it later, when the
        /// last reference is released; otherwise it is freed immediately and any
        /// associated file descriptors are closed.
        /// </summary>
        /// <param name="wim">Pointer to the ::WIMStruct to release.  If @c NULL, no action is taken.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void wimlib_free(IntPtr wim);
        internal static wimlib_free Free;

        /// <summary>
        /// Convert a wimlib error code into a string describing it.
        /// </summary>
        /// <param name="code">An error code returned by one of wimlib's functions.</param>
        /// <returns>
        /// Pointer to a statically allocated string describing the error code.  If
        /// the value was unrecognized, then the resulting string will be "Unknown
        /// error".
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        internal delegate string wimlib_get_error_string(WimLibErrorCode code);
        internal static wimlib_get_error_string GetErrorString;

        /// <summary>
        /// Extract an image, or all images, from a ::WIMStruct.
        ///
        /// The exact behavior of how wimlib extracts files from a WIM image is
        /// controllable by the @p extract_flags parameter, but there also are
        /// differences depending on the platform (UNIX-like vs Windows).  See the
        /// documentation for <b>wimapply</b> for more information, including about the
        /// NTFS-3G extraction mode.
        /// </summary>
        /// <param name="wim">
        /// The WIM from which to extract the image(s), specified as a pointer to the
        /// ::WIMStruct for a standalone WIM file, a delta WIM file, or part 1 of a
        /// split WIM.  In the case of a WIM file that is not standalone, this
        /// ::WIMStruct must have had any needed external resources previously
        /// referenced using wimlib_reference_resources() or
        /// wimlib_reference_resource_files().
        /// </param>
        /// <param name="image">
        /// The 1-based index of the image to extract, or ::WIMLIB_ALL_IMAGES to
        /// extract all images.  Note: ::WIMLIB_ALL_IMAGES is unsupported in NTFS-3G
        /// extraction mode.
        /// </param>
        /// <param name="target">
        /// A null-terminated string which names the location to which the image(s)
        /// will be extracted.  By default, this is interpreted as a path to a
        /// directory.  Alternatively, if ::WIMLIB_EXTRACT_FLAG_NTFS is specified in
        /// @p extract_flags, then this is interpreted as a path to an unmounted
        /// NTFS volume.
        /// </param>
        /// <param name="extract_flags">
        /// Bitwise OR of flags prefixed with WIMLIB_EXTRACT_FLAG.
        /// </param>
        /// <returns>
        /// return 0 on success; a ::wimlib_error_code value on failure.
        /// </returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate WimLibErrorCode wimlib_extract_image(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string target,
            WimLibExtractFlags extract_flags);
        internal static wimlib_extract_image ExtractImage;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wim"></param>
        /// <param name="write_flags"></param>
        /// <param name="numThreads"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate WimLibErrorCode wimlib_overwrite(
            IntPtr wim,
            WimLibWriteFlags write_flags,
            uint numThreads);
        internal static wimlib_overwrite Overwrite;
        #endregion
    }
    #endregion

    #region Enum Progress
    /// <summary>
    /// Possible values of the first parameter to the user-supplied
    /// ::wimlib_progress_func_t progress function
    /// </summary>
    public enum WimLibProgressMsg
    {
        /** A WIM image is about to be extracted.  @p info will point to
         * ::wimlib_progress_info.extract.  This message is received once per
         * image for calls to wimlib_extract_image() and
         * wimlib_extract_image_from_pipe().  */
        EXTRACT_IMAGE_BEGIN = 0,

        /** One or more file or directory trees within a WIM image is about to
         * be extracted.  @p info will point to ::wimlib_progress_info.extract.
         * This message is received only once per wimlib_extract_paths() and
         * wimlib_extract_pathlist(), since wimlib combines all paths into a
         * single extraction operation for optimization purposes.  */
        EXTRACT_TREE_BEGIN = 1,

        /** This message may be sent periodically (not for every file) while
         * files and directories are being created, prior to file data
         * extraction.  @p info will point to ::wimlib_progress_info.extract.
         * In particular, the @p current_file_count and @p end_file_count
         * members may be used to track the progress of this phase of
         * extraction.  */
        EXTRACT_FILE_STRUCTURE = 3,

        /** File data is currently being extracted.  @p info will point to
         * ::wimlib_progress_info.extract.  This is the main message to track
         * the progress of an extraction operation.  */
        EXTRACT_STREAMS = 4,

        /** Starting to read a new part of a split pipable WIM over the pipe.
         * @p info will point to ::wimlib_progress_info.extract.  */
        EXTRACT_SPWM_PART_BEGIN = 5,

        /** This message may be sent periodically (not necessarily for every
         * file) while file and directory metadata is being extracted, following
         * file data extraction.  @p info will point to
         * ::wimlib_progress_info.extract.  The @p current_file_count and @p
         * end_file_count members may be used to track the progress of this
         * phase of extraction.  */
        EXTRACT_METADATA = 6,

        /** The image has been successfully extracted.  @p info will point to
         * ::wimlib_progress_info.extract.  This is paired with
         * ::WIMLIB_PROGRESS_MSG_EXTRACT_IMAGE_BEGIN.  */
        EXTRACT_IMAGE_END = 7,

        /** The files or directory trees have been successfully extracted.  @p
         * info will point to ::wimlib_progress_info.extract.  This is paired
         * with ::WIMLIB_PROGRESS_MSG_EXTRACT_TREE_BEGIN.  */
        EXTRACT_TREE_END = 8,

        /** The directory or NTFS volume is about to be scanned for metadata.
         * @p info will point to ::wimlib_progress_info.scan.  This message is
         * received once per call to wimlib_add_image(), or once per capture
         * source passed to wimlib_add_image_multisource(), or once per add
         * command passed to wimlib_update_image().  */
        SCAN_BEGIN = 9,

        /** A directory or file has been scanned.  @p info will point to
         * ::wimlib_progress_info.scan, and its @p cur_path member will be
         * valid.  This message is only sent if ::WIMLIB_ADD_FLAG_VERBOSE has
         * been specified.  */
        SCAN_DENTRY = 10,

        /** The directory or NTFS volume has been successfully scanned.  @p info
         * will point to ::wimlib_progress_info.scan.  This is paired with a
         * previous ::WIMLIB_PROGRESS_MSG_SCAN_BEGIN message, possibly with many
         * intervening ::WIMLIB_PROGRESS_MSG_SCAN_DENTRY messages.  */
        SCAN_END = 11,

        /** File data is currently being written to the WIM.  @p info will point
         * to ::wimlib_progress_info.write_streams.  This message may be
         * received many times while the WIM file is being written or appended
         * to with wimlib_write(), wimlib_overwrite(), or wimlib_write_to_fd().
         */
        WRITE_STREAMS = 12,

        /** Per-image metadata is about to be written to the WIM file.  @p info
         * will not be valid. */
        WRITE_METADATA_BEGIN = 13,

        /** The per-image metadata has been written to the WIM file.  @p info
         * will not be valid.  This message is paired with a preceding
         * ::WIMLIB_PROGRESS_MSG_WRITE_METADATA_BEGIN message.  */
        WRITE_METADATA_END = 14,

        /** wimlib_overwrite() has successfully renamed the temporary file to
         * the original WIM file, thereby committing the changes to the WIM
         * file.  @p info will point to ::wimlib_progress_info.rename.  Note:
         * this message is not received if wimlib_overwrite() chose to append to
         * the WIM file in-place.  */
        RENAME = 15,

        /** The contents of the WIM file are being checked against the integrity
         * table.  @p info will point to ::wimlib_progress_info.integrity.  This
         * message is only received (and may be received many times) when
         * wimlib_open_wim_with_progress() is called with the
         * ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY flag.  */
        VERIFY_INTEGRITY = 16,

        /** An integrity table is being calculated for the WIM being written.
         * @p info will point to ::wimlib_progress_info.integrity.  This message
         * is only received (and may be received many times) when a WIM file is
         * being written with the flag ::WIMLIB_WRITE_FLAG_CHECK_INTEGRITY.  */
        CALC_INTEGRITY = 17,

        /** A wimlib_split() operation is in progress, and a new split part is
         * about to be started.  @p info will point to
         * ::wimlib_progress_info.split.  */
        SPLIT_BEGIN_PART = 19,

        /** A wimlib_split() operation is in progress, and a split part has been
         * finished. @p info will point to ::wimlib_progress_info.split.  */
        SPLIT_END_PART = 20,

        /** A WIM update command is about to be executed. @p info will point to
         * ::wimlib_progress_info.update.  This message is received once per
         * update command when wimlib_update_image() is called with the flag
         * ::WIMLIB_UPDATE_FLAG_SEND_PROGRESS.  */
        UPDATE_BEGIN_COMMAND = 21,

        /** A WIM update command has been executed. @p info will point to
         * ::wimlib_progress_info.update.  This message is received once per
         * update command when wimlib_update_image() is called with the flag
         * ::WIMLIB_UPDATE_FLAG_SEND_PROGRESS.  */
        UPDATE_END_COMMAND = 22,

        /** A file in the image is being replaced as a result of a
         * ::wimlib_add_command without ::WIMLIB_ADD_FLAG_NO_REPLACE specified.
         * @p info will point to ::wimlib_progress_info.replace.  This is only
         * received when ::WIMLIB_ADD_FLAG_VERBOSE is also specified in the add
         * command.  */
        REPLACE_FILE_IN_WIM = 23,

        /** An image is being extracted with ::WIMLIB_EXTRACT_FLAG_WIMBOOT, and
         * a file is being extracted normally (not as a "WIMBoot pointer file")
         * due to it matching a pattern in the <c>[PrepopulateList]</c> section
         * of the configuration file
         * <c>/Windows/System32/WimBootCompress.ini</c> in the WIM image.  @p
         * info will point to ::wimlib_progress_info.wimboot_exclude.  */
        WIMBOOT_EXCLUDE = 24,

        /** Starting to unmount an image.  @p info will point to
         * ::wimlib_progress_info.unmount.  */
        UNMOUNT_BEGIN = 25,

        /** wimlib has used a file's data for the last time (including all data
         * streams, if it has multiple).  @p info will point to
         * ::wimlib_progress_info.done_with_file.  This message is only received
         * if ::WIMLIB_WRITE_FLAG_SEND_DONE_WITH_FILE_MESSAGES was provided.  */
        DONE_WITH_FILE = 26,

        /** wimlib_verify_wim() is starting to verify the metadata for an image.
         * @p info will point to ::wimlib_progress_info.verify_image.  */
        BEGIN_VERIFY_IMAGE = 27,

        /** wimlib_verify_wim() has finished verifying the metadata for an
         * image.  @p info will point to ::wimlib_progress_info.verify_image.
         */
        END_VERIFY_IMAGE = 28,

        /** wimlib_verify_wim() is verifying file data integrity.  @p info will
         * point to ::wimlib_progress_info.verify_streams.  */
        VERIFY_STREAMS = 29,

        /**
         * The progress function is being asked whether a file should be
         * excluded from capture or not.  @p info will point to
         * ::wimlib_progress_info.test_file_exclusion.  This is a bidirectional
         * message that allows the progress function to set a flag if the file
         * should be excluded.
         *
         * This message is only received if the flag
         * ::WIMLIB_ADD_FLAG_TEST_FILE_EXCLUSION is used.  This method for file
         * exclusions is independent of the "capture configuration file"
         * mechanism.
         */
        TEST_FILE_EXCLUSION = 30,

        /// <summary>
        /// An error has occurred and the progress function is being asked
        /// whether to ignore the error or not.  @p info will point to
        /// ::wimlib_progress_info.handle_error.  This is a bidirectional
        ///  essage.
        /// 
        /// This message provides a limited capability for applications to
        /// recover from "unexpected" errors (i.e. those with no in-library
        /// handling policy) arising from the underlying operating system.
        /// Normally, any such error will cause the library to abort the current
        /// operation.  By implementing a handler for this message, the
        /// application can instead choose to ignore a given error.
        /// 
        /// Currently, only the following types of errors will result in this
        /// progress message being sent:
        /// 
        /// 	- Directory tree scan errors, e.g. from wimlib_add_image()
        /// 	- Most extraction errors; currently restricted to the Windows
        /// 	  build of the library only.
        /// </summary>
        HANDLE_ERROR = 31,
    }

    /// <summary>
    /// A pointer to this union is passed to the user-supplied
    /// ::wimlib_progress_func_t progress function.One(or none) of the structures
    /// contained in this union will be applicable for the operation
    /// (::wimlib_progress_msg) indicated in the first argument to the progress
    /// function.
    /// </summary>
    public enum WimLibProgressStatus
    {
        /// <summary>
        /// The operation should be continued.  This is the normal return value.
        /// </summary>
        CONTINUE = 0,
        /// <summary>
        /// he operation should be aborted.  This will cause the current
        /// operation to fail with ::WIMLIB_ERR_ABORTED_BY_PROGRESS.
        /// </summary>
        ABORT = 1,
    }
    #endregion

    #region Enum ErrorCode 
    public enum WimLibErrorCode : int
    {
        SUCCESS = 0,
        ALREADY_LOCKED = 1,
        DECOMPRESSION = 2,
        FUSE = 6,
        GLOB_HAD_NO_MATCHES = 8,
        /// <summary>
        /// The number of metadata resources found in the WIM did not match the
        /// image count specified in the WIM header, or the number of &lt; IMAGE&gt;
        /// elements in the XML data of the WIM did not match the image count
        /// specified in the WIM header.
        /// </summary>
        IMAGE_COUNT = 10,
        IMAGE_NAME_COLLISION = 11,
        INSUFFICIENT_PRIVILEGES = 12,
        /// <summary>
        /// ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY was specified in @p open_flags, and
        /// the WIM file failed the integrity check.
        /// </summary>
        INTEGRITY = 13,
        INVALID_CAPTURE_CONFIG = 14,
        /// <summary>
        /// The library did not recognize the compression chunk size of the WIM as
        /// valid for its compression type.
        /// </summary>
        INVALID_CHUNK_SIZE = 15,
        /// <summary>
        /// The library did not recognize the compression type of the WIM.
        /// </summary>
        INVALID_COMPRESSION_TYPE = 16,
        /// <summary>
        /// The header of the WIM was otherwise invalid.
        /// </summary>
        INVALID_HEADER = 17,
        INVALID_IMAGE = 18,
        /// <summary>
        /// ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY was specified in @p open_flags and
        /// the WIM contained an integrity table, but the integrity table was
        /// invalid.
        /// </summary>
        INVALID_INTEGRITY_TABLE = 19,
        /// <summary>
        /// The lookup table of the WIM was invalid.
        /// </summary>
        INVALID_LOOKUP_TABLE_ENTRY = 20,
        INVALID_METADATA_RESOURCE = 21,
        INVALID_OVERLAY = 23,
        /// <summary>
        /// @p wim_ret was @c NULL; or, @p wim_file was not a nonempty string.
        /// </summary>
        INVALID_PARAM = 24,
        INVALID_PART_NUMBER = 25,
        INVALID_PIPABLE_WIM = 26,
        INVALID_REPARSE_DATA = 27,
        INVALID_RESOURCE_HASH = 28,
        INVALID_UTF16_STRING = 30,
        INVALID_UTF8_STRING = 31,
        IS_DIRECTORY = 32,
        /// <summary>
        /// The WIM was a split WIM and ::WIMLIB_OPEN_FLAG_ERROR_IF_SPLIT was
        /// specified in @p open_flags.
        /// </summary>
        IS_SPLIT_WIM = 33,
        LINK = 35,
        METADATA_NOT_FOUND = 36,
        MKDIR = 37,
        MQUEUE = 38,
        NOMEM = 39,
        NOTDIR = 40,
        NOTEMPTY = 41,
        NOT_A_REGULAR_FILE = 42,
        /// <summary>
        /// The file did not begin with the magic characters that identify a WIM
        /// file.
        /// </summary>
        NOT_A_WIM_FILE = 43,
        NOT_PIPABLE = 44,
        NO_FILENAME = 45,
        NTFS_3G = 46,
        /// <summary>
        /// Failed to open the WIM file for reading.  Some possible reasons: the WIM
        /// file does not exist, or the calling process does not have permission to
        /// open it.
        /// </summary>
        OPEN = 47,
        /// <summary>
        /// Failed to read data from the WIM file.
        /// </summary>
        OPENDIR = 48,
        PATH_DOES_NOT_EXIST = 49,
        READ = 50,
        READLINK = 51,
        RENAME = 52,
        REPARSE_POINT_FIXUP_FAILED = 54,
        RESOURCE_NOT_FOUND = 55,
        RESOURCE_ORDER = 56,
        SET_ATTRIBUTES = 57,
        SET_REPARSE_DATA = 58,
        SET_SECURITY = 59,
        SET_SHORT_NAME = 60,
        SET_TIMESTAMPS = 61,
        SPLIT_INVALID = 62,
        STAT = 63,
        /// <summary>
        /// Unexpected end-of-file while reading data from the WIM file.
        /// </summary>
        UNEXPECTED_END_OF_FILE = 65,
        UNICODE_STRING_NOT_REPRESENTABLE = 66,
        /// <summary>
        /// The WIM version number was not recognized. (May be a pre-Vista WIM.)
        /// </summary>
        UNKNOWN_VERSION = 67,
        UNSUPPORTED = 68,
        UNSUPPORTED_FILE = 69,
        /// <summary>
        /// ::WIMLIB_OPEN_FLAG_WRITE_ACCESS was specified but the WIM file was
        /// considered read-only because of any of the reasons mentioned in the
        /// documentation for the ::WIMLIB_OPEN_FLAG_WRITE_ACCESS flag.
        /// </summary>
        WIM_IS_READONLY = 71,
        WRITE = 72,
        /// <summary>
        /// The XML data of the WIM was invalid.
        /// </summary>
        XML = 73,
        /// <summary>
        /// The WIM cannot be opened because it contains encrypted segments.  (It
        /// may be a Windows 8 "ESD" file.)
        /// </summary>
        WIM_IS_ENCRYPTED = 74,
        WIMBOOT = 75,
        ABORTED_BY_PROGRESS = 76,
        UNKNOWN_PROGRESS_STATUS = 77,
        MKNOD = 78,
        MOUNTED_IMAGE_IS_BUSY = 79,
        NOT_A_MOUNTPOINT = 80,
        NOT_PERMITTED_TO_UNMOUNT = 81,
        FVE_LOCKED_VOLUME = 82,
        UNABLE_TO_READ_CAPTURE_CONFIG = 83,
        /// <summary>
        /// The WIM file is not complete (e.g. the program which wrote it was
        /// terminated before it finished)
        /// </summary>
        WIM_IS_INCOMPLETE = 84,
        COMPACTION_NOT_POSSIBLE = 85,
        IMAGE_HAS_MULTIPLE_REFERENCES = 86,
        DUPLICATE_EXPORTED_IMAGE = 87,
        CONCURRENT_MODIFICATION_DETECTED = 88,
        SNAPSHOT_FAILURE = 89,
        INVALID_XATTR = 90,
        SET_XATTR = 91,
    }
    #endregion

    #region Enum Flags
    [Flags]
    public enum WimLibInitFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// Windows-only: do not attempt to acquire additional privileges (currently
        /// SeBackupPrivilege, SeRestorePrivilege, SeSecurityPrivilege,
        /// SeTakeOwnershipPrivilege, and SeManageVolumePrivilege) when initializing the
        /// library.  This flag is intended for the case where the calling program
        /// manages these privileges itself.  Note: by default, no error is issued if
        /// privileges cannot be acquired, although related errors may be reported later,
        /// depending on if the operations performed actually require additional
        /// privileges or not.
        /// </summary>
        DONT_ACQUIRE_PRIVILEGES = 0x00000002,
        /// <summary>
        /// Windows only:  If ::WIMLIB_INIT_FLAG_DONT_ACQUIRE_PRIVILEGES not specified,
        /// return ::WIMLIB_ERR_INSUFFICIENT_PRIVILEGES if privileges that may be needed
        /// to read all possible data and metadata for a capture operation could not be
        /// acquired.  Can be combined with ::WIMLIB_INIT_FLAG_STRICT_APPLY_PRIVILEGES.
        /// </summary>
        STRICT_CAPTURE_PRIVILEGES = 0x00000004,
        /// <summary>
        /// Windows only:  If ::WIMLIB_INIT_FLAG_DONT_ACQUIRE_PRIVILEGES not specified,
        /// return ::WIMLIB_ERR_INSUFFICIENT_PRIVILEGES if privileges that may be needed
        /// to restore all possible data and metadata for an apply operation could not be
        /// acquired.  Can be combined with ::WIMLIB_INIT_FLAG_STRICT_CAPTURE_PRIVILEGES.
        /// </summary>
        STRICT_APPLY_PRIVILEGES = 0x00000008,
        /// <summary>
        /// Default to interpreting WIM paths case sensitively (default on UNIX-like
        /// systems).
        /// </summary>
        DEFAULT_CASE_SENSITIVE = 0x00000010,
        /// <summary>
        /// Default to interpreting WIM paths case insensitively (default on Windows).
        /// This does not apply to mounted images.
        /// </summary>
        DEFAULT_CASE_INSENSITIVE = 0x00000020,
    }

    [Flags]
    public enum WimLibAddFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// UNIX-like systems only: Directly capture an NTFS volume rather than a
        /// generic directory.  This requires that wimlib was compiled with support for
        /// libntfs-3g.
        ///
        /// This flag cannot be combined with ::WIMLIB_ADD_FLAG_DEREFERENCE or
        /// ::WIMLIB_ADD_FLAG_UNIX_DATA.
        ///
        /// Do not use this flag on Windows, where wimlib already supports all
        /// Windows-native filesystems, including NTFS, through the Windows APIs.
        /// </summary>
        NTFS = 0x00000001,
        /// <summary>
        /// Follow symbolic links when scanning the directory tree.  Currently only
        /// supported on UNIX-like systems.
        /// </summary>
        DEREFERENCE = 0x00000002,
        /// <summary>
        /// Call the progress function with the message
        /// ::WIMLIB_PROGRESS_MSG_SCAN_DENTRY when each directory or file has been
        /// scanned.
        /// </summary>
        VERBOSE = 0x00000004,
        /// <summary>
        /// Mark the image being added as the bootable image of the WIM.  This flag is
        /// valid only for wimlib_add_image() and wimlib_add_image_multisource().
        ///
        /// Note that you can also change the bootable image of a WIM using
        /// wimlib_set_wim_info().
        ///
        /// Note: ::WIMLIB_ADD_FLAG_BOOT does something different from, and independent
        /// from, ::WIMLIB_ADD_FLAG_WIMBOOT.
        /// </summary>
        BOOT = 0x00000008,
        /// <summary>
        /// UNIX-like systems only: Store the UNIX owner, group, mode, and device ID
        /// (major and minor number) of each file.  In addition, capture special files
        /// such as device nodes and FIFOs.  Since wimlib v1.11.0, on Linux also capture
        /// extended attributes.  See the documentation for the <b>--unix-data</b> option
        /// to <b>wimcapture</b> for more information.
        /// </summary>
        UNIX_DATA = 0x00000010,
        /// <summary>
        /// Do not capture security descriptors.  Only has an effect in NTFS-3G capture
        /// mode, or in Windows native builds.
        /// </summary>
        NO_ACLS = 0x00000020,
        /// <summary>
        /// Fail immediately if the full security descriptor of any file or directory
        /// cannot be accessed.  Only has an effect in Windows native builds.  The
        /// default behavior without this flag is to first try omitting the SACL from the
        /// security descriptor, then to try omitting the security descriptor entirely.
        /// </summary>
        STRICT_ACLS = 0x00000040,
        /// <summary>
        /// Call the progress function with the message
        /// ::WIMLIB_PROGRESS_MSG_SCAN_DENTRY when a directory or file is excluded from
        /// capture.  This is a subset of the messages provided by
        /// ::WIMLIB_ADD_FLAG_VERBOSE.
        /// </summary>
        EXCLUDE_VERBOSE = 0x00000080,
        /// <summary>
        /// Reparse-point fixups:  Modify absolute symbolic links (and junctions, in the
        /// case of Windows) that point inside the directory being captured to instead be
        /// absolute relative to the directory being captured.
        ///
        /// Without this flag, the default is to do reparse-point fixups if
        /// <c>WIM_HDR_FLAG_RP_FIX</c> is set in the WIM header or if this is the first
        /// image being added.
        /// </summary>
        RPFIX = 0x00000100,
        /// <summary>
        /// Don't do reparse point fixups.  See ::WIMLIB_ADD_FLAG_RPFIX.
        /// </summary>
        NORPFIX = 0x00000200,
        /// <summary>
        /// Do not automatically exclude unsupported files or directories from capture,
        /// such as encrypted files in NTFS-3G capture mode, or device files and FIFOs on
        /// UNIX-like systems when not also using ::WIMLIB_ADD_FLAG_UNIX_DATA.  Instead,
        /// fail with ::WIMLIB_ERR_UNSUPPORTED_FILE when such a file is encountered.
        /// </summary>
        NO_UNSUPPORTED_EXCLUDE = 0x00000400,
        /// <summary>
        /// Automatically select a capture configuration appropriate for capturing
        /// filesystems containing Windows operating systems.  For example,
        /// <c>/pagefile.sys</c> and <c>"/System Volume Information"</c> will be
        /// excluded.
        ///
        /// When this flag is specified, the corresponding @p config parameter (for
        /// wimlib_add_image()) or member (for wimlib_update_image()) must be @c NULL.
        /// Otherwise, ::WIMLIB_ERR_INVALID_PARAM will be returned.
        ///
        /// Note that the default behavior--- that is, when neither
        /// ::WIMLIB_ADD_FLAG_WINCONFIG nor ::WIMLIB_ADD_FLAG_WIMBOOT is specified and @p
        /// config is @c NULL--- is to use no capture configuration, meaning that no
        /// files are excluded from capture.
        /// </summary>
        WINCONFIG = 0x00000800,
        /// <summary>
        /// Capture image as "WIMBoot compatible".  In addition, if no capture
        /// configuration file is explicitly specified use the capture configuration file
        /// <c>$SOURCE/Windows/System32/WimBootCompress.ini</c> if it exists, where
        /// <c>$SOURCE</c> is the directory being captured; or, if a capture
        /// configuration file is explicitly specified, use it and also place it at
        /// <c>/Windows/System32/WimBootCompress.ini</c> in the WIM image.
        ///
        /// This flag does not, by itself, change the compression type or chunk size.
        /// Before writing the WIM file, you may wish to set the compression format to
        /// be the same as that used by WIMGAPI and DISM:
        ///
        /// \code
        /// wimlib_set_output_compression_type(wim, WIMLIB_COMPRESSION_TYPE_XPRESS);
        /// wimlib_set_output_chunk_size(wim, 4096);
        /// \endcode
        ///
        /// However, "WIMBoot" also works with other XPRESS chunk sizes as well as LZX
        /// with 32768 byte chunks.
        ///
        /// Note: ::WIMLIB_ADD_FLAG_WIMBOOT does something different from, and
        /// independent from, ::WIMLIB_ADD_FLAG_BOOT.
        ///
        /// Since wimlib v1.8.3, ::WIMLIB_ADD_FLAG_WIMBOOT also causes offline WIM-backed
        /// files to be added as the "real" files rather than as their reparse points,
        /// provided that their data is already present in the WIM.  This feature can be
        /// useful when updating a backing WIM file in an "offline" state.
        /// </summary>
        WIMBOOT = 0x00001000,
        /// <summary>
        /// If the add command involves adding a non-directory file to a location at
        /// which there already exists a nondirectory file in the image, issue
        /// ::WIMLIB_ERR_INVALID_OVERLAY instead of replacing the file.  This was the
        /// default behavior before wimlib v1.7.0.
        /// </summary>
        NO_REPLACE = 0x00002000,
        /// <summary>
        /// Send ::WIMLIB_PROGRESS_MSG_TEST_FILE_EXCLUSION messages to the progress
        /// function.
        ///
        /// Note: This method for file exclusions is independent from the capture
        /// configuration file mechanism.
        /// </summary>
        TEST_FILE_EXCLUSION = 0x00004000,
        /// <summary>
        /// Since wimlib v1.9.0: create a temporary filesystem snapshot of the source
        /// directory and add the files from it.  Currently, this option is only
        /// supported on Windows, where it uses the Volume Shadow Copy Service (VSS).
        /// Using this option, you can create a consistent backup of the system volume of
        /// a running Windows system without running into problems with locked files.
        /// For the VSS snapshot to be successfully created, your application must be run
        /// as an Administrator, and it cannot be run in WoW64 mode (i.e. if Windows is
        /// 64-bit, then your application must be 64-bit as well).
        /// </summary>
        SNAPSHOT = 0x00008000,
        /// <summary>
        /// Since wimlib v1.9.0: permit the library to discard file paths after the
        /// initial scan.  If the application won't use
        /// ::WIMLIB_WRITE_FLAG_SEND_DONE_WITH_FILE_MESSAGES while writing the WIM
        /// archive, this flag can be used to allow the library to enable optimizations
        /// such as opening files by inode number rather than by path.  Currently this
        /// only makes a difference on Windows.
        /// </summary>
        FILE_PATHS_UNNEEDED = 0x00010000,
    }

    [Flags]
    public enum WimLibOpenFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// Verify the WIM contents against the WIM's integrity table, if present.  The
        /// integrity table stores checksums for the raw data of the WIM file, divided
        /// into fixed size chunks.  Verification will compute checksums and compare them
        /// with the stored values.  If there are any mismatches, then
        /// ::WIMLIB_ERR_INTEGRITY will be issued.  If the WIM file does not contain an
        /// integrity table, then this flag has no effect.
        /// </summary>
        CHECK_INTEGRITY = 0x00000001,
        /// <summary>
        /// Issue an error (::WIMLIB_ERR_IS_SPLIT_WIM) if the WIM is part of a split
        /// WIM.  Software can provide this flag for convenience if it explicitly does
        /// not want to support split WIMs.
        /// </summary>
        ERROR_IF_SPLIT = 0x00000002,
        /// <summary>
        /// Check if the WIM is writable and issue an error
        /// (::WIMLIB_ERR_WIM_IS_READONLY) if it is not.  A WIM is considered writable
        /// only if it is writable at the filesystem level, does not have the
        /// <c>WIM_HDR_FLAG_READONLY</c> flag set in its header, and is not part of a
        /// spanned set.  It is not required to provide this flag before attempting to
        /// make changes to the WIM, but with this flag you get an error immediately
        /// rather than potentially much later, when wimlib_overwrite() is finally
        /// called.
        /// </summary>
        WRITE_ACCESS = 0x00000004,
    }

    [Flags]
    public enum WimLibWriteFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// Include an integrity table in the resulting WIM file.
        ///
        /// For ::WIMStruct's created with wimlib_open_wim(), the default behavior is to
        /// include an integrity table if and only if one was present before.  For
        /// ::WIMStruct's created with wimlib_create_new_wim(), the default behavior is
        /// to not include an integrity table.
        /// </summary>
        CHECK_INTEGRITY = 0x00000001,
        /// <summary>
        /// Do not include an integrity table in the resulting WIM file.  This is the
        /// default behavior, unless the ::WIMStruct was created by opening a WIM with an
        /// integrity table.
        /// </summary>
        NO_CHECK_INTEGRITY = 0x00000002,
        /// <summary>
        /// Write the WIM as "pipable".  After writing a WIM with this flag specified,
        /// images from it can be applied directly from a pipe using
        /// wimlib_extract_image_from_pipe().  See the documentation for the
        /// <b>--pipable</b> option of <b>wimcapture</b> for more information.  Beware:
        /// WIMs written with this flag will not be compatible with Microsoft's software.
        ///
        /// For ::WIMStruct's created with wimlib_open_wim(), the default behavior is to
        /// write the WIM as pipable if and only if it was pipable before.  For
        /// ::WIMStruct's created with wimlib_create_new_wim(), the default behavior is
        /// to write the WIM as non-pipable.
        /// </summary>
        PIPABLE = 0x00000004,
        /// <summary>
        /// Do not write the WIM as "pipable".  This is the default behavior, unless the
        /// ::WIMStruct was created by opening a pipable WIM.
        /// </summary>
        NOT_PIPABLE = 0x00000008,
        /// <summary>
        /// When writing data to the WIM file, recompress it, even if the data is already
        /// available in the desired compressed form (for example, in a WIM file from
        /// which an image has been exported using wimlib_export_image()).
        ///
        /// ::WIMLIB_WRITE_FLAG_RECOMPRESS can be used to recompress with a higher
        /// compression ratio for the same compression type and chunk size.  Simply using
        /// the default compression settings may suffice for this, especially if the WIM
        /// file was created using another program/library that may not use as
        /// sophisticated compression algorithms.  Or,
        /// wimlib_set_default_compression_level() can be called beforehand to set an
        /// even higher compression level than the default.
        ///
        /// If the WIM contains solid resources, then ::WIMLIB_WRITE_FLAG_RECOMPRESS can
        /// be used in combination with ::WIMLIB_WRITE_FLAG_SOLID to prevent any solid
        /// resources from being re-used.  Otherwise, solid resources are re-used
        /// somewhat more liberally than normal compressed resources.
        ///
        /// ::WIMLIB_WRITE_FLAG_RECOMPRESS does <b>not</b> cause recompression of data
        /// that would not otherwise be written.  For example, a call to
        /// wimlib_overwrite() with ::WIMLIB_WRITE_FLAG_RECOMPRESS will not, by itself,
        /// cause already-existing data in the WIM file to be recompressed.  To force the
        /// WIM file to be fully rebuilt and recompressed, combine
        /// ::WIMLIB_WRITE_FLAG_RECOMPRESS with ::WIMLIB_WRITE_FLAG_REBUILD.
        /// </summary>
        RECOMPRESS = 0x00000010,
        /// <summary>
        /// Immediately before closing the WIM file, sync its data to disk.
        ///
        /// This flag forces the function to wait until the data is safely on disk before
        /// returning success.  Otherwise, modern operating systems tend to cache data
        /// for some time (in some cases, 30+ seconds) before actually writing it to
        /// disk, even after reporting to the application that the writes have succeeded.
        ///
        /// wimlib_overwrite() will set this flag automatically if it decides to
        /// overwrite the WIM file via a temporary file instead of in-place.  This is
        /// necessary on POSIX systems; it will, for example, avoid problems with delayed
        /// allocation on ext4.
        /// </summary>
        FSYNC = 0x00000020,
        /// <summary>
        /// For wimlib_overwrite(): rebuild the entire WIM file, even if it otherwise
        /// could be updated in-place by appending to it.  Any data that existed in the
        /// original WIM file but is not actually needed by any of the remaining images
        /// will not be included.  This can free up space left over after previous
        /// in-place modifications to the WIM file.
        ///
        /// This flag can be combined with ::WIMLIB_WRITE_FLAG_RECOMPRESS to force all
        /// data to be recompressed.  Otherwise, compressed data is re-used if possible.
        ///
        /// wimlib_write() ignores this flag.
        /// </summary>
        REBUILD = 0x00000040,
        /// <summary>
        /// For wimlib_overwrite(): override the default behavior after one or more calls
        /// to wimlib_delete_image(), which is to rebuild the entire WIM file.  With this
        /// flag, only minimal changes to correctly remove the image from the WIM file
        /// will be taken.  This can be much faster, but it will result in the WIM file
        /// getting larger rather than smaller.
        ///
        /// wimlib_write() ignores this flag.
        /// </summary>
        SOFT_DELETE = 0x00000080,
        /// <summary>
        /// For wimlib_overwrite(), allow overwriting the WIM file even if the readonly
        /// flag (<c>WIM_HDR_FLAG_READONLY</c>) is set in the WIM header.  This can be
        /// used following a call to wimlib_set_wim_info() with the
        /// ::WIMLIB_CHANGE_READONLY_FLAG flag to actually set the readonly flag on the
        /// on-disk WIM file.
        ///
        /// wimlib_write() ignores this flag.
        /// </summary>
        IGNORE_READONLY_FLAG = 0x00000100,
        /// <summary>
        /// Do not include file data already present in other WIMs.  This flag can be
        /// used to write a "delta" WIM after the WIM files on which the delta is to be
        /// based were referenced with wimlib_reference_resource_files() or
        /// wimlib_reference_resources().
        /// </summary>
        SKIP_EXTERNAL_WIMS = 0x00000200,
        /// <summary>
        /// For wimlib_write(), retain the WIM's GUID instead of generating a new one.
        ///
        /// wimlib_overwrite() sets this by default, since the WIM remains, logically,
        /// the same file.
        /// </summary>
        RETAIN_GUID = 0x00000800,
        /// <summary>
        /// Concatenate files and compress them together, rather than compress each file
        /// independently.  This is also known as creating a "solid archive".  This tends
        /// to produce a better compression ratio at the cost of much slower random
        /// access.
        ///
        /// WIM files created with this flag are only compatible with wimlib v1.6.0 or
        /// later, WIMGAPI Windows 8 or later, and DISM Windows 8.1 or later.  WIM files
        /// created with this flag use a different version number in their header (3584
        /// instead of 68864) and are also called "ESD files".
        ///
        /// Note that providing this flag does not affect the "append by default"
        /// behavior of wimlib_overwrite().  In other words, wimlib_overwrite() with just
        /// ::WIMLIB_WRITE_FLAG_SOLID can be used to append solid-compressed data to a
        /// WIM file that originally did not contain any solid-compressed data.  But if
        /// you instead want to rebuild and recompress an entire WIM file in solid mode,
        /// then also provide ::WIMLIB_WRITE_FLAG_REBUILD and
        /// ::WIMLIB_WRITE_FLAG_RECOMPRESS.
        ///
        /// Currently, new solid resources will, by default, be written using LZMS
        /// compression with 64 MiB (67108864 byte) chunks.  Use
        /// wimlib_set_output_pack_compression_type() and/or
        /// wimlib_set_output_pack_chunk_size() to change this.  This is independent of
        /// the WIM's main compression type and chunk size; you can have a WIM that
        /// nominally uses LZX compression and 32768 byte chunks but actually contains
        /// LZMS-compressed solid resources, for example.  However, if including solid
        /// resources, I suggest that you set the WIM's main compression type to LZMS as
        /// well, either by creating the WIM with
        /// ::wimlib_create_new_wim(::WIMLIB_COMPRESSION_TYPE_LZMS, ...) or by calling
        /// ::wimlib_set_output_compression_type(..., ::WIMLIB_COMPRESSION_TYPE_LZMS).
        ///
        /// This flag will be set by default when writing or overwriting a WIM file that
        /// either already contains solid resources, or has had solid resources exported
        /// into it and the WIM's main compression type is LZMS.
        /// </summary>
        SOLID = 0x00001000,
        /// <summary>
        /// Send ::WIMLIB_PROGRESS_MSG_DONE_WITH_FILE messages while writing the WIM
        /// file.  This is only needed in the unusual case that the library user needs to
        /// know exactly when wimlib has read each file for the last time.
        /// </summary>
        SEND_DONE_WITH_FILE_MESSAGES = 0x00002000,
        /// <summary>
        /// Do not consider content similarity when arranging file data for solid
        /// compression.  Providing this flag will typically worsen the compression
        /// ratio, so only provide this flag if you know what you are doing.
        /// </summary>
        NO_SOLID_SORT = 0x00004000,
        /// <summary>
        /// Since wimlib v1.8.3 and for wimlib_overwrite() only: <b>unsafely</b> compact
        /// the WIM file in-place, without appending.  Existing resources are shifted
        /// down to fill holes and new resources are appended as needed.  The WIM file is
        /// truncated to its final size, which may shrink the on-disk file.  <b>This
        /// operation cannot be safely interrupted.  If the operation is interrupted,
        /// then the WIM file will be corrupted, and it may be impossible (or at least
        /// very difficult) to recover any data from it.  Users of this flag are expected
        /// to know what they are doing and assume responsibility for any data corruption
        /// that may result.</b>
        ///
        /// If the WIM file cannot be compacted in-place because of its structure, its
        /// layout, or other requested write parameters, then wimlib_overwrite() fails
        /// with ::WIMLIB_ERR_COMPACTION_NOT_POSSIBLE, and the caller may wish to retry
        /// the operation without this flag.
        /// </summary>
        UNSAFE_COMPACT = 0x00008000,
    }

    [Flags]
    public enum WimLibExtractFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// Extract the image directly to an NTFS volume rather than a generic directory.
        /// This mode is only available if wimlib was compiled with libntfs-3g support;
        /// if not, ::WIMLIB_ERR_UNSUPPORTED will be returned.  In this mode, the
        /// extraction target will be interpreted as the path to an NTFS volume image (as
        /// a regular file or block device) rather than a directory.  It will be opened
        /// using libntfs-3g, and the image will be extracted to the NTFS filesystem's
        /// root directory.  Note: this flag cannot be used when wimlib_extract_image()
        /// is called with ::WIMLIB_ALL_IMAGES as the @p image, nor can it be used with
        /// wimlib_extract_paths() when passed multiple paths.
        /// </summary>
        NTFS = 0x00000001,
        /// <summary>
        /// UNIX-like systems only:  Extract UNIX-specific metadata captured with
        /// ::WIMLIB_ADD_FLAG_UNIX_DATA.
        /// </summary>
        UNIX_DATA = 0x00000020,
        /// <summary>
        /// Do not extract security descriptors.  This flag cannot be combined with
        /// ::WIMLIB_EXTRACT_FLAG_STRICT_ACLS.
        /// </summary>
        NO_ACLS = 0x00000040,
        /// <summary>
        /// Fail immediately if the full security descriptor of any file or directory
        /// cannot be set exactly as specified in the WIM image.  On Windows, the default
        /// behavior without this flag when wimlib does not have permission to set the
        /// correct security descriptor is to fall back to setting the security
        /// descriptor with the SACL omitted, then with the DACL omitted, then with the
        /// owner omitted, then not at all.  This flag cannot be combined with
        /// ::WIMLIB_EXTRACT_FLAG_NO_ACLS.
        /// </summary>
        STRICT_ACLS = 0x00000080,
        /// <summary>
        /// This is the extraction equivalent to ::WIMLIB_ADD_FLAG_RPFIX.  This forces
        /// reparse-point fixups on, so absolute symbolic links or junction points will
        /// be fixed to be absolute relative to the actual extraction root.  Reparse-
        /// point fixups are done by default for wimlib_extract_image() and
        /// wimlib_extract_image_from_pipe() if <c>WIM_HDR_FLAG_RP_FIX</c> is set in the
        /// WIM header.  This flag cannot be combined with ::WIMLIB_EXTRACT_FLAG_NORPFIX.
        /// </summary>
        RPFIX = 0x00000100,
        /// <summary>
        /// Force reparse-point fixups on extraction off, regardless of the state of the
        /// WIM_HDR_FLAG_RP_FIX flag in the WIM header.  This flag cannot be combined
        /// with ::WIMLIB_EXTRACT_FLAG_RPFIX.
        /// </summary>
        NORPFIX = 0x00000200,
        /// <summary>
        /// For wimlib_extract_paths() and wimlib_extract_pathlist() only:  Extract the
        /// paths, each of which must name a regular file, to standard output.
        /// </summary>
        TO_STDOUT = 0x00000400,
        /// <summary>
        /// Instead of ignoring files and directories with names that cannot be
        /// represented on the current platform (note: Windows has more restrictions on
        /// filenames than POSIX-compliant systems), try to replace characters or append
        /// junk to the names so that they can be extracted in some form.
        ///
        /// Note: this flag is unlikely to have any effect when extracting a WIM image
        /// that was captured on Windows.
        /// </summary>
        REPLACE_INVALID_FILENAMES = 0x00000800,
        /// <summary>
        /// On Windows, when there exist two or more files with the same case insensitive
        /// name but different case sensitive names, try to extract them all by appending
        /// junk to the end of them, rather than arbitrarily extracting only one.
        ///
        /// Note: this flag is unlikely to have any effect when extracting a WIM image
        /// that was captured on Windows.
        /// </summary>
        ALL_CASE_CONFLICTS = 0x00001000,
        /// <summary>
        /// Do not ignore failure to set timestamps on extracted files.  This flag
        /// currently only has an effect when extracting to a directory on UNIX-like
        /// systems.
        /// </summary>
        STRICT_TIMESTAMPS = 0x00002000,
        /// <summary>
        /// Do not ignore failure to set short names on extracted files.  This flag
        /// currently only has an effect on Windows.
        /// </summary>
        STRICT_SHORT_NAMES = 0x00004000,
        /// <summary>
        /// Do not ignore failure to extract symbolic links and junctions due to
        /// permissions problems.  This flag currently only has an effect on Windows.  By
        /// default, such failures are ignored since the default configuration of Windows
        /// only allows the Administrator to create symbolic links.
        /// </summary>
        STRICT_SYMLINKS = 0x00008000,
        /// <summary>
        /// For wimlib_extract_paths() and wimlib_extract_pathlist() only:  Treat the
        /// paths to extract as wildcard patterns ("globs") which may contain the
        /// wildcard characters @c ? and @c *.  The @c ? character matches any
        /// non-path-separator character, whereas the @c * character matches zero or more
        /// non-path-separator characters.  Consequently, each glob may match zero or
        /// more actual paths in the WIM image.
        ///
        /// By default, if a glob does not match any files, a warning but not an error
        /// will be issued.  This is the case even if the glob did not actually contain
        /// wildcard characters.  Use ::WIMLIB_EXTRACT_FLAG_STRICT_GLOB to get an error
        /// instead.
        /// </summary>
        GLOB_PATHS = 0x00040000,
        /// <summary>
        /// In combination with ::WIMLIB_EXTRACT_FLAG_GLOB_PATHS, causes an error
        /// (::WIMLIB_ERR_PATH_DOES_NOT_EXIST) rather than a warning to be issued when
        /// one of the provided globs did not match a file.
        /// </summary>
        STRICT_GLOB = 0x00080000,
        /// <summary>
        /// Do not extract Windows file attributes such as readonly, hidden, etc.
        ///
        /// This flag has an effect on Windows as well as in the NTFS-3G extraction mode.
        /// </summary>
        NO_ATTRIBUTES = 0x00100000,
        /// <summary>
        /// For wimlib_extract_paths() and wimlib_extract_pathlist() only:  Do not
        /// preserve the directory structure of the archive when extracting --- that is,
        /// place each extracted file or directory tree directly in the target directory.
        /// The target directory will still be created if it does not already exist.
        /// </summary>
        NO_PRESERVE_DIR_STRUCTURE = 0x00200000,
        /// <summary>
        /// Windows only: Extract files as "pointers" back to the WIM archive.
        ///
        /// The effects of this option are fairly complex.  See the documentation for the
        /// <b>--wimboot</b> option of <b>wimapply</b> for more information.
        /// </summary>
        WIMBOOT = 0x00400000,
        /// <summary>
        /// Since wimlib v1.8.2 and Windows-only: compress the extracted files using
        /// System Compression, when possible.  This only works on either Windows 10 or
        /// later, or on an older Windows to which Microsoft's wofadk.sys driver has been
        /// added.  Several different compression formats may be used with System
        /// Compression; this particular flag selects the XPRESS compression format with
        /// 4096 byte chunks.
        /// </summary>
        COMPACT_XPRESS4K = 0x01000000,
        /// <summary>
        /// Like ::WIMLIB_EXTRACT_FLAG_COMPACT_XPRESS4K, but use XPRESS compression with
        /// 8192 byte chunks.
        /// </summary>
        COMPACT_XPRESS8K = 0x02000000,
        /// <summary>
        /// Like ::WIMLIB_EXTRACT_FLAG_COMPACT_XPRESS4K, but use XPRESS compression with
        /// 16384 byte chunks.
        /// </summary>
        COMPACT_XPRESS16K = 0x04000000,
        /// <summary>
        /// Like ::WIMLIB_EXTRACT_FLAG_COMPACT_XPRESS4K, but use LZX compression with
        /// 32768 byte chunks.
        /// </summary>
        COMPACT_LZX = 0x08000000,
    }
    #endregion

    #region WimLibException
    public class WimLibException : Exception
    {
        public string ErrorMsg;
        public WimLibErrorCode ErrorCode;

        public WimLibException(WimLibErrorCode errorCode)
            : base($"Error Code {errorCode}" + Environment.NewLine + WimLibNative.GetErrorString(errorCode))
        {
            this.ErrorMsg = WimLibNative.GetErrorString(errorCode);
            this.ErrorCode = errorCode;
        }

        public static void CheckWimLibError(WimLibErrorCode ret)
        {
            if (ret != WimLibErrorCode.SUCCESS)
                throw new WimLibException(ret);
        }
    }
    #endregion
}

