using ETS2LA.Game.SDK;
using ETS2LA.Logging;

using System.Numerics;
using Hexa.NET.ImGui;
using TruckLib;
using Hexa.NET.OpenGL;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Avalonia.Remote.Protocol.Designer;
using ETS2LA.Telemetry;

namespace ETS2LA.Overlay.AR;

public class ARRenderer
{
    private CameraData cameraData;
    private GameTelemetryData telemetryData;
    private List<ARRenderCallback> renderCallbacks = new();
    private Matrix4x4 thisFrameProjection;
    private Matrix4x4 thisFrameView;
    private int thisFrameOffsetX = 0;
    private int thisFrameOffsetZ = 0;
    private int thisFrameWidth = 0;
    private int thisFrameHeight = 0;

    // These are all variables that are needed for
    // rendering ImGui windows in 3D space.
    private GL gl;
    private bool isWindowContextInitialized = false;
    private ImGuiContextPtr arWindowContext;
    private ARWindowBuffer currentWindowBuffer;
    private ImGuiContextPtr oldContext;

    public ARRenderer(GL gl)
    {
        this.gl = gl;

        // The font atlas needs to be shared between the main context
        // and our AR context here.
        var mainContext = ImGui.GetCurrentContext();
        unsafe
        {
            arWindowContext = ImGui.CreateContext(mainContext.IO.Fonts);
        }

        // I've set the default size of the AR window buffer to 1280x720.
        // TODO: Dynamic? Needs changing? TBD
        ImGui.SetCurrentContext(arWindowContext);
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(1280, 720);
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

        currentWindowBuffer = new ARWindowBuffer(gl, 1280, 720);
        cameraData = CameraProvider.Current.GetCurrentData();
        telemetryData = GameTelemetry.Current.GetCurrentData();

        ImGui.SetCurrentContext(mainContext);
    }

    /// <summary>
    ///  Renders all registered AR callbacks. This should be called every frame
    ///  from the main overlay rendering loop. <br/> 
    ///  Note: 3rd parties should not call this function.
    /// </summary>
    public void Render()
    {
        thisFrameProjection = default;
        thisFrameView = default;

        thisFrameWidth = (int)OverlayHandler.Current.OverlayWidth;
        thisFrameHeight = (int)OverlayHandler.Current.OverlayHeight;
        
        foreach (var callback in renderCallbacks)
        {
            try { callback.Render3D(); }
            catch (Exception ex)
            {
                Logger.Error($"Exception in AR render callback '{callback.Definition.Name}': {ex}");
            }
        }
    }

    /// <summary>
    ///  Register a new AR rendering callback. The provided callback will be called
    ///  every frame.
    /// </summary>
    /// <param name="callback">Callback definition / instance</param>
    public void RegisterRenderCallback(ARRenderCallback callback)
    {
        renderCallbacks.Add(callback);
    }

    /// <summary>
    ///  Unregister a previously registered callback.
    /// </summary>
    /// <param name="name">Name of the callback to remove</param>
    public void UnregisterRenderCallback(string name)
    {
        renderCallbacks.RemoveAll(callback => callback.Definition.Name == name);
    }

    /// <summary>
    ///  Get the current camera's view matrix. <br/>
    ///  NOTE: This matrix does not take into account ETS2 world wrapping.
    ///        You'll need to negate 512 * cameraData.cx from X and 512 * cameraData.cy from Z 
    ///        of other objects to get the relative position to the camera right. This is already
    ///        applied in all functions of this class.
    /// </summary>
    /// <returns>View matrix computed from camera data.</returns>
    public Matrix4x4 GetViewMatrix()
    {
        if (thisFrameView != default)
            return thisFrameView;
        

        thisFrameOffsetX = 512 * cameraData.cx;
        thisFrameOffsetZ = 512 * cameraData.cy;

        Vector3 camPos = new Vector3(
            cameraData.position.X, 
            cameraData.position.Y, 
            cameraData.position.Z
        );

        Quaternion camRot = new Quaternion(
            cameraData.rotation.X,
            cameraData.rotation.Y,
            cameraData.rotation.Z,
            cameraData.rotation.W
        );

        // The game's output is a bit weird, so we need to do adjustments to get it
        // to match what System.Numerics expects. There might be multiple inversions here?
        // TODO: Could be simplified?
        Quaternion invQuat = Quaternion.Conjugate(camRot);
        Vector3 euler = invQuat.ToEuler();
        Quaternion filteredRot = Quaternion.CreateFromYawPitchRoll(-euler.Y + (float)Math.PI, -euler.Z + (float)Math.PI, -euler.X);

        Matrix4x4 cameraWorldMatrix = Matrix4x4.CreateFromQuaternion(filteredRot) * 
                                      Matrix4x4.CreateTranslation(camPos);

        Matrix4x4.Invert(cameraWorldMatrix, out Matrix4x4 viewMatrix);
        thisFrameView = viewMatrix;
        return viewMatrix;
    }

