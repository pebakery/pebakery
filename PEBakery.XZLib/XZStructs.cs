/*
    Derived from liblzma header files (Public Domain)

    C# Wrapper written by Hajin Jang
    Copyright (C) 2018 Hajin Jang

    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

/* This file includes definition from external C library.
 * This lines supresses error and warning from code analyzer, due to this file's C-style naming and others.
 */
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
#pragma warning disable 169

namespace PEBakery.XZLib
{
    #region Struct LzmaStream
    /// <summary>
    /// Passing data to and from liblzma
    /// </summary>
    /// <remarks>
    /// The lzma_stream structure is used for
    ///  - passing pointers to input and output buffers to liblzma;
    ///  - defining custom memory hander functions; and
    ///  - holding a pointer to coder-specific internal data structures.
    ///
    /// Typical usage:
    ///
    ///  - After allocating lzma_stream (on stack or with malloc()), it must be
    ///    initialized to LZMA_STREAM_INIT (see LZMA_STREAM_INIT for details).
    ///
    ///  - Initialize a coder to the lzma_stream, for example by using
    ///    lzma_easy_encoder() or lzma_auto_decoder(). Some notes:
    ///      - In contrast to zlib, strm->next_in and strm->next_out are
    ///        ignored by all initialization functions, thus it is safe
    ///        to not initialize them yet.
    ///      - The initialization functions always set strm->total_in and
    ///        strm->total_out to zero.
    ///      - If the initialization function fails, no memory is left allocated
    ///        that would require freeing with lzma_end() even if some memory was
    ///        associated with the lzma_stream structure when the initialization
    ///        function was called.
    ///
    ///  - Use lzma_code() to do the actual work.
    ///
    ///  - Once the coding has been finished, the existing lzma_stream can be
    ///    reused. It is OK to reuse lzma_stream with different initialization
    ///    function without calling lzma_end() first. Old allocations are
    ///    automatically freed.
    ///
    ///  - Finally, use lzma_end() to free the allocated memory. lzma_end() never
    ///    frees the lzma_stream structure itself.
    ///
    /// Application may modify the values of total_in and total_out as it wants.
    /// They are updated by liblzma to match the amount of data read and
    /// written but aren't used for anything else except as a possible return
    /// values from lzma_get_progress().
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class LzmaStream
    {
        /// <summary>
        ///  Pointer to the next input byte.
        /// </summary>
        public IntPtr NextIn = IntPtr.Zero;

        /// <summary>
        /// Number of available input bytes in next_in.
        /// </summary>
        public ulong AvailIn
        {
            get => AvailInVal.ToUInt64();
            set => AvailInVal = (UIntPtr) value;
        }
        private UIntPtr AvailInVal = UIntPtr.Zero;

        /// <summary>
        /// Total number of bytes read by liblzma.
        /// </summary>
        public ulong TotalIn = 0;

        /// <summary>
        /// Pointer to the next output position.
        /// </summary>
        public IntPtr NextOut = IntPtr.Zero;

        /// <summary>
        /// Amount of free space in next_out.
        /// </summary>
        public ulong AvailOut
        {
            get => AvailOutVal.ToUInt64();
            set => AvailOutVal = (UIntPtr)value;
        }
        private UIntPtr AvailOutVal = UIntPtr.Zero;

        /// <summary>
        /// Total number of bytes written by liblzma.
        /// </summary>
        public ulong TotalOut = 0;

        /// <summary>
        /// Custom memory allocation functions
        /// </summary>
        /// <remarks>
        /// In most cases this is NULL which makes liblzma use the standard malloc() and free().
        /// </remarks>
        private IntPtr Allocator = IntPtr.Zero;
        /// <summary>
        /// Internal state is not visible to applications.
        /// </summary>
        private IntPtr Internal = IntPtr.Zero;

        /// <summary>
        /// Rerved space to allow possible future extensions without
        /// breaking the ABI. Excluding the initialization of this structure,
        /// you should not touch these, because the names of these variables
        /// may change.
        /// </summary>
        private IntPtr ReservedPtr1 = IntPtr.Zero;
        private IntPtr ReservedPtr2 = IntPtr.Zero;
        private IntPtr ReservedPtr3 = IntPtr.Zero;
        private IntPtr ReservedPtr4 = IntPtr.Zero;
        private ulong ReservedInt1 = 0;
        private ulong ReservedInt2 = 0;
        private UIntPtr ReservedInt3 = UIntPtr.Zero;
        private UIntPtr ReservedInt4 = UIntPtr.Zero;
        private uint ReservedEnum1 = 0;
        private uint ReservedEnum2 = 0;
    }
    #endregion

