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
        public IntPtr Ptr { get; private set; }
        private ManagedWimLibCallback managedCallback;
        #endregion

        #region Const
        public const int AllImages = -1;
        public const int DefaultThreads = 0;
        #endregion

        #region Constructor (private)
        private Wim(IntPtr ptr)
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
        /// Open a WIM file and create a instance of Wim class for it.
        /// </summary>
        /// <param name="wimFile">The path to the WIM file to open.</param>
        /// <param name="openFlags">Bitwise OR of flags prefixed with WIMLIB_OPEN_FLAG.</param>
        /// <returns>
        /// On success, a new instance of Wim class backed by the specified
        ///	on-disk WIM file is returned. This instance must be disposed 
        ///	when finished with it.
        ///	</returns>
        ///	<exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
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
        /// Same as OpenWim(), but allows specifying a progress function and progress context.  
        /// </summary>
        /// <remarks>
        /// If successful, the progress function will be registered in
        /// the newly open ::WIMStruct, as if by an automatic call to
        /// wimlib_register_progress_function().  In addition, if
        /// ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY is specified in @p open_flags, then the
        /// progress function will receive ::WIMLIB_PROGRESS_MSG_VERIFY_INTEGRITY
        /// messages while checking the WIM file's integrity.
        /// </remarks>
        /// <param name="wimFile">The path to the WIM file to open.</param>
        /// <param name="openFlags">Bitwise OR of flags prefixed with WIMLIB_OPEN_FLAG.</param>
        /// <returns>
        /// On success, a new instance of Wim class backed by the specified
        ///	on-disk WIM file is returned. This instance must be disposed 
        ///	when finished with it.
        ///	</returns>
        ///	<exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
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
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
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
        #region RegisterCallback
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
                managedCallback = new ManagedWimLibCallback(callback, userData);
                WimLibNative.RegisterProgressFunction(Ptr, managedCallback.NativeFunc, IntPtr.Zero);
            }
            else
            { // Delete callback
                managedCallback = null;
                WimLibNative.RegisterProgressFunction(Ptr, null, IntPtr.Zero);
            }
        }
        #endregion

        #region GetWimInfo
        /// <summary>
        /// Get basic information about a WIM file.
        /// </summary>
        /// <returns>Return 0</returns>
        public WimInfo GetWimInfo()
        {
            // This function always return 0, so no need to check exception
            IntPtr infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WimInfo)));
            try
            {
                WimLibNative.GetWimInfo(Ptr, infoPtr);
                return (WimInfo)Marshal.PtrToStructure(infoPtr, typeof(WimInfo));
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }
        }
        #endregion

        #region ExtractImage, ExtractPath, ExtractPaths, ExtractPathList
        /// <summary>
        /// Extract an image, or all images, from a ::WIMStruct.
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
        /// <remarks>
        /// The exact behavior of how wimlib extracts files from a WIM image is
        /// controllable by the @p extract_flags parameter, but there also are
        /// differences depending on the platform (UNIX-like vs Windows).  See the
        /// documentation for <b>wimapply</b> for more information, including about the
        /// NTFS-3G extraction mode.
        /// </remarks>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void ExtractImage(int image, string target, WimLibExtractFlags extractFlags)
        {
            WimLibErrorCode ret = WimLibNative.ExtractImage(Ptr, image, target, extractFlags);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Extract zero or more paths (files or directory trees) from the specified WIM image.
        /// </summary>
        /// <remarks>
        /// By default, each path will be extracted to a corresponding subdirectory of
        /// the target based on its location in the image.  For example, if one of the
        /// paths to extract is <c>/Windows/explorer.exe</c> and the target is
        /// <c>outdir</c>, the file will be extracted to
        /// <c>outdir/Windows/explorer.exe</c>.  This behavior can be changed by
        /// providing the flag ::WIMLIB_EXTRACT_FLAG_NO_PRESERVE_DIR_STRUCTURE, which
        /// will cause each file or directory tree to be placed directly in the target
        /// directory --- so the same example would extract <c>/Windows/explorer.exe</c>
        /// to <c>outdir/explorer.exe</c>.
        ///
        /// With globbing turned off (the default), paths are always checked for
        /// existence strictly; that is, if any path to extract does not exist in the
        /// image, then nothing is extracted and the function fails with
        /// ::WIMLIB_ERR_PATH_DOES_NOT_EXIST.  But with globbing turned on
        /// (::WIMLIB_EXTRACT_FLAG_GLOB_PATHS specified), globs are by default permitted
        /// to match no files, and there is a flag (::WIMLIB_EXTRACT_FLAG_STRICT_GLOB) to
        /// enable the strict behavior if desired.
        ///
        /// Symbolic links are not dereferenced when paths in the image are interpreted.
        /// </remarks>
        /// <param name="image">
        /// The 1-based index of the WIM image from which to extract the paths.
        /// </param>
        /// <param name="target">
        /// Directory to which to extract the paths.
        /// </param>
        /// <param name="paths">
        /// Array of paths to extract.
        /// Each element must be the absolute path to a file or directory within the image.
        /// Path separators may be either forwards or backwards slashes, and leading path separators are optional.
        /// The paths will be interpreted either case-sensitively (UNIX default) or case-insensitively (Windows default);
        /// however, the case sensitivity can be configured explicitly at library initialization time by passing an
        /// appropriate flag to AssemblyInit().
        /// </param>
        /// <param name="num_paths">
        /// Number of paths specified in paths.
        /// </param>
        /// <param name="extract_flags">
        /// Bitwise OR of flags prefixed with WIMLIB_EXTRACT_FLAG.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void ExtractPaths(int image, string target, IEnumerable<string> paths, WimLibExtractFlags extractFlags)
        {
            WimLibErrorCode ret = WimLibNative.ExtractPaths(Ptr, image, target, paths.ToArray(), new IntPtr(paths.Count()), extractFlags);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Version of ExtractPaths() extracting only one file.
        /// </summary>
        /// <remarks>
        /// By default, each path will be extracted to a corresponding subdirectory of
        /// the target based on its location in the image.  For example, if one of the
        /// paths to extract is <c>/Windows/explorer.exe</c> and the target is
        /// <c>outdir</c>, the file will be extracted to
        /// <c>outdir/Windows/explorer.exe</c>.  This behavior can be changed by
        /// providing the flag ::WIMLIB_EXTRACT_FLAG_NO_PRESERVE_DIR_STRUCTURE, which
        /// will cause each file or directory tree to be placed directly in the target
        /// directory --- so the same example would extract <c>/Windows/explorer.exe</c>
        /// to <c>outdir/explorer.exe</c>.
        ///
        /// With globbing turned off (the default), paths are always checked for
        /// existence strictly; that is, if any path to extract does not exist in the
        /// image, then nothing is extracted and the function fails with
        /// ::WIMLIB_ERR_PATH_DOES_NOT_EXIST.  But with globbing turned on
        /// (::WIMLIB_EXTRACT_FLAG_GLOB_PATHS specified), globs are by default permitted
        /// to match no files, and there is a flag (::WIMLIB_EXTRACT_FLAG_STRICT_GLOB) to
        /// enable the strict behavior if desired.
        ///
        /// Symbolic links are not dereferenced when paths in the image are interpreted.
        /// </remarks>
        /// <param name="image">
        /// The 1-based index of the WIM image from which to extract the paths.
        /// </param>
        /// <param name="target">
        /// Directory to which to extract the paths.
        /// </param>
        /// <param name="paths">
        /// path to extract, must be the absolute path to a file or directory within the image.
        /// Path separators may be either forwards or backwards slashes, and leading path separators are optional.
        /// The paths will be interpreted either case-sensitively (UNIX default) or case-insensitively (Windows default);
        /// however, the case sensitivity can be configured explicitly at library initialization time by passing an
        /// appropriate flag to AssemblyInit().
        /// </param>
        /// <param name="extract_flags">
        /// Bitwise OR of flags prefixed with WIMLIB_EXTRACT_FLAG.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void ExtractPath(int image, string target, string path, WimLibExtractFlags extractFlags)
        {
            WimLibErrorCode ret = WimLibNative.ExtractPaths(Ptr, image, target, new string[1] { path }, new IntPtr(1), extractFlags);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Similar to ExtractPaths(), but the paths to extract from the WIM
        /// image are specified in the ASCII, UTF-8, or UTF-16LE text file named by
        /// path_list_file which itself contains the list of paths to use, one per line.
        /// </summary>
        /// <remarks>
        /// Leading and trailing whitespace is ignored.  Empty lines and lines beginning
        /// with the ';' or '#' characters are ignored.  No quotes are needed, as paths
        /// are otherwise delimited by the newline character.  However, quotes will be
        /// stripped if present.
        /// </remarks>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void ExtractPathList(int image, string target, string pathListFile, WimLibExtractFlags extractFlags)
        {
            WimLibErrorCode ret = WimLibNative.ExtractPathList(Ptr, image, target, pathListFile, extractFlags);
            WimLibException.CheckWimLibError(ret);
        }
        #endregion

        #region AddImage
        /// <summary>
        /// Add an image to a ::WIMStruct from an on-disk directory tree or NTFS volume.
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
        /// <remarks>
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
        /// </remarks>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void AddImage(string source, string name, string configFile, WimLibAddFlags addFlags)
        {
            WimLibErrorCode ret = WimLibNative.AddImage(Ptr, source, name, configFile, addFlags);
            WimLibException.CheckWimLibError(ret);
        }
        #endregion

        #region UpdateImage
        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="cmds"></param>
        /// <param name="updateFlags"></param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void UpdateImage(int image, UpdateCommand cmd, WimLibUpdateFlags updateFlags)
        {
            WimLibErrorCode ret;
            switch (cmd.Op)
            {
                case WimLibUpdateOp.ADD:
                    ret = WimLibNative.UpdateImageAdd(Ptr, image, new UpdateAddCommand[] { cmd.AddCmd }, new IntPtr(1), updateFlags);
                    break;
                case WimLibUpdateOp.DELETE:
                    ret = WimLibNative.UpdateImageDelete(Ptr, image, new UpdateDeleteCommand[] { cmd.DeleteCmd }, new IntPtr(1), updateFlags);
                    break;
                case WimLibUpdateOp.RENAME:
                    ret = WimLibNative.UpdateImageRename(Ptr, image, new UpdateRenameCommand[] { cmd.RenameCmd }, new IntPtr(1), updateFlags);
                    break;
                default:
                    throw new ArgumentException("cmd");
            }
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="cmds"></param>
        /// <param name="updateFlags"></param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void UpdateImage(int image, IEnumerable<UpdateCommand> cmds, WimLibUpdateFlags updateFlags)
        {
            foreach (UpdateCommand cmd in cmds)
            {
                UpdateImage(image, cmd, updateFlags);
            }
        }
        #endregion

        #region SetOutputCompressionType, SetOutputPackCompressionType
        /// <summary>
        /// Set a ::WIMStruct's output compression type.  This is the compression type
        /// that will be used for writing non-solid resources in subsequent calls to
        /// wimlib_write() or wimlib_overwrite().
        /// </summary>
        /// <param name="compType">
        /// The compression type to set.  If this compression type is incompatible
        /// with the current output chunk size, then the output chunk size will be
        /// reset to the default for the new compression type.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void SetOutputCompressionType(WimLibCompressionType compType)
        {
            WimLibErrorCode ret = WimLibNative.SetOutputCompressionType(Ptr, compType);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Similar to wimlib_set_output_compression_type(), but set the compression type
        /// for writing solid resources.  This cannot be ::WIMLIB_COMPRESSION_TYPE_NONE.
        /// </summary>
        /// <param name="compType">
        /// The compression type to set.  If this compression type is incompatible
        /// with the current output chunk size, then the output chunk size will be
        /// reset to the default for the new compression type.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void SetOutputPackCompressionType(WimLibCompressionType compType)
        {
            WimLibErrorCode ret = WimLibNative.SetOutputPackCompressionType(Ptr, compType);
            WimLibException.CheckWimLibError(ret);
        }
        #endregion

        #region Write, Overwrite
        /// <summary>
        /// Persist a ::WIMStruct to a new on-disk WIM file.
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
        /// <remarks>
        /// This brings in file data from any external locations, such as directory trees
        /// or NTFS volumes scanned with wimlib_add_image(), or other WIM files via
        /// wimlib_export_image(), and incorporates it into a new on-disk WIM file.
        ///
        /// By default, the new WIM file is written as stand-alone.  Using the
        /// ::WIMLIB_WRITE_FLAG_SKIP_EXTERNAL_WIMS flag, a "delta" WIM can be written
        /// instead.  However, this function cannot directly write a "split" WIM; use
        /// wimlib_split() for that.
        /// </remarks>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void Write(string path, int image, WimLibWriteFlags writeFlags, uint numThreads)
        {
            WimLibErrorCode ret = WimLibNative.Write(Ptr, path, image, writeFlags, numThreads);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Commit a ::WIMStruct to disk, updating its backing file.
        /// </summary>
        /// <param name="wim">Pointer to a ::WIMStruct to commit to its backing file.</param>
        /// <param name="write_flags">Bitwise OR of relevant flags prefixed with WIMLIB_WRITE_FLAG.</param>
        /// <param name="num_threads">
        /// The number of threads to use for compressing data, or 0 to have the
        /// library automatically choose an appropriate number.
        /// </param>
        /// <remarks>
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
        /// </remarks>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void Overwrite(WimLibWriteFlags writeFlags, uint numThreads)
        {
            WimLibErrorCode ret = WimLibNative.Overwrite(Ptr, writeFlags, numThreads);
            WimLibException.CheckWimLibError(ret);
        }
        #endregion

        #region SetImageProperty, SetImageName, SetImageDescription, SetImageFlags
        /// <summary>
        /// Since wimlib v1.8.3: add, modify, or remove a per-image property from the
        /// WIM's XML document.
        /// </summary>
        /// <remarks>
        /// This is an alternative to wimlib_set_image_name(),
        /// wimlib_set_image_descripton(), and wimlib_set_image_flags() which allows
        /// manipulating any simple string property.
        /// </remarks>
        /// <param name="wim">Pointer to the ::WIMStruct for the WIM.</param>
        /// <param name="image">The 1-based index of the image for which to set the property.</param>
        /// <param name="property_name">
        /// The name of the image property in the same format documented for wimlib_get_image_property().
        /// </param>
        /// <param name="property_value">
        /// If not NULL and not empty, the property is set to this value.
        /// Otherwise, the property is removed from the XML document.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void SetImageProperty(int image, string propertyName, string propertyValue)
        {
            WimLibErrorCode ret = WimLibNative.SetImageProperty(Ptr, image, propertyName, propertyValue);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Change the description of a WIM image.
        /// Equivalent to SetImageProperty(image, "DESCRIPTION", description)
        /// </summary>
        /// <param name="image">The 1-based index of the image for which to set the property.</param>
        /// <param name="description">
        /// If not NULL and not empty, the property is set to this value.
        /// Otherwise, the property is removed from the XML document.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void SetImageDescription(int image, string description)
        {
            SetImageProperty(image, "DESCRIPTION", description);
        }

        /// <summary>
        /// Change what is stored in the \<FLAGS\> element in the WIM XML document (usually something like "Core" or "Ultimate"). 
        /// Equivalent to SetImageProperty(image, "FLAGS", flags)
        /// </summary>
        /// <param name="image">The 1-based index of the image for which to set the property.</param>
        /// <param name="flags"></param>
        /// If not NULL and not empty, the property is set to this value.
        /// Otherwise, the property is removed from the XML document.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void SetImageFlags(int image, string flags)
        {
            SetImageProperty(image, "FLAGS", flags);
        }

        /// <summary>
        /// Change the name of a WIM image.
        /// Equivalent to SetImageProperty(image, "FLAGS", flags)
        /// </summary>
        /// <param name="image">The 1-based index of the image for which to set the property.</param>
        /// <param name="name"></param>
        /// If not NULL and not empty, the property is set to this value.
        /// Otherwise, the property is removed from the XML document.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void SetImageName(int image, string name)
        {
            SetImageProperty(image, "NAME", name);
        }
        #endregion

        #region GetImageProperty, GetImageDescription, GetImageName
        /// <summary>
        /// Since wimlib v1.8.3: get a per-image property from the WIM's XML document.
        /// </summary>
        /// <remarks>
        /// This is an alternative to wimlib_get_image_name() and
        /// wimlib_get_image_description() which allows getting any simple string
        /// property.
        /// </remarks>
        /// <param name="image">The 1-based index of the image for which to set the property.</param>
        /// <param name="property_name">
        /// The name of the image property, for example "NAME", "DESCRIPTION", or
        /// "TOTALBYTES".  The name can contain forward slashes to indicate a nested
        /// XML element; for example, "WINDOWS/VERSION/BUILD" indicates the BUILD
        /// element nested within the VERSION element nested within the WINDOWS
        /// element.  Since wimlib v1.9.0, a bracketed number can be used to
        /// indicate one of several identically-named elements; for example,
        /// "WINDOWS/LANGUAGES/LANGUAGE[2]" indicates the second "LANGUAGE" element
        /// nested within the "WINDOWS/LANGUAGES" element.  Note that element names
        /// are case sensitive.
        /// </param>
        /// <returns>
        /// The property's value as a ::wimlib_tchar string, or @c NULL if there is
        /// no such property. 
        /// </returns>
        public string GetImageProperty(int image, string propertyName)
        {
            IntPtr ptr = WimLibNative.GetImageProperty(Ptr, image, propertyName);
            return Marshal.PtrToStringUni(ptr);
        }

        /// <summary>
        /// Get the description of the specified image.
        /// Equivalent to GetImageProperty(image, "DESCRIPTION")
        /// </summary>
        /// <param name="image">The 1-based index of the image for which to set the property.</param>
        public string GetImageDescription(int image)
        {
            return GetImageProperty(image, "DESCRIPTION");
        }

        /// <summary>
        /// Get the name of the specified image.
        /// Equivalent to GetImageProperty(image, "NAME")
        /// </summary>
        /// <param name="image">The 1-based index of the image for which to set the property.</param>
        /// <remarks>
        /// GetImageName() will return an empty string if the image is unnamed
        /// whereas GetImageProperty() may return null in that case.
        /// </remarks>
        public string GetImageName(int image)
        {
            string str = GetImageProperty(image, "NAME");
            if (str == null)
                return string.Empty;
            else
                return str;
        }
        #endregion

        #region IsImageNameInUse
        /// <summary>
        /// Determine if an image name is already used by some image in the WIM.
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <returns>
        /// true if there is already an image in wim named name;
        /// false if there is no image named name in wim.
        /// If name is NULL or the empty string, then false is returned.
        /// </returns>
        public bool IsImageNameInUse(string name)
        {
            return WimLibNative.IsImageNameInUse(Ptr, name);
        }
        #endregion

        #region ReferenceTemplateImage, ReferenceResourceFiles
        /// <summary>
        /// Declare that a newly added image is mostly the same as a prior image, but
        /// captured at a later point in time, possibly with some modifications in the
        /// intervening time.  This is designed to be used in incremental backups of the
        /// same filesystem or directory tree.
        /// </summary>
        /// <param name="new_image">The 1-based index in @p wim of the newly added image.</param>
        /// <param name="template_image">The 1-based index in @p template_wim of the template image.</param>
        /// <remarks>
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
        /// </remarks>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
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
        /// </summary>
        /// <param name="new_image">The 1-based index in @p wim of the newly added image.</param>
        /// <param name="template_image">The 1-based index in @p template_wim of the template image.</param>
        /// <remarks>
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
        /// </remarks>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void ReferenceTemplateImage(int newImage, Wim template, int templateImage)
        {
            WimLibErrorCode ret = WimLibNative.ReferenceTemplateImage(Ptr, newImage, template.Ptr, templateImage, 0);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Reference file data from other WIM files or split WIM parts. 
        /// This function can be used on WIMs that are not standalone, such as split or "delta" WIMs,
        /// to load additional file data before calling a function such as wimlib_extract_image() that requires the file data to be present.
        /// </summary>
        /// <remarks>
        /// In the case of split WIMs, instance of WimStruct should be the
        /// first part, since only the first part contains the metadata resources.
        /// In the case of delta WIMs, this should be the delta WIM rather than the
        /// WIM on which it is based.
        /// </remarks>
        /// <param name="resourceWimFiles">
        /// A path to WIM file and/or split WIM parts to reference.
        /// Alternatively, when WimLibRefFlag.GLOB_ENABLE is specified in refFlags, these are treated as globs rather than literal paths.
        /// That is, using this function you can specify zero or more globs, each of which expands to one or more literal paths.
        /// </param>
        /// <param name="refFlags">
        /// Bitwise OR of ::WIMLIB_REF_FLAG_GLOB_ENABLE and/or
        /// ::WIMLIB_REF_FLAG_GLOB_ERR_ON_NOMATCH.
        /// </param>
        /// <param name="openFlags">
        /// Additional open flags, such as ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY, to
        /// pass to internal calls to wimlib_open_wim() on the reference files.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void ReferenceResourceFile(string resourceWimFile, WimLibReferenceFlags refFlags, WimLibOpenFlags openFlags)
        {
            //WimLibErrorCode ret = WimLibNative.ReferenceResourceFiles(Ptr, new string[1] { resourceWimFile }, 1u, refFlags, openFlags);
            // WimLibException.CheckWimLibError(ret);

            IntPtr strPtr = Marshal.StringToHGlobalUni(resourceWimFile);

            try
            {
                WimLibErrorCode ret = WimLibNative.ReferenceResourceFiles(Ptr, new IntPtr[1] { strPtr }, 1u, refFlags, openFlags);
                WimLibException.CheckWimLibError(ret);
            }
            finally
            {
                Marshal.FreeHGlobal(strPtr);
            }
        }

        /// <summary>
        /// Reference file data from other WIM files or split WIM parts. 
        /// This function can be used on WIMs that are not standalone, such as split or "delta" WIMs,
        /// to load additional file data before calling a function such as wimlib_extract_image() that requires the file data to be present.
        /// </summary>
        /// <remarks>
        /// In the case of split WIMs, instance of WimStruct should be the
        /// first part, since only the first part contains the metadata resources.
        /// In the case of delta WIMs, this should be the delta WIM rather than the
        /// WIM on which it is based.
        /// </remarks>
        /// <param name="resourceWimFiles">
        /// Array of paths to WIM files and/or split WIM parts to reference.
        /// Alternatively, when WimLibRefFlag.GLOB_ENABLE is specified in refFlags, these are treated as globs rather than literal paths.
        /// That is, using this function you can specify zero or more globs, each of which expands to one or more literal paths.
        /// </param>
        /// <param name="refFlags">
        /// Bitwise OR of ::WIMLIB_REF_FLAG_GLOB_ENABLE and/or
        /// ::WIMLIB_REF_FLAG_GLOB_ERR_ON_NOMATCH.
        /// </param>
        /// <param name="openFlags">
        /// Additional open flags, such as ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY, to
        /// pass to internal calls to wimlib_open_wim() on the reference files.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void ReferenceResourceFiles(IEnumerable<string> resourceWimFiles, WimLibReferenceFlags refFlags, WimLibOpenFlags openFlags)
        {
            List<IntPtr> arr = new List<IntPtr>(resourceWimFiles.Count());
            foreach (string f in resourceWimFiles)
                arr.Add(Marshal.StringToHGlobalUni(f));

            try
            {
                WimLibErrorCode ret = WimLibNative.ReferenceResourceFiles(Ptr, arr.ToArray(), (uint)resourceWimFiles.Count(), refFlags, openFlags);
                WimLibException.CheckWimLibError(ret);
            }
            finally
            {
                for (int i = 0; i < arr.Count; i++)
                    Marshal.FreeHGlobal(arr[i]);
            }
        }
        #endregion

        #region IterateDirTree, IterateLookupTable
        /// <summary>
        /// Iterate through a file or directory tree in a WIM image.  By specifying
        /// appropriate flags and a callback function, you can get the attributes of a
        /// file in the image, get a directory listing, or even get a listing of the
        /// entire image.
        /// </summary>
        /// <param name="image">
        /// The 1-based index of the image that contains the files or directories to
        /// iterate over, or ::WIMLIB_ALL_IMAGES to iterate over all images.
        /// </param>
        /// <param name="path">
        /// Path in the image at which to do the iteration.
        /// </param>
        /// <param name="iterateFlags">
        /// Bitwise OR of flags prefixed with WIMLIB_ITERATE_DIR_TREE_FLAG.
        /// </param>
        /// <param name="callback">
        /// A callback function that will receive each directory entry.
        /// </param>
        /// <param name="userData">
        /// An extra parameter that will always be passed to the callback function.
        /// </param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void IterateDirTree(int image, string path, WimLibIterateFlags iterateFlags, IterateDirTreeCallback callback, object userData)
        {
            ManagedIterateDirTreeCallback cb = new ManagedIterateDirTreeCallback(callback, userData);

            WimLibErrorCode ret = WimLibNative.IterateDirTree(Ptr, image, path, iterateFlags, cb.NativeFunc, IntPtr.Zero);
            WimLibException.CheckWimLibError(ret);
        }

        /// <summary>
        /// Iterate through the blob lookup table of a ::WIMStruct.  This can be used to
        /// directly get a listing of the unique "blobs" contained in a WIM file, which
        /// are deduplicated over all images.
        /// </summary>
        /// <param name="callback">A callback function that will receive each blob.</param>
        /// <param name="userData">An extra parameter that will always be passed to the callback function</param>
        /// <exception cref="WimLibException">wimlib did not return WIMLIB_ERR_SUCCESS.</exception>
        public void IterateLookupTable(IterateLookupTableCallback callback, object userData)
        {
            ManagedIterateLookupTableCallback cb = new ManagedIterateLookupTableCallback(callback, userData);

            WimLibErrorCode ret = WimLibNative.IterateLookupTable(Ptr, 0, cb.NativeFunc, IntPtr.Zero);
            WimLibException.CheckWimLibError(ret);
        }
        #endregion
        #endregion
    }
    #endregion
}
