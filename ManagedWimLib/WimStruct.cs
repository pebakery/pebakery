using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedWimLib
{
    #region WimStruct
    public class Wim : IDisposable
    { // Wrapper of WimStruct and API
        #region Field
        public IntPtr Ptr = IntPtr.Zero;
        public ManagedWimLibCallback ManagedCallback;
        #endregion

        #region Constructor
        public Wim(IntPtr ptr)
        {
            if (!WimLibNative.Loaded)
                throw new InvalidOperationException(WimLibNative.InitFirstErrorMsg);

            Ptr = ptr;
        }
        #endregion

        #region Disposable Pattern
        ~Wim()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Ptr != IntPtr.Zero)
                {
                    RegisterCallback(null);
                    WimLibNative.Free(Ptr);
                    Ptr = IntPtr.Zero;
                }
            }
        }
        #endregion

        #region OpenWim
        /// <summary>
        /// Open a WIM file and create a ::WIMStruct for it.
        /// </summary>
        /// <param name="wimFile">The path to the WIM file to open.</param>
        /// <param name="openFlags">Bitwise OR of flags prefixed with WIMLIB_OPEN_FLAG.</param>
        /// <returns>
        /// On success, a pointer to a new ::WIMStruct backed by the specified
        ///	on-disk WIM file is written to the memory location pointed to by this
        ///	parameter.This ::WIMStruct must be freed using using wimlib_free()
        ///	when finished with it.
        ///	</returns>
        ///	<exception cref="WimLibException">wimlib does not return WIMLIB_ERR_SUCCESS.</exception>
        public static Wim OpenWim(string wimFile, WimLibOpenFlags openFlags)
        {
            WimLibErrorCode ret = WimLibNative.OpenWim(wimFile, openFlags, out IntPtr wimPtr);
            if (ret != WimLibErrorCode.SUCCESS)
                throw new WimLibException(ret);

            return new Wim(wimPtr);
        }

        /// <summary>
        /// Same as wimlib_open_wim(), but allows specifying a progress function and\
        /// progress context.  If successful, the progress function will be registered in
        /// the newly open ::WIMStruct, as if by an automatic call to
        /// wimlib_register_progress_function().  In addition, if
        /// ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY is specified in @p open_flags, then the
        /// progress function will receive ::WIMLIB_PROGRESS_MSG_VERIFY_INTEGRITY
        /// messages while checking the WIM file's integrity.
        /// </summary>
        /// <param name="wimFile">The path to the WIM file to open.</param>
        /// <param name="openFlags">Bitwise OR of flags prefixed with WIMLIB_OPEN_FLAG.</param>
        /// <returns>
        /// On success, a pointer to a new ::WIMStruct backed by the specified
        ///	on-disk WIM file is written to the memory location pointed to by this
        ///	parameter.This ::WIMStruct must be freed using using wimlib_free()
        ///	when finished with it.
        ///	</returns>
        ///	<exception cref="WimLibException">wimlib does not return WIMLIB_ERR_SUCCESS.</exception>
        public static Wim OpenWim(string wimFile, WimLibOpenFlags openFlags, WimLibCallback callback = null, object userData = null)
        {
            WimLibErrorCode ret = WimLibNative.OpenWim(wimFile, openFlags, out IntPtr wimPtr);
            WimLibException.CheckWimLibError(ret);

            Wim wim = new Wim(wimPtr);

            if (callback != null)
                wim.RegisterCallback(callback, userData);

            return wim;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Register a progress function with a ::WIMStruct.
        /// </summary>
        /// <param name="wim">The ::WIMStruct for which to register the progress function.</param>
        /// <param name="callback">
        /// Pointer to the progress function to register.  If the WIM already has a
        /// progress function registered, it will be replaced with this one.  If @p
        /// NULL, the current progress function (if any) will be unregistered.
        /// </param>
        /// <param name="userData">
        /// The value which will be passed as the third argument to calls to @p
        /// progfunc.
        /// </param>
        public void RegisterCallback(WimLibCallback callback, object userData = null)
        {
            if (callback != null)
            { // RegisterCallback
                ManagedCallback = new ManagedWimLibCallback(callback, userData);
                WimLibNative.RegisterProgressFunction(Ptr, ManagedCallback.NativeFunc, IntPtr.Zero);

                //ManagedCallback = new ManagedWimLibCallback(callback, userData);
                //WimLibNative.RegisterProgressFunction(Ptr, ManagedCallback.DummyCallback, IntPtr.Zero);
            }
            else
            { // Delete callback
                ManagedCallback = null;
                WimLibNative.RegisterProgressFunction(Ptr, null, IntPtr.Zero);
            }
        }

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
        public void ExtractImage(int image, string target, WimLibExtractFlags extractFlags)
        {
            WimLibErrorCode ret = WimLibNative.ExtractImage(Ptr, image, target, extractFlags);
            WimLibException.CheckWimLibError(ret);
        }
        #endregion
    }
    #endregion
}