    #region Struct LzmaStreamFlags
    /// <summary>
    /// Options for encoding/decoding Stream Header and Stream Footer
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class LzmaStreamFlags
    {
        /// <summary>
        /// Stream Flags format version
        /// </summary>
        /// <remarks>
        /// To prevent API and ABI breakages if new features are needed in
        /// Stream Header or Stream Footer, a version number is used to
        /// indicate which fields in this structure are in use. For now,
        /// version must always be zero. With non-zero version, the
        /// lzma_stream_header_encode() and lzma_stream_footer_encode()
        /// will return LZMA_OPTIONS_ERROR.
        ///
        /// lzma_stream_header_decode() and lzma_stream_footer_decode()
        /// will always set this to the lowest value that supports all the
        /// features indicated by the Stream Flags field. The application
        /// must check that the version number set by the decoding functions
        /// is supported by the application. Otherwise it is possible that
        /// the application will decode the Stream incorrectly.
        /// </remarks>
        private uint Version = 0;
        /// <summary>
        /// Backward Size
        /// </summary>
        /// <remarks>
        /// Backward Size must be a multiple of four bytes. In this Stream
        /// format version, Backward Size is the size of the Index field.
        ///
        /// Backward Size isn't actually part of the Stream Flags field, but
        /// it is convenient to include in this structure anyway. Backward
        /// Size is present only in the Stream Footer. There is no need to
        /// initialize backward_size when encoding Stream Header.
        ///
        /// lzma_stream_header_decode() always sets backward_size to
        /// LZMA_VLI_UNKNOWN so that it is convenient to use
        /// lzma_stream_flags_compare() when both Stream Header and Stream
        /// Footer have been decoded.
        /// </remarks>
        public ulong BackwardSize;
        /// <summary>
        /// This indicates the type of the integrity check calculated from 
        /// uncompressed data.
        /// </summary>
        public LzmaCheck Check;

        private uint ReservedEnum1;
        private uint ReservedEnum2;
        private uint ReservedEnum3;
        private uint ReservedEnum4;
        private byte ReservedBool1;
        private byte ReservedBool2;
        private byte ReservedBool3;
        private byte ReservedBool4;
        private byte ReservedBool5;
        private byte ReservedBool6;
        private byte ReservedBool7;
        private byte ReservedBool8;
        private uint ReservedInt1;
        private uint ReservedInt2;
    }
    #endregion

