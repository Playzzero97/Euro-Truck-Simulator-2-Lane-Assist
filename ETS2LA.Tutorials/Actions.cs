using ETS2LA.Notifications;
using ETS2LA.Audio;
using Hexa.NET.ImGui;

namespace ETS2LA.Tutorials;

public enum TutorialActionType
{
    // Instant actions, these don't clear automatically.
    // Instead they wait for the next wait action to be
    // executed. PlaySound is an exception, as it only
    // plays a sound once.
    ShowMessage,
    ShowImguiWindow,
    SendNotification,
    PointAtScreen,
    PointAtCoordinate,
    PlaySound,
    ExecuteFunction,

    // These are so called "wait" actions.
    // They will clear previous instant actions
    // once completed.
    ShowMessageWaitNext,
    WaitForInput,
    WaitForEvent
}

public enum PointFrom
{
    Up,
    Down,
    Left,
    Right
}

public interface TutorialAction
{
    public TutorialActionType ActionType { get; }
}

public struct ShowMessageAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.ShowMessage;
    public string? Message { get; set; }
    public (float x, float y)? ScreenPosition { get; set; }
    public Func<(int, int)>? ScreenPositionCallback { get; set; }
    public (float x, float y)? Size { get; set; }
    public Func<(int, int)>? SizeCallback { get; set; }
    public ImGuiWindowFlags? ImGuiWindowFlags;
}

public struct ShowImguiWindowAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.ShowImguiWindow;
    public (float x, float y)? ScreenPosition { get; set; }
    public Func<(int, int)>? ScreenPositionCallback { get; set; }
    public (float x, float y)? Size { get; set; }
    public Func<(int, int)>? SizeCallback { get; set; }
    public Action? ImGuiCallback { get; set; }
    public ImGuiWindowFlags? ImGuiWindowFlags;
}

public struct SendNotificationAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.SendNotification;
    public string Title;
    public string Message;
    public NotificationLevel Level = NotificationLevel.Information;
    public float CloseAfter = 5f;

    public SendNotificationAction(string title, string message)
    {
        Title = title;
        Message = message;
    }
}

public struct PlaySoundAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.PlaySound;
    public string SoundFilePath { get; set; }
}

public struct ExecuteFunctionAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.ExecuteFunction;
    public Action? Function { get; set; }
}

public struct PointAtScreenAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.PointAtScreen;
    public (float x, float y)? ScreenPosition { get; set; }
    public Func<(int, int)>? ScreenPositionCallback { get; set; }
    public PointFrom PointFrom { get; set; }
}

public struct ShowMessageWaitNextAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.ShowMessageWaitNext;
    public string? Message { get; set; }
    public (float x, float y)? ScreenPosition { get; set; }
    public Func<(int, int)>? ScreenPositionCallback { get; set; }
    public (float x, float y)? Size { get; set; }
    public Func<(int, int)>? SizeCallback { get; set; }
    public ImGuiWindowFlags? ImGuiWindowFlags;
}

public struct WaitForInputAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.WaitForInput;
    public string ControlId { get; set; }
}

public struct WaitForEventAction : TutorialAction
{
    public TutorialActionType ActionType => TutorialActionType.WaitForEvent;
    public string EventId { get; set; }
}