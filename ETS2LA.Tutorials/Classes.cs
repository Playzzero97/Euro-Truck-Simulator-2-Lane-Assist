using Hexa.NET.ImGui;
using ETS2LA.Overlay;
using ETS2LA.Controls;
using ETS2LA.Backend.Events;
using ETS2LA.Logging;
using ETS2LA.Notifications;
using ETS2LA.Audio;

namespace ETS2LA.Tutorials;

public struct TutorialSection
{
    public string Title;
    public List<TutorialAction> Actions;
}

public class Tutorial
{
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string Source { get; private set; }
    public List<TutorialSection> Sections { get; private set; }

    public Tutorial(string title, string description, string source, List<TutorialSection> sections)
    {
        Title = title;
        Description = description;
        Source = source;
        Sections = sections;
    }
}

public class TutorialExecutor
{
    public bool shutdown = false;

    private Tutorial tutorial;
    private int sectionIndex;
    private int actionIndex;

    private bool clearAfter = false;
    private bool actionLocked = false;

    private string waitingForInput = string.Empty;
    private string waitingForEvent = string.Empty;
    private string waitingForEventData = string.Empty;

    private List<Action> imguiCallbacks;
    private List<WindowDefinition> imguiWindowDefinitions;

    public event EventHandler<string> OnTutorialComplete;

    public TutorialExecutor(Tutorial tutorial)
    {
        this.tutorial = tutorial;
        sectionIndex = 0;
        actionIndex = 0;
        imguiCallbacks = new List<Action>();
        imguiWindowDefinitions = new List<WindowDefinition>();

        Thread tutorialThread = new Thread(ExecutionThread);
        tutorialThread.Start();
    }

    private void RegisterImGuiWindow(WindowDefinition def, Action callback)
    {
        imguiWindowDefinitions.Add(def);
        imguiCallbacks.Add(callback);
        OverlayHandler.Current.RegisterWindow(imguiWindowDefinitions.Last(), imguiCallbacks.Last());
    }

    private void ClearWaitingForInput(object sender, ControlChangeEventArgs args)
    {
        actionLocked = false;
        ControlsBackend.Current.UnregisterListener(waitingForInput, ClearWaitingForInput);
        waitingForInput = string.Empty;
    }

    private void ClearWaitingForEvent(EventArgs args)
    {
        actionLocked = false;
        Events.Current.Unsubscribe<EventArgs>(waitingForEvent, ClearWaitingForEvent);
        waitingForEvent = string.Empty;
    }