    /// <summary>
    ///  Get the camera's projection matrix.
    /// </summary>
    /// <returns>Projection matrix returned by the game.</returns>
    public Matrix4x4 GetProjectionMatrix()
    {
        if (thisFrameProjection != default)
            return thisFrameProjection;
        
        // Game uses 0-1 for depth, but System.Numerics expects -1 to 1.
        // Simple transpose is enough to convert.
        thisFrameProjection = Matrix4x4.Transpose(cameraData.projection);
        return thisFrameProjection;
    }

    /// <summary>
    ///  Convert a world position into a screen coordinate. This takes all current
    ///  camera variables into account.
    /// </summary>
    /// <param name="worldPos">Target world position.</param>
    /// <param name="destinationWidth">Width of the destination viewport.</param>
    /// <param name="destinationHeight">Height of the destination viewport.</param>
    /// <returns>Screen coordinate if the world position is visible, otherwise null.</returns>
    public Vector2? WorldToScreen(Vector3 worldPos, int destinationWidth, int destinationHeight)
    {
        Matrix4x4 viewMatrix = GetViewMatrix();
        Matrix4x4 projectionMatrix = GetProjectionMatrix();

        Vector4 clipSpacePos = Vector4.Transform(new Vector4(worldPos, 1.0f), viewMatrix * projectionMatrix);
        if (clipSpacePos.W <= 0.1f) return null; // behind the camera

        // perspective divide to normalize coordinates
        // so that means x,y is -1 to 1 where 0,0 is the center
        Vector3 ndc = new Vector3(clipSpacePos.X, clipSpacePos.Y, clipSpacePos.Z) / clipSpacePos.W;
        // now -1 to 1 to screen coordinates
        float x = (ndc.X + 1.0f) * 0.5f * destinationWidth;
        float y = (1.0f - ndc.Y) * 0.5f * destinationHeight;

        return new Vector2(x, y);
    }

    /// <summary>
    ///  This function will return the world space coordinates of the provided ARCoordinate
    ///  while also taking into account the coordinate center. Truck positions are interpolated
    ///  to avoid jitter.
    /// </summary>
    /// <param name="coord">ARCoordinate to convert.</param>
    /// <returns>World space coordinates.</returns>
    /// <exception cref="ArgumentException"></exception>
    public Vector3 ARCoordinateToVector3(ARCoordinate coord)
    {
        switch (coord.Center)
        {
            case ARCoordinateCenter.World:
                return new Vector3(
                    coord.OffsetX - thisFrameOffsetX, 
                    coord.OffsetY, 
                    coord.OffsetZ - thisFrameOffsetZ
                );
            case ARCoordinateCenter.Truck:
                Vector3 offset = coord.OffsetToVector3();
                return new Vector3(
                    offset.X + cameraData.truckPosition.X - thisFrameOffsetX,
                    offset.Y + cameraData.truckPosition.Y,
                    offset.Z + cameraData.truckPosition.Z - thisFrameOffsetZ
                );
            case ARCoordinateCenter.Camera:
                return coord.OffsetToVector3() + cameraData.position;
            default:
                throw new ArgumentException("Invalid ARCoordinateCenter");
        }
    }

