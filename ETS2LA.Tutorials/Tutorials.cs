using ETS2LA.Backend.Events;
using ETS2LA.Settings;
using ETS2LA.Tutorials.DefaultTutorials;

namespace ETS2LA.Tutorials;

[Serializable]
class TutorialSettings
{
    public List<string> CompletedTutorials = new();
}

public class TutorialHandler
{
    private static readonly Lazy<TutorialHandler> _instance = new(() => new TutorialHandler());
    public static TutorialHandler Current => _instance.Value;

    public List<Tutorial> Tutorials { get; private set; }
    public List<TutorialExecutor> Executors { get; private set; }

    private TutorialSettings settings;
    private SettingsHandler settingsHandler;

    private TutorialHandler()
    {
        Tutorials = new List<Tutorial>();
        Executors = new List<TutorialExecutor>();

        settingsHandler = new();
        settings = settingsHandler.Load<TutorialSettings>("TutorialSettings.json");
        
        if (!settings.CompletedTutorials.Contains("OnboardingPart1"))
        {
            Events.Current.Subscribe<EventArgs>("ETS2LA.UI.WindowOpened", (e) =>
            {
                RegisterTutorial(new OnboardingPart1().Create());
                StartTutorial("OnboardingPart1");
            });
        } else if (!settings.CompletedTutorials.Contains("OnboardingPart2"))
        {
            Events.Current.Subscribe<EventArgs>("ETS2LA.UI.WindowOpened", (e) =>
            {
                RegisterTutorial(new OnboardingPart2().Create());
                StartTutorial("OnboardingPart2");
            });
        }
    }

    public void RegisterTutorial(Tutorial tutorial)
    {
        Tutorials.Add(tutorial);
    }

    public void RemoveTutorial(Tutorial tutorial)
    {
        Tutorials.Remove(tutorial);
    }

    private void CompleteTutorial(object sender, string tutorialTitle)
    {
        settings.CompletedTutorials.Add(tutorialTitle);
        settingsHandler.Save("TutorialSettings.json", settings);
    }

    public void StartTutorial(string tutorialTitle)
    {
        var tutorial = Tutorials.FirstOrDefault(t => t.Title == tutorialTitle);
        if (tutorial != null)
        {
            var executor = new TutorialExecutor(tutorial);
            executor.OnTutorialComplete += CompleteTutorial;
            Executors.Add(executor);
        }
    }

    public void Shutdown()
    {
        foreach (var executor in Executors)
        {
            executor.shutdown = true;
        }
    }
}