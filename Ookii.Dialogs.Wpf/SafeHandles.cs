// Copyright ?Sven Groot (Ookii.org) 2009
// BSD license; see license.txt for details.

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace Ookii.Dialogs.Wpf
{
    class SafeModuleHandle : SafeHandle
    {
        public SafeModuleHandle()
            : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero; }
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.FreeLibrary(handle);
        }
    }

    class ActivationContextSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ActivationContextSafeHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.ReleaseActCtx(handle);
            return true;
        }
    }
}