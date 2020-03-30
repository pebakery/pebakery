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

namespace PEBakery.Helper
{
    /// <summary>
    /// Report result of any opertaion on exception-less way.
    /// </summary>
    public class ResultReport
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public ResultReport(bool success)
        {
            Success = success;
        }

        public ResultReport(bool success, string message)
        {
            if (message == null)
                message = string.Empty;

            Success = success;
            Message = message;
        }

        public ResultReport(Exception e)
        {
            Success = false;
            Message = e.Message;
        }
    }

    /// <summary>
    /// Report result of any opertaion on exception-less way.
    /// </summary>
    public class ResultReport<T>
    {
        public bool Success { get; set; }
        public T Result { get; set; }
        public string Message { get; set; } = string.Empty;

        public ResultReport(bool success, T result)
        {
            Success = success;
            Result = result;
        }

        public ResultReport(bool success, T result, string message)
        {
            if (message == null)
                message = string.Empty;

            Success = success;
            Result = result;
            Message = message;
        }

        public ResultReport(Exception e, T result)
        {
            Success = false;
            Result = result;
            Message = e.Message;
        }
    }

    /// <summary>
    /// Report result of any opertaion on exception-less way.
    /// </summary>
    public class ResultReport<T1, T2>
    {
        public bool Success { get; set; }
        public T1 Result1 { get; set; }
        public T2 Result2 { get; set; }
        public string Message { get; set; } = string.Empty;

        public ResultReport(bool success, T1 result1, T2 result2)
        {
            Success = success;
            Result1 = result1;
            Result2 = result2;
        }

        public ResultReport(bool success, T1 result1, T2 result2, string message)
        {
            if (message == null)
                message = string.Empty;

            Success = success;
            Result1 = result1;
            Result2 = result2;
            Message = message;
        }

        public ResultReport(Exception e, T1 result1, T2 result2)
        {
            Success = false;
            Result1 = result1;
            Result2 = result2;
            Message = e.Message;
        }
    }
}
