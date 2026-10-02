using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DebugHudController : MonoBehaviour
{
    private void Awake()
    {
        if (!DeveloperModeGate.IsEnabled)
            gameObject.SetActive(false);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private TMP_Text outputText;
    [SerializeField, Min(0.1f)] private float refreshIntervalSeconds = 0.5f;

    private float _nextRefreshTime;

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double refreshMs = 0.0;

        bool refreshed = false;

        try
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            Refresh();

            refreshMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            refreshed = true;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            VisualUpdateAggregateLog.Record(
                "DebugHudController.Update",
                elapsedMs,
                "Name=" + name +
                " | Refreshed=" + refreshed +
                " | RefreshMs=" + refreshMs.ToString("F3"));
        }
    }

    [ContextMenu("Refresh Debug HUD")]
    private void Refresh()
    {
        _nextRefreshTime =
            Time.unscaledTime + refreshIntervalSeconds;

        if (outputText == null)
            return;

        outputText.text = BuildSummary();
    }

    private static string BuildSummary()
    {
        var text = new StringBuilder(256);
        text.AppendLine("STAR FRONTIER — DEBUG HUD");
        text.Append("App: ").AppendLine(Application.version);
        text.Append("Scene: ")
            .AppendLine(SceneManager.GetActiveScene().name);

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
        {
            text.AppendLine("Bootstrap: unavailable");
            return text.ToString();
        }

        IServiceRegistry registry = bootstrapper.ServiceRegistry;
        text.Append("Services: ").AppendLine(registry.Count.ToString());

        if (registry.TryGet<ITickService>(out ITickService tickService))
        {
            text.Append("Tick registrations: ")
                .AppendLine(tickService.Count.ToString());
            text.Append("Tick active: ")
                .AppendLine(tickService.IsTicking ? "yes" : "no");
        }
        else
        {
            text.AppendLine("Tick: unavailable");
        }

        if (registry.TryGet<IGameSessionService>(
            out IGameSessionService sessionService))
        {
            AppendStateSummary(text, sessionService.State);
        }
        else
        {
            text.AppendLine("State: unavailable");
        }

        return text.ToString();
    }

    private static void AppendStateSummary(
        StringBuilder text,
        GameRuntimeState state)
    {
        if (state == null)
        {
            text.AppendLine("State: null");
            return;
        }

        text.Append("Save data version: ")
            .AppendLine(state.Meta?.SaveDataVersion.ToString() ?? "unavailable");
        text.Append("Save version: ")
            .AppendLine(state.Meta?.SaveVersion.ToString() ?? "unavailable");
        text.Append("System: ")
            .AppendLine(SafeValue(state.Player?.CurrentSystemId));
        text.Append("Planet: ")
            .AppendLine(SafeValue(state.Player?.CurrentPlanetId));
    }

    private static string SafeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unavailable"
            : value;
    }
#endif
}