    #region Struct LzmaMt
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class LzmaMt
    {
        /// <summary>
        /// Set this to zero if no flags are wanted.
        ///
        /// No flags are currently supported.
        /// </summary>
        public uint Flags;
        /// <summary>
        /// Number of worker threads to use
        /// </summary>
        public uint Threads;
        /// <summary>
        /// Maximum uncompressed size of a Block
        /// </summary>
        /// <remarks>
        /// The encoder will start a new .xz Block every block_size bytes.
        /// Using LZMA_FULL_FLUSH or LZMA_FULL_BARRIER with lzma_code()
        /// the caller may tell liblzma to start a new Block earlier.
        ///
        /// With LZMA2, a recommended block size is 2-4 times the LZMA2
        /// dictionary size. With very small dictionaries, it is recommended
        /// to use at least 1 MiB block size for good compression ratio, even
        /// if this is more than four times the dictionary size. Note that
        /// these are only recommendations for typical use cases; feel free
        /// to use other values. Just keep in mind that using a block size
        /// less than the LZMA2 dictionary size is waste of RAM.
        ///
        /// Set this to 0 to let liblzma choose the block size depending
        /// on the compression options. For LZMA2 it will be 3*dict_size
        /// or 1 MiB, whichever is more.
        ///
        /// For each thread, about 3 * block_size bytes of memory will be
        /// allocated. This may change in later liblzma versions. If so,
        /// the memory usage will probably be reduced, not increased.
        /// </remarks>
        public ulong BlockSize;
        /// <summary>
        /// Timeout to allow lzma_code() to return early
        /// </summary>
        /// <remarks>
        /// Multithreading can make liblzma to consume input and produce
        /// output in a very bursty way: it may first read a lot of input
        /// to fill internal buffers, then no input or output occurs for
        /// a while.
        ///
        /// In single-threaded mode, lzma_code() won't return until it has
        /// either consumed all the input or filled the output buffer. If
        /// this is done in multithreaded mode, it may cause a call
        /// lzma_code() to take even tens of seconds, which isn't acceptable
        /// in all applications.
        ///
        /// To avoid very long blocking times in lzma_code(), a timeout
        /// (in milliseconds) may be set here. If lzma_code() would block
        /// longer than this number of milliseconds, it will return with
        /// LZMA_OK. Reasonable values are 100 ms or more. The xz command
        /// line tool uses 300 ms.
        ///
        /// If long blocking times are fine for you, set timeout to a special
        /// value of 0, which will disable the timeout mechanism and will make
        /// lzma_code() block until all the input is consumed or the output
        /// buffer has been filled.
        ///
        /// note         Even with a timeout, lzma_code() might sometimes take
        ///              somewhat long time to return. No timing guarantees
        ///              are made.
        /// </remarks>
        public uint TimeOut;
        /// <summary>
        /// Compression preset (level and possible flags)
        /// </summary>
        /// <remarks>
        /// The preset is set just like with lzma_easy_encoder().
        /// The preset is ignored if filters below is non-NULL.
        /// </remarks>
        public uint Preset;
        /// <summary>
        /// Filter chain (alternative to a preset)
        /// </summary>
        /// <remarks>
        /// If this is NULL, the preset above is used. Otherwise the preset
        /// is ignored and the filter chain specified here is used.
        /// </remarks>
        public IntPtr Filters;
        /// <summary>
        /// Integrity check type
        /// </summary>
        /// <remarks>
        /// See check.h for available checks. The xz command line tool
        /// defaults to LZMA_CHECK_CRC64, which is a good choice if you
        /// are unsure.
        /// </remarks>
        public LzmaCheck Check;

        private uint ReservedEnum1;
        private uint ReservedEnum2;
        private uint ReservedEnum3;
        private uint ReservedInt1;
        private uint ReservedInt2;
        private uint ReservedInt3;
        private uint ReservedInt4;
        private ulong ReservedInt5;
        private ulong ReservedInt6;
        private ulong ReservedInt7;
        private ulong ReservedInt8;
        private IntPtr ReservedPtr1;
        private IntPtr ReservedPtr2;
        private IntPtr ReservedPtr3;
        private IntPtr ReservedPtr4;
    }
    #endregion

    #region Struct LzmaFilter
    /// <summary>
    /// Filter options
    /// </summary>
    /// <remarks>
    /// This structure is used to pass Filter ID and a pointer filter's
    /// options to liblzma. A few functions work with a single lzma_filter
    /// structure, while most functions expect a filter chain.
    ///
    /// A filter chain is indicated with an array of lzma_filter structures.
    /// The array is terminated with .id L= LZMA_VLI_UNKNOWN. Thus, the filter
    /// array must have LZMA_FILTERS_MAX + 1 elements (that is, five) to
    /// be able to hold any arbitrary filter chain. This is important when
    /// using lzma_block_header_decode() from block.h, because too small
    /// array would make liblzma write past the end of the filters array.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct LzmaFilter
    {
        /// <summary>
        /// Filter ID
        /// </summary>
        /// <remarks>
        /// Use constants whose name begin with `LZMA_FILTER_' to specify
        /// different filters. In an array of lzma_filter structures, use
        /// LZMA_VLI_UNKNOWN to indicate end of filters.
        /// </remarks>
        public ulong Id;
        /// <summary>
        /// Pointer to filter-specific options structure
        /// </summary>
        /// <remarks>
        /// If the filter doesn't need options, set this to NULL. If id is
        /// set to LZMA_VLI_UNKNOWN, options is ignored, and thus
        /// doesn't need be initialized.
        /// </remarks>
        public IntPtr Options;
    }
    #endregion