    private uint ConvertColor(uint rgba)
    {
        // ImGui uses ABGR, but we have ARGB, so we need to convert it.
        return
            ((rgba & 0xFF000000) >> 24) |
            ((rgba & 0x00FF0000) >> 8)  |
            ((rgba & 0x0000FF00) << 8)  |
            ((rgba & 0x000000FF) << 24); 
    }

    /// <summary>
    ///  Draw a line in 3D space. The line will be transformed and projected
    ///  onto the AR overlay.
    /// </summary>
    /// <param name="start">Start position of the line.</param>
    /// <param name="end">End position of the line.</param>
    /// <param name="color">Color of the line.</param>
    /// <param name="thickness">Thickness of the line in pixels.</param>
    public void Draw3DLine(ARCoordinate start, ARCoordinate end, UInt32 color, float thickness = 1.0f)
    {
        Vector2? p1 = WorldToScreen(ARCoordinateToVector3(start), thisFrameWidth, thisFrameHeight);
        Vector2? p2 = WorldToScreen(ARCoordinateToVector3(end), thisFrameWidth, thisFrameHeight);

        if (!p1.HasValue || !p2.HasValue) return;

        ImGui.GetBackgroundDrawList().AddLine(
            p1.Value, p2.Value, 
            ConvertColor(color), thickness
        );
    }

    /// <summary>
    ///  Draw a 3D circle in world space.
    /// </summary>
    /// <param name="center">The center of the circle.</param>
    /// <param name="radius">The radius of the circle.</param>
    /// <param name="color">The color of the circle.</param>
    /// <param name="filled">Whether the circle should be filled.</param>
    /// <param name="thickness">The thickness of the circle outline.</param>
    public void Draw3DCircle(ARCoordinate center, float radius, UInt32 color, bool filled = false, float thickness = 1)
    {
        Vector2? centerScreen = WorldToScreen(ARCoordinateToVector3(center), thisFrameWidth, thisFrameHeight);
        if (!centerScreen.HasValue) 
            return;

        if (filled) {
            ImGui.GetBackgroundDrawList().AddCircleFilled(
                centerScreen.Value, radius, ConvertColor(color)
            );
        }
        else {
            ImGui.GetBackgroundDrawList().AddCircle(
                centerScreen.Value, radius, ConvertColor(color), thickness
            );
        }
    }

    /// <summary>
    ///  Draw a 3D polygon in world space.
    /// </summary>
    /// <param name="points">List of points to draw, automatically closed.</param>
    /// <param name="color">The color of the polygon.</param>
    /// <param name="filled">Whether the polygon should be filled.</param>
    /// <param name="thickness">The thickness of the polygon outline.</param>
    public void Draw3DPolygon(ARCoordinate[] points, UInt32 color, bool filled = false, float thickness = 1)
    {
        if (points == null || points.Length < 3)
            return;

        Vector2[] screenPoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            Vector2? screenPos = WorldToScreen(ARCoordinateToVector3(points[i]), thisFrameWidth, thisFrameHeight);
            if (!screenPos.HasValue)
                return;

            screenPoints[i] = screenPos.Value;
        }

