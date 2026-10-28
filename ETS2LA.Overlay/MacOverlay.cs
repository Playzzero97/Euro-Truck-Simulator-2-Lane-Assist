using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using ETS2LA.Logging;
using Hexa.NET.GLFW;
using HexaGen.Runtime;

namespace ETS2LA.Overlay;

/// <summary>
/// The macOS-only bits the overlay needs. On macOS, windows and OpenGL drawing belong to the
/// main thread, and Photino's UI loop owns that thread. So the render thread only keeps the
/// timing: it hands the GLFW setup and each frame to the main thread with dispatch_sync and
/// waits for it to finish. The main thread never waits for the render thread, so the two
/// can't block each other, and Photino's loop handles the overlay's events.
/// </summary>
internal static unsafe class MacOverlay
{
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(LibSystem)] private static extern int pthread_main_np();
    [DllImport(LibSystem)] private static extern void dispatch_sync_f(nint queue, nint context, delegate* unmanaged<nint, void> work);
    [DllImport(LibObjC)] private static extern nint objc_getClass(string name);
    [DllImport(LibObjC)] private static extern nint sel_registerName(string name);
    [DllImport(LibObjC)] private static extern nint objc_msgSend(nint receiver, nint selector);
    [DllImport(LibObjC)] private static extern void objc_msgSend(nint receiver, nint selector, nint arg);

    private static nint mainQueue;
    private static readonly HashSet<string> logged = new();

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
    /// Runs the GLFW setup on the main thread. glfwInit makes GLFW the app's delegate, so
    /// Photino's delegate is put back afterwards and the app keeps quitting as before.
    /// </summary>
    public static bool InitGLFWOnMainThread(Func<bool> init)
    {
        bool result = false;
        RunOnMainThread("GLFW setup", () =>
        {
            nint app = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            nint photinoDelegate = objc_msgSend(app, sel_registerName("delegate"));
            try { result = init(); }
            finally { objc_msgSend(app, sel_registerName("setDelegate:"), photinoDelegate); }
        });
        return result;
    }

    /// <summary>Runs the ImGui setup on the main thread, which owns the OpenGL context from now on.</summary>
    public static bool InitImGuiOnMainThread(Func<bool> init)
    {
        GLFW.MakeContextCurrent(default); // let go of the context on the render thread
        bool result = false;
        RunOnMainThread("ImGui setup", () => result = init());
        return result;
    }

    /// <summary>Same window level your old Mac overlay used, so it stays above the game.</summary>
    public static void FloatAboveGame(nint nsWindow) =>
        objc_msgSend(nsWindow, sel_registerName("setLevel:"), 1000); // NSScreenSaverWindowLevel

    /// <summary>
    /// Runs <paramref name="work"/> on the main thread and waits for it. It starts once Photino's
    /// loop is running. The first run of each step is logged, so a hang shows up in the log.
    /// </summary>
    public static void RunOnMainThread(string step, Action work)
    {
        bool log = logged.Add(step);
        if (log) Logger.Info($"Mac overlay: {step} dispatched to the main thread");

        Exception? error = null;
        Action wrapped = () =>
        {
            if (log) Logger.Info($"Mac overlay: {step} running on the main thread");
            try { work(); }
            catch (Exception ex) { error = ex; }
        };

        if (mainQueue == 0) mainQueue = NativeLibrary.GetExport(NativeLibrary.Load(LibSystem), "_dispatch_main_q");
        GCHandle handle = GCHandle.Alloc(wrapped);
        try { dispatch_sync_f(mainQueue, GCHandle.ToIntPtr(handle), &RunAction); }
        finally { handle.Free(); }

        if (error != null)
        {
            Logger.Error($"Mac overlay: {step} failed on the main thread: {error}");
            ExceptionDispatchInfo.Throw(error); // same as on Windows and Linux: the render loop stops
        }
        if (log) Logger.Info($"Mac overlay: {step} done");
    }

    [UnmanagedCallersOnly]
    private static void RunAction(nint context) => ((Action)GCHandle.FromIntPtr(context).Target!)();
}