    public void ExecuteAction()
    {
        var action = tutorial.Sections[sectionIndex].Actions[actionIndex];
        switch (action.ActionType)
        {
            case TutorialActionType.ShowMessage:
                ShowMessageAction showMessage = (ShowMessageAction)action;
                RegisterImGuiWindow(new WindowDefinition
                {
                    Title = $"ShowMessage {sectionIndex} - {actionIndex}",
                    Flags = showMessage.ImGuiWindowFlags.GetValueOrDefault(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize),
                    X = showMessage.ScreenPosition.HasValue ? (int)showMessage.ScreenPosition.Value.x : -1,
                    Y = showMessage.ScreenPosition.HasValue ? (int)showMessage.ScreenPosition.Value.y : -1,
                    LocationFunction = showMessage.ScreenPositionCallback != null ? showMessage.ScreenPositionCallback : null,
                    SizingFunction = showMessage.SizeCallback != null ? showMessage.SizeCallback : null
                }, () =>
                {
                    ImGui.Text(showMessage.Message);
                });
                break;
            case TutorialActionType.ShowMessageWaitNext:
                ShowMessageWaitNextAction showMessageWaitNext = (ShowMessageWaitNextAction)action;
                RegisterImGuiWindow(new WindowDefinition
                {
                    Title = $"ShowMessageWaitNext {sectionIndex} - {actionIndex}",
                    Flags = showMessageWaitNext.ImGuiWindowFlags.GetValueOrDefault(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize),
                    X = showMessageWaitNext.ScreenPosition.HasValue ? (int)showMessageWaitNext.ScreenPosition.Value.x : -1,
                    Y = showMessageWaitNext.ScreenPosition.HasValue ? (int)showMessageWaitNext.ScreenPosition.Value.y : -1,
                    LocationFunction = showMessageWaitNext.ScreenPositionCallback != null ? showMessageWaitNext.ScreenPositionCallback : null,
                    SizingFunction = showMessageWaitNext.SizeCallback != null ? showMessageWaitNext.SizeCallback : null
                }, () =>
                {
                    ImGui.Text(showMessageWaitNext.Message);
                    ImGui.Button("Next");
                    if (ImGui.IsItemClicked())
                    {
                        actionLocked = false;
                    }
                });
                clearAfter = true;
                actionLocked = true;
                break;
            case TutorialActionType.ShowImguiWindow:
                ShowImguiWindowAction showImguiWindow = (ShowImguiWindowAction)action;
                RegisterImGuiWindow(new WindowDefinition
                {
                    Title = $"ShowImguiWindow {sectionIndex} - {actionIndex}",
                    Flags = showImguiWindow.ImGuiWindowFlags.GetValueOrDefault(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize),
                    X = showImguiWindow.ScreenPosition.HasValue ? (int)showImguiWindow.ScreenPosition.Value.x : -1,
                    Y = showImguiWindow.ScreenPosition.HasValue ? (int)showImguiWindow.ScreenPosition.Value.y : -1,
                    LocationFunction = showImguiWindow.ScreenPositionCallback != null ? showImguiWindow.ScreenPositionCallback : null,
                    SizingFunction = showImguiWindow.SizeCallback != null ? showImguiWindow.SizeCallback : null
                }, () =>
                {
                    showImguiWindow.ImGuiCallback?.Invoke();
                });
                break;
            case TutorialActionType.SendNotification:
                SendNotificationAction sendNotification = (SendNotificationAction)action;
                NotificationHandler.Current.SendNotification(new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = sendNotification.Title,
                    Content = sendNotification.Message,
                    Level = sendNotification.Level,
                    CloseAfter = sendNotification.CloseAfter
                });
                break;
            case TutorialActionType.PointAtScreen:
                // Point at a specific screen position
                break;
            case TutorialActionType.PointAtCoordinate:
                // Point at a specific coordinate
                break;
            case TutorialActionType.PlaySound:
                PlaySoundAction playSound = (PlaySoundAction)action;
                AudioHandler.Current.Queue(playSound.SoundFilePath);
                break;
            case TutorialActionType.ExecuteFunction:
                ExecuteFunctionAction executeFunction = (ExecuteFunctionAction)action;
                executeFunction.Function?.Invoke();
                break;
            case TutorialActionType.WaitForInput:
                WaitForInputAction waitForInput = (WaitForInputAction)action;
                clearAfter = true;
                actionLocked = true;
                waitingForInput = waitForInput.ControlId;
                ControlsBackend.Current.On(waitForInput.ControlId, ClearWaitingForInput);
                break;
            case TutorialActionType.WaitForEvent:
                WaitForEventAction waitForEvent = (WaitForEventAction)action;
                clearAfter = true;
                actionLocked = true;
                waitingForEvent = waitForEvent.EventId;
                Events.Current.Subscribe<EventArgs>(waitForEvent.EventId, ClearWaitingForEvent);
                break;
        }
    }

    public void ExecuteSection()
    {
        actionIndex = 0;
        while (actionIndex < tutorial.Sections[sectionIndex].Actions.Count && !shutdown)
        {
            ExecuteAction();
            while (actionLocked && !shutdown)
            {
                Thread.Sleep(100);
            }

            if (clearAfter || actionIndex >= tutorial.Sections[sectionIndex].Actions.Count)
            {
                int windowCount = imguiCallbacks.Count;
                for (int i = 0; i < windowCount; i++)
                {
                    OverlayHandler.Current.UnregisterWindow(imguiWindowDefinitions[i]);
                }
                imguiWindowDefinitions.Clear();
                imguiCallbacks.Clear();
                clearAfter = false;
            }

            actionIndex++;
        }
    }

    public void ExecutionThread()
    {
        sectionIndex = 0;
        while (sectionIndex < tutorial.Sections.Count && !shutdown)
        {
            Logger.Info($"Executing section {sectionIndex} of tutorial {tutorial.Title}");
            ExecuteSection();
            sectionIndex++;
        }
        if (!shutdown)
        {
            Logger.Success($"Finished executing tutorial {tutorial.Title}");
            OnTutorialComplete?.Invoke(this, tutorial.Title);
        }
    }
}