using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LibKyrka.Interop;

/// <summary>
/// Owns a native KYRKA context together with the GC handle that native
/// callbacks use to find their managed <see cref="KyrkaContext"/>.
/// </summary>
/// <remarks>
/// The GC handle is weak, so it never keeps the context alive by itself.
/// If a context is not disposed, this handle's finalizer still releases the
/// native memory (zeroized by libkyrka) and the GC handle.
/// </remarks>
internal sealed class KyrkaSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private GCHandle _owner;

    public KyrkaSafeHandle()
        : base(ownsHandle: true)
    {
    }

    /// <summary>The value passed to libkyrka as the callback user data.</summary>
    public nint UserData => GCHandle.ToIntPtr(_owner);

    public static KyrkaContext? ContextFromUserData(nint userData)
    {
        if (userData == 0)
        {
            return null;
        }

        return GCHandle.FromIntPtr(userData).Target as KyrkaContext;
    }

    /// <summary>Allocates the GC handle; must be called before the native context.</summary>
    public void SetOwner(KyrkaContext owner) => _owner = GCHandle.Alloc(owner, GCHandleType.Weak);

    public void SetNativeContext(nint context) => SetHandle(context);

    protected override bool ReleaseHandle()
    {
        lock (Kyrka.NativeLock)
        {
            NativeMethods.kyrka_ctx_free(handle);
        }

        FreeOwner();
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Also covers the case where the native context was never created.
        FreeOwner();
    }

    private void FreeOwner()
    {
        if (_owner.IsAllocated)
        {
            _owner.Free();
        }
    }
}