    #region Enum LzmaAction
    /// <summary>
    /// The `action' argument for lzma_code()
    /// </summary>
    /// <remarks>
    /// After the first use of LZMA_SYNC_FLUSH, LZMA_FULL_FLUSH, LZMA_FULL_BARRIER,
    /// or LZMA_FINISH, the same `action' must is used until lzma_code() returns
    /// LZMA_STREAM_END. Also, the amount of input (that is, strm->avail_in) must
    /// not be modified by the application until lzma_code() returns
    /// LZMA_STREAM_END. Changing the `action' or modifying the amount of input
    /// will make lzma_code() return LZMA_PROG_ERROR.
    /// </remarks>
    public enum LzmaAction : uint
    {
        /// <summary>
        /// Continue coding
        /// </summary>
        /// <remarks>
        /// Encoder: Encode as much input as possible. Some internal
        /// buffering will probably be done (depends on the filter
        /// chain in use), which causes latency: the input used won't
        /// usually be decodeable from the output of the same
        /// lzma_code() call.
        ///
        /// Decoder: Decode as much input as possible and produce as
        /// much output as possible.
        /// </remarks>
        RUN = 0,
        /// <summary>
        /// Make all the input available at output
        /// </summary>
        /// <remarks>
        /// Normally the encoder introduces some latency.
        /// LZMA_SYNC_FLUSH forces all the buffered data to be
        /// available at output without resetting the internal
        /// state of the encoder. This way it is possible to use
        /// compressed stream for example for communication over
        /// network.
        ///
        /// Only some filters support LZMA_SYNC_FLUSH. Trying to use
        /// LZMA_SYNC_FLUSH with filters that don't support it will
        /// make lzma_code() return LZMA_OPTIONS_ERROR. For example,
        /// LZMA1 doesn't support LZMA_SYNC_FLUSH but LZMA2 does.
        ///
        /// Using LZMA_SYNC_FLUSH very often can dramatically reduce
        /// the compression ratio. With some filters (for example,
        /// LZMA2), fine-tuning the compression options may help
        /// mitigate this problem significantly (for example,
        /// match finder with LZMA2).
        ///
        /// Decoders don't support LZMA_SYNC_FLUSH.
        /// </remarks>
        SYNC_FLUSH = 1,
        /// <summary>
        /// Finish encoding of the current Block
        /// </summary>
        /// <remarks>
        /// All the input data going to the current Block must have
        /// been given to the encoder (the last bytes can still be
        /// pending in *next_in). Call lzma_code() with LZMA_FULL_FLUSH
        /// until it returns LZMA_STREAM_END. Then continue normally
        /// with LZMA_RUN or finish the Stream with LZMA_FINISH.
        ///
        /// This action is currently supported only by Stream encoder
        /// and easy encoder (which uses Stream encoder). If there is
        /// no unfinished Block, no empty Block is created.
        /// </remarks>
        FULL_FLUSH = 2,
        /// <summary>
        /// Finish encoding of the current Block
        /// </summary>
        /// <remarks>
        /// This is like LZMA_FULL_FLUSH except that this doesn't
        /// necessarily wait until all the input has been made
        /// available via the output buffer. That is, lzma_code()
        /// might return LZMA_STREAM_END as soon as all the input
        /// has been consumed (avail_in == 0).
        ///
        /// LZMA_FULL_BARRIER is useful with a threaded encoder if
        /// one wants to split the .xz Stream into Blocks at specific
        /// offsets but doesn't care if the output isn't flushed
        /// immediately. Using LZMA_FULL_BARRIER allows keeping
        /// the threads busy while LZMA_FULL_FLUSH would make
        /// lzma_code() wait until all the threads have finished
        /// until more data could be passed to the encoder.
        ///
        /// With a lzma_stream initialized with the single-threaded
        /// lzma_stream_encoder() or lzma_easy_encoder(),
        /// LZMA_FULL_BARRIER is an alias for LZMA_FULL_FLUSH.
        /// </remarks>
        FULL_BARRIER = 4,
        /// <summary>
        /// Finish the coding operation
        /// </summary>
        /// <remarks>
        /// All the input data must have been given to the encoder
        /// (the last bytes can still be pending in next_in).
        /// Call lzma_code() with LZMA_FINISH until it returns
        /// LZMA_STREAM_END. Once LZMA_FINISH has been used,
        /// the amount of input must no longer be changed by
        /// the application.
        ///
        /// When decoding, using LZMA_FINISH is optional unless the
        /// LZMA_CONCATENATED flag was used when the decoder was
        /// initialized. When LZMA_CONCATENATED was not used, the only
        /// effect of LZMA_FINISH is that the amount of input must not
        /// be changed just like in the encoder.
        /// </remarks>
        FINISH = 3,
    }
    #endregion

    #region Enum LzmaRet
    /// <summary>
    /// Return values used by several functions in liblzma
    /// </summary>
    /// <remarks>
    /// Check the descriptions of specific functions to find out which return
    /// values they can return. With some functions the return values may have
    /// more specific meanings than described here; those differences are
    /// described per-function basis.
    /// </remarks>
    public enum LzmaRet : uint
    {
        /// <summary>
        /// Operation completed successfully
        /// </summary>
        OK = 0,
        /// <summary>
        /// End of stream was reached
        /// </summary>
        /// <remarks>
        /// In encoder, LZMA_SYNC_FLUSH, LZMA_FULL_FLUSH, or
        /// LZMA_FINISH was finished. In decoder, this indicates
        /// that all the data was successfully decoded.
        ///
        /// In all cases, when LZMA_STREAM_END is returned, the last
        /// output bytes should be picked from strm->next_out.
        /// </remarks>
        STREAM_END = 1,
        /// <summary>
        /// Input stream has no integrity check
        /// </summary>
        /// <remarks>
        /// This return value can be returned only if the
        /// LZMA_TELL_NO_CHECK flag was used when initializing
        /// the decoder. LZMA_NO_CHECK is just a warning, and
        /// the decoding can be continued normally.
        ///
        /// It is possible to call lzma_get_check() immediately after
        /// lzma_code has returned LZMA_NO_CHECK. The result will
        /// naturally be LZMA_CHECK_NONE, but the possibility to call
        /// lzma_get_check() may be convenient in some applications.
        /// </remarks>
        NO_CHECK = 2,
        /// <summary>
        /// Cannot calculate the integrity check
        /// </summary>
        /// <remarks>
        /// The usage of this return value is different in encoders
        /// and decoders.
        ///
        /// Encoders can return this value only from the initialization
        /// function. If initialization fails with this value, the
        /// encoding cannot be done, because there's no way to produce
        /// output with the correct integrity check.
        ///
        /// Decoders can return this value only from lzma_code() and
        /// only if the LZMA_TELL_UNSUPPORTED_CHECK flag was used when
        /// initializing the decoder. The decoding can still be
        /// continued normally even if the check type is unsupported,
        /// but naturally the check will not be validated, and possible
        /// errors may go undetected.
        ///
        /// With decoder, it is possible to call lzma_get_check()
        /// immediately after lzma_code() has returned
        /// LZMA_UNSUPPORTED_CHECK. This way it is possible to find
        /// out what the unsupported Check ID was.
        /// </remarks>
        UNSUPPORTED_CHECK = 3,
        /// <summary>
        /// Integrity check type is now available
        /// </summary>
        /// <remarks>
        /// This value can be returned only by the lzma_code() function
        /// and only if the decoder was initialized with the
        /// LZMA_TELL_ANY_CHECK flag. LZMA_GET_CHECK tells the
        /// application that it may now call lzma_get_check() to find
        /// out the Check ID. This can be used, for example, to
        /// implement a decoder that accepts only files that have
        /// strong enough integrity check.
        /// </remarks>
        GET_CHECK = 4,
        /// <summary>
        /// Cannot allocate memory
        /// </summary>
        /// <remarks>
        /// Memory allocation failed, or the size of the allocation
        /// would be greater than SIZE_MAX.
        ///
        /// Due to internal implementation reasons, the coding cannot
        /// be continued even if more memory were made available after
        /// LZMA_MEM_ERROR.
        /// </remarks>
        MEM_ERROR = 5,
        /// <summary>
        /// Memory usage limit was reached
        /// </summary>
        /// <remarks>
        /// Decoder would need more memory than allowed by the
        /// specified memory usage limit. To continue decoding,
        /// the memory usage limit has to be increased with
        /// lzma_memlimit_set().
        /// </remarks>
        MEMLIMIT_ERROR = 6,
        /// <summary>
        /// File format not recognized
        /// </summary>
        /// <remarks>
        /// The decoder did not recognize the input as supported file
        /// format. This error can occur, for example, when trying to
        /// decode .lzma format file with lzma_stream_decoder,
        /// because lzma_stream_decoder accepts only the .xz format.
        /// </remarks>
        FORMAT_ERROR = 7,
        /// <summary>
        /// Invalid or unsupported options
        /// </summary>
        /// <remarks>
        /// Invalid or unsupported options, for example
        ///  - unsupported filter(s) or filter options; or
        ///  - reserved bits set in headers (decoder only).
        ///
        /// Rebuilding liblzma with more features enabled, or
        /// upgrading to a newer version of liblzma may help.
        /// </remarks>
        OPTIONS_ERROR = 8,
        /// <summary>
        /// Data is corrupt
        /// </summary>
        /// <remarks>
        /// The usage of this return value is different in encoders
        /// and decoders. In both encoder and decoder, the coding
        /// cannot continue after this error.
        ///
        /// Encoders return this if size limits of the target file
        /// format would be exceeded. These limits are huge, thus
        /// getting this error from an encoder is mostly theoretical.
        /// For example, the maximum compressed and uncompressed
        /// size of a .xz Stream is roughly 8 EiB (2^63 bytes).
        ///
        /// Decoders return this error if the input data is corrupt.
        /// This can mean, for example, invalid CRC32 in headers
        /// or invalid check of uncompressed data.
        /// </remarks>
        DATA_ERROR = 9,
        /// <summary>
        /// No progress is possible
        /// </summary>
        /// <remarks>
        /// This error code is returned when the coder cannot consume
        /// any new input and produce any new output. The most common
        /// reason for this error is that the input stream being
        /// decoded is truncated or corrupt.
        ///
        /// This error is not fatal. Coding can be continued normally
        /// by providing more input and/or more output space, if
        /// possible.
        ///
        /// Typically the first call to lzma_code() that can do no
        /// progress returns LZMA_OK instead of LZMA_BUF_ERROR. Only
        /// the second consecutive call doing no progress will return
        /// LZMA_BUF_ERROR. This is intentional.
        ///
        /// With zlib, Z_BUF_ERROR may be returned even if the
        /// application is doing nothing wrong, so apps will need
        /// to handle Z_BUF_ERROR specially. The above hack
        /// guarantees that liblzma never returns LZMA_BUF_ERROR
        /// to properly written applications unless the input file
        /// is truncated or corrupt. This should simplify the
        /// applications a little.
        /// </remarks>
        BUF_ERROR = 10,
        /// <summary>
        /// Programming error
        /// </summary>
        /// <remarks>
        /// This indicates that the arguments given to the function are
        /// invalid or the internal state of the decoder is corrupt.
        ///   - Function arguments are invalid or the structures
        ///     pointed by the argument pointers are invalid
        ///     e.g. if strm->next_out has been set to NULL and
        ///     strm->avail_out > 0 when calling lzma_code().
        ///   - lzma_* functions have been called in wrong order
        ///     e.g. lzma_code() was called right after lzma_end().
        ///   - If errors occur randomly, the reason might be flaky
        ///     hardware.
        ///
        /// If you think that your code is correct, this error code
        /// can be a sign of a bug in liblzma. See the documentation
        /// how to report bugs.
        /// </remarks>
        PROG_ERROR = 11,
    }
    #endregion

