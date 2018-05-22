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
using System.Text;
using System.Threading.Tasks;

namespace ManagedWimLib
{
    #region WimLibException
    public class WimLibException : Exception
    {
        public ErrorCode ErrorCode;

        public WimLibException(ErrorCode errorCode)
            : base(ForgeErrorMessages(errorCode, true))
        {
            ErrorCode = errorCode;
        }

        internal static string ForgeErrorMessages(ErrorCode errorCode, bool full)
        {
            StringBuilder b = new StringBuilder();

            if (full)
                b.Append($"[{errorCode}] ");

            b.Append(Wim.GetLastError() ?? Wim.GetErrorString(errorCode));

            return b.ToString();
        }

        public static void CheckWimLibError(ErrorCode ret)
        {
            if (ret != ErrorCode.SUCCESS)
                throw new WimLibException(ret);
        }
    }
    #endregion
}
