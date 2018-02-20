namespace Ookii.Dialogs.Wpf.Interop
{
    internal enum HRESULT
    {
        S_FALSE = 0x0001,
        S_OK = 0x0000,
        E_INVALIDARG = unchecked((int)0x80070057),
        E_OUTOFMEMORY = unchecked((int)0x8007000E),
        ERROR_CANCELLED = unchecked((int)0x800704C7)
    }
}