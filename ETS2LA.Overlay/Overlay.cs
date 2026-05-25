// Much of this file is based on the Hexa.NET.ImGui example code. See the relevant example here:
// https://github.com/HexaEngine/Hexa.NET.ImGui/blob/main/Examples/ExampleGLFWOpenGL3/Program.cs

using Hexa.NET.GLFW;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.OpenGL;
using HexaGen.Runtime;
using GLFWwindowPtr = Hexa.NET.GLFW.GLFWwindowPtr;

using System.Runtime.CompilerServices;
using System.Numerics;
using Avalonia.Data;

using ETS2LA.Logging;
using ETS2LA.Controls;
using ETS2LA.Overlay.Window;
using ETS2LA.Overlay.AR;
using System.Diagnostics;

namespace ETS2LA.Overlay;

public class OverlayHandler
{
    private static readonly Lazy<OverlayHandler> _instance = new(() => new OverlayHandler());
    public static OverlayHandler Current => _instance.Value;

    public ControlDefinition Interact = new ControlDefinition
    {   
        Id = "ETS2LA.Overlay.Interact",
        Name = "Overlay Interaction",
        Description = "When this key is held, the overlay will receive mouse input and allow you to interact with it. NOTE: Interaction with items below the overlay is not possible during this time.",
        DefaultKeybind = "RightAlt",
        Type = ControlType.Boolean
    };

    public ARRenderer AR;

    private bool isInteracting = false;
    private float bgOpacityTarget = 0.0f;
    private bool shutdown = false;
    public bool IsShuttingDown => shutdown;
    private List<float> frameTimes = new List<float>();
    
    private List<InternalWindow> windows = new();

    private int _winHeight, _winWidth;

    public bool IsOverlayFocused => isInteracting;
    public float AverageFrameTime => frameTimes.Count > 0 ? frameTimes.Average() : 0f;

    public float OverlayWidth => GLFW.GetVideoMode(GLFW.GetPrimaryMonitor()).Width;
    public float OverlayHeight => GLFW.GetVideoMode(GLFW.GetPrimaryMonitor()).Height;

    private string glslVersion = "#version 150";
    private GLFWwindowPtr glfwWindow;
    private ImGuiContextPtr imGuiContext;
    private ImGuiIOPtr io;
    private GL gl;

    #if MACOSX
        [System.Runtime.InteropServices.DllImport("libobjc.dylib")]
        private static extern IntPtr sel_registerName(string name);
        
        [System.Runtime.InteropServices.DllImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_void_int(IntPtr receiver, IntPtr selector, int value);
    #endif  

    public OverlayHandler()
    {
        ControlsBackend.Current.RegisterControl(Interact);
        ControlsBackend.Current.On(Interact.Id, HandleInput);

        #if !MACOSX
            Task.Run(() => RenderLoop());
        #endif
        
        windows.Add(new ConsoleWindow());
        windows.Add(new OverlayInfoWindow());
        windows.Add(new DemoWindow());
        windows.Add(new StateWindow());
    }

    private void HandleInput(object sender, ControlChangeEventArgs e)
    {
        bool b = (bool)e.NewValue;
        if (b == isInteracting) { return; }
        isInteracting = b;
    }

#if MACOSX
    // ── macOS main-thread render path ────────────────────────────────────────
    // On macOS, GLFW and OpenGL must run on the main thread.
    // The host should call InitWindowOnMainThread() once at startup,
    // then call RenderFrame() every frame from the main run loop.
    // DO NOT call RenderLoop() on macOS.

    private bool _renderInitialized = false;
    private Stopwatch _frameTimer = Stopwatch.StartNew();
    private double _lastFrameStart = 0;

    /// <summary>
    /// Call once from the main thread before the first RenderFrame().
    /// Creates the GLFW window and initialises OpenGL + ImGui.
    /// </summary>
    public unsafe bool InitWindowOnMainThread()
    {
        Logger.Info("Initializing GLFW Version: " + Utils.DecodeStringUTF8(GLFW.GetVersionString()));
        Console.WriteLine("Initializing GLFW...");
        GLFW.Init();
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 3);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 2);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_FORWARD_COMPAT, 1); // Required on macOS

        GLFW.WindowHint(GLFW.GLFW_TRANSPARENT_FRAMEBUFFER, 1);
        GLFW.WindowHint(GLFW.GLFW_DECORATED, 0);
        GLFW.WindowHint(GLFW.GLFW_FLOATING, 1);
        GLFW.WindowHint(GLFW.GLFW_FOCUSED, 0);
        GLFW.WindowHint(GLFW.GLFW_FOCUS_ON_SHOW, 0);

        var mon = GLFW.GetPrimaryMonitor();
        int width  = GLFW.GetVideoMode(mon).Width;
        int height = GLFW.GetVideoMode(mon).Height;

        glfwWindow = GLFW.CreateWindow(width - 1, height - 1, "ETS2LA overlay", null, null);
        if (glfwWindow.IsNull)
        {
            Logger.Error("Failed to create GLFW window");
            GLFW.Terminate();
            return false;
        }

        GLFW.SwapInterval(0);

        SetMacOSWindowLevel();
        Console.WriteLine("GLFW window created successfully");

        GLFW.MakeContextCurrent(glfwWindow);
        gl = new GL(new BindingsContext(glfwWindow));

        if (!InitImGui())
        {
            Logger.Error("InitWindowOnMainThread: InitImGui failed");
            return false;
        }

        _lastFrameStart = _frameTimer.Elapsed.TotalMilliseconds;
        _renderInitialized = true;
        Logger.Info("InitWindowOnMainThread complete");
        return true;
    }

    private bool _firstFrame = true;
    private int _renderingFlag = 0;

    public void RenderFrame()
    {
       if (!_renderInitialized || glfwWindow.IsNull) return;
        if (System.Threading.Interlocked.CompareExchange(ref _renderingFlag, 1, 0) != 0) return;

        try
        {
        
        if (!_renderInitialized || glfwWindow.IsNull) return;

        GLFW.MakeContextCurrent(glfwWindow);

        // Poll events (must be called from the main thread on macOS)
        GLFW.PollEvents();

        if (!isInteracting)
        {
            ImGui.GetPlatformIO().Viewports[0].Flags |= ImGuiViewportFlags.NoInputs;
            GLFW.SetWindowAttrib(glfwWindow, GLFW.GLFW_MOUSE_PASSTHROUGH, 1);
            bgOpacityTarget = 0.0f;
        }
        else
        {
            ImGui.GetPlatformIO().Viewports[0].Flags &= ~ImGuiViewportFlags.NoInputs;
            GLFW.SetWindowAttrib(glfwWindow, GLFW.GLFW_MOUSE_PASSTHROUGH, 0);
            GLFW.FocusWindow(glfwWindow);
            bgOpacityTarget = 0.5f;
        }

        // Query actual framebuffer / window sizes for HiDPI correctness
        int fbWidth, fbHeight, winWidth, winHeight;
        unsafe
        {
            GLFW.GetFramebufferSize(glfwWindow, &fbWidth, &fbHeight);
            GLFW.GetWindowSize(glfwWindow, &winWidth, &winHeight);
        }
        _winWidth  = winWidth;
        _winHeight = winHeight;

        if (_firstFrame)
        {
            _firstFrame = false;
            Console.WriteLine($"[Overlay] First frame: window={winWidth}x{winHeight}, fb={fbWidth}x{fbHeight}");
            Console.WriteLine($"[Overlay] Font atlas built: {io.Fonts.TexIsBuilt}, TexID: {io.Fonts.TexData}");
            Console.WriteLine($"[Overlay] isInteracting={isInteracting}, bgOpacityTarget={bgOpacityTarget}");
        }

        gl.Viewport(0, 0, fbWidth, fbHeight);

        io.DisplaySize             = new Vector2(winWidth, winHeight);
        io.DisplayFramebufferScale = new Vector2((float)fbWidth / winWidth, (float)fbHeight / winHeight);

        #if MACOSX
            if (isInteracting)
            {
                double mouseX, mouseY;
                unsafe { GLFW.GetCursorPos(glfwWindow, &mouseX, &mouseY); }
                io.AddMousePosEvent((float)mouseX, (float)mouseY);
            }
            else
            {
                io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
            }

            io.AddMouseButtonEvent(0, GLFW.GetMouseButton(glfwWindow, 0) == GLFW.GLFW_PRESS);
            io.AddMouseButtonEvent(1, GLFW.GetMouseButton(glfwWindow, 1) == GLFW.GLFW_PRESS);
            io.AddMouseButtonEvent(2, GLFW.GetMouseButton(glfwWindow, 2) == GLFW.GLFW_PRESS);
        #endif

        ImGuiImplOpenGL3.NewFrame();
        #if !MACOSX
        ImGuiImplGLFW.NewFrame();
        #else
        io.DeltaTime = Math.Max((float)((_frameTimer.Elapsed.TotalMilliseconds - _lastFrameStart) / 1000.0), 0.0001f);
        #endif
        ImGui.NewFrame();

        // AR pass
        try
        {
            if (AR == null) { AR = new ARRenderer(gl); }
            AR.Render();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in AR rendering: {ex}");
        }

        // UI pass (full window rendering, same as non-macOS)
        try { OnUIRender(); }
        catch (Exception ex)
        {
            Logger.Error($"Error rendering overlay: {ex}");
        }

        gl.ClearColor(0f, 0f, 0f, bgOpacityTarget);
        gl.Clear(GLClearBufferMask.ColorBufferBit);
        ImGui.Render();
        ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            ImGui.UpdatePlatformWindows();
            ImGui.RenderPlatformWindowsDefault();
        }

        GLFW.SwapBuffers(glfwWindow); // swap once only

        double now = _frameTimer.Elapsed.TotalMilliseconds;
        frameTimes.Add((float)(now - _lastFrameStart));
        if (frameTimes.Count > 60) frameTimes.RemoveAt(0);
        _lastFrameStart = now;
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _renderingFlag, 0);
        }
    }
