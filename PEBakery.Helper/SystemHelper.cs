/*
    Copyright (C) 2019-2020 Hajin Jang
    Licensed under MIT License.
 
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
using System.Runtime.InteropServices;
using System.Text;

namespace PEBakery.Helper
{
    /// <summary>
    /// Abbreviated version of MEMORYSTATUSEX
    /// </summary>
    public class MemorySnapshot
    {
        public ulong TotalPhysical;
        public ulong AvailPhysical;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine($"Total Physical : {NumberHelper.ByteSizeToSIUnit((long)TotalPhysical, 1)} ({TotalPhysical})");
            b.AppendLine($"Avail Physical : {NumberHelper.ByteSizeToSIUnit((long)AvailPhysical, 1)} ({AvailPhysical})");
            b.AppendLine($"Total PageFile : {NumberHelper.ByteSizeToSIUnit((long)TotalPageFile, 1)} ({TotalPageFile})");
            b.AppendLine($"Avail PageFile : {NumberHelper.ByteSizeToSIUnit((long)AvailPageFile, 1)} ({AvailPageFile})");
            b.AppendLine($"Total Virtual  : {NumberHelper.ByteSizeToSIUnit((long)TotalVirtual, 1)} ({TotalVirtual})");
            b.AppendLine($"Avail Virtual  : {NumberHelper.ByteSizeToSIUnit((long)AvailVirtual, 1)} ({AvailVirtual})");
            return b.ToString();
        }
    }

    public static class SystemHelper
    {
        /// <remarks>
        /// It works only on Windows.  
        /// </remarks>
        public static MemorySnapshot GetMemorySnapshot()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NativeMethods.MEMORYSTATUSEX native = new NativeMethods.MEMORYSTATUSEX();
                if (!NativeMethods.GlobalMemoryStatusEx(native))
                    return null;
                return new MemorySnapshot
                {
                    TotalPhysical = native.ullTotalPhys,
                    AvailPhysical = native.ullAvailPhys,
                    TotalPageFile = native.ullTotalPageFile,
                    AvailPageFile = native.ullAvailPageFile,
                    TotalVirtual = native.ullTotalVirtual,
                    AvailVirtual = native.ullAvailVirtual,
                };
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        /// <summary>
        /// Query how much system memory is available 
        /// </summary>
        /// <param name="maxReqMem">Max limit of requested memory which program is going to use</param>
        /// <param name="usableSysMemPercent">How much percent of memory program is allowed to use</param>
        /// <returns></returns>
        public static ulong AvailableSystemMemory(ulong maxReqMem, double usableSysMemPercent)
        {
            // Get the max capacity of physical memory we can use
            MemorySnapshot m = GetMemorySnapshot();
            ulong reservedPhysical = (ulong)(m.TotalPhysical * (1 - usableSysMemPercent));
            if (m.AvailPhysical < reservedPhysical) // Possibly no free physical memory (aside from reserved memory)!
                return 0;

            // Get the max capacity of virtual memory we can use
            ulong totalVirtual = m.TotalVirtual;
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X86:
                case Architecture.Arm: // Not tested yet
                    // In some cases 32bit Windows process can use 3GB of virtual memory, but let's be safe
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        totalVirtual = Math.Max(totalVirtual, 2 * NumberHelper.GigaByte);
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        totalVirtual = Math.Max(totalVirtual, 3 * NumberHelper.GigaByte);
                    break;
            }
            ulong reservedVirtual = (ulong)(totalVirtual * (1 - usableSysMemPercent));
            if (m.AvailVirtual < reservedVirtual) // Possibly no free virtual memory (aside from reserved memory)!
                return 0;
            ulong freePhysical = Math.Min(m.AvailPhysical - reservedPhysical, maxReqMem);
            ulong freeVirtual = Math.Min(m.AvailVirtual - reservedVirtual, maxReqMem);
            return Math.Min(freePhysical, freeVirtual);
        }

        public static int AdaptThreadCount(int reqThreads, ulong memPerThread, ulong maxReqMem, double usableSysMemPercent)
        {
            if (usableSysMemPercent < 0 || 1 < usableSysMemPercent)
                throw new ArgumentOutOfRangeException(nameof(usableSysMemPercent), $"The allowed range is 0 <= nameof({usableSysMemPercent}) <= 1");

            // Max amount of free memory program is allowed to use
            ulong freeMem = AvailableSystemMemory(maxReqMem, usableSysMemPercent);

            // Max thread count with Environment.ProcessCount
            int threads = Math.Min(reqThreads, Environment.ProcessorCount);

            // System has enough free memory, so thread is enough
            if ((uint)threads * memPerThread <= freeMem)
                return threads;
            // System has not enough free memory, so adapt the thread count to the limit
            return Math.Max((int)(freeMem / memPerThread), 1);
        }

        public delegate ulong QueryMemoryUsageDelegate(int threads);

        public static int AdaptThreadCount(int reqThreads, QueryMemoryUsageDelegate memQuery, ulong maxReqMem, double usableSysMemPercent)
        {
            if (usableSysMemPercent < 0 || 1 < usableSysMemPercent)
                throw new ArgumentOutOfRangeException(nameof(usableSysMemPercent), $"The allowed range is 0 <= nameof({usableSysMemPercent}) <= 1");

            // Max amount of free memory program is allowed to use
            ulong freeMem = AvailableSystemMemory(maxReqMem, usableSysMemPercent);

            // Max thread count with Environment.ProcessCount
            int threads = Math.Min(reqThreads, Environment.ProcessorCount);

            for (int t = threads; 0 < t; t--)
            {
                ulong memUsage = memQuery(t);

                // System has enough free memory to allow `t` threads
                if (memUsage <= freeMem)
                    return t;
            }

            // Every try failed, fail-safe to 1 threads
            return 1;
        }
    }
}
