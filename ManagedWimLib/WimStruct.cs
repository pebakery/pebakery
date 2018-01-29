/*
    Licensed under LGPLv3

    Derived from wimlib's original header files
    Copyright (C) 2012, 2013, 2014 Eric Biggers

    C# Wrapper written by Hajin Jang
    Copyright (C) 2017-2018 Hajin Jang

    This file is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by the Free
    Software Foundation; either version 3 of the License, or (at your option) any
    later version.

    This file is distributed in the hope that it will be useful, but WITHOUT
    ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
    FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more
    details.

    You should have received a copy of the GNU Lesser General Public License
    along with this file; if not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
        /// Get basic information about a WIM file.
        /// </summary>
        /// <returns>Return 0</returns>
        public WimInfo GetWimInfo()
        {
            // This function always return 0, so no need to check exception
            IntPtr infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WimInfo)));
            WimLibNative.GetWimInfo(Ptr, infoPtr);
            WimInfo info = (WimInfo) Marshal.PtrToStructure(infoPtr, typeof(WimInfo));
            Marshal.FreeHGlobal(infoPtr);

            return info;
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

        /// <summary>
        /// Commit a ::WIMStruct to disk, updating its backing file.
        ///
        /// There are several alternative ways in which changes may be committed:
        ///
        ///   1. Full rebuild: write the updated WIM to a temporary file, then rename the
        /// temporary file to the original.
        ///   2. Appending: append updates to the new original WIM file, then overwrite
        /// its header such that those changes become visible to new readers.
        ///   3. Compaction: normally should not be used; see
        /// ::WIMLIB_WRITE_FLAG_UNSAFE_COMPACT for details.
        ///
        /// Append mode is often much faster than a full rebuild, but it wastes some
        /// amount of space due to leaving "holes" in the WIM file.  Because of the
        /// greater efficiency, wimlib_overwrite() normally defaults to append mode.
        /// However, ::WIMLIB_WRITE_FLAG_REBUILD can be used to explicitly request a full
        /// rebuild.  In addition, if wimlib_delete_image() has been used on the
        /// ::WIMStruct, then the default mode switches to rebuild mode, and
        /// ::WIMLIB_WRITE_FLAG_SOFT_DELETE can be used to explicitly request append
        /// mode.
        ///
        /// If this function completes successfully, then no more functions can be called
        /// on the ::WIMStruct other than wimlib_free().  If you need to continue using
        /// the WIM file, you must use wimlib_open_wim() to open a new ::WIMStruct for
        /// it.
        /// </summary>
        /// <param name="wim">Pointer to a ::WIMStruct to commit to its backing file.</param>
        /// <param name="write_flags">Bitwise OR of relevant flags prefixed with WIMLIB_WRITE_FLAG.</param>
        /// <param name="num_threads">
        /// The number of threads to use for compressing data, or 0 to have the
        /// library automatically choose an appropriate number.
        /// </param>
        /// <returns>
        /// return 0 on success; a ::wimlib_error_code value on failure.  This function
        /// may return most error codes returned by wimlib_write() as well as the
        /// following error codes:
        ///
        /// @retval ::WIMLIB_ERR_ALREADY_LOCKED
        /// Another process is currently modifying the WIM file.
        /// @retval ::WIMLIB_ERR_NO_FILENAME
        /// @p wim is not backed by an on-disk file.  In other words, it is a
        /// ::WIMStruct created by wimlib_create_new_wim() rather than
        /// wimlib_open_wim().
        /// @retval ::WIMLIB_ERR_RENAME
        /// The temporary file to which the WIM was written could not be renamed to
        /// the original file.
        /// @retval ::WIMLIB_ERR_WIM_IS_READONLY
        /// The WIM file is considered read-only because of any of the reasons
        /// mentioned in the documentation for the ::WIMLIB_OPEN_FLAG_WRITE_ACCESS
        /// flag.
        ///
        /// If a progress function is registered with @p wim, then it will receive the
        /// messages ::WIMLIB_PROGRESS_MSG_WRITE_STREAMS,
        /// ::WIMLIB_PROGRESS_MSG_WRITE_METADATA_BEGIN, and
        /// ::WIMLIB_PROGRESS_MSG_WRITE_METADATA_END.
        /// </returns>
        public void OverWrite(WimLibWriteFlags writeFlags, uint numThreads)
        {
            WimLibErrorCode ret = WimLibNative.OverWrite(Ptr, writeFlags, numThreads);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Since wimlib v1.8.3: add, modify, or remove a per-image property from the
        /// WIM's XML document.  This is an alternative to wimlib_set_image_name(),
        /// wimlib_set_image_descripton(), and wimlib_set_image_flags() which allows
        /// manipulating any simple string property.
        /// </summary>
        /// <param name="wim">Pointer to the ::WIMStruct for the WIM.</param>
        /// <param name="image">The 1-based index of the image for which to set the property.</param>
        /// <param name="property_name">
        /// The name of the image property in the same format documented for wimlib_get_image_property().
        /// 
        /// Note: if creating a new element using a bracketed index such as
        /// "WINDOWS/LANGUAGES/LANGUAGE[2]", the highest index that can be specified
        /// is one greater than the number of existing elements with that same name,
        /// excluding the index.  That means that if you are adding a list of new
        /// elements, they must be added sequentially from the first index (1) to
        /// the last index (n).
        /// </param>
        /// <param name="property_value">
        /// If not NULL and not empty, the property is set to this value.
        /// Otherwise, the property is removed from the XML document.
        /// </param>
        /// <returns>
        /// return 0 on success; a ::wimlib_error_code value on failure.
        /// 
        /// @retval ::WIMLIB_ERR_IMAGE_NAME_COLLISION
        /// The user requested to set the image name (the <tt>NAME</tt> property),
        /// but another image in the WIM already had the requested name.
        /// @retval ::WIMLIB_ERR_INVALID_IMAGE
        /// @p image does not exist in @p wim.
        /// @retval ::WIMLIB_ERR_INVALID_PARAM
        /// @p property_name has an unsupported format, or @p property_name included
        /// a bracketed index that was too high.
        /// </returns>
        public void SetImageProperty(int image, string propertyName, string propertyValue)
        {
            WimLibErrorCode ret = WimLibNative.SetImageProperty(Ptr, image, propertyName, propertyValue);
            WimLibException.CheckWimLibError(ret);
        }

        public void SetImageDescription(int image, string description)
        {
            SetImageProperty(image, "DESCRIPTION", description);
        }

        public void SetImageFlags(int image, string flags)
        {
            SetImageProperty(image, "FLAGS", flags);
        }

        public void SetImageName(int image, string name)
        {
            SetImageProperty(image, "NAME", name);
        }

        /// <summary>
        /// Determine if an image name is already used by some image in the WIM.
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <returns>
        /// @c true if there is already an image in @p wim named @p name; @c false
        /// if there is no image named @p name in @p wim.If @p name is @c NULL or
        /// the empty string, then @c false is returned.
        /// </returns>
        public bool IsImageNameInUse(string name)
        {
            return WimLibNative.IsImageNameInUse(Ptr, name);
        }

        /// <summary>
        /// Declare that a newly added image is mostly the same as a prior image, but
        /// captured at a later point in time, possibly with some modifications in the
        /// intervening time.  This is designed to be used in incremental backups of the
        /// same filesystem or directory tree.
        ///
        /// This function compares the metadata of the directory tree of the newly added
        /// image against that of the old image.  Any files that are present in both the
        /// newly added image and the old image and have timestamps that indicate they
        /// haven't been modified are deemed not to have been modified and have their
        /// checksums copied from the old image.  Because of this and because WIM uses
        /// single-instance streams, such files need not be read from the filesystem when
        /// the WIM is being written or overwritten.  Note that these unchanged files
        /// will still be "archived" and will be logically present in the new image; the
        /// optimization is that they don't need to actually be read from the filesystem
        /// because the WIM already contains them.
        ///
        /// This function is provided to optimize incremental backups.  The resulting WIM
        /// file will still be the same regardless of whether this function is called.
        /// (This is, however, assuming that timestamps have not been manipulated or
        /// unmaintained as to trick this function into thinking a file has not been
        /// modified when really it has.  To partly guard against such cases, other
        /// metadata such as file sizes will be checked as well.)
        ///
        /// This function must be called after adding the new image (e.g. with
        /// wimlib_add_image()), but before writing the updated WIM file (e.g. with
        /// wimlib_overwrite()).
        /// </summary>
        /// <param name="new_image">The 1-based index in @p wim of the newly added image.</param>
        /// <param name="template_image">The 1-based index in @p template_wim of the template image.</param>
        /// <returns>return 0 on success; a ::wimlib_error_code value on failure.
        /// 
        /// @retval ::WIMLIB_ERR_INVALID_IMAGE
        /// @p new_image does not exist in @p wim or @p template_image does not
        /// exist in @p template_wim.
        /// @retval ::WIMLIB_ERR_METADATA_NOT_FOUND
        /// At least one of @p wim and @p template_wim does not contain image
        /// metadata; for example, one of them represents a non-first part of a
        /// split WIM.
        /// @retval ::WIMLIB_ERR_INVALID_PARAM
        /// Identical values were provided for the template and new image; or @p
        /// new_image specified an image that had not been modified since opening
        /// the WIM.
        ///
        /// This function can additionally return ::WIMLIB_ERR_DECOMPRESSION,
        /// ::WIMLIB_ERR_INVALID_METADATA_RESOURCE, ::WIMLIB_ERR_METADATA_NOT_FOUND,
        /// ::WIMLIB_ERR_READ, or ::WIMLIB_ERR_UNEXPECTED_END_OF_FILE, all of which
        /// indicate failure (for different reasons) to read the metadata resource for
        /// the template image.
        /// </returns>
        public void ReferenceTemplateImage(int newImage, int templateImage)
        {
            WimLibErrorCode ret = WimLibNative.ReferenceTemplateImage(Ptr, newImage, Ptr, templateImage, 0);
            WimLibException.CheckWimLibError(ret);
        }
        
        /// <summary>
        /// Declare that a newly added image is mostly the same as a prior image, but
        /// captured at a later point in time, possibly with some modifications in the
        /// intervening time.  This is designed to be used in incremental backups of the
        /// same filesystem or directory tree.
        ///
        /// This function compares the metadata of the directory tree of the newly added
        /// image against that of the old image.  Any files that are present in both the
        /// newly added image and the old image and have timestamps that indicate they
        /// haven't been modified are deemed not to have been modified and have their
        /// checksums copied from the old image.  Because of this and because WIM uses
        /// single-instance streams, such files need not be read from the filesystem when
        /// the WIM is being written or overwritten.  Note that these unchanged files
        /// will still be "archived" and will be logically present in the new image; the
        /// optimization is that they don't need to actually be read from the filesystem
        /// because the WIM already contains them.
        ///
        /// This function is provided to optimize incremental backups.  The resulting WIM
        /// file will still be the same regardless of whether this function is called.
        /// (This is, however, assuming that timestamps have not been manipulated or
        /// unmaintained as to trick this function into thinking a file has not been
        /// modified when really it has.  To partly guard against such cases, other
        /// metadata such as file sizes will be checked as well.)
        ///
        /// This function must be called after adding the new image (e.g. with
        /// wimlib_add_image()), but before writing the updated WIM file (e.g. with
        /// wimlib_overwrite()).
        /// </summary>
        /// <param name="new_image">The 1-based index in @p wim of the newly added image.</param>
        /// <param name="template_wim">Pointer to the ::WIMStruct containing the template image.  This can be, but does not have to be, the same ::WIMStruct as @p wim.</param>
        /// <param name="template_image">The 1-based index in @p template_wim of the template image.</param>
        /// <returns>return 0 on success; a ::wimlib_error_code value on failure.
        /// 
        /// @retval ::WIMLIB_ERR_INVALID_IMAGE
        /// @p new_image does not exist in @p wim or @p template_image does not
        /// exist in @p template_wim.
        /// @retval ::WIMLIB_ERR_METADATA_NOT_FOUND
        /// At least one of @p wim and @p template_wim does not contain image
        /// metadata; for example, one of them represents a non-first part of a
        /// split WIM.
        /// @retval ::WIMLIB_ERR_INVALID_PARAM
        /// Identical values were provided for the template and new image; or @p
        /// new_image specified an image that had not been modified since opening
        /// the WIM.
        ///
        /// This function can additionally return ::WIMLIB_ERR_DECOMPRESSION,
        /// ::WIMLIB_ERR_INVALID_METADATA_RESOURCE, ::WIMLIB_ERR_METADATA_NOT_FOUND,
        /// ::WIMLIB_ERR_READ, or ::WIMLIB_ERR_UNEXPECTED_END_OF_FILE, all of which
        /// indicate failure (for different reasons) to read the metadata resource for
        /// the template image.
        /// </returns>
        public void ReferenceTemplateImage(int newImage, Wim template, int templateImage)
        {
            WimLibErrorCode ret = WimLibNative.ReferenceTemplateImage(Ptr, newImage, template.Ptr, templateImage, 0);
            WimLibException.CheckWimLibError(ret);
        }
        #endregion
    }
    #endregion
}