#endif

    public void RenderLoop()
    {
        #if MACOSX
            // macOS must never reach here — all rendering is driven by RenderFrame()
            // called from the main thread. This guard is a safety net.
            Logger.Error("RenderLoop called on macOS – this should never happen. Use InitWindowOnMainThread() + RenderFrame() instead.");
            return;
        #else
        if (!InitGLFW()) { Logger.Error("Failed to initialize overlay"); return; }

        GLFW.MakeContextCurrent(glfwWindow);
        gl = new GL(new BindingsContext(glfwWindow));

        if (!InitImGui()) { Logger.Error("RenderLoop: InitImGui failed"); return; }
        Console.WriteLine("RenderLoop: ImGui initialized, entering loop");

        Stopwatch fs = Stopwatch.StartNew();
        int targetFramerate = GLFW.GetVideoMode(GLFW.GetPrimaryMonitor()).RefreshRate;
        double interval = 1000.0 / targetFramerate;
        double next  = fs.Elapsed.TotalMilliseconds;
        double start = fs.Elapsed.TotalMilliseconds;

        while (GLFW.WindowShouldClose(glfwWindow) == 0 && !shutdown)
        {
            start = fs.Elapsed.TotalMilliseconds;
            next += interval;

            Stopwatch InteractionStopwatch = Stopwatch.StartNew();
            if (!isInteracting) 
            { 
                // This has to be called each frame to properly update the flags.
                // For whatever reason they are set back to default. Shouldn't affect
                // performance, it's just weird...
                ImGui.GetPlatformIO().Viewports[0].Flags |= ImGuiViewportFlags.NoInputs;

                # if LINUX
                GLFW.SetWindowAttrib(glfwWindow, GLFW.GLFW_MOUSE_PASSTHROUGH, 1);
                # endif
                
                bgOpacityTarget = 0.0f;
            }
            else 
            {
                # if LINUX
                GLFW.SetWindowAttrib(glfwWindow, GLFW.GLFW_MOUSE_PASSTHROUGH, 0);
                # endif
                bgOpacityTarget = 0.5f;
            }
            InteractionStopwatch.Stop();

            Stopwatch PollEventsStopwatch = Stopwatch.StartNew();
            GLFW.PollEvents();
            PollEventsStopwatch.Stop();

            Stopwatch NewFrameStopwatch = Stopwatch.StartNew();
            // Skip rendering if we're minimized, though this should actually
            // never happen for the overlay.
            if (GLFW.GetWindowAttrib(glfwWindow, GLFW.GLFW_ICONIFIED) != 0)
            {
                ImGuiImplGLFW.Sleep(10);
                continue;
            }

            GLFW.MakeContextCurrent(glfwWindow);

            ImGuiImplOpenGL3.NewFrame();
            ImGuiImplGLFW.NewFrame();
            ImGui.NewFrame();
            NewFrameStopwatch.Stop();
            
            // The actual rendering is happening here,
            // all other calls are just setup.
            Stopwatch ARStopwatch = Stopwatch.StartNew();
            try { 
                if (AR == null) { AR = new ARRenderer(gl); }
                AR.Render(); 
            }
            catch (Exception ex) {
                Logger.Error($"Error in AR rendering: {ex}");
            }
            ARStopwatch.Stop();

            Stopwatch UIRenderStopwatch = Stopwatch.StartNew();
            try { OnUIRender(); }
            catch (Exception ex) {
                Logger.Error($"Error rendering overlay: {ex}");
            }
             UIRenderStopwatch.Stop();
            // ---

            Stopwatch RenderStopwatch = Stopwatch.StartNew();

            ImGui.Render();

            gl.ClearColor(0f, 0f, 0f, bgOpacityTarget);
            gl.Clear(GLClearBufferMask.ColorBufferBit);
            
            ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());
            RenderStopwatch.Stop();

            Stopwatch UpdatePlatformWindowsStopwatch = Stopwatch.StartNew();
            if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
            {
                ImGui.UpdatePlatformWindows();
                ImGui.RenderPlatformWindowsDefault();
            }
            UpdatePlatformWindowsStopwatch.Stop();

            Stopwatch SwapBuffersStopwatch = Stopwatch.StartNew();
            GLFW.SwapInterval(0); // disable vsync
            GLFW.SwapBuffers(glfwWindow);
            SwapBuffersStopwatch.Stop();

            double remaining = next - fs.Elapsed.TotalMilliseconds;
            if (remaining > 1.0)
                Thread.Sleep((int)(remaining - 1));
            
            // Busy wait the end
            while (fs.Elapsed.TotalMilliseconds < next)
                Thread.SpinWait(10);
            
            frameTimes.Add((float)(fs.Elapsed.TotalMilliseconds - start));
            if (frameTimes.Count > targetFramerate) { frameTimes.RemoveAt(0); }

            // TODO: There are some lag spikes that can't be explained using the 
            // stopwatches in use here, where are those coming from?
            // if (fs.Elapsed.TotalMilliseconds - start > interval + 20)
            // {
            //     Logger.Warn($"Overlay is running behind! Missed frame time by {fs.Elapsed.TotalMilliseconds - start - interval} ms");
            //     Logger.Warn($"AR rendering took {ARStopwatch.Elapsed.TotalMilliseconds} ms, UI rendering took {UIRenderStopwatch.Elapsed.TotalMilliseconds} ms");
            //     Logger.Warn($"Interaction took {InteractionStopwatch.Elapsed.TotalMilliseconds} ms, PollEvents took {PollEventsStopwatch.Elapsed.TotalMilliseconds} ms, NewFrame took {NewFrameStopwatch.Elapsed.TotalMilliseconds} ms");
            //     Logger.Warn($"Render took {RenderStopwatch.Elapsed.TotalMilliseconds} ms, UpdatePlatformWindows took {UpdatePlatformWindowsStopwatch.Elapsed.TotalMilliseconds} ms, SwapBuffers took {SwapBuffersStopwatch.Elapsed.TotalMilliseconds} ms");
            // }
        }

        ImGuiImplOpenGL3.Shutdown();
        ImGuiImplOpenGL3.SetCurrentContext(null);
        ImGuiImplGLFW.Shutdown();
        ImGuiImplGLFW.SetCurrentContext(null);
        ImGui.DestroyContext();
        gl.Dispose();

        // Clean up and terminate GLFW
        GLFW.DestroyWindow(glfwWindow);
        GLFW.Terminate();
        #endif
    }

    private void OnUIRender()
    {
        if (isInteracting)
        {
            ImGui.Begin("Interaction Mode", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoBackground);
            ImGui.SetWindowPos(new Vector2(OverlayWidth / 2 - 60, 10), ImGuiCond.Always);
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Interaction Mode");

            ImGui.Spacing();
            foreach (var window in windows) {
                bool isOpen = window.IsWindowOpen;
                Vector4 color = isOpen ? new Vector4(0.5f, 0.6f, 0.5f, 1f) : new Vector4(0.6f, 0.5f, 0.5f, 1f);

                ImGui.TextColored(color, isOpen ? "[X]" : "[   ]");
                ImGui.SameLine();
                ImGui.TextColored(color, window.Definition.Title);
    
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Click to " + (isOpen ? "hide" : "show") + " this window");
                }
                if (ImGui.IsItemClicked())
                {
                    window.IsWindowOpen = !window.IsWindowOpen;
                }
            }
            ImGui.End();
        }

        ImGui.SetNextWindowPos(new Vector2(OverlayWidth - 10, 10), ImGuiCond.Always, new Vector2(1f, 0f));
        ImGui.Begin("Performance Overlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoBackground);
        ImGui.TextColored(new Vector4(1f,1f,1f,0.5f), $"{(int)(1/(AverageFrameTime / 1000f))}");
        ImGui.End();

        foreach (InternalWindow window in windows)
        {
            if (!window.IsWindowOpen) { continue; }
            if (window.Definition.NoWindow.GetValueOrDefault(false))
            {
                window.Render();
                continue;
            }

            ImGui.SetNextWindowSize(new Vector2(
                window.Definition.Width.GetValueOrDefault(480), 
                window.Definition.Height.GetValueOrDefault(320)
            ), ImGuiCond.Once);
            
            ImGui.SetNextWindowBgAlpha(window.Definition.Alpha.GetValueOrDefault(1f));
            ImGui.Begin(window.Definition.Title, window.Definition.Flags.GetValueOrDefault(ImGuiWindowFlags.None));
            
            ImGui.SetWindowPos(new Vector2(
                (int)window.Definition.X.GetValueOrDefault(OverlayWidth / 2), 
                (int)window.Definition.Y.GetValueOrDefault(OverlayHeight / 2)
            ), ImGuiCond.Once);

            var isCollapsed = ImGui.IsWindowCollapsed();
            if (isCollapsed) {
                ImGui.End(); 
                continue; 
            }

            try
            {
                RenderWindowContextMenu(window);
            } catch (Exception ex)
            {
                Logger.Error($"Error rendering context menu for window {window.Definition.Title}: {ex}");
            }

            try
            {
                window.Render();
            } catch (Exception ex)
            {
                Logger.Error($"Error rendering window {window.Definition.Title}: {ex}");
            }

            ImGui.End();
        }
    }

    private unsafe void RenderWindowContextMenu(InternalWindow window)
    {
        if (ImGui.BeginPopupContextWindow((byte*)0, ImGuiPopupFlags.MouseButtonRight))
        {
            window.RenderContextMenu();
            if (ImGui.MenuItem("Close"))
            {
                window.IsWindowOpen = false;
            }
            ImGui.EndPopup();
        }
    }

    private bool InitImGui()
    {
        imGuiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(imGuiContext);

        io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;     // Enable Keyboard Controls
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;      // Enable Gamepad Controls
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;         // Enable Docking
        // TODO: This is disabled for now as it causes submenus to appear below main windows.
        //io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;       // Enable Multi-Viewport / Platform Windows

        #if MACOSX
            float mainScale = 0.8f;
        #else
                var mon = GLFW.GetPrimaryMonitor();
                float mainScale = ImGuiImplGLFW.GetContentScaleForMonitor(Unsafe.BitCast<Hexa.NET.GLFW.GLFWmonitorPtr, Hexa.NET.ImGui.Backends.GLFW.GLFWmonitorPtr>(mon));
        #endif
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        // style.ScaleAllSizes(1.5f);

        style.ScaleAllSizes(mainScale);
        style.FontScaleDpi = mainScale;
        io.ConfigDpiScaleFonts = true;
        io.ConfigDpiScaleViewports = true;

        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            style.WindowRounding = 0.0f;
        }

        // Set fonts
        unsafe
        {
            string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Fonts", "Geist-Medium.ttf");
            if (!File.Exists(fontPath))
            {
                Logger.Error($"Font file not found at {fontPath}");
                // style.FontSizeBase = 18f;
            }
            else
            {
                io.Fonts.AddFontFromFileTTF(fontPath);
                style.FontSizeBase = 18f;
            }
        }

    #if !MACOSX
        ImGuiImplGLFW.SetCurrentContext(imGuiContext);
        if (!ImGuiImplGLFW.InitForOpenGL(Unsafe.BitCast<GLFWwindowPtr, Hexa.NET.ImGui.Backends.GLFW.GLFWwindowPtr>(glfwWindow), true))
        {
            Logger.Error("Failed to init ImGui Impl GLFW");
            GLFW.Terminate();
            return false;
        }
    #else
        // On macOS, ImGuiImplGLFW.dylib embeds its own copy of libglfw causing
        // ObjC class conflicts that corrupt the font atlas. Skip it entirely.
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
    #endif

        ImGuiImplOpenGL3.SetCurrentContext(imGuiContext);
        if (!ImGuiImplOpenGL3.Init(glslVersion))
        {
            Logger.Error("Failed to init ImGui Impl OpenGL3");
            GLFW.Terminate();
            return false;
        }

        gl.Enable(GLEnableCap.Blend);
        gl.BlendFunc(GLBlendingFactor.SrcAlpha, GLBlendingFactor.OneMinusSrcAlpha);
        gl.ClearColor(0f, 0f, 0f, 0f); // Transparent background
        return true;
    }

    private bool InitGLFW()
    {
        unsafe
        {
            GLFW.SetErrorCallback((error, description) =>
            {
                # if DEBUG
                Logger.Error($"GLFW Error {error}: {Utils.DecodeStringUTF8(description)}");
                # endif
            });
        }

        unsafe
        {
            Logger.Info("Initializing GLFW Version: " + Utils.DecodeStringUTF8(GLFW.GetVersionString()));
        }

        // This code sets the platform to X11 instead of wayland. This only needs to be
        // done inside vscode for whatever reason. https://github.com/opentk/opentk/issues/1823
        string? sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        string? useWayland  = Environment.GetEnvironmentVariable("GLFW_USE_WAYLAND");
        if (sessionType == "wayland" && useWayland == "0")
        {
            GLFW.InitHint(GLFW.GLFW_PLATFORM, GLFW.GLFW_PLATFORM_X11);
        }

        Console.WriteLine("Initializing GLFW...");
        GLFW.Init();
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 3);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 2);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE);  // 3.2+ only

        GLFW.WindowHint(GLFW.GLFW_TRANSPARENT_FRAMEBUFFER, 1);  // Transparent
        GLFW.WindowHint(GLFW.GLFW_DECORATED, 0);                // No window decorations
        GLFW.WindowHint(GLFW.GLFW_FLOATING, 1);                 // Always on top
        GLFW.WindowHint(GLFW.GLFW_FOCUSED, 0);                  // Start unfocused
        GLFW.WindowHint(GLFW.GLFW_FOCUS_ON_SHOW, 0);            // Start unfocused
        
        var mon = GLFW.GetPrimaryMonitor();
        int width, height;
        width = GLFW.GetVideoMode(mon).Width;
        height = GLFW.GetVideoMode(mon).Height;

        // NOTE: Width and height set to screen-1
        // If they are set to the screen size, windows does some optimizations that cause the window
        // to go full black when focused. Setting these to -1 seems to prevent that.
        glfwWindow = GLFW.CreateWindow(width - 1, height - 1, "ETS2LA overlay", null, null);
        if (glfwWindow.IsNull)
        {
            Logger.Error("Failed to create GLFW window");
            GLFW.Terminate();
            return false;
        }

        return true;
    }

    #if MACOSX
        private unsafe void SetMacOSWindowLevel()
        {
            IntPtr nsWindow = (IntPtr)GLFW.GetCocoaWindow(glfwWindow);
            IntPtr sel = sel_registerName("setLevel:");
            // NSScreenSaverWindowLevel = 1000, appears above fullscreen apps.
            // NSFloatingWindowLevel = 3 is NOT enough when a game runs fullscreen.
            int NSScreenSaverWindowLevel = 1000;
            objc_msgSend_void_int(nsWindow, sel, NSScreenSaverWindowLevel);
        }
    #endif

    public void RegisterWindow(WindowDefinition def, Action renderAction, Optional<Action> renderContextMenuAction = default)
    {
        foreach (var window in windows)
        {
            if (window.Definition.Title == def.Title)
            {
                window.Definition = def;
                window.Render = renderAction;
                window.RenderContextMenu = renderContextMenuAction.GetValueOrDefault(() => { });
                return;
            }
        }
        
        var newWindow = new ExternalWindow(def, renderAction, renderContextMenuAction.GetValueOrDefault(() => { }));
        windows.Add(newWindow);
    }

    public void UnregisterWindow(WindowDefinition def)
    {
        windows.RemoveAll(w => w.Definition.Title == def.Title);
    }

    public void Shutdown()
    {
        shutdown = true;
    }
}