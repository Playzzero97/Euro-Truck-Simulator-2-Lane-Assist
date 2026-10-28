using System.Runtime.InteropServices;
using ETS2LA.Logging;
using HexaGen.Runtime;

namespace ETS2LA.Overlay;

/// <summary>
/// The macOS-only bits the overlay needs. On macOS every window belongs to the main thread,
/// and Photino's UI loop owns that thread. So the GLFW window is created there, Photino's loop
/// handles its events, and the render thread holds the main thread still while it draws a frame,
/// so the two threads never touch the window at the same time.
/// </summary>
internal static unsafe class MacOverlay
{
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(LibSystem)] private static extern int pthread_main_np();
    [DllImport(LibSystem)] private static extern void dispatch_sync_f(nint queue, nint context, delegate* unmanaged<nint, void> work);
    [DllImport(LibSystem)] private static extern void dispatch_async_f(nint queue, nint context, delegate* unmanaged<nint, void> work);
    [DllImport(LibObjC)] private static extern nint objc_getClass(string name);
    [DllImport(LibObjC)] private static extern nint sel_registerName(string name);
    [DllImport(LibObjC)] private static extern nint objc_msgSend(nint receiver, nint selector);
    [DllImport(LibObjC)] private static extern void objc_msgSend(nint receiver, nint selector, nint arg);

    private static readonly nint mainQueue = NativeLibrary.GetExport(NativeLibrary.Load(LibSystem), "_dispatch_main_q");
    private static readonly SemaphoreSlim mainHeld = new(0), mainReleased = new(0);
    private static bool holding;

    public static bool IsMainThread => pthread_main_np() == 1;

    /// <summary>
    /// The ImGui GLFW backend for macOS has its own copy of GLFW built in. Load GLFW from that
    /// same file, so the overlay and the backend use one GLFW instead of two separate ones.
    /// </summary>
    public static void UseImGuiGLFW() =>
        LibraryLoader.InterceptLibraryName += (ref string name) =>
        {
            if (name != "libglfw") return false;
            name = "ImGuiImplGLFW";
            return true;
        };

    /// <summary>
    /// Runs the GLFW setup on the main thread and waits for it. glfwInit makes GLFW the app's
    /// delegate, so Photino's delegate is put back afterwards and the app keeps quitting as before.
    /// </summary>
    public static bool InitOnMainThread(Func<bool> init)
    {
        bool result = false;
        Action work = () =>
        {
            nint app = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            nint photinoDelegate = objc_msgSend(app, sel_registerName("delegate"));
            try { result = init(); }
            finally { objc_msgSend(app, sel_registerName("setDelegate:"), photinoDelegate); }
        };

        GCHandle handle = GCHandle.Alloc(work);
        try { dispatch_sync_f(mainQueue, GCHandle.ToIntPtr(handle), &RunAction); }
        finally { handle.Free(); }
        return result;
    }

    /// <summary>Same window level your old Mac overlay used, so it stays above the game.</summary>
    public static void FloatAboveGame(nint nsWindow) =>
        objc_msgSend(nsWindow, sel_registerName("setLevel:"), 1000); // NSScreenSaverWindowLevel

    /// <summary>Holds the main thread until <see cref="ReleaseMainThread"/>, so a frame can use the window alone.</summary>
    public static void HoldMainThread()
    {
        if (holding) return;
        dispatch_async_f(mainQueue, 0, &WaitOnMainThread);
        mainHeld.Wait();
        holding = true;
    }

    public static void ReleaseMainThread()
    {
        if (!holding) return;
        holding = false;
        mainReleased.Release();
    }

    [UnmanagedCallersOnly]
    private static void WaitOnMainThread(nint _)
    {
        mainHeld.Release();
        mainReleased.Wait();
    }

    [UnmanagedCallersOnly]
    private static void RunAction(nint context)
    {
        try { ((Action)GCHandle.FromIntPtr(context).Target!)(); }
        catch (Exception ex) { Logger.Error($"Overlay setup on the main thread failed: {ex}"); }
    }
}