        if (filled)
        {
            unsafe
            {
                fixed (Vector2* p = &screenPoints[0])
                {
                    ImGui.GetBackgroundDrawList().AddConvexPolyFilled(p, screenPoints.Length, ConvertColor(color));
                }
            }
        }
        else
        {
            for (int i = 0; i < screenPoints.Length; i++)
            {
                ImGui.GetBackgroundDrawList().AddLine(
                    screenPoints[i], screenPoints[(i + 1) % screenPoints.Length], ConvertColor(color), thickness
                );
            }
        }
    }

    /// <summary>
    ///  Draw a 3D quad in world space.
    /// </summary>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <param name="p3"></param>
    /// <param name="p4"></param>
    /// <param name="color">Color of the quad.</param>
    /// <param name="filled">Whether this quad should be filled.</param>
    /// <param name="thickness">The thickness of a non filled quad.</param>
    public void Draw3DQuad(ARCoordinate p1, ARCoordinate p2, ARCoordinate p3, ARCoordinate p4, UInt32 color, bool filled = false, float thickness = 1)
    {
        Vector2? p1s = WorldToScreen(ARCoordinateToVector3(p1), thisFrameWidth, thisFrameHeight);
        Vector2? p2s = WorldToScreen(ARCoordinateToVector3(p2), thisFrameWidth, thisFrameHeight);
        Vector2? p3s = WorldToScreen(ARCoordinateToVector3(p3), thisFrameWidth, thisFrameHeight);
        Vector2? p4s = WorldToScreen(ARCoordinateToVector3(p4), thisFrameWidth, thisFrameHeight);

        if (!p1s.HasValue || !p2s.HasValue || !p3s.HasValue || !p4s.HasValue)
            return;

        if (filled)
        {
            ImGui.GetBackgroundDrawList().AddQuadFilled(
                p1s.Value, p2s.Value, p3s.Value, p4s.Value, 
                ConvertColor(color)
            );
        }
        else
        {            
            ImGui.GetBackgroundDrawList().AddQuad(
                p1s.Value, p2s.Value, p3s.Value, p4s.Value, 
                ConvertColor(color), thickness
            );
        }
    }

    /// <summary>
    ///  Draw a triangle in world space.
    /// </summary>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <param name="p3"></param>
    /// <param name="color">The color of the triangle.</param>
    /// <param name="filled">Whether this triangle should be filled.</param>
    /// <param name="thickness">The thickness of a non filled triangle.</param>
    public void Draw3DTriangle(ARCoordinate p1, ARCoordinate p2, ARCoordinate p3, UInt32 color, bool filled = false, float thickness = 1)
    {
        Vector2? p1s = WorldToScreen(ARCoordinateToVector3(p1), thisFrameWidth, thisFrameHeight);
        Vector2? p2s = WorldToScreen(ARCoordinateToVector3(p2), thisFrameWidth, thisFrameHeight);
        Vector2? p3s = WorldToScreen(ARCoordinateToVector3(p3), thisFrameWidth, thisFrameHeight);

        if (!p1s.HasValue || !p2s.HasValue || !p3s.HasValue)
            return;

        if (filled)
        {
            ImGui.GetBackgroundDrawList().AddTriangleFilled(
                p1s.Value, p2s.Value, p3s.Value, 
                ConvertColor(color)
            );
        }
        else
        {
            ImGui.GetBackgroundDrawList().AddTriangle(
                p1s.Value, p2s.Value, p3s.Value, 
                ConvertColor(color), thickness
            );
        }
    }

    /// <summary>
    ///  Begin rendering an ImGui window in the AR overlay.
    /// </summary>
    /// <param name="id">Id of the window.</param>
    /// <param name="flags">Flags for the window.</param>
    /// <param name="forceWidth">Forced width of the window.</param>
    /// <param name="forceHeight">Forced height of the window.</param>
    /// <param name="bgOpacity">Background opacity of the window.</param>
    public void BeginWindow(
        string id, 
        ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize,
        int forceWidth = 0,
        int forceHeight = 0,
        float bgOpacity = 1.0f
    )
    {
        if (isWindowContextInitialized) {
            Logger.Warn("AR window context already initialized. You probably called BeginWindow without a matching EndWindow.");
            return;
        }

        oldContext = ImGui.GetCurrentContext();
        ImGui.SetCurrentContext(arWindowContext);

        ImGuiImplOpenGL3.NewFrame();
        ImGui.NewFrame();

        ImGui.SetNextWindowBgAlpha(bgOpacity);
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        if (forceWidth > 0 && forceHeight > 0)
            ImGui.SetNextWindowSize(new Vector2(forceWidth, forceHeight));
        else
            ImGui.SetNextWindowSizeConstraints(new Vector2(0, 0), new Vector2(currentWindowBuffer.Width, currentWindowBuffer.Height));

        ImGui.Begin(id, flags);
        isWindowContextInitialized = true;
    }

    /// <summary>
    ///  End the rendering of an ImGui window. This will render the window into a texture
    ///  and then draw that texture onto the AR overlay.
    /// </summary>
    /// <param name="center">Center position of the window in world coordinates.</param>
    /// <param name="rotation">Rotation of the window in world coordinates.</param>
    /// <param name="width">Width of the window in world coordinates.</param>
    public void EndWindow(ARCoordinate center, Quaternion rotation, float width, bool invertY = false)
    {
        var windowSize = ImGui.GetWindowSize();
        var windowPos = ImGui.GetWindowPos(); 

        ImGui.End(); 
        ImGui.Render();

        // Here we clear the FBO and then render ImGui's
        // data into it.
        gl.BindFramebuffer(GLFramebufferTarget.Framebuffer, currentWindowBuffer.Fbo);
        gl.Viewport(0, 0, currentWindowBuffer.Width, currentWindowBuffer.Height);
        gl.ClearColor(0, 0, 0, 0);
        gl.Clear(GLClearBufferMask.ColorBufferBit);

        ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());
        gl.BindFramebuffer(GLFramebufferTarget.Framebuffer, 0);

        // Back to the main context, as we want to draw the texture
        // onto the main overlay background.
        ImGui.SetCurrentContext(oldContext);

        float windowAspectRatio = windowSize.Y / windowSize.X;
        float height = width * windowAspectRatio;

        float halfW = width / 2f;
        float halfH = height / 2f;

        // Mirroring the same quaternion adjustments as in GetViewMatrix.
        // Again, if anyone has a better understanding they can fix this.
        Quaternion invQuat = Quaternion.Conjugate(rotation);
        Vector3 euler = invQuat.ToEuler();
        Quaternion correctedRot = Quaternion.CreateFromYawPitchRoll(
            -euler.Y + (float)Math.PI, 
            -euler.Z + (float)Math.PI, 
            -euler.X
        );

        // Local space
        Vector3 localTL = new Vector3(-halfW,  halfH, 0);
        Vector3 localTR = new Vector3( halfW,  halfH, 0);
        Vector3 localBR = new Vector3( halfW, -halfH, 0);
        Vector3 localBL = new Vector3(-halfW, -halfH, 0);

        // Into world space
        Matrix4x4 modelMatrix = Matrix4x4.CreateFromQuaternion(correctedRot) 
                              * Matrix4x4.CreateTranslation(ARCoordinateToVector3(center));
        
        Vector3 pTL = Vector3.Transform(localTL, modelMatrix);
        Vector3 pTR = Vector3.Transform(localTR, modelMatrix);
        Vector3 pBR = Vector3.Transform(localBR, modelMatrix);
        Vector3 pBL = Vector3.Transform(localBL, modelMatrix);

        // And then projection into screen space
        Vector2? s1 = WorldToScreen(pTL, thisFrameWidth, thisFrameHeight);
        Vector2? s2 = WorldToScreen(pTR, thisFrameWidth, thisFrameHeight);
        Vector2? s3 = WorldToScreen(pBR, thisFrameWidth, thisFrameHeight);
        Vector2? s4 = WorldToScreen(pBL, thisFrameWidth, thisFrameHeight);

        if (s1.HasValue && s2.HasValue && s3.HasValue && s4.HasValue)
        {
            // Top/Bottom has to be 1f- to match properly.
            float uvLeft   = windowPos.X / currentWindowBuffer.Width;
            float uvTop    = 1.0f - ((windowPos.Y + windowSize.Y) / currentWindowBuffer.Height);
            float uvRight  = (windowPos.X + windowSize.X) / currentWindowBuffer.Width;
            float uvBottom = 1.0f - (windowPos.Y / currentWindowBuffer.Height);

            ImTextureID texID = (ImTextureID)currentWindowBuffer.Texture;
            ImTextureRef texRef;
            unsafe { texRef = new ImTextureRef(null, texID); }

            ImGui.GetBackgroundDrawList().AddImageQuad(
                texRef,
                s1.Value, s2.Value, s3.Value, s4.Value,
                invertY ? new Vector2(uvLeft, uvBottom) : new Vector2(uvLeft,  uvTop),
                invertY ? new Vector2(uvRight, uvBottom) : new Vector2(uvRight,  uvTop),
                invertY ? new Vector2(uvRight, uvTop)    : new Vector2(uvRight, uvBottom),
                invertY ? new Vector2(uvLeft, uvTop)      : new Vector2(uvLeft, uvBottom),
                0xFFFFFFFF
            );
        }

        isWindowContextInitialized = false;
    }
}