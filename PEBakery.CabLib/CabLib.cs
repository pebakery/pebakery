/*
    Pinvoke of cabinet.dll

    Based on https://code.msdn.microsoft.com/vstudio/Programmatically-generate-9f08bf6a/sourcecode?fileId=123713&pathId=1073809333
    Original work by singhal

    Copyright (c) 2014 singhal
    Copyright (c) 2016-2017 Hajin Jang

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.CabLib
{
    [StructLayout(LayoutKind.Sequential)]
    public class CabinetInfo //Cabinet API: "FDCABINETINFO"
    {
        public int cbCabinet;
        public short cFolders;
        public short cFiles;
        public short setID;
        public short iCabinet;
        public int fReserve;
        public int hasprev;
        public int hasnext;
    }

    public class CabExtract : IDisposable
    {
        //If any of these classes end up with a different size to its C equivilent, we end up with crash and burn.
        [StructLayout(LayoutKind.Sequential)]
        private class CabError //Cabinet API: "ERF"
        {
            public int erfOper;
            public int erfType;
            public int fError;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class FdiNotification //Cabinet API: "FDINOTIFICATION"
        {
            public int cb;
            public string psz1;
            public string psz2;
            public string psz3;
            public IntPtr userData;
            public IntPtr hf;
            public short date;
            public short time;
            public short attribs;
            public short setID;
            public short iCabinet;
            public short iFolder;
            public int fdie;
        }

        private enum FdiNotificationType
        {
            CabinetInfo,
            PartialFile,
            CopyFile,
            CloseFileInfo,
            NextCabinet,
            Enumerate
        }

        private class DecompressFile
        {
            public IntPtr Handle { get; set; }
            public string Name { get; set; }
            public bool Found { get; set; }
            public int Length { get; set; }
            public byte[] Data { get; set; }

            public void WriteToFile(string destDir)
            {
                string path = Path.Combine(destDir, Name);
                string parentDir = Path.GetDirectoryName(path);
                if (!Directory.Exists(parentDir) && !parentDir.Equals(string.Empty, StringComparison.Ordinal))
                    Directory.CreateDirectory(parentDir);

                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(Data, 0, Length);
                }
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr FdiMemAllocDelegate(int numBytes);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FdiMemFreeDelegate(IntPtr mem);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr FdiFileOpenDelegate(string fileName, int oflag, int pmode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Int32 FdiFileReadDelegate(
            IntPtr hf,
            [In, Out] [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2, ArraySubType = UnmanagedType.U1)] byte[] buffer,
            int cb);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Int32 FdiFileWriteDelegate(
            IntPtr hf,
            [In] [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2, ArraySubType = UnmanagedType.U1)] byte[] buffer,
            int cb);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Int32 FdiFileCloseDelegate(IntPtr hf);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Int32 FdiFileSeekDelegate(IntPtr hf, int dist, int seektype);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr FdiNotifyDelegate(
            FdiNotificationType fdint,
            [In] [MarshalAs(UnmanagedType.LPStruct)] FdiNotification fdin);

        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "FDICreate", CharSet = CharSet.Ansi)]
        private static extern IntPtr FdiCreate(
            FdiMemAllocDelegate fnMemAlloc,
            FdiMemFreeDelegate fnMemFree,
            FdiFileOpenDelegate fnFileOpen,
            FdiFileReadDelegate fnFileRead,
            FdiFileWriteDelegate fnFileWrite,
            FdiFileCloseDelegate fnFileClose,
            FdiFileSeekDelegate fnFileSeek,
            int cpuType,
            [MarshalAs(UnmanagedType.LPStruct)] CabError erf);

        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "FDIIsCabinet", CharSet = CharSet.Ansi)]
        private static extern bool FdiIsCabinet(
            IntPtr hfdi,
            IntPtr hf,
            [MarshalAs(UnmanagedType.LPStruct)] CabinetInfo cabInfo);

        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "FDIDestroy", CharSet = CharSet.Ansi)]
        private static extern bool FdiDestroy(IntPtr hfdi);

        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "FDICopy", CharSet = CharSet.Ansi)]
        private static extern bool FdiCopy(
            IntPtr hfdi,
            string cabinetName,
            string cabinetPath,
            int flags,
            FdiNotifyDelegate fnNotify,
            IntPtr fnDecrypt,
            IntPtr userData);

        private readonly FdiFileCloseDelegate _fileCloseDelegate;
        private readonly FdiFileOpenDelegate _fileOpenDelegate;
        private readonly FdiFileReadDelegate _fileReadDelegate;
        private readonly FdiFileSeekDelegate _fileSeekDelegate;
        private readonly FdiFileWriteDelegate _fileWriteDelegate;
        private readonly FdiMemAllocDelegate _femAllocDelegate;
        private readonly FdiMemFreeDelegate _memFreeDelegate;

        private readonly CabError _erf;
        private bool _decompressAll;
        private readonly List<DecompressFile> _decompressFiles;
        // _inputData should be stream, but it is said that cabinet.dll traverse _inputData several times
        private readonly byte[] _inputData;

        private IntPtr _hfdi;
        private bool _disposed = false;
        private const int CpuTypeUnknown = -1;

        private const int TRUE = 1;
        private const int FALSE = 0;

        /// <summary>
        /// Constructor recieves cab file's binary data as stream
        /// </summary>
        /// <param name="inputData"></param>
        public CabExtract(Stream stream, bool leaveOpen = false)
        {
            _fileReadDelegate = FileRead;
            _fileOpenDelegate = InputFileOpen;
            _femAllocDelegate = MemAlloc;
            _fileSeekDelegate = FileSeek;
            _memFreeDelegate = MemFree;
            _fileWriteDelegate = FileWrite;
            _fileCloseDelegate = InputFileClose;
            
            _decompressAll = false; // Default value
            _decompressFiles = new List<DecompressFile>();
            _erf = new CabError();
            _hfdi = IntPtr.Zero;

            _inputData = new byte[stream.Length];
            stream.Read(_inputData, 0, _inputData.Length);
            if (leaveOpen == false)
                stream.Close();
        }

        public CabExtract(byte[] inputData, bool leaveOpen = false)
        {
            _fileReadDelegate = FileRead;
            _fileOpenDelegate = InputFileOpen;
            _femAllocDelegate = MemAlloc;
            _fileSeekDelegate = FileSeek;
            _memFreeDelegate = MemFree;
            _fileWriteDelegate = FileWrite;
            _fileCloseDelegate = InputFileClose;
            _inputData = inputData;
            _decompressAll = false; // Default value
            _decompressFiles = new List<DecompressFile>();
            _erf = new CabError();
            _hfdi = IntPtr.Zero;
        }

        private static IntPtr FdiCreate(
            FdiMemAllocDelegate fnMemAlloc,
            FdiMemFreeDelegate fnMemFree,
            FdiFileOpenDelegate fnFileOpen,
            FdiFileReadDelegate fnFileRead,
            FdiFileWriteDelegate fnFileWrite,
            FdiFileCloseDelegate fnFileClose,
            FdiFileSeekDelegate fnFileSeek,
            CabError erf)
        {
            return FdiCreate(fnMemAlloc, fnMemFree, fnFileOpen, fnFileRead, fnFileWrite,
                             fnFileClose, fnFileSeek, CpuTypeUnknown, erf);
        }

        private static bool FdiCopy(
            IntPtr hfdi,
            FdiNotifyDelegate fnNotify)
        {
            return FdiCopy(hfdi, "NOT_USED", "NOT_USED", 0, fnNotify, IntPtr.Zero, IntPtr.Zero);
        }

        private IntPtr FdiContext
        {
            get
            {
                if (_hfdi == IntPtr.Zero)
                {
                    _hfdi = FdiCreate(_femAllocDelegate, _memFreeDelegate, _fileOpenDelegate, _fileReadDelegate, _fileWriteDelegate, _fileCloseDelegate, _fileSeekDelegate, _erf);
                    if (_hfdi == IntPtr.Zero)
                        throw new ApplicationException("Failed to create FDI context.");
                }
                return _hfdi;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_hfdi != IntPtr.Zero)
                {
                    // _streamHandle will be destroyed in FdiDestroy()
                    FdiDestroy(_hfdi);
                    _hfdi = IntPtr.Zero;
                }

                _disposed = true;
            }
        }

        private IntPtr NotifyCallback(FdiNotificationType fdint, FdiNotification fdin)
        {
            switch (fdint)
            {
                case FdiNotificationType.CopyFile:
                    return OutputFileOpen(fdin);
                case FdiNotificationType.CloseFileInfo:
                    return OutputFileClose(fdin);
                default:
                    return IntPtr.Zero;
            }
        }

        private IntPtr InputFileOpen(string fileName, int oflag, int pmode)
        {
            MemoryStream stream = new MemoryStream(_inputData);
            GCHandle gch = GCHandle.Alloc(stream);
            return (IntPtr)gch;
        }

        private int InputFileClose(IntPtr hf)
        {
            StreamFromHandle(hf).Close();
            ((GCHandle)(hf)).Free();
            return 0;
        }

        private IntPtr OutputFileOpen(FdiNotification fdin)
        {
            if (_decompressAll)
            { // Extract all files
                DecompressFile extractFile = new DecompressFile();
                _decompressFiles.Add(extractFile);

                MemoryStream stream = new MemoryStream();
                GCHandle gch = GCHandle.Alloc(stream);
                extractFile.Name = fdin.psz1;
                extractFile.Handle = (IntPtr) gch;

                return extractFile.Handle;
            }
            else
            { // Extract only some files
                DecompressFile extractFile = _decompressFiles.Where(ef => ef.Name == fdin.psz1).SingleOrDefault();
                if (extractFile == null)
                { // Do not extract, not in the decompress list
                    return IntPtr.Zero;
                }
                else
                { // Do extract, found in the decompress List
                    MemoryStream stream = new MemoryStream();
                    GCHandle gch = GCHandle.Alloc(stream);
                    extractFile.Handle = (IntPtr)gch;

                    return extractFile.Handle;
                }
            }
        }

        private IntPtr OutputFileClose(FdiNotification fdin)
        {
            DecompressFile extractFile = _decompressFiles.Where(ef => ef.Handle == fdin.hf).Single();

            using (Stream stream = StreamFromHandle(fdin.hf))
            {
                extractFile.Found = true;
                extractFile.Length = (int)stream.Length;

                if (0 < stream.Length)
                {
                    extractFile.Data = new byte[stream.Length];
                    stream.Position = 0;
                    stream.Read(extractFile.Data, 0, (int)stream.Length);
                }
            }

            return (IntPtr)TRUE;
        }

        private int FileRead(IntPtr hf, byte[] buffer, int cb)
        {
            Stream stream = StreamFromHandle(hf);
            return stream.Read(buffer, 0, cb);
        }

        private int FileWrite(IntPtr hf, byte[] buffer, int cb)
        {
            Stream stream = StreamFromHandle(hf);
            stream.Write(buffer, 0, cb);
            return cb;
        }

        private static Stream StreamFromHandle(IntPtr hf)
        {
            return (Stream)((GCHandle)hf).Target;
        }

        private IntPtr MemAlloc(int cb)
        {
            return Marshal.AllocHGlobal(cb);
        }

        private void MemFree(IntPtr mem)
        {
            Marshal.FreeHGlobal(mem);
        }

        private int FileSeek(IntPtr hf, int dist, int seekType)
        {
            Stream stream = StreamFromHandle(hf);
            return (int)stream.Seek(dist, (SeekOrigin)seekType);
        }

        /// <summary>
        /// Extract single file from cabinet into memory
        /// </summary>
        /// <param name="fileToExtract"></param>
        /// <param name="outputData"></param>
        /// <param name="outputLength"></param>
        /// <returns></returns>
        public bool ExtractSingleFile(string fileToExtract, out byte[] outputData, out int outputLength)
        {
            if (_disposed)
                throw new ObjectDisposedException("CabExtract");

            DecompressFile fileToDecompress = new DecompressFile
            {
                Found = false,
                Name = fileToExtract
            };

            _decompressAll = false;
            _decompressFiles.Add(fileToDecompress);

            FdiCopy(FdiContext, NotifyCallback);

            if (fileToDecompress.Found)
            {
                outputData = fileToDecompress.Data;
                outputLength = fileToDecompress.Length;
                _decompressFiles.Remove(fileToDecompress);
                return true;
            }

            outputData = null;
            outputLength = 0;
            return false;
        }

        /// <summary>
        /// Extract single file from cabinet into file
        /// </summary>
        /// <param name="fileToExtract"></param>
        /// <param name="destDir"></param>
        /// <returns></returns>
        public bool ExtractSingleFile(string fileToExtract, string destDir)
        {
            if (_disposed)
                throw new ObjectDisposedException("CabExtract");

            DecompressFile fileToDecompress = new DecompressFile
            {
                Found = false,
                Name = fileToExtract
            };

            _decompressAll = false;
            _decompressFiles.Add(fileToDecompress);

            FdiCopy(FdiContext, NotifyCallback);

            if (fileToDecompress.Found)
            {
                fileToDecompress.WriteToFile(destDir);
                _decompressFiles.Remove(fileToDecompress);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extract all files from cabinet into directory
        /// </summary>
        /// <param name="destDir"></param>
        /// <returns></returns>
        public bool ExtractAll(string destDir, out List<string> extractedList)
        {
            if (_disposed)
                throw new ObjectDisposedException("CabExtract");

            extractedList = new List<string>();

            _decompressAll = true;
            _decompressFiles.Clear();

            FdiCopy(FdiContext, NotifyCallback);

            if (!(0 < _decompressFiles.Count))
                return false; // No file extracted
            else
            {
                for (int i = 0; i < _decompressFiles.Count; i++)
                {
                    extractedList.Add(_decompressFiles[i].Name);
                    _decompressFiles[i].WriteToFile(destDir);
                }
                return true;
            }
        }

        public bool IsCabinetFile()
        {
            GetCabinetInfo(out bool isCabinet);
            return isCabinet;
        }

        public static bool IsCabinetFile(Stream stream)
        {
            using (CabExtract decomp = new CabExtract(stream))
            {
                return decomp.IsCabinetFile();
            }
        }

        public CabinetInfo GetCabinetInfo(out bool isCabinet)
        {
            if (_disposed)
                throw new ObjectDisposedException("CabExtract");

            MemoryStream ms = new MemoryStream(_inputData);
            GCHandle gch = GCHandle.Alloc(ms);

            try
            {
                CabinetInfo info = new CabinetInfo();
                isCabinet = FdiIsCabinet(FdiContext, (IntPtr)gch, info);
                return info;
            }
            finally
            {
                gch.Free();
                ms.Close();
            }
        }

        public static CabinetInfo GetCabinetInfo(Stream stream)
        {
            using (CabExtract decomp = new CabExtract(stream))
            {
                return decomp.GetCabinetInfo(out bool isCabinet);
            }
        }
    }
}
