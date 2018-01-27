/*
    Copyright (C) 2017-2018 Hajin Jang
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
*/

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

        #region Const
        public const int AllImages = -1;
        public const int DefaultThreads = 0;
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

        #region Static Methods
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
            if (!WimLibNative.Loaded)
                throw new InvalidOperationException(WimLibNative.InitFirstErrorMsg);

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
            if (!WimLibNative.Loaded)
                throw new InvalidOperationException(WimLibNative.InitFirstErrorMsg);

            WimLibErrorCode ret = WimLibNative.OpenWim(wimFile, openFlags, out IntPtr wimPtr);
            WimLibException.CheckWimLibError(ret);

            Wim wim = new Wim(wimPtr);

            if (callback != null)
                wim.RegisterCallback(callback, userData);

            return wim;
        }

        /// <summary>
        /// Create a ::WIMStruct which initially contains no images and is not backed by
        /// an on-disk file.
        /// </summary>
        /// <param name="ctype">
        /// The "output compression type" to assign to the ::WIMStruct.  This is the
        /// compression type that will be used if the ::WIMStruct is later persisted
        /// to an on-disk file using wimlib_write().
        /// 
        /// This choice is not necessarily final.  If desired, it can still be
        /// changed at any time before wimlib_write() is called, using
        /// wimlib_set_output_compression_type().  In addition, if you wish to use a
        /// non-default compression chunk size, then you will need to call
        /// wimlib_set_output_chunk_size().
        /// </param>
        /// <param name="wim_ret">
        /// On success, a pointer to the new ::WIMStruct is written to the memory
        /// location pointed to by this parameter.  This ::WIMStruct must be freed
        /// using using wimlib_free() when finished with it.
        /// </param>
        /// <returns>
        /// return 0 on success; a ::wimlib_error_code value on failure.
        ///
        /// @retval ::WIMLIB_ERR_INVALID_COMPRESSION_TYPE
        /// @p ctype was not a supported compression type.
        /// @retval ::WIMLIB_ERR_NOMEM
        /// Insufficient memory to allocate a new ::WIMStruct.
        /// </returns>
        public static Wim CreateNewWim(WimLibCompressionType compType)
        {
            if (!WimLibNative.Loaded)
                throw new InvalidOperationException(WimLibNative.InitFirstErrorMsg);

            WimLibErrorCode ret = WimLibNative.CreateNewWim(compType, out IntPtr wimPtr);
            WimLibException.CheckWimLibError(ret);

            return new Wim(wimPtr);
        }
        #endregion

        #region Instance Methods
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

        /// <summary>
        /// Add an image to a ::WIMStruct from an on-disk directory tree or NTFS volume.
        ///
        /// The directory tree or NTFS volume is scanned immediately to load the dentry
        /// tree into memory, and file metadata is read.  However, actual file data may
        /// not be read until the ::WIMStruct is persisted to disk using wimlib_write()
        /// or wimlib_overwrite().
        ///
        /// See the documentation for the @b wimlib-imagex program for more information
        /// about the "normal" capture mode versus the NTFS capture mode (entered by
        /// providing the flag ::WIMLIB_ADD_FLAG_NTFS).
        ///
        /// Note that no changes are committed to disk until wimlib_write() or
        /// wimlib_overwrite() is called.
        /// </summary>
        /// <param name="wim">
        /// Pointer to the ::WIMStruct to which to add the image.
        /// </param>
        /// <param name="source">
        /// A path to a directory or unmounted NTFS volume that will be captured as
        /// a WIM image.
        /// </param>
        /// <param name="name">
        /// Name to give the new image.  If @c NULL or empty, the new image is given
        /// no name.  If nonempty, it must specify a name that does not already
        /// exist in @p wim.
        /// </param>
        /// <param name="config_file">
        /// Path to capture configuration file, or @c NULL.  This file may specify,
        /// among other things, which files to exclude from capture.  See the
        /// documentation for <b>wimcapture</b> (<b>--config</b> option) for details
        /// of the file format.  If @c NULL, the default capture configuration will
        /// be used.  Ordinarily, the default capture configuration will result in
        /// no files being excluded from capture purely based on name; however, the
        /// ::WIMLIB_ADD_FLAG_WINCONFIG and ::WIMLIB_ADD_FLAG_WIMBOOT flags modify
        /// the default.
        /// </param>
        /// <param name="add_flags">
        /// Bitwise OR of flags prefixed with WIMLIB_ADD_FLAG.
        /// </param>
        /// <returns>
        /// 0 on success; a ::wimlib_error_code value on failure.
        /// </returns>
        /// <remarks>
        /// This function is implemented by calling wimlib_add_empty_image(), then
        /// calling wimlib_update_image() with a single "add" command, so any error code
        /// returned by wimlib_add_empty_image() may be returned, as well as any error
        /// codes returned by wimlib_update_image() other than ones documented as only
        /// being returned specifically by an update involving delete or rename commands.
        ///
        /// If a progress function is registered with @p wim, then it will receive the
        /// messages ::WIMLIB_PROGRESS_MSG_SCAN_BEGIN and ::WIMLIB_PROGRESS_MSG_SCAN_END.
        /// In addition, if ::WIMLIB_ADD_FLAG_VERBOSE is specified in @p add_flags, it
        /// will receive ::WIMLIB_PROGRESS_MSG_SCAN_DENTRY.
        /// </remarks>
        public void AddImage(string source, string name, string configFile, WimLibAddFlags addFlags)
        {
            WimLibErrorCode ret = WimLibNative.AddImage(Ptr, source, name, configFile, addFlags);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Persist a ::WIMStruct to a new on-disk WIM file.
        ///
        /// This brings in file data from any external locations, such as directory trees
        /// or NTFS volumes scanned with wimlib_add_image(), or other WIM files via
        /// wimlib_export_image(), and incorporates it into a new on-disk WIM file.
        ///
        /// By default, the new WIM file is written as stand-alone.  Using the
        /// ::WIMLIB_WRITE_FLAG_SKIP_EXTERNAL_WIMS flag, a "delta" WIM can be written
        /// instead.  However, this function cannot directly write a "split" WIM; use
        /// wimlib_split() for that.
        /// </summary>
        /// <param name="wim">
        /// Pointer to the ::WIMStruct being persisted.
        /// </param>
        /// <param name="path">
        /// The path to the on-disk file to write.
        /// </param>
        /// <param name="image">
        /// Normally, specify ::WIMLIB_ALL_IMAGES here.  This indicates that all
        /// images are to be included in the new on-disk WIM file.  If for some
        /// reason you only want to include a single image, specify the 1-based
        /// index of that image instead.
        /// </param>
        /// <param name="write_flags">
        /// Bitwise OR of flags prefixed with @c WIMLIB_WRITE_FLAG.
        /// </param>
        /// <param name="num_threads">
        /// The number of threads to use for compressing data, or 0 to have the
        /// library automatically choose an appropriate number.
        /// </param>
        /// <returns>
        /// 0 on success; a ::wimlib_error_code value on failure.
        /// 
        /// @retval ::WIMLIB_ERR_CONCURRENT_MODIFICATION_DETECTED
        /// A file that had previously been scanned for inclusion in the WIM was
        /// concurrently modified.
        /// @retval ::WIMLIB_ERR_INVALID_IMAGE
        /// @p image did not exist in @p wim.
        /// @retval ::WIMLIB_ERR_INVALID_RESOURCE_HASH
        /// A file, stored in another WIM, which needed to be written was corrupt.
        /// @retval ::WIMLIB_ERR_INVALID_PARAM
        /// @p path was not a nonempty string, or invalid flags were passed.
        /// @retval ::WIMLIB_ERR_OPEN
        /// Failed to open the output WIM file for writing, or failed to open a file
        /// whose data needed to be included in the WIM.
        /// @retval ::WIMLIB_ERR_READ
        /// Failed to read data that needed to be included in the WIM.
        /// @retval ::WIMLIB_ERR_RESOURCE_NOT_FOUND
        /// A file data blob that needed to be written could not be found in the
        /// blob lookup table of @p wim.  See @ref G_nonstandalone_wims.
        /// @retval ::WIMLIB_ERR_WRITE
        /// An error occurred when trying to write data to the new WIM file.
        /// </returns>
        /// <remarks>
        /// This function can additionally return ::WIMLIB_ERR_DECOMPRESSION,
        /// ::WIMLIB_ERR_INVALID_METADATA_RESOURCE, ::WIMLIB_ERR_METADATA_NOT_FOUND,
        /// ::WIMLIB_ERR_READ, or ::WIMLIB_ERR_UNEXPECTED_END_OF_FILE, all of which
        /// indicate failure (for different reasons) to read the data from a WIM file.
        ///
        /// If a progress function is registered with @p wim, then it will receive the
        /// messages ::WIMLIB_PROGRESS_MSG_WRITE_STREAMS,
        /// ::WIMLIB_PROGRESS_MSG_WRITE_METADATA_BEGIN, and
        /// ::WIMLIB_PROGRESS_MSG_WRITE_METADATA_END.
        /// </remarks>
        public void Write(string path, int image, WimLibWriteFlags writeFlags, uint numThreads)
        {
            WimLibErrorCode ret = WimLibNative.Write(Ptr, path, image, writeFlags, numThreads);
            WimLibException.CheckWimLibError(ret);
        }
        #endregion
    }
    #endregion
}
