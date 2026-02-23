using System;
using System.Runtime.InteropServices;

namespace LoqNova.Lib.Utils;

public sealed class NullSafeHandle() : SafeHandle(IntPtr.Zero, true)
{
    public static readonly NullSafeHandle Null = new();

    public override bool IsInvalid => false;

    protected override bool ReleaseHandle() => true;
}
