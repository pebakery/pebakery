/*
    Copyright (C) 2019 Hajin Jang
 
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace PEBakery.Helper.Tests
{
    #region SystemHelper
    [TestClass]
    [TestCategory("StringHelper")]
    public class SystemHelperTests
    {
        [TestMethod]        
        public void PrintMemorySnapshot()
        {
            MemorySnapshot m = SystemHelper.GetMemorySnapshot();
            Console.WriteLine(m);
        }

        [TestMethod]
        public void AvailableSystemMemory()
        {
            (ulong MaxReq, double MaxPercent)[] samples = new (ulong, double)[]
            {
                (1 * NumberHelper.GigaByte, 0.7),
                (2 * NumberHelper.GigaByte, 0.7),
                (3 * NumberHelper.GigaByte, 0.7),
                (4 * NumberHelper.GigaByte, 0.7),
                (4 * NumberHelper.GigaByte, 0.8),
                (4 * NumberHelper.GigaByte, 0.9),
                (6 * NumberHelper.GigaByte, 0.9),
                (8 * NumberHelper.GigaByte, 0.9),
            };

            ulong[] results = new ulong[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                (ulong maxReq, double maxPercent) = samples[i];
                ulong availMem = SystemHelper.AvailableSystemMemory(maxReq, maxPercent);
                results[i] = availMem;

                string siMaxReq = NumberHelper.ByteSizeToSIUnit((long)maxReq, 1);
                string siAvailMem = NumberHelper.ByteSizeToSIUnit((long)availMem, 1);
                Console.WriteLine($"MaxReq = {siMaxReq} ({maxReq}), MaxPercent = {maxPercent}, AvailMem = {siAvailMem} ({availMem})");
            }

            for (int i = 0; i < results.Length - 1; i++)
            {
                ulong last = results[i];
                ulong next = results[i + 1];
                Assert.IsTrue(last <= next);
            }
        }

        [TestMethod]
        public void AdaptThreadCount()
        {
            const ulong memPerThread = 384 * NumberHelper.MegaByte;
            ulong QueryMemUsage(int threads) => (uint)(threads + Math.Sqrt(threads)) * memPerThread;

            int reqThreads = Environment.ProcessorCount * 2;
            (ulong MaxReq, double MaxPercent)[] samples = new (ulong, double)[]
            {
                (1 * NumberHelper.GigaByte, 0.7),
                (2 * NumberHelper.GigaByte, 0.7),
                (3 * NumberHelper.GigaByte, 0.7),
                (4 * NumberHelper.GigaByte, 0.7),
                (4 * NumberHelper.GigaByte, 0.8),
                (4 * NumberHelper.GigaByte, 0.9),
                (6 * NumberHelper.GigaByte, 0.9),
                (8 * NumberHelper.GigaByte, 0.9),
            };

            int[] results1 = new int[samples.Length];
            int[] results2 = new int[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                (ulong maxReq, double maxPercent) = samples[i];

                results1[i] = SystemHelper.AdaptThreadCount(reqThreads, memPerThread, maxReq, maxPercent);
                results2[i] = SystemHelper.AdaptThreadCount(reqThreads, QueryMemUsage, maxReq, maxPercent);
                Assert.IsTrue(1 <= results1[i]);
                Assert.IsTrue(1 <= results2[i]);

                string siMaxReq = NumberHelper.ByteSizeToSIUnit((long)maxReq, 1);
                Console.WriteLine($"MaxReq = {siMaxReq} ({maxReq}), MaxPercent = {maxPercent}, AdaptedThreads = (1) {results1[i]}, (2) {results2[i]}");
            }

            foreach (int[] results in new int[][] { results1, results2 })
            {
                for (int i = 0; i < results.Length - 1; i++)
                {
                    int last = results[i];
                    int next = results[i + 1];
                    Assert.IsTrue(last <= next);
                }
            }
        }
    }
    #endregion
}
