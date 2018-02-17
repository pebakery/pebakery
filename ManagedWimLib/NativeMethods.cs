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

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        private GCHandle hObject;
        private object _object;
        public IntPtr Ptr => hObject.AddrOfPinnedObject();

        public PinnedObject(object _object)
        {
            this._object = _object;
            hObject = GCHandle.Alloc(_object, GCHandleType.Pinned);
        }

        ~PinnedObject()
        {
            Dispose(false);
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
                if (hObject.IsAllocated)
                    hObject.Free();
            }
        }
    }

    internal class PinnedArray : IDisposable
    {
        private GCHandle hArray;
        public Array Array;
        public IntPtr Ptr => hArray.AddrOfPinnedObject();

        public IntPtr this[int idx] => Marshal.UnsafeAddrOfPinnedArrayElement(Array, idx);
        public static implicit operator IntPtr(PinnedArray fixedArray) => fixedArray[0];

        public PinnedArray(Array array)
        {
            this.Array = array;
            hArray = GCHandle.Alloc(array, GCHandleType.Pinned);
        }

        ~PinnedArray()
        {
            Dispose(false);
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
                if (hArray.IsAllocated)
                    hArray.Free();
            }
        }
    }
    #endregion

    #region NativeMethods
    public static class NativeMethods
    {
        #region Const and Fields
        internal const string InitFirstErrorMsg = "Please call WimLibNative.AssemblyInit() first!";

        internal static SafeLibraryHandle hModule = null;
        public static bool Loaded => (hModule != null);
        public static string ErrorFile { get; private set; }
        #endregion

        #region AssemblyInit, AssemblyCleanup
        public static SafeLibraryHandle AssemblyInit(string dllPath, InitFlags initFlags = InitFlags.DEFAULT)
        {
            if (hModule == null)
            {
                if (dllPath == null) throw new ArgumentNullException("dllPath");
                else if (!File.Exists(dllPath)) throw new FileNotFoundException("Specified dll does not exist");

                hModule = LoadLibrary(dllPath);
                if (hModule.IsInvalid)
                    throw new ArgumentException($"Unable to load [{dllPath}]", new Win32Exception());

                // Check if dll is valid (wimlib-15.dll)
                if (GetProcAddress(hModule, "wimlib_open_wim") == IntPtr.Zero)
                {
                    AssemblyCleanup();
                    throw new ArgumentException($"[{dllPath}] is not a valid wimlib library");
                }

                try
                {
                    LoadFuntions(hModule);

                    ErrorFile = Path.GetTempFileName();
                    WimLibException.CheckWimLibError(SetErrorFile(ErrorFile));
                }
                catch (Exception e)
                {
                    AssemblyCleanup();
                    throw e;
                }

                ErrorCode ret = NativeMethods.GlobalInit(initFlags);
                WimLibException.CheckWimLibError(ret);
            }

            /*
            // Set WimStructSize, value of wimlib 1.12 (mingw)
            if (IntPtr.Size == 8) // 64
                WimStructSize = 0x1B8;
            else if (IntPtr.Size == 4) // 32
                WimStructSize = 0x190;
            else
                throw new PlatformNotSupportedException();
            */

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

                if (File.Exists(ErrorFile))
                    File.Delete(ErrorFile);
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
            #region Init and Cleanup - GlobalInit, GlobalCleanup, Free
            GlobalInit = (wimlib_global_init)GetFuncPtr("wimlib_global_init", typeof(wimlib_global_init));
            GlobalCleanup = (wimlib_global_cleanup)GetFuncPtr("wimlib_global_cleanup", typeof(wimlib_global_cleanup));
            Free = (wimlib_free)GetFuncPtr("wimlib_free", typeof(wimlib_free));
            #endregion

            #region WimStruct - OpenWim, OpenWimWithProgress, CreateWim, RegsiterProgressFunction
            OpenWim = (wimlib_open_wim)GetFuncPtr("wimlib_open_wim", typeof(wimlib_open_wim));
            OpenWimWithProgress = (wimlib_open_wim_with_progress)GetFuncPtr("wimlib_open_wim_with_progress", typeof(wimlib_open_wim_with_progress));
            CreateNewWim = (wimlib_create_new_wim)GetFuncPtr("wimlib_create_new_wim", typeof(wimlib_create_new_wim));
            RegisterProgressFunction = (wimlib_register_progress_function_delegate)GetFuncPtr("wimlib_register_progress_function", typeof(wimlib_register_progress_function_delegate));
            #endregion

            #region Error - GetErrorString, SetErrorFile
            GetErrorStringPtr = (wimlib_get_error_string)GetFuncPtr("wimlib_get_error_string", typeof(wimlib_get_error_string));
            SetErrorFile = (wimlib_set_error_file_by_name)GetFuncPtr("wimlib_set_error_file_by_name", typeof(wimlib_set_error_file_by_name));
            #endregion

            #region Add - AddEmptyImage, AddImage, AddImageMultiSource, AddTree
            AddEmptyImage = (wimlib_add_empty_image)GetFuncPtr("wimlib_add_empty_image", typeof(wimlib_add_empty_image));
            AddImage = (wimlib_add_image)GetFuncPtr("wimlib_add_image", typeof(wimlib_add_image));
            AddImageMultiSource = (wimlib_add_image_multisource)GetFuncPtr("wimlib_add_image_multisource", typeof(wimlib_add_image_multisource));
            AddTree = (wimlib_add_tree)GetFuncPtr("wimlib_add_tree", typeof(wimlib_add_tree));
            #endregion

            #region Delete - DeleteImage, DeletePath
            DeleteImage = (wimlib_delete_image)GetFuncPtr("wimlib_delete_image", typeof(wimlib_delete_image));
            DeletePath = (wimlib_delete_path)GetFuncPtr("wimlib_delete_path", typeof(wimlib_delete_path));
            #endregion

            #region Export - ExportImage
            ExportImage = (wimlib_export_image)GetFuncPtr("wimlib_export_image", typeof(wimlib_export_image));
            #endregion

            #region Extract - ExtractImage, ExtractPathList, ExtractPaths
            ExtractImage = (wimlib_extract_image)GetFuncPtr("wimlib_extract_image", typeof(wimlib_extract_image));
            ExtractPathList = (wimlib_extract_pathlist)GetFuncPtr("wimlib_extract_pathlist", typeof(wimlib_extract_pathlist));
            ExtractPaths = (wimlib_extract_paths)GetFuncPtr("wimlib_extract_paths", typeof(wimlib_extract_paths));
            #endregion



            #region GetImageInfo - GetImageDescription, GetImageName, GetImageProperty
            GetImageDescription = (wimlib_get_image_description)GetFuncPtr("wimlib_get_image_description", typeof(wimlib_get_image_description));
            GetImageName = (wimlib_get_image_name)GetFuncPtr("wimlib_get_image_name", typeof(wimlib_get_image_name));
            GetImageProperty = (wimlib_get_image_property)GetFuncPtr("wimlib_get_image_property", typeof(wimlib_get_image_property));
            #endregion

            #region GetVersion - GetVersion
            GetVersionPtr = (wimlib_get_version)GetFuncPtr("wimlib_get_version", typeof(wimlib_get_version));
            #endregion

            #region GetWimInfo - GetWimInfo, GetXmlData
            GetWimInfo = (wimlib_get_wim_info)GetFuncPtr("wimlib_get_wim_info", typeof(wimlib_get_wim_info));
            GetXmlData = (wimlib_get_xml_data)GetFuncPtr("wimlib_get_xml_data", typeof(wimlib_get_xml_data));
            #endregion

            Overwrite = (wimlib_overwrite)GetFuncPtr("wimlib_overwrite", typeof(wimlib_overwrite));
            
            Write = (wimlib_write)GetFuncPtr("wimlib_write", typeof(wimlib_write));
            
            SetImageProperty = (wimlib_set_image_property)GetFuncPtr("wimlib_set_image_property", typeof(wimlib_set_image_property));
            IsImageNameInUse = (wimlib_image_name_in_use)GetFuncPtr("wimlib_image_name_in_use", typeof(wimlib_image_name_in_use));
            ReferenceTemplateImage = (wimlib_reference_template_image)GetFuncPtr("wimlib_reference_template_image", typeof(wimlib_reference_template_image));
            
            SetOutputCompressionType = (wimlib_set_output_compression_type)GetFuncPtr("wimlib_set_output_compression_type", typeof(wimlib_set_output_compression_type));
            SetOutputPackCompressionType = (wimlib_set_output_pack_compression_type)GetFuncPtr("wimlib_set_output_pack_compression_type", typeof(wimlib_set_output_pack_compression_type));
            UpdateImage32 = (wimlib_update_image_32)GetFuncPtr("wimlib_update_image", typeof(wimlib_update_image_32));
            UpdateImage64 = (wimlib_update_image_64)GetFuncPtr("wimlib_update_image", typeof(wimlib_update_image_64));
            ReferenceResourceFiles = (wimlib_reference_resource_files)GetFuncPtr("wimlib_reference_resource_files", typeof(wimlib_reference_resource_files));
            IterateDirTree = (wimlib_iterate_dir_tree)GetFuncPtr("wimlib_iterate_dir_tree", typeof(wimlib_iterate_dir_tree));
            IterateLookupTable = (wimlib_iterate_lookup_table)GetFuncPtr("wimlib_iterate_lookup_table", typeof(wimlib_iterate_lookup_table));
            
        }

        private static void ResetFuntions()
        {
            #region Init and Cleanup - GlobalInit, GlobalCleanup, Free
            GlobalInit = null;
            GlobalCleanup = null;
            Free = null;
            #endregion

            #region WimStruct - OpenWim, OpenWimWithProgress, CreateWim, RegsiterProgressFunction
            OpenWim = null;
            OpenWimWithProgress = null;
            CreateNewWim = null;
            RegisterProgressFunction = null;
            #endregion

            #region Error - GetErrorString, SetErrorFile
            GetErrorStringPtr = null;
            SetErrorFile = null;
            #endregion

            #region Add - AddEmptyImage, AddImage, AddImageMultiSource, AddTree
            AddEmptyImage = null;
            AddImage = null;
            AddImageMultiSource = null;
            AddTree = null;
            #endregion

            #region Delete - DeleteImage, DeletePath
            DeleteImage = null;
            DeletePath = null;
            #endregion

            #region Export - ExportImage
            ExportImage = null;
            #endregion

            #region Extract - ExtractImage, ExtractPathList, ExtractPaths
            ExtractImage = null;
            ExtractPathList = null;
            ExtractPaths = null;
            #endregion

            #region GetImageInfo - GetImageDescription, GetImageName, GetImageProperty
            GetImageDescription = null;
            GetImageName = null;
            GetImageProperty = null;
            #endregion

            #region GetVersion - GetVersion
            GetVersionPtr = null;
            #endregion

            #region GetWimInfo - GetWimInfo, GetXmlData
            GetWimInfo = null;
            GetXmlData = null;
            #endregion

            Overwrite = null;
            Write = null;
            
            SetImageProperty = null;
            IsImageNameInUse = null;
            ReferenceTemplateImage = null;
            
            SetOutputCompressionType = null;
            SetOutputPackCompressionType = null;
            UpdateImage32 = null;
            UpdateImage64 = null;
            ReferenceResourceFiles = null;
            IterateDirTree = null;
            IterateLookupTable = null;
        }
        #endregion

        #region Windows API
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeLibraryHandle LoadLibrary([MarshalAs(UnmanagedType.LPTStr)] string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);
        #endregion

        #region WimLib Function Pointer
        #region GlobalInit, GlobalCleanup
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_global_init(InitFlags init_flags);
        internal static wimlib_global_init GlobalInit;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void wimlib_global_cleanup();
        internal static wimlib_global_cleanup GlobalCleanup;
        #endregion

        #region OpenWim, CreateNewWim, Callback, Free, GetErrorString
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_open_wim(
            [MarshalAs(UnmanagedType.LPWStr)] string wim_file,
            OpenFlags open_flags,
            out IntPtr wim_ret);
        internal static wimlib_open_wim OpenWim;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_create_new_wim(
            CompressionType ctype,
            out IntPtr wim_ret);
        internal static wimlib_create_new_wim CreateNewWim;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_open_wim_with_progress(
            [MarshalAs(UnmanagedType.LPWStr)] string wim_file,
            OpenFlags open_flags,
            out IntPtr wim_ret,
            [MarshalAs(UnmanagedType.FunctionPtr)] NativeProgressFunc progfunc,
            IntPtr progctx);
        internal static wimlib_open_wim_with_progress OpenWimWithProgress;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate CallbackStatus NativeProgressFunc(
            ProgressMsg msg_type,
            IntPtr info,
            IntPtr progctx);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void wimlib_register_progress_function_delegate(
            IntPtr wim,
            [MarshalAs(UnmanagedType.FunctionPtr)] NativeProgressFunc progfunc,
            IntPtr progctx);
        internal static wimlib_register_progress_function_delegate RegisterProgressFunction;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void wimlib_free(IntPtr wim);
        internal static wimlib_free Free;
        #endregion

        #region GetErrorString, SetErrorFile
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr wimlib_get_error_string(ErrorCode code);
        internal static wimlib_get_error_string GetErrorStringPtr;
        /// <summary>
        /// Convert a wimlib error code into a string describing it.
        /// </summary>
        /// <param name="code">An error code returned by one of wimlib's functions.</param>
        /// <returns>
        /// string describing the error code.
        /// If the value was unrecognized, then the resulting string will be "Unknown error".
        /// </returns>
        public static string GetErrorString(ErrorCode code)
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.InitFirstErrorMsg);

            IntPtr ptr = NativeMethods.GetErrorStringPtr(ErrorCode.INVALID_IMAGE);
            return Marshal.PtrToStringUni(ptr);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_set_error_file_by_name(
            [MarshalAs(UnmanagedType.LPWStr)] string path);
        internal static wimlib_set_error_file_by_name SetErrorFile;
        #endregion

        #region Add - AddEmptyImage, AddImage, AddImageMultiSource, AddTree
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_add_empty_image(
            IntPtr wim,
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            out int new_idx_ret);
        internal static wimlib_add_empty_image AddEmptyImage;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_add_image(
            IntPtr wim,
            [MarshalAs(UnmanagedType.LPWStr)] string source,
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            [MarshalAs(UnmanagedType.LPWStr)] string config_file,
            AddFlags add_flags);
        internal static wimlib_add_image AddImage;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_add_image_multisource(
            IntPtr wim,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct)] CaptureSource[] sources,
            IntPtr num_sources, // size_t
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            [MarshalAs(UnmanagedType.LPWStr)] string config_file,
            AddFlags add_flags);
        internal static wimlib_add_image_multisource AddImageMultiSource;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_add_tree(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string fs_source_path,
            [MarshalAs(UnmanagedType.LPWStr)] string wim_target_path,
            AddFlags add_flags);
        internal static wimlib_add_tree AddTree;
        #endregion

        #region Delete - DeleteImage, DeletePath
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_delete_image(
            IntPtr wim,
            int image);
        internal static wimlib_delete_image DeleteImage;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_delete_path(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            DeleteFlags delete_flags);
        internal static wimlib_delete_path DeletePath;
        #endregion

        #region Export - ExportImage
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_export_image(
            IntPtr src_wim,
            int src_image,
            IntPtr dest_wim,
            [MarshalAs(UnmanagedType.LPWStr)] string dest_name,
            [MarshalAs(UnmanagedType.LPWStr)] string dest_description,
            ExportFlags export_flags);
        internal static wimlib_export_image ExportImage;
        #endregion

        #region Extract - ExtractImage, ExtractPaths, ExtractPathList
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_extract_image(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string target,
            ExtractFlags extract_flags);
        internal static wimlib_extract_image ExtractImage;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_extract_pathlist(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string target,
            [MarshalAs(UnmanagedType.LPWStr)] string path_list_file,
            ExtractFlags extract_flags);
        internal static wimlib_extract_pathlist ExtractPathList;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_extract_paths(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string target,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] paths,
            IntPtr num_paths, // size_t, in fact
            ExtractFlags extract_flags);
        internal static wimlib_extract_paths ExtractPaths;
        #endregion

        #region GetImageInfo - GetImageDescription, GetImageName, GetImageProperty
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr wimlib_get_image_description(
            IntPtr wim,
            int image);
        internal static wimlib_get_image_description GetImageDescription;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr wimlib_get_image_name(
            IntPtr wim,
            int image);
        internal static wimlib_get_image_name GetImageName;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr wimlib_get_image_property(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string property_name);
        internal static wimlib_get_image_property GetImageProperty;
        #endregion

        #region GetVersion - GetVersion, GetVersionTuple
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate uint wimlib_get_version();
        /// <summary>
        /// Return the version of wimlib as a 32-bit number whose top 12 bits contain the
        /// major version, the next 10 bits contain the minor version, and the low 10
        /// bits contain the patch version.
        /// </summary>
        /// <remarks>
        /// In other words, the returned value is equal to ((WIMLIB_MAJOR_VERSION &lt;&lt;
        /// 20) | (WIMLIB_MINOR_VERSION &lt;&lt; 10) | WIMLIB_PATCH_VERSION) for the
        /// corresponding header file.
        /// </remarks>
        internal static wimlib_get_version GetVersionPtr;
        /// <summary>
        /// Return the version of wimlib as a Version instance.
        /// Major, Minor and Build (Patch) properties will be populated.
        /// </summary>
        public static Version GetVersion()
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.InitFirstErrorMsg);

            uint dword = GetVersionPtr();
            ushort major = (ushort)(dword >> 20);
            ushort minor = (ushort)((dword % (1 << 20)) >> 10);
            ushort patch = (ushort)(dword % (1 << 10));

            return new Version(major, minor, patch);
        }

        /// <summary>
        /// Return the version of wimlib as a Tuple.
        /// Tuple's items will be populated in a order of Major, Minor, and Patch.
        /// </summary>
        public static Tuple<ushort, ushort, ushort> GetVersionTuple()
        {
            if (!NativeMethods.Loaded)
                throw new InvalidOperationException(NativeMethods.InitFirstErrorMsg);

            uint dword = GetVersionPtr();
            ushort major = (ushort)(dword >> 20);
            ushort minor = (ushort)((dword % (1 << 20)) >> 10);
            ushort patch = (ushort)(dword % (1 << 10));

            return new Tuple<ushort, ushort, ushort>(major, minor, patch);
        }
        #endregion

        #region GetWimInfo - GetWimInfo, GetXmlData
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_get_wim_info(
            IntPtr wim,
            IntPtr info);
        internal static wimlib_get_wim_info GetWimInfo;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_get_xml_data(
            IntPtr wim,
            ref IntPtr buf_ret,
            ref IntPtr bufsize_ret); // size_t
        internal static wimlib_get_xml_data GetXmlData;

        // wimlib_get_xml_data(WIMStruct* wim, void** buf_ret, size_t* bufsize_ret);
        #endregion







        #region UpdateImage
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal delegate ErrorCode wimlib_update_image_32(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct)] UpdateCommand32[] cmds,
            uint num_cmds,
            UpdateFlags update_flags);
        internal static wimlib_update_image_32 UpdateImage32;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal delegate ErrorCode wimlib_update_image_64(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct)] UpdateCommand64[] cmds,
            ulong num_cmds,
            UpdateFlags update_flags);
        internal static wimlib_update_image_64 UpdateImage64;
        #endregion

        #region SetOutputCompressionType, SetOutputPackCompressionType
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_set_output_compression_type(
            IntPtr wim,
            CompressionType ctype);
        internal static wimlib_set_output_compression_type SetOutputCompressionType;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_set_output_pack_compression_type(
            IntPtr wim,
            CompressionType ctype);
        internal static wimlib_set_output_pack_compression_type SetOutputPackCompressionType;
        #endregion

        #region Write, OverWrite
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_write(
            IntPtr wim,
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            int image,
            WriteFlags write_flags,
            uint num_threads);
        internal static wimlib_write Write;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_overwrite(
            IntPtr wim,
            WriteFlags write_flags,
            uint numThreads);
        internal static wimlib_overwrite Overwrite;
        #endregion

        #region SetImageProperty
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_set_image_property(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string property_name,
            [MarshalAs(UnmanagedType.LPWStr)] string property_value);
        internal static wimlib_set_image_property SetImageProperty;
        #endregion

        #region IsImageNameInUse, ReferenceTemplateImage, ReferenceResourceFiles
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate bool wimlib_image_name_in_use(
            IntPtr wim,
            [MarshalAs(UnmanagedType.LPWStr)] string name);
        internal static wimlib_image_name_in_use IsImageNameInUse;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate ErrorCode wimlib_reference_template_image(
            IntPtr wim,
            int new_image,
            IntPtr template_wim,
            int template_image,
            int flags);
        internal static wimlib_reference_template_image ReferenceTemplateImage;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal delegate ErrorCode wimlib_reference_resource_files(
            IntPtr wim,
            // [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] resource_wimfiles_or_globs,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] resource_wimfiles_or_globs,
            uint count,
            RefFlags ref_flags,
            OpenFlags open_flags);
        internal static wimlib_reference_resource_files ReferenceResourceFiles;
        #endregion

        #region IterateDirTree, IterateLookupTable
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal delegate CallbackStatus NativeIterateDirTreeCallback(
            IntPtr dentry,
            IntPtr progctx);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal delegate ErrorCode wimlib_iterate_dir_tree(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IterateFlags flags,
            [MarshalAs(UnmanagedType.FunctionPtr)] NativeIterateDirTreeCallback cb,
            IntPtr user_ctx);
        internal static wimlib_iterate_dir_tree IterateDirTree;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal delegate CallbackStatus NativeIterateLookupTableCallback(
            IntPtr resoure,
            IntPtr progctx);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal delegate ErrorCode wimlib_iterate_lookup_table(
            IntPtr wim,
            int image,
            [MarshalAs(UnmanagedType.FunctionPtr)] NativeIterateLookupTableCallback cb,
            IntPtr user_ctx);
        internal static wimlib_iterate_lookup_table IterateLookupTable;
        #endregion

        
        #endregion

        #region Utility
        internal static bool GetBitField(uint bitField, int bitShift)
        {
            return (bitField & (1 << bitShift)) != 0;
        }
        #endregion
    }
    #endregion

    #region Enum CompressionType
    /// <summary>
    /// Specifies a compression type.
    ///
    /// A WIM file has a default compression type, indicated by its file header.
    /// Normally, each resource in the WIM file is compressed with this compression
    /// type.  However, resources may be stored as uncompressed; for example, wimlib
    /// may do so if a resource does not compress to less than its original size.  In
    /// addition, a WIM with the new version number of 3584, or "ESD file", might
    /// contain solid resources with different compression types.
    /// </summary>
    public enum CompressionType
    {
        /// <summary>
        /// No compression.
        /// </summary>
        NONE = 0,
        /// <summary>
        /// The XPRESS compression format.  This format combines Lempel-Ziv
        /// factorization with Huffman encoding.  Compression and decompression
        /// are both fast. 
        /// </summary>
        XPRESS = 1,
        /// <summary>
        /// The LZX compression format.  This format combines Lempel-Ziv
        /// factorization with Huffman encoding, but with more features and
        /// complexity than XPRESS.  Compression is slow to somewhat fast,
        /// depending on the settings.  Decompression is fast but slower than
        /// XPRESS.
        /// </summary>
        LZX = 2,
        /// <summary>
        /// The LZMS compression format.  This format combines Lempel-Ziv
        /// factorization with adaptive Huffman encoding and range coding.
        /// Compression and decompression are both fairly slow.
        /// </summary>
        LZMS = 3,
    }
    #endregion

    #region Enum Progress
    /// <summary>
    /// Possible values of the first parameter to the user-supplied
    /// ::wimlib_progress_func_t progress function
    /// </summary>
    public enum ProgressMsg
    {
        /// <summary>
        /// A WIM image is about to be extracted.  @p info will point to
        /// ::wimlib_progress_info.extract.  This message is received once per
        /// image for calls to wimlib_extract_image() and
        /// wimlib_extract_image_from_pipe().
        /// </summary>
        EXTRACT_IMAGE_BEGIN = 0,
        /// <summary>
        /// One or more file or directory trees within a WIM image is about to
        /// be extracted.  @p info will point to ::wimlib_progress_info.extract.
        /// This message is received only once per wimlib_extract_paths() and
        /// wimlib_extract_pathlist(), since wimlib combines all paths into a
        /// single extraction operation for optimization purposes.
        /// </summary>
        EXTRACT_TREE_BEGIN = 1,
        /// <summary>
        /// This message may be sent periodically (not for every file) while
        /// files and directories are being created, prior to file data
        /// extraction.  @p info will point to ::wimlib_progress_info.extract.
        /// In particular, the @p current_file_count and @p end_file_count
        /// members may be used to track the progress of this phase of
        /// extraction.
        /// </summary>
        EXTRACT_FILE_STRUCTURE = 3,
        /// <summary>
        /// File data is currently being extracted.  @p info will point to
        /// ::wimlib_progress_info.extract.  This is the main message to track
        /// the progress of an extraction operation.
        /// </summary>
        EXTRACT_STREAMS = 4,
        /// <summary>
        /// Starting to read a new part of a split pipable WIM over the pipe.
        /// @p info will point to ::wimlib_progress_info.extract.
        /// </summary>
        EXTRACT_SPWM_PART_BEGIN = 5,
        /// <summary>
        /// This message may be sent periodically (not necessarily for every
        /// file) while file and directory metadata is being extracted, following
        /// file data extraction.  @p info will point to
        /// ::wimlib_progress_info.extract.  The @p current_file_count and @p
        /// end_file_count members may be used to track the progress of this
        /// phase of extraction.
        /// </summary>
        EXTRACT_METADATA = 6,
        /// <summary>
        /// The image has been successfully extracted.  @p info will point to
        /// ::wimlib_progress_info.extract.  This is paired with
        /// ::WIMLIB_PROGRESS_MSG_EXTRACT_IMAGE_BEGIN.
        /// </summary>
        EXTRACT_IMAGE_END = 7,
        /// <summary>
        /// The files or directory trees have been successfully extracted.  @p
        /// info will point to ::wimlib_progress_info.extract.  This is paired
        /// with ::WIMLIB_PROGRESS_MSG_EXTRACT_TREE_BEGIN.
        /// </summary>
        EXTRACT_TREE_END = 8,
        /// <summary>
        /// The directory or NTFS volume is about to be scanned for metadata.
        /// @p info will point to ::wimlib_progress_info.scan.  This message is
        /// received once per call to wimlib_add_image(), or once per capture
        /// source passed to wimlib_add_image_multisource(), or once per add
        /// command passed to wimlib_update_image().
        /// </summary>
        SCAN_BEGIN = 9,
        /// <summary>
        /// A directory or file has been scanned.  @p info will point to
        /// ::wimlib_progress_info.scan, and its @p cur_path member will be
        /// valid.  This message is only sent if ::WIMLIB_ADD_FLAG_VERBOSE has
        /// been specified.
        /// </summary>
        SCAN_DENTRY = 10,
        /// <summary>
        /// The directory or NTFS volume has been successfully scanned.  @p info
        /// will point to ::wimlib_progress_info.scan.  This is paired with a
        /// previous ::WIMLIB_PROGRESS_MSG_SCAN_BEGIN message, possibly with many
        /// intervening ::WIMLIB_PROGRESS_MSG_SCAN_DENTRY messages.
        /// </summary>
        SCAN_END = 11,
        /// <summary>
        /// File data is currently being written to the WIM.  @p info will point
        /// to ::wimlib_progress_info.write_streams.  This message may be
        /// received many times while the WIM file is being written or appended
        /// to with wimlib_write(), wimlib_overwrite(), or wimlib_write_to_fd().
        /// </summary>
        WRITE_STREAMS = 12,
        /// <summary>
        /// Per-image metadata is about to be written to the WIM file.  @p info
        /// will not be valid.
        /// </summary>
        WRITE_METADATA_BEGIN = 13,
        /// <summary>
        /// The per-image metadata has been written to the WIM file.  @p info
        /// will not be valid.  This message is paired with a preceding
        /// ::WIMLIB_PROGRESS_MSG_WRITE_METADATA_BEGIN message.
        /// </summary>
        WRITE_METADATA_END = 14,
        /// <summary>
        /// wimlib_overwrite() has successfully renamed the temporary file to
        /// the original WIM file, thereby committing the changes to the WIM
        /// file.  @p info will point to ::wimlib_progress_info.rename.  Note:
        /// this message is not received if wimlib_overwrite() chose to append to
        /// the WIM file in-place.
        /// </summary>
        RENAME = 15,
        /// <summary>
        /// The contents of the WIM file are being checked against the integrity
        /// table.  @p info will point to ::wimlib_progress_info.integrity.  This
        /// message is only received (and may be received many times) when
        /// wimlib_open_wim_with_progress() is called with the
        /// ::WIMLIB_OPEN_FLAG_CHECK_INTEGRITY flag.
        /// </summary>
        VERIFY_INTEGRITY = 16,
        /// <summary>
        /// An integrity table is being calculated for the WIM being written.
        /// @p info will point to ::wimlib_progress_info.integrity.  This message
        /// is only received (and may be received many times) when a WIM file is
        /// being written with the flag ::WIMLIB_WRITE_FLAG_CHECK_INTEGRITY.
        /// </summary>
        CALC_INTEGRITY = 17,
        /// <summary>
        /// A wimlib_split() operation is in progress, and a new split part is
        /// about to be started.  @p info will point to
        /// ::wimlib_progress_info.split.
        /// </summary>
        SPLIT_BEGIN_PART = 19,
        /// <summary>
        /// A wimlib_split() operation is in progress, and a split part has been
        /// finished. @p info will point to ::wimlib_progress_info.split.
        /// </summary>
        SPLIT_END_PART = 20,
        /// <summary>
        /// A WIM update command is about to be executed. @p info will point to
        /// ::wimlib_progress_info.update.  This message is received once per
        /// update command when wimlib_update_image() is called with the flag
        /// ::WIMLIB_UPDATE_FLAG_SEND_PROGRESS.
        /// </summary>
        UPDATE_BEGIN_COMMAND = 21,
        /// <summary>
        /// A WIM update command has been executed. @p info will point to
        /// ::wimlib_progress_info.update.  This message is received once per
        /// update command when wimlib_update_image() is called with the flag
        /// ::WIMLIB_UPDATE_FLAG_SEND_PROGRESS.
        /// </summary>
        UPDATE_END_COMMAND = 22,
        /// <summary>
        /// A file in the image is being replaced as a result of a
        /// ::wimlib_add_command without ::WIMLIB_ADD_FLAG_NO_REPLACE specified.
        /// @p info will point to ::wimlib_progress_info.replace.  This is only
        /// received when ::WIMLIB_ADD_FLAG_VERBOSE is also specified in the add
        /// command.
        /// </summary>
        REPLACE_FILE_IN_WIM = 23,
        /// <summary>
        /// An image is being extracted with ::WIMLIB_EXTRACT_FLAG_WIMBOOT, and
        /// a file is being extracted normally (not as a "WIMBoot pointer file")
        /// due to it matching a pattern in the <c>[PrepopulateList]</c> section
        /// of the configuration file
        /// <c>/Windows/System32/WimBootCompress.ini</c> in the WIM image.  @p
        /// info will point to ::wimlib_progress_info.wimboot_exclude.
        /// </summary>
        WIMBOOT_EXCLUDE = 24,
        /// <summary>
        /// Starting to unmount an image.  @p info will point to
        /// ::wimlib_progress_info.unmount.
        /// </summary>
        UNMOUNT_BEGIN = 25,
        /// <summary>
        /// wimlib has used a file's data for the last time (including all data
        /// streams, if it has multiple).  @p info will point to
        /// ::wimlib_progress_info.done_with_file.  This message is only received
        /// if ::WIMLIB_WRITE_FLAG_SEND_DONE_WITH_FILE_MESSAGES was provided.
        /// </summary>
        DONE_WITH_FILE = 26,
        /// <summary>
        /// wimlib_verify_wim() is starting to verify the metadata for an image.
        /// @p info will point to ::wimlib_progress_info.verify_image.
        /// </summary>
        BEGIN_VERIFY_IMAGE = 27,
        /// <summary>
        /// wimlib_verify_wim() has finished verifying the metadata for an
        /// image.  @p info will point to ::wimlib_progress_info.verify_image.
        /// </summary>
        END_VERIFY_IMAGE = 28,
        /// <summary>
        /// wimlib_verify_wim() is verifying file data integrity.  @p info will
        /// point to ::wimlib_progress_info.verify_streams.
        /// </summary>
        VERIFY_STREAMS = 29,
        /// <summary>
        /// The progress function is being asked whether a file should be
        /// excluded from capture or not.  @p info will point to
        /// ::wimlib_progress_info.test_file_exclusion.  This is a bidirectional
        /// message that allows the progress function to set a flag if the file
        /// should be excluded.
        ///
        /// This message is only received if the flag
        /// ::WIMLIB_ADD_FLAG_TEST_FILE_EXCLUSION is used.  This method for file
        /// exclusions is independent of the "capture configuration file"
        /// mechanism.
        /// </summary>
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
    public enum CallbackStatus : int
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
    public enum ErrorCode : int
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

    #region Enum IterateFlags
    [Flags]
    public enum IterateFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// For wimlib_iterate_dir_tree(): Iterate recursively on children rather than just on the specified path.
        /// </summary>
        RECURSIVE = 0x00000001,
        /// <summary>
        /// For wimlib_iterate_dir_tree(): Don't iterate on the file or directory itself;
        /// only its children (in the case of a non-empty directory)
        /// </summary>
        CHILDREN = 0x00000002,
        /// <summary>
        /// Return ::WIMLIB_ERR_RESOURCE_NOT_FOUND if any file data blobs needed to fill
        /// in the ::wimlib_resource_entry's for the iteration cannot be found in the
        /// blob lookup table of the ::WIMStruct.  The default behavior without this flag
        /// is to fill in the @ref wimlib_resource_entry::sha1_hash "sha1_hash" and set
        /// the @ref wimlib_resource_entry::is_missing "is_missing" flag.
        /// </summary>
        RESOURCES_NEEDED = 0x00000004,
    }
    #endregion

    #region Enum AddFlags
    [Flags]
    public enum AddFlags : uint
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
    #endregion

    #region Enum DeleteFlags
    [Flags]
    public enum DeleteFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// Do not issue an error if the path to delete does not exist.
        /// </summary>
        FORCE = 0x00000001,
        /// <summary>
        /// Delete the file or directory tree recursively; if not specified, an error is issued if the path to delete is a directory.
        /// </summary>
        RECURSIVE = 0x00000002,
    }
    #endregion

    #region Enum ExportFlags
    [Flags]
    public enum ExportFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// If a single image is being exported, mark it bootable in the destination WIM.
        /// Alternatively, if ::WIMLIB_ALL_IMAGES is specified as the image to export,
        /// the image in the source WIM (if any) that is marked as bootable is also
        /// marked as bootable in the destination WIM.
        /// </summary>
        BOOT = 0x00000001,
        /// <summary>
        /// Give the exported image(s) no names.  Avoids problems with image name collisions.
        /// </summary>
        NO_NAMES = 0x00000002,
        /// <summary>
        /// Give the exported image(s) no descriptions.
        /// </summary>
        NO_DESCRIPTIONS = 0x00000004,
        /// <summary>
        /// This advises the library that the program is finished with the source
        /// WIMStruct and will not attempt to access it after the call to
        /// wimlib_export_image(), with the exception of the call to wimlib_free().
        /// </summary>
        GIFT = 0x00000008,
        /// <summary>
        /// Mark each exported image as WIMBoot-compatible.
        ///
        /// Note: by itself, this does change the destination WIM's compression type, nor
        /// does it add the file @c \\Windows\\System32\\WimBootCompress.ini in the WIM
        /// image.  
        /// </summary>
        /// <remarks>
        /// Before writing the destination WIM, it's recommended to do something
        /// like:
        ///
        /// \code
        /// wimlib_set_output_compression_type(wim, WIMLIB_COMPRESSION_TYPE_XPRESS);
        /// wimlib_set_output_chunk_size(wim, 4096);
        /// wimlib_add_tree(wim, image, L"myconfig.ini",
        ///   L"\\Windows\\System32\\WimBootCompress.ini", 0);
        /// \endcode
        /// </remarks>
        WIMBOOT = 0x00000010,
    }
    #endregion

    #region Enum ExtractFlags
    [Flags]
    public enum ExtractFlags : uint
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

    #region Enum MountFlags (Linux Only)
    [Flags]
    public enum MountFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// Mount the WIM image read-write rather than the default of read-only.
        /// </summary>
        READWRITE = 0x00000001,
        /// <summary>
        /// Enable FUSE debugging by passing the -d option to fuse_main().
        /// </summary>
        DEBUG = 0x00000002,
        /// <summary>
        /// Do not allow accessing named data streams in the mounted WIM image.
        /// </summary>
        STREAM_INTERFACE_NONE = 0x00000004,
        /// <summary>
        /// Access named data streams in the mounted WIM image through extended file
        /// attributes named "user.X", where X is the name of a data stream.  This is the
        /// default mode.
        /// </summary>
        STREAM_INTERFACE_XATTR = 0x00000008,
        /// <summary>
        /// Access named data streams in the mounted WIM image by specifying the file
        /// name, a colon, then the name of the data stream.
        /// </summary>
        STREAM_INTERFACE_WINDOWS = 0x00000010,
        /// <summary>
        /// Support UNIX owners, groups, modes, and special files.
        /// </summary>
        UNIX_DATA = 0x00000020,
        /// <summary>
        /// Allow other users to see the mounted filesystem.  This passes the allow_other option to fuse_main().
        /// </summary>
        ALLOW_OTHER = 0x00000040,        
    }
    #endregion

    #region Enum OpenFlags
    [Flags]
    public enum OpenFlags : int
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
    #endregion

    #region Enum UnmountFlags (Linux Only)
    [Flags]
    public enum UnmountFlags : uint
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// Provide ::WIMLIB_WRITE_FLAG_CHECK_INTEGRITY when committing the WIM image.
        /// Ignored if ::WIMLIB_UNMOUNT_FLAG_COMMIT not also specified.
        /// </summary>
        CHECK_INTEGRITY = 0x00000001,
        /// <summary>
        /// Commit changes to the read-write mounted WIM image.
        /// If this flag is not specified, changes will be discarded.
        /// </summary>
        COMMIT = 0x00000002,
        /// <summary>
        /// Provide ::WIMLIB_WRITE_FLAG_REBUILD when committing the WIM image.
        /// Ignored if ::WIMLIB_UNMOUNT_FLAG_COMMIT not also specified.
        /// </summary>
        REBUILD = 0x00000004,
        /// <summary>
        /// Provide ::WIMLIB_WRITE_FLAG_RECOMPRESS when committing the WIM image.
        /// Ignored if ::WIMLIB_UNMOUNT_FLAG_COMMIT not also specified.
        /// </summary>
        RECOMPRESS = 0x00000008,
        /// <summary>
        /// In combination with ::WIMLIB_UNMOUNT_FLAG_COMMIT for a read-write mounted WIM
        /// image, forces all file descriptors to the open WIM image to be closed before
        /// committing it.
        /// </summary>
        /// <remarks>
        /// Without ::WIMLIB_UNMOUNT_FLAG_COMMIT or with a read-only mounted WIM image,
        /// this flag has no effect.
        /// </remarks>
        FORCE = 0x00000010,
        /// <summary>
        /// In combination with ::WIMLIB_UNMOUNT_FLAG_COMMIT for a read-write mounted
        /// WIM image, causes the modified image to be committed to the WIM file as a
        /// new, unnamed image appended to the archive.  The original image in the WIM
        /// file will be unmodified.
        /// </summary>
        NEW_IMAGE = 0x00000020,
    }
    #endregion

    #region Enum UpdateFlags
    [Flags]
    public enum UpdateFlags : uint
    {
        /// <summary>
        /// Send WIMLIB_PROGRESS_MSG_UPDATE_BEGIN_COMMAND and WIMLIB_PROGRESS_MSG_UPDATE_END_COMMAND messages.
        /// </summary>
        SEND_PROGRESS = 0x00000001,
    }
    #endregion

    #region Enum WriteFlags
    [Flags]
    public enum WriteFlags : uint
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
    #endregion

    #region Enum InitFlags
    [Flags]
    public enum InitFlags : uint
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
    #endregion

    #region Enum RefFlags
    [Flags]
    public enum RefFlags : int
    {
        DEFAULT = 0x00000000,
        /// <summary>
        /// For wimlib_reference_resource_files(), enable shell-style filename globbing.
        /// Ignored by wimlib_reference_resources().
        /// </summary>
        GLOB_ENABLE = 0x00000001,
        /// <summary>
        /// For wimlib_reference_resource_files(), issue an error (WIMLIB_ERR_GLOB_HAD_NO_MATCHES) if a glob did not match any files. 
        /// The default behavior without this flag is to issue no error at that point, but then attempt to open
        /// the glob as a literal path, which of course will fail anyway if no file exists at that path. 
        /// No effect if WIMLIB_REF_FLAG_GLOB_ENABLE is not also specified.
        /// Ignored by wimlib_reference_resources().
        /// </summary>
        GLOB_ERR_ON_NOMATCH = 0x00000002,
    }
    #endregion

    #region Struct CaptureSource
    /// <summary>
    /// An array of these structures is passed to Wim.AddImageMultiSource() to specify the sources from which to create a WIM image. 
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CaptureSource
    {
        /// <summary>
        /// Absolute or relative path to a file or directory on the external filesystem to be included in the image.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FsSourcePath;
        /// <summary>
        /// Destination path in the image.
        /// To specify the root directory of the image, use @"\". 
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string WimTargetPath;
#pragma warning disable 0414
        // private long reserved;
        private uint reserved;

        public CaptureSource(string fsSourcePath, string wimTargetPath)
        {
            FsSourcePath = fsSourcePath;
            WimTargetPath = wimTargetPath;
            reserved = 0;
        }
    };

    /*
    /// <summary>
    /// An array of these structures is passed to Wim.AddImageMultiSource() to specify the sources from which to create a WIM image. 
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CaptureSource
    {
        /// <summary>
        /// Absolute or relative path to a file or directory on the external filesystem to be included in the image.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FsSourcePath;
        /// <summary>
        /// Destination path in the image.
        /// To specify the root directory of the image, use @"\". 
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string WimTargetPath;
#pragma warning disable 0414
        private long reserved;

        public CaptureSource(string fsSourcePath, string wimTargetPath)
        {
            FsSourcePath = fsSourcePath;
            WimTargetPath = wimTargetPath;
            reserved = 0;
        }
    };
    */

    /*
    /// <summary>
    /// An array of these structures is passed to Wim.AddImageMultiSource() to specify the sources from which to create a WIM image. 
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CaptureSource
    {
        /// <summary>
        /// Absolute or relative path to a file or directory on the external filesystem to be included in the image.
        /// </summary>
        private IntPtr FsSourcePathPtr;
        public string FsSourcePath
        {
            get => Marshal.PtrToStringUni(FsSourcePathPtr);
            set => UpdatePtr(ref FsSourcePathPtr, value);
        }
        // [MarshalAs(UnmanagedType.LPWStr)]
        // public string FsSourcePath;
        /// <summary>
        /// Destination path in the image.
        /// To specify the root directory of the image, use @"\". 
        /// </summary>
        private IntPtr WimTargetPathPtr;
        public string WimTargetPath
        {
            get => Marshal.PtrToStringUni(WimTargetPathPtr);
            set => UpdatePtr(ref WimTargetPathPtr, value);
        }
        //[MarshalAs(UnmanagedType.LPWStr)]
        // public string WimTargetPath;
#pragma warning disable 0414
        private long reserved;

        #region Free
        internal void Free()
        {
            FreePtr(ref FsSourcePathPtr);
            FreePtr(ref WimTargetPathPtr);
        }

        internal void FreePtr(ref IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
            ptr = IntPtr.Zero;
        }

        internal void UpdatePtr(ref IntPtr ptr, string str)
        {
            FreePtr(ref ptr);
            ptr = Marshal.StringToHGlobalUni(str);
        }
        #endregion
    };
    */
    #endregion

    #region Struct WimInfo
    [StructLayout(LayoutKind.Sequential)]
    public struct WimInfo
    {
        /// <summary>
        /// The globally unique identifier for this WIM.  (Note: all parts of a split WIM normally have identical GUIDs.)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Guid;
        /// <summary>
        /// The number of images in this WIM file.
        /// </summary>
        public uint ImageCount;
        /// <summary>
        /// The 1-based index of the bootable image in this WIM file, or 0 if no image is bootable.
        /// </summary>
        public uint BootIndex;
        /// <summary>
        /// The version of the WIM file format used in this WIM file.
        /// </summary>
        public uint WimVersion;
        /// <summary>
        /// The default compression chunk size of resources in this WIM file.
        /// </summary>
        public uint ChunkSize;
        /// <summary>
        /// For split WIMs, the 1-based index of this part within the split WIM; otherwise 1.
        /// </summary>
        public ushort PartNumber;
        /// <summary>
        /// For split WIMs, the total number of parts in the split WIM; otherwise 1.
        /// </summary>
        public ushort TotalParts;
        /// <summary>
        /// The default compression type of resources in this WIM file, as WimLibCompressionType enum.
        /// </summary>
        public CompressionType CompressionType => (CompressionType)CompressionTypeInt;
        private int CompressionTypeInt;
        /// <summary>
        /// The size of this WIM file in bytes, excluding the XML data and integrity table.
        /// </summary>
        public ulong TotalBytes;
        /// <summary>
        /// Bit 0 - 9 : Information Flags
        /// Bit 10 - 31 : Reserved
        /// </summary>
        private uint bitFlag;
        /// <summary>
        /// 1 iff this WIM file has an integrity table.
        /// </summary>
        public bool HasIntegrityTable => NativeMethods.GetBitField(bitFlag, 0);
        /// <summary>
        /// 1 iff this info struct is for a ::WIMStruct that has a backing file.
        /// </summary>
        public bool OpenedFromFile => NativeMethods.GetBitField(bitFlag, 1);
        /// <summary>
        /// 1 iff this WIM file is considered readonly for any reason (e.g. the
        /// "readonly" header flag is set, or this is part of a split WIM, or
        /// filesystem permissions deny writing)
        /// </summary>
        public bool IsReadonly => NativeMethods.GetBitField(bitFlag, 2);
        /// <summary>
        /// 1 iff the "reparse point fix" flag is set in this WIM's header
        /// </summary>
        public bool HasRpfix => NativeMethods.GetBitField(bitFlag, 3);
        /// <summary>
        /// 1 iff the "readonly" flag is set in this WIM's header
        /// </summary>
        public bool IsMarkedReadonly => NativeMethods.GetBitField(bitFlag, 4);
        /// <summary>
        /// 1 iff the "spanned" flag is set in this WIM's header
        /// </summary>
        public bool Spanned => NativeMethods.GetBitField(bitFlag, 5);
        /// <summary>
        /// 1 iff the "write in progress" flag is set in this WIM's header
        /// </summary>
        public bool WriteInProgress => NativeMethods.GetBitField(bitFlag, 6);
        /// <summary>
        /// 1 iff the "metadata only" flag is set in this WIM's header
        /// </summary>
        public bool MetadataOnly => NativeMethods.GetBitField(bitFlag, 7);
        /// <summary>
        /// 1 iff the "resource only" flag is set in this WIM's header
        /// </summary>
        public bool ResourceOnly => NativeMethods.GetBitField(bitFlag, 8);
        /// <summary>
        /// 1 iff this WIM file is pipable (see ::WIMLIB_WRITE_FLAG_PIPABLE).
        /// </summary>
        public bool Pipable => NativeMethods.GetBitField(bitFlag, 9);

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        private uint[] reserved;
    }
    #endregion

    #region Struct UpdateCommand 
    [Flags]
    public enum UpdateOp : uint
    {
        /// <summary>
        /// Add a new file or directory tree to the image.
        /// </summary>
        ADD = 0,
        /// <summary>
        /// Delete a file or directory tree from the image.
        /// </summary>
        DELETE = 1,
        /// <summary>
        /// Rename a file or directory tree in the image.
        /// </summary>
        RENAME = 2,
    };

    #region UpdateCommand
    public class UpdateCommand : IDisposable
    {
        #region Struct UpdateCommand
        internal UpdateCommand32 Cmd32;
        internal UpdateCommand64 Cmd64;
        public static int Size
        {
            get
            {
                switch (IntPtr.Size)
                {
                    case 4:
                        return Marshal.SizeOf(typeof(UpdateCommand32));
                    case 8:
                        return Marshal.SizeOf(typeof(UpdateCommand64));
                    default:
                        throw new PlatformNotSupportedException();
                }
            }
        }
        #endregion

        #region Properties
        public UpdateOp Op
        {
            get
            {
                switch (IntPtr.Size)
                {
                    case 4:
                        return Cmd32.Op;
                    case 8:
                        return Cmd64.Op;
                    default:
                        throw new PlatformNotSupportedException();
                }
            }
        }

        public UpdateAdd AddCommand
        {
            get
            {
                switch (IntPtr.Size)
                {
                    case 4:
                        return new UpdateAdd(Cmd32.AddFsSourcePath, Cmd32.AddWimTargetPath, Cmd32.AddConfigFile, Cmd32.AddFlags);
                    case 8:
                        return new UpdateAdd(Cmd64.AddFsSourcePath, Cmd64.AddWimTargetPath, Cmd64.AddConfigFile, Cmd64.AddFlags);
                    default:
                        throw new PlatformNotSupportedException();
                }
            }
        }

        public UpdateDelete DeleteCommand
        {
            get
            {
                switch (IntPtr.Size)
                {
                    case 4:
                        return new UpdateDelete(Cmd32.DelWimPath, Cmd32.DeleteFlags);
                    case 8:
                        return new UpdateDelete(Cmd64.DelWimPath, Cmd64.DeleteFlags);
                    default:
                        throw new PlatformNotSupportedException();
                }
            }
        }

        public UpdateRename RenameCommand
        {
            get
            {
                switch (IntPtr.Size)
                {
                    case 4:
                        return new UpdateRename(Cmd32.RenWimSourcePath, Cmd32.RenWimTargetPath);
                    case 8:
                        return new UpdateRename(Cmd64.RenWimSourcePath, Cmd64.RenWimTargetPath);
                    default:
                        throw new PlatformNotSupportedException();
                }
            }
        }
        #endregion

        #region Constructor in private
        private UpdateCommand() { }
        #endregion

        #region Disposable Pattern
        ~UpdateCommand()
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
                Cmd32.Free();
                Cmd64.Free();
            }
        }
        #endregion

        #region AddCommand, DeleteCommand, RenameCommand
        public class UpdateAdd
        {
            /// <summary>
            /// Filesystem path to the file or directory tree to add.
            /// </summary>
            public string FsSourcePath;
            /// <summary>
            /// Destination path in the image.  To specify the root directory of the image, use WIMLIB_WIM_ROOT_PATH.
            /// </summary>
            public string WimTargetPath;
            /// <summary>
            /// Path to capture configuration file to use, or null if not specified.
            /// </summary>
            public string ConfigFile;
            /// <summary>
            /// Bitwise OR of WimLibAddFlags.
            /// </summary>
            public AddFlags AddFlags;

            public UpdateAdd(string fsSourcePath, string wimTargetPath, string configFile, AddFlags addFlags)
            {
                FsSourcePath = fsSourcePath;
                WimTargetPath = wimTargetPath;
                ConfigFile = configFile;
                AddFlags = addFlags;
            }
        }

        public class UpdateDelete
        {
            /// <summary>
            /// The path to the file or directory within the image to delete.
            /// </summary>
            public string WimPath;
            /// <summary>
            /// Bitwise OR of WimLibDeleteFlags.
            /// </summary>
            public DeleteFlags DeleteFlags;

            public UpdateDelete(string wimPath, DeleteFlags deleteFlags)
            {
                WimPath = wimPath;
                DeleteFlags = deleteFlags;
            }
        }

        public class UpdateRename
        {
            /// <summary>
            /// The path to the source file or directory within the image.
            /// </summary>
            public string WimSourcePath;
            /// <summary>
            /// The path to the destination file or directory within the image.
            /// </summary>
            public string WimTargetPath;

            public UpdateRename(string wimSourcePath, string wimTargetPath)
            {
                WimSourcePath = wimSourcePath;
                WimTargetPath = wimTargetPath;
            }
        }

        public static UpdateCommand Add(string fsSourcePath, string wimTargetPath, string configFile, AddFlags addFlags)
        {
            UpdateCommand cmd = new UpdateCommand();
            switch (IntPtr.Size)
            {
                case 4:
                    cmd.Cmd32 = new UpdateCommand32()
                    {
                        Op = UpdateOp.ADD,
                        AddFsSourcePath = fsSourcePath,
                        AddWimTargetPath = wimTargetPath,
                        AddConfigFile = configFile,
                        AddFlags = addFlags,
                    };
                    break;
                case 8:
                    cmd.Cmd64 = new UpdateCommand64()
                    {
                        Op = UpdateOp.ADD,
                        AddFsSourcePath = fsSourcePath,
                        AddWimTargetPath = wimTargetPath,
                        AddConfigFile = configFile,
                        AddFlags = addFlags,
                    };
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }

            return cmd;
        }

        public static UpdateCommand Delete(string wimPath, DeleteFlags deleteFlags)
        {
            UpdateCommand cmd = new UpdateCommand();
            switch (IntPtr.Size)
            {
                case 4:
                    cmd.Cmd32 = new UpdateCommand32()
                    {
                        Op = UpdateOp.DELETE,
                        DelWimPath = wimPath,
                        DeleteFlags = deleteFlags,
                    };
                    break;
                case 8:
                    cmd.Cmd64 = new UpdateCommand64()
                    {
                        Op = UpdateOp.DELETE,
                        DelWimPath = wimPath,
                        DeleteFlags = deleteFlags,
                    };
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }

            return cmd;
        }

        public static UpdateCommand Rename(string wimSourcePath, string wimTargetPath)
        {
            UpdateCommand cmd = new UpdateCommand();
            switch (IntPtr.Size)
            {
                case 4:
                    cmd.Cmd32 = new UpdateCommand32()
                    {
                        Op = UpdateOp.RENAME,
                        RenWimSourcePath = wimSourcePath,
                        RenWimTargetPath = wimTargetPath,
                    };
                    break;
                case 8:
                    cmd.Cmd64 = new UpdateCommand64()
                    {
                        Op = UpdateOp.RENAME,
                        RenWimSourcePath = wimSourcePath,
                        RenWimTargetPath = wimTargetPath,
                    };
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }

            return cmd;
        }
        #endregion
    }
    #endregion

    #region UpdateCommand32
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct UpdateCommand32
    {
        [FieldOffset(0)]
        public UpdateOp Op;

        #region UpdateAddCommand
        /// <summary>
        /// Filesystem path to the file or directory tree to add.
        /// </summary>
        [FieldOffset(4)]
        private IntPtr AddFsSourcePathPtr;
        public string AddFsSourcePath
        {
            get => Marshal.PtrToStringUni(AddFsSourcePathPtr);
            set => UpdatePtr(ref AddFsSourcePathPtr, value);
        }
        /// <summary>
        /// Destination path in the image.  To specify the root directory of the image, use WIMLIB_WIM_ROOT_PATH.
        /// </summary>
        [FieldOffset(8)]
        private IntPtr AddWimTargetPathPtr;
        public string AddWimTargetPath
        {
            get => Marshal.PtrToStringUni(AddWimTargetPathPtr);
            set => UpdatePtr(ref AddWimTargetPathPtr, value);
        }
        /// <summary>
        /// Path to capture configuration file to use, or null if not specified.
        /// </summary>
        [FieldOffset(12)]
        private IntPtr AddConfigFilePtr;
        public string AddConfigFile
        {
            get => Marshal.PtrToStringUni(AddConfigFilePtr);
            set => UpdatePtr(ref AddConfigFilePtr, value);
        }
        /// <summary>
        /// Bitwise OR of WimLibAddFlags.
        /// </summary>
        [FieldOffset(16)]
        public AddFlags AddFlags;
        #endregion

        #region UpdateDeleteCommand
        /// <summary>
        /// The path to the file or directory within the image to delete.
        /// </summary>
        [FieldOffset(4)]
        private IntPtr DelWimPathPtr;
        public string DelWimPath
        {
            get => Marshal.PtrToStringUni(DelWimPathPtr);
            set => UpdatePtr(ref DelWimPathPtr, value);
        }
        /// <summary>
        /// Bitwise OR of WimLibDeleteFlags.
        /// </summary>
        [FieldOffset(8)]
        public DeleteFlags DeleteFlags;
        #endregion

        #region UpdateRenameCommand
        /// <summary>
        /// The path to the source file or directory within the image.
        /// </summary>
        [FieldOffset(4)]
        private IntPtr RenWimSourcePathPtr;
        public string RenWimSourcePath
        {
            get => Marshal.PtrToStringUni(RenWimSourcePathPtr);
            set => UpdatePtr(ref RenWimSourcePathPtr, value);
        }
        /// <summary>
        /// The path to the destination file or directory within the image.
        /// </summary>
        [FieldOffset(8)]
        private IntPtr RenWimTargetPathPtr;
        public string RenWimTargetPath
        {
            get => Marshal.PtrToStringUni(RenWimTargetPathPtr);
            set => UpdatePtr(ref RenWimTargetPathPtr, value);
        }
        /// <summary>
        /// Reserved; set to 0. 
        /// </summary>
        [FieldOffset(12)]
        private int RenameFlags;
        #endregion

        #region Free
        public void Free()
        {
            FreePtr(ref AddFsSourcePathPtr);
            FreePtr(ref AddWimTargetPathPtr);
            FreePtr(ref AddConfigFilePtr);
            FreePtr(ref DelWimPathPtr);
            FreePtr(ref RenWimSourcePathPtr);
            FreePtr(ref RenWimTargetPathPtr);
        }

        internal void FreePtr(ref IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
            ptr = IntPtr.Zero;
        }

        internal void UpdatePtr(ref IntPtr ptr, string str)
        {
            FreePtr(ref ptr);
            ptr = Marshal.StringToHGlobalUni(str);
        }
        #endregion

        #region Convert
        public UpdateCommand Convert()
        {
            switch (Op)
            {
                case UpdateOp.ADD:
                    return UpdateCommand.Add(AddFsSourcePath, AddWimTargetPath, AddConfigFile, AddFlags);
                case UpdateOp.DELETE:
                    return UpdateCommand.Delete(DelWimPath, DeleteFlags);
                case UpdateOp.RENAME:
                    return UpdateCommand.Rename(RenWimSourcePath, RenWimTargetPath);
                default:
                    throw new InvalidOperationException("Internal Logic Error at UpdateCommand32.Convert()");
            }
        }
        #endregion
    }
    #endregion

    #region UpdateCommand64
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct UpdateCommand64
    {
        [FieldOffset(0)]
        public UpdateOp Op;

        #region UpdateAddCommand
        /// <summary>
        /// Filesystem path to the file or directory tree to add.
        /// </summary>
        [FieldOffset(8)]
        private IntPtr AddFsSourcePathPtr;
        public string AddFsSourcePath
        {
            get => Marshal.PtrToStringUni(AddFsSourcePathPtr);
            set => UpdatePtr(ref AddFsSourcePathPtr, value);
        }
        /// <summary>
        /// Destination path in the image.  To specify the root directory of the image, use WIMLIB_WIM_ROOT_PATH.
        /// </summary>
        [FieldOffset(16)]
        private IntPtr AddWimTargetPathPtr;
        public string AddWimTargetPath
        {
            get => Marshal.PtrToStringUni(AddWimTargetPathPtr);
            set => UpdatePtr(ref AddWimTargetPathPtr, value);
        }
        /// <summary>
        /// Path to capture configuration file to use, or null if not specified.
        /// </summary>
        [FieldOffset(24)]
        private IntPtr AddConfigFilePtr;
        public string AddConfigFile
        {
            get => Marshal.PtrToStringUni(AddConfigFilePtr);
            set => UpdatePtr(ref AddConfigFilePtr, value);
        }
        /// <summary>
        /// Bitwise OR of WimLibAddFlags.
        /// </summary>
        [FieldOffset(32)]
        public AddFlags AddFlags;
        #endregion

        #region UpdateDeleteCommand
        /// <summary>
        /// The path to the file or directory within the image to delete.
        /// </summary>
        [FieldOffset(8)]
        private IntPtr DelWimPathPtr;
        public string DelWimPath
        {
            get => Marshal.PtrToStringUni(DelWimPathPtr);
            set => UpdatePtr(ref DelWimPathPtr, value);
        }
        /// <summary>
        /// Bitwise OR of WimLibDeleteFlags.
        /// </summary>
        [FieldOffset(16)]
        public DeleteFlags DeleteFlags;
        #endregion

        #region UpdateRenameCommand
        /// <summary>
        /// The path to the source file or directory within the image.
        /// </summary>
        [FieldOffset(8)]
        private IntPtr RenWimSourcePathPtr;
        public string RenWimSourcePath
        {
            get => Marshal.PtrToStringUni(RenWimSourcePathPtr);
            set => UpdatePtr(ref RenWimSourcePathPtr, value);
        }
        /// <summary>
        /// The path to the destination file or directory within the image.
        /// </summary>
        [FieldOffset(16)]
        private IntPtr RenWimTargetPathPtr;
        public string RenWimTargetPath
        {
            get => Marshal.PtrToStringUni(RenWimTargetPathPtr);
            set => UpdatePtr(ref RenWimTargetPathPtr, value);
        }
        /// <summary>
        /// Reserved; set to 0. 
        /// </summary>
        [FieldOffset(24)]
        private int RenameFlags;
        #endregion

        #region Free
        public void Free()
        {
            FreePtr(ref AddFsSourcePathPtr);
            FreePtr(ref AddWimTargetPathPtr);
            FreePtr(ref AddConfigFilePtr);
            FreePtr(ref DelWimPathPtr);
            FreePtr(ref RenWimSourcePathPtr);
            FreePtr(ref RenWimTargetPathPtr);
        }

        internal void FreePtr(ref IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
            ptr = IntPtr.Zero;
        }

        internal void UpdatePtr(ref IntPtr ptr, string str)
        {
            FreePtr(ref ptr);
            ptr = Marshal.StringToHGlobalUni(str);
        }
        #endregion

        #region Convert
        public UpdateCommand Convert()
        {
            switch (Op)
            {
                case UpdateOp.ADD:
                    return UpdateCommand.Add(AddFsSourcePath, AddWimTargetPath, AddConfigFile, AddFlags);
                case UpdateOp.DELETE:
                    return UpdateCommand.Delete(DelWimPath, DeleteFlags);
                case UpdateOp.RENAME:
                    return UpdateCommand.Rename(RenWimSourcePath, RenWimTargetPath);
                default:
                    throw new InvalidOperationException("Internal Logic Error at UpdateCommand64.Convert()");
            }
        }
        #endregion
    }
    #endregion
    #endregion

    #region Struct DirEntry
    /// <summary>
    /// Structure passed to the wimlib_iterate_dir_tree() callback function.
    /// Roughly, the information about a "file" in the WIM image --- but really a
    /// directory entry ("dentry") because hard links are allowed.  The
    /// hard_link_group_id field can be used to distinguish actual file inodes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DirEntryBase
    {
        /// <summary>
        /// Name of the file, or NULL if this file is unnamed. Only the root directory of an image will be unnamed.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FileName;
        /// <summary>
        /// 8.3 name (or "DOS name", or "short name") of this file; or NULL if this file has no such name.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string DosName;
        /// <summary>
        /// Full path to this file within the image.  Path separators will be ::WIMLIB_WIM_PATH_SEPARATOR.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FullPath;
        /// <summary>
        /// Depth of this directory entry, where 0 is the root, 1 is the root's children, ..., etc.
        /// </summary>
        public ulong Depth => DepthVal.ToUInt64();
        private UIntPtr DepthVal; // size_t
        /// <summary>
        /// Pointer to the security descriptor for this file, in Windows 
        /// SECURITY_DESCRIPTOR_RELATIVE format, or NULL if this file has no
        /// security descriptor.
        /// </summary>
        public byte[] SecurityDescriptor
        {
            get
            {
                byte[] buf = new byte[SecurityDescriptorSize];
                Marshal.Copy(SecurityDescriptorPtr, buf, 0, (int)SecurityDescriptorSizeVal.ToUInt32());
                return buf;
            }
        }
        public IntPtr SecurityDescriptorPtr;
        /// <summary>
        /// Size of the above security descriptor, in bytes. 
        /// </summary>
        public ulong SecurityDescriptorSize => SecurityDescriptorSizeVal.ToUInt64();
        private UIntPtr SecurityDescriptorSizeVal; // size_t
        /// <summary>
        /// File attributes, such as whether the file is a directory or not.
        /// These are the "standard" Windows FILE_ATTRIBUTE_* values, although in
        /// wimlib.h they are defined as WIMLIB_FILE_ATTRIBUTE_* for convenience
        /// on other platforms.
        /// </summary>
        public FileAttribute Attributes;
        /// <summary>
        /// If the file is a reparse point (FILE_ATTRIBUTE_REPARSE_POINT set in
        /// the attributes), this will give the reparse tag.  This tells you
        /// whether the reparse point is a symbolic link, junction point, or some
        /// other, more unusual kind of reparse point.
        /// </summary>
        public ReparseTag ReparseTag;
        /// <summary>
        /// Number of links to this file's inode (hard links).
        ///
        /// Currently, this will always be 1 for directories.  However, it can be
        /// greater than 1 for nondirectory files.
        /// </summary>
        public uint NumLinks;
        /// <summary>
        /// Number of named data streams this file has.  Normally 0.
        /// </summary>
        public uint NumNamedStreams;
        /// <summary>
        /// A unique identifier for this file's inode.  However, as a special
        /// case, if the inode only has a single link (@p num_links == 1), this
        /// value may be 0.
        ///
        /// Note: if a WIM image is captured from a filesystem, this value is not
        /// guaranteed to be the same as the original number of the inode on the
        /// filesystem.
        /// </summary>
        public ulong HardLinkGroupId;
        /// <summary>
        /// Time this file was created.
        /// </summary>
        public DateTime CreationTime => CreationTimeVal.ToDateTime(CreationTimeHigh);
        private WimTimeSpec CreationTimeVal;
        /// <summary>
        /// Time this file was last written to.
        /// </summary>
        public DateTime LastWriteTime => LastWriteTimeVal.ToDateTime(LastWriteTimeHigh);
        private WimTimeSpec LastWriteTimeVal;
        /// <summary>
        /// Time this file was last accessed.
        /// </summary>
        public DateTime LastAccessTime => LastAccessTimeVal.ToDateTime(LastAccessTimeHigh);
        private WimTimeSpec LastAccessTimeVal;
        /// <summary>
        /// The UNIX user ID of this file.  This is a wimlib extension.
        ///
        /// This field is only valid if @p unix_mode != 0.
        /// </summary>
        public uint UnixUserId;
        /// <summary>
        /// The UNIX group ID of this file.  This is a wimlib extension.
        ///
        /// This field is only valid if @p unix_mode != 0.
        /// </summary>
        public uint UnixGroupId;
        /// <summary>
        /// The UNIX mode of this file.  This is a wimlib extension.
        ///
        /// If this field is 0, then @p unix_uid, @p unix_gid, @p unix_mode, and
        /// @p unix_rdev are all unknown (fields are not present in the WIM
        /// image).
        /// </summary>
        public uint UnixMode;
        /// <summary>
        /// The UNIX device ID (major and minor number) of this file.  This is a
        /// wimlib extension.
        ///
        /// This field is only valid if @p unix_mode != 0.
        /// </summary>
        public uint UnixRootDevice;
        /// <summary>
        /// The object ID of this file, if any.  Only valid if object_id.object_id is not all zeroes.
        /// </summary>
        public WimObjectId ObjectId;
        private int CreationTimeHigh;
        private int LastWriteTimeHigh;
        private int LastAccessTimeHigh;
        private int Reserved2;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private ulong[] Reserved;
    }

    public struct DirEntry
    {
        /// <summary>
        /// Name of the file, or NULL if this file is unnamed. Only the root directory of an image will be unnamed.
        /// </summary>
        public string FileName;
        /// <summary>
        /// 8.3 name (or "DOS name", or "short name") of this file; or NULL if this file has no such name.
        /// </summary>
        public string DosName;
        /// <summary>
        /// Full path to this file within the image.  Path separators will be ::WIMLIB_WIM_PATH_SEPARATOR.
        /// </summary>
        public string FullPath;
        /// <summary>
        /// Depth of this directory entry, where 0 is the root, 1 is the root's children, ..., etc.
        /// </summary>
        public ulong Depth;
        /// <summary>
        /// Pointer to the security descriptor for this file, in Windows 
        /// SECURITY_DESCRIPTOR_RELATIVE format, or NULL if this file has no
        /// security descriptor.
        /// </summary>
        public byte[] SecurityDescriptor;
        /// <summary>
        /// File attributes, such as whether the file is a directory or not.
        /// These are the "standard" Windows FILE_ATTRIBUTE_* values, although in
        /// wimlib.h they are defined as WIMLIB_FILE_ATTRIBUTE_* for convenience
        /// on other platforms.
        /// </summary>
        public FileAttribute Attributes;
        /// <summary>
        /// If the file is a reparse point (FILE_ATTRIBUTE_REPARSE_POINT set in
        /// the attributes), this will give the reparse tag.  This tells you
        /// whether the reparse point is a symbolic link, junction point, or some
        /// other, more unusual kind of reparse point.
        /// </summary>
        public ReparseTag ReparseTag;
        /// <summary>
        /// Number of links to this file's inode (hard links).
        ///
        /// Currently, this will always be 1 for directories.  However, it can be
        /// greater than 1 for nondirectory files.
        /// </summary>
        public uint NumLinks;
        /// <summary>
        /// Number of named data streams this file has.  Normally 0.
        /// </summary>
        public uint NumNamedStreams;
        /// <summary>
        /// A unique identifier for this file's inode.  However, as a special
        /// case, if the inode only has a single link (@p num_links == 1), this
        /// value may be 0.
        ///
        /// Note: if a WIM image is captured from a filesystem, this value is not
        /// guaranteed to be the same as the original number of the inode on the
        /// filesystem.
        /// </summary>
        public ulong HardLinkGroupId;
        /// <summary>
        /// Time this file was created.
        /// </summary>
        public DateTime CreationTime;
        /// <summary>
        /// Time this file was last written to.
        /// </summary>
        public DateTime LastWriteTime;
        /// <summary>
        /// Time this file was last accessed.
        /// </summary>
        public DateTime LastAccessTime;
        /// <summary>
        /// The UNIX user ID of this file.  This is a wimlib extension.
        ///
        /// This field is only valid if @p unix_mode != 0.
        /// </summary>
        public uint UnixUserId;
        /// <summary>
        /// The UNIX group ID of this file.  This is a wimlib extension.
        ///
        /// This field is only valid if @p unix_mode != 0.
        /// </summary>
        public uint UnixGroupId;
        /// <summary>
        /// The UNIX mode of this file.  This is a wimlib extension.
        ///
        /// If this field is 0, then @p unix_uid, @p unix_gid, @p unix_mode, and
        /// @p unix_rdev are all unknown (fields are not present in the WIM
        /// image).
        /// </summary>
        public uint UnixMode;
        /// <summary>
        /// The UNIX device ID (major and minor number) of this file.  This is a
        /// wimlib extension.
        ///
        /// This field is only valid if @p unix_mode != 0.
        /// </summary>
        public uint UnixRootDevice;
        /// <summary>
        /// The object ID of this file, if any.  Only valid if object_id.object_id is not all zeroes.
        /// </summary>
        public WimObjectId ObjectId;
        /// <summary>
        /// Variable-length array of streams that make up this file.
        ///
        /// The first entry will always exist and will correspond to the unnamed
        /// data stream (default file contents), so it will have <c>stream_name
        /// == NULL</c>.  Alternatively, for reparse point files, the first entry
        /// will correspond to the reparse data stream.  Alternatively, for
        /// encrypted files, the first entry will correspond to the encrypted
        /// data.
        ///
        /// Then, following the first entry, there be @p num_named_streams
        /// additional entries that specify the named data streams, if any, each
        /// of which will have <c>stream_name != NULL</c>.
        /// </summary>
        public StreamEntry[] Streams;
    }

    public enum FileAttribute : uint
    {
        READONLY = 0x00000001,
        HIDDEN = 0x00000002,
        SYSTEM = 0x00000004,
        DIRECTORY = 0x00000010,
        ARCHIVE = 0x00000020,
        DEVICE = 0x00000040,
        NORMAL = 0x00000080,
        TEMPORARY = 0x00000100,
        SPARSE_FILE = 0x00000200,
        REPARSE_POINT = 0x00000400,
        COMPRESSED = 0x00000800,
        OFFLINE = 0x00001000,
        NOT_CONTENT_INDEXED = 0x00002000,
        ENCRYPTED = 0x00004000,
        VIRTUAL = 0x00010000,
    }

    public enum ReparseTag : uint
    {
        RESERVED_ZERO = 0x00000000,
        RESERVED_ONE = 0x00000001,
        MOUNT_POINT = 0xA0000003,
        HSM = 0xC0000004,
        HSM2 = 0x80000006,
        DRIVER_EXTENDER = 0x80000005,
        SIS = 0x80000007,
        DFS = 0x8000000A,
        DFSR = 0x80000012,
        FILTER_MANAGER = 0x8000000B,
        WOF = 0x80000017,
        SYMLINK = 0xA000000C,
    }
    #endregion

    #region Struct WimTimeSpec
    [StructLayout(LayoutKind.Sequential)]
    internal struct WimTimeSpec
    {
        /// <summary>
        /// Seconds since start of UNIX epoch (January 1, 1970)
        /// </summary>
        public long UnixEpoch => UnixEpochVal.ToInt64();
        private IntPtr UnixEpochVal; // int64_t in 64bit, int32_t in 32bit
        /// <summary>
        /// Nanoseconds (0-999999999)
        /// </summary>
        public int NanoSeconds;

        internal DateTime ToDateTime(int high)
        {
            // C# DateTime has a resolution of 100ns
            DateTime genesis = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            genesis.AddSeconds(this.UnixEpoch);
            genesis.AddTicks(this.NanoSeconds / 100);

            // wimlib provide high 32bit seperately if timespec.tv_sec is only 32bit
            if (IntPtr.Size == 4)
            {
                long high64 = (long)high << 32;
                genesis.AddSeconds(high64);
            }

            return genesis;
        }
    }
    #endregion

    #region Struct WimObjectId
    /// <summary>
    /// Since wimlib v1.9.1: an object ID, which is an extra piece of metadata that
    /// may be associated with a file on NTFS filesystems.  See:
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa363997(v=vs.85).aspx
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WimObjectId
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ObjectId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] BirthVolumeId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] BirthObjectId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] DomainId;
    }
    #endregion

    #region Struct ResourceEntry
    /// <summary>
    /// Information about a "blob", which is a fixed length sequence of binary data.
    /// Each nonempty stream of each file in a WIM image is associated with a blob.
    /// Blobs are deduplicated within a WIM file.
    /// </summary>
    /// <remarks>
    /// TODO: this struct needs to be renamed, and perhaps made into a union since
    /// there are several cases.  I'll try to list them below:
    ///
    /// 1. The blob is "missing", meaning that it is referenced by hash but not
    ///    actually present in the WIM file.  In this case we only know the
    ///    sha1_hash.  This case can only occur with wimlib_iterate_dir_tree(), never
    ///    wimlib_iterate_lookup_table().
    ///
    /// 2. Otherwise we know the sha1_hash, the uncompressed_size, the
    ///    reference_count, and the is_metadata flag.  In addition:
    ///
    ///    A. If the blob is located in a non-solid WIM resource, then we also know
    ///       the compressed_size and offset.
    ///
    ///    B. If the blob is located in a solid WIM resource, then we also know the
    ///       offset, raw_resource_offset_in_wim, raw_resource_compressed_size, and
    ///       raw_resource_uncompressed_size.  But the "offset" is actually the
    ///       offset in the uncompressed solid resource rather than the offset from
    ///       the beginning of the WIM file.
    ///
    ///    C. If the blob is *not* located in any type of WIM resource, then we don't
    ///       know any additional information.
    ///
    /// Unknown or irrelevant fields are left zeroed.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ResourceEntry
    {
        /// <summary>
        /// If this blob is not missing, then this is the uncompressed size of this blob in bytes.
        /// </summary>
        ulong UncompressedSize;
        /// <summary>
        /// If this blob is located in a non-solid WIM resource, then this is the compressed size of that resource. 
        /// </summary>
        ulong CompressedSize;
        /// <summary>
        /// If this blob is located in a non-solid WIM resource, then this is
        /// the offset of that resource within the WIM file containing it.  If
        /// this blob is located in a solid WIM resource, then this is the offset
        /// of this blob within that solid resource when uncompressed.
        /// </summary>
        ulong Offset;
        /// <summary>
        /// The SHA-1 message digest of the blob's uncompressed contents.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] SHA1;
        /// <summary>
        /// If this blob is located in a WIM resource, then this is the part
        /// number of the WIM file containing it.
        /// </summary>
        uint PartNumber;
        /// <summary>
        /// If this blob is not missing, then this is the number of times this
        /// blob is referenced over all images in the WIM.  This number is not
        /// guaranteed to be correct.
        /// </summary>
        uint ReferenceCount;
        /// <summary>
        /// Bit 0 - 6 : Bool Flags
        /// Bit 7 - 31 : Reserved
        /// </summary>
        private uint bitFlag;
        /// <summary>
        /// 1 iff this blob is located in a non-solid compressed WIM resource.
        /// </summary>
        public bool IsCompressed => NativeMethods.GetBitField(bitFlag, 0);
        /// <summary>
        /// 1 iff this blob contains the metadata for an image. 
        /// </summary>
        public bool IsMetadata => NativeMethods.GetBitField(bitFlag, 1);
        public bool IsFree => NativeMethods.GetBitField(bitFlag, 2);
        public bool IsSpanned => NativeMethods.GetBitField(bitFlag, 3);
        /// <summary>
        /// 1 iff a blob with this hash was not found in the blob lookup table
        /// of the ::WIMStruct.  This normally implies a missing call to
        /// wimlib_reference_resource_files() or wimlib_reference_resources().
        /// </summary>
        public bool IsMissing => NativeMethods.GetBitField(bitFlag, 4);
        /// <summary>
        /// 1 iff this blob is located in a solid resource.
        /// </summary>
        public bool Packed => NativeMethods.GetBitField(bitFlag, 5);
        /// <summary>
        /// If this blob is located in a solid WIM resource, then this is the
        /// offset of that solid resource within the WIM file containing it.
        /// </summary>
        public ulong RawResourceOffsetInWim;
        /// <summary>
        /// If this blob is located in a solid WIM resource, then this is the
        /// compressed size of that solid resource.
        /// </summary>
        public ulong RawResourceCompressedSize;
        /// <summary>
        /// If this blob is located in a solid WIM resource, then this is the
        /// uncompressed size of that solid resource.
        /// </summary>
        public ulong RawResourceUncompressedSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        private ulong[] reserved;
    }
    #endregion

    #region Struct StreamEntry
    /// <summary>
    /// Information about a stream of a particular file in the WIM.
    ///
    /// Normally, only WIM images captured from NTFS filesystems will have multiple
    /// streams per file.  In practice, this is a rarely used feature of the
    /// filesystem.
    ///
    /// TODO: the library now explicitly tracks stream types, which allows it to have
    /// multiple unnamed streams (e.g. both a reparse point stream and unnamed data
    /// stream).  However, this isn't yet exposed by wimlib_iterate_dir_tree().
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct StreamEntry
    {
        /// <summary>
        /// Name of the stream, or NULL if the stream is unnamed.
        /// </summary>
        public string StreamName;
        /// <summary>
        /// Info about this stream's data, such as its hash and size if known.
        /// </summary>
        public ResourceEntry Resource;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private ulong[] Reserved;
    }
    #endregion

    #region WimLibException
    public class WimLibException : Exception
    {
        public string ErrorMsg;
        public ErrorCode ErrorCode;

        public WimLibException(ErrorCode errorCode)
            : base($"[{errorCode}] {NativeMethods.GetErrorString(errorCode)}")
        {
            this.ErrorMsg = NativeMethods.GetErrorString(errorCode);
            this.ErrorCode = errorCode;
        }

        public static void CheckWimLibError(ErrorCode ret)
        {
            if (ret != ErrorCode.SUCCESS)
                throw new WimLibException(ret);
        }
    }
    #endregion
}