    #region Enum LzmaCheck
    public enum LzmaCheck
    {
        /// <summary>
        /// No Check is calculated.
        ///
        /// Size of the Check field: 0 bytes
        /// </summary>
        CHECK_NONE = 0,
        /// <summary>
        /// CRC32 using the polynomial from the IEEE 802.3 standard
        ///
        /// Size of the Check field: 4 bytes
        /// </summary>
        CHECK_CRC32 = 1,
        /// <summary>
        /// CRC64 using the polynomial from the ECMA-182 standard
        ///
        /// Size of the Check field: 8 bytes
        /// </summary>
        CHECK_CRC64 = 4,
        /// <summary>
        /// SHA-256
        ///
        /// Size of the Check field: 32 bytes
        /// </summary>
        CHECK_SHA256 = 10,
    }
    #endregion

    #region Enum LzmaDecodingFlag
    [Flags]
    public enum LzmaDecodingFlag : uint
    {
        /// <summary>
        /// This flag makes lzma_code() return LZMA_NO_CHECK if the input stream
        /// being decoded has no integrity check. Note that when used with
        /// lzma_auto_decoder(), all .lzma files will trigger LZMA_NO_CHECK
        /// if LZMA_TELL_NO_CHECK is used.
        /// </summary>
        TELL_NO_CHECK = 0x01,
        /// <summary>
        /// This flag makes lzma_code() return LZMA_UNSUPPORTED_CHECK if the input
        /// stream has an integrity check, but the type of the integrity check is not
        /// supported by this liblzma version or build. Such files can still be
        /// decoded, but the integrity check cannot be verified.
        /// </summary>
        TELL_UNSUPPORTED_CHECK = 0x02,
        /// <summary>
        /// This flag makes lzma_code() return LZMA_GET_CHECK as soon as the type
        /// of the integrity check is known. The type can then be got with
        /// lzma_get_check().
        /// </summary>
        TELL_ANY_CHECK = 0x04,
        /// <summary>
        /// This flag makes lzma_code() not calculate and verify the integrity check
        /// of the compressed data in .xz files. This means that invalid integrity
        /// check values won't be detected and LZMA_DATA_ERROR won't be returned in
        /// such cases.
        /// </summary>
        /// <remarks>
        /// This flag only affects the checks of the compressed data itself; the CRC32
        /// values in the .xz headers will still be verified normally.
        ///
        /// Don't use this flag unless you know what you are doing. Possible reasons
        /// to use this flag:
        ///
        ///   - Trying to recover data from a corrupt .xz file.
        ///
        ///   - Speeding up decompression, which matters mostly with SHA-256
        ///     or with files that have compressed extremely well. It's recommended
        ///     to not use this flag for this purpose unless the file integrity is
        ///     verified externally in some other way.
        ///
        /// Support for this flag was added in liblzma 5.1.4beta.
        /// </remarks>
        IGNORE_CHECK = 0x10,
        /// <summary>
        /// This flag enables decoding of concatenated files with file formats that
        /// allow concatenating compressed files as is. From the formats currently
        /// supported by liblzma, only the .xz format allows concatenated files.
        /// Concatenated files are not allowed with the legacy .lzma format.
        /// </summary>
        /// <remarks>
        /// This flag also affects the usage of the `action' argument for lzma_code().
        /// When LZMA_CONCATENATED is used, lzma_code() won't return LZMA_STREAM_END
        /// unless LZMA_FINISH is used as `action'. Thus, the application has to set
        /// LZMA_FINISH in the same way as it does when encoding.
        ///
        /// If LZMA_CONCATENATED is not used, the decoders still accept LZMA_FINISH
        /// as `action' for lzma_code(), but the usage of LZMA_FINISH isn't required.
        /// </remarks>
        CONCATENATED = 0x08,
    }
    #endregion

    #region Enum LzmaMode
    public enum LzmaMode
    {
        Compress,
        Decompress,
    }
    #endregion
}
