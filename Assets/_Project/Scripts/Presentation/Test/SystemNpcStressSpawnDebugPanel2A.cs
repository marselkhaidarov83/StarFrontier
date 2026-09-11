using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemNpcStressSpawnDebugPanel2A : MonoBehaviour
{
    private const int SpawnCount = 10;
    private const int EnemySpawnCount = 10;
    private const float RazmerShrifta = 23f;
    private const float CurrentSystemCountRefreshSeconds = 5f;

    private static readonly AllyRole2A[] SpawnRoles =
    {
        AllyRole2A.Ranger,
        AllyRole2A.Military,
        AllyRole2A.Trader,
        AllyRole2A.Science,
        AllyRole2A.Medic
    };

    private TMP_Text countText;
    private TMP_Text currentSystemCountText;
    private Button spawnNpcsButton;
    private Button spawnEnemyWaveButton;
    private float nextCurrentSystemCountRefreshTime;

    public static void EnsureCreated()
    {
        if (FindFirstObjectByType<SystemNpcStressSpawnDebugPanel2A>() != null)
            return;

        SystemSceneRoot2A sceneRoot =
            FindFirstObjectByType<SystemSceneRoot2A>();

        Canvas canvas =
            sceneRoot != null && sceneRoot.MetaCanvas != null
                ? sceneRoot.MetaCanvas
                : FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[SystemNpcStressSpawnDebugPanel2A] Canvas not found.");
            return;
        }

        GameObject panelObject =
            new GameObject(
                "SystemNpcStressSpawnDebugPanel2A",
                typeof(RectTransform));

        panelObject.transform.SetParent(canvas.transform, false);
        panelObject.AddComponent<SystemNpcStressSpawnDebugPanel2A>();
    }

    private void Awake()
    {
        BuildUi();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCurrentSystemCountRefreshTime)
            return;

        RefreshCurrentSystemCountText();
    }

    private void OnDestroy()
    {
        if (spawnNpcsButton != null)
            spawnNpcsButton.onClick.RemoveListener(SpawnNpcs);

        if (spawnEnemyWaveButton != null)
            spawnEnemyWaveButton.onClick.RemoveListener(SpawnEnemyWave);
    }

    private void BuildUi()
    {
        RectTransform rectTransform =
            GetComponent<RectTransform>();

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(16f, -156f);
        rectTransform.sizeDelta = new Vector2(260f, 146f);

        Image background =
            gameObject.AddComponent<Image>();

        background.color = new Color(0.03f, 0.05f, 0.07f, 0.82f);

        VerticalLayoutGroup layout =
            gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateSpawnButton();
        CreateEnemyWaveButton();
        CreateCountText();
        CreateCurrentSystemCountText();
        RefreshCurrentSystemCountText();
    }

    private void CreateSpawnButton()
    {
        GameObject buttonObject =
            new GameObject(
                "Spawn" + SpawnCount + "NpcsButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));

        buttonObject.transform.SetParent(transform, false);

        LayoutElement layoutElement =
            buttonObject.GetComponent<LayoutElement>();

        layoutElement.preferredHeight = 34f;

        Image image =
            buttonObject.GetComponent<Image>();

        image.color = new Color(0.14f, 0.20f, 0.24f, 0.96f);

        spawnNpcsButton =
            buttonObject.GetComponent<Button>();

        spawnNpcsButton.onClick.AddListener(SpawnNpcs);

        TextMeshProUGUI label =
            CreateTextObject(
                "Label",
                buttonObject.transform,
                "Spawn " + SpawnCount + " NPC",
                RazmerShrifta,
                TextAlignmentOptions.Center);

        Stretch(label.GetComponent<RectTransform>());
    }

    private void CreateEnemyWaveButton()
    {
        GameObject buttonObject =
            new GameObject(
                "SpawnEnemyWaveButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));

        buttonObject.transform.SetParent(transform, false);

        LayoutElement layoutElement =
            buttonObject.GetComponent<LayoutElement>();

        layoutElement.preferredHeight = 34f;

        Image image =
            buttonObject.GetComponent<Image>();

        image.color = new Color(0.30f, 0.12f, 0.10f, 0.96f);

        spawnEnemyWaveButton =
            buttonObject.GetComponent<Button>();

        spawnEnemyWaveButton.onClick.AddListener(SpawnEnemyWave);

        TextMeshProUGUI label =
            CreateTextObject(
                "Label",
                buttonObject.transform,
                "Spawn Enemy Wave",
                RazmerShrifta,
                TextAlignmentOptions.Center);

        Stretch(label.GetComponent<RectTransform>());
    }

    private void CreateCountText()
    {
        TextMeshProUGUI text =
            CreateTextObject(
                "NpcCountText",
                transform,
                "NPC total: -",
                RazmerShrifta,
                TextAlignmentOptions.Center);

        LayoutElement layoutElement =
            text.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = 22f;

        countText = text;
    }

    private void CreateCurrentSystemCountText()
    {
        TextMeshProUGUI text =
            CreateTextObject(
                "CurrentSystemNpcCountText",
                transform,
                "NPC in system: -",
                RazmerShrifta,
                TextAlignmentOptions.Center);

        LayoutElement layoutElement =
            text.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = 22f;

        currentSystemCountText = text;
    }

    private void SpawnNpcs()
    {
        if (!TryGetServices(
                out ISystemNpcPopulationService populationService,
                out ISystemNpcRuntimeService runtimeService))
        {
            return;
        }

        int beforeCount =
            GetNpcCount(runtimeService);

        int spawnedCommands = 0;
        int failedCommands = 0;

        for (int i = 0; i < SpawnCount; i++)
        {
            if (TrySpawnAnyAlly(populationService, i))
                spawnedCommands++;
            else
                failedCommands++;
        }

        int afterCount =
            GetNpcCount(runtimeService);

        if (countText != null)
            countText.text = "NPC total: " + afterCount;

        RefreshCurrentSystemCountText();

        Debug.Log(
            "[SystemNpcStressSpawnDebugPanel2A] Spawn " + SpawnCount + " NPC requested. " +
            "NpcCountBefore: " + beforeCount +
            ", NpcCountAfter: " + afterCount +
            ", Delta: " + (afterCount - beforeCount) +
            ", SuccessfulCommands: " + spawnedCommands +
            ", FailedCommands: " + failedCommands);
    }

    private void SpawnEnemyWave()
    {
        if (!TryGetServices(
                out ISystemNpcPopulationService populationService,
                out ISystemNpcRuntimeService runtimeService))
        {
            return;
        }

        int beforeCount =
            GetNpcCount(runtimeService);

        int spawnedEnemies = 0;
        int failedEnemies = 0;

        for (int i = 0; i < EnemySpawnCount; i++)
        {
            if (populationService.DebugSpawnEnemyInCurrentSystem())
                spawnedEnemies++;
            else
                failedEnemies++;
        }

        int afterCount =
            GetNpcCount(runtimeService);

        if (countText != null)
            countText.text = "NPC total: " + afterCount;

        RefreshCurrentSystemCountText();

        Debug.Log(
            "[SystemNpcStressSpawnDebugPanel2A] Spawn Enemy Wave requested. " +
            "RequestedEnemies: " + EnemySpawnCount +
            ", SpawnedEnemies: " + spawnedEnemies +
            ", FailedEnemies: " + failedEnemies +
            ", NpcCountBefore: " + beforeCount +
            ", NpcCountAfter: " + afterCount +
            ", Delta: " + (afterCount - beforeCount));
    }

    private void RefreshCurrentSystemCountText()
    {
        nextCurrentSystemCountRefreshTime =
            Time.unscaledTime + CurrentSystemCountRefreshSeconds;

        if (currentSystemCountText == null)
            return;

        if (!TryGetCurrentSystemNpcCount(
                out int count,
                out string currentSystemId))
        {
            currentSystemCountText.text = "NPC in system: -";
            return;
        }

        currentSystemCountText.text =
            "NPC in system: " + count;
    }

    private static bool TrySpawnAnyAlly(
        ISystemNpcPopulationService populationService,
        int spawnIndex)
    {
        for (int i = 0; i < SpawnRoles.Length; i++)
        {
            AllyRole2A role =
                SpawnRoles[(spawnIndex + i) % SpawnRoles.Length];

            if (populationService.DebugSpawnAllyInCurrentSystem(role))
                return true;
        }

        return false;
    }

    private static bool TryGetServices(
        out ISystemNpcPopulationService populationService,
        out ISystemNpcRuntimeService runtimeService)
    {
        populationService = null;
        runtimeService = null;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            Debug.LogWarning("[SystemNpcStressSpawnDebugPanel2A] Bootstrapper is unavailable.");
            return false;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance.ServiceRegistry;

        if (!registry.TryGet<ISystemNpcPopulationService>(
                out populationService) ||
            populationService == null)
        {
            Debug.LogWarning("[SystemNpcStressSpawnDebugPanel2A] ISystemNpcPopulationService is not registered.");
            return false;
        }

        if (!registry.TryGet<ISystemNpcRuntimeService>(
                out runtimeService) ||
            runtimeService == null)
        {
            Debug.LogWarning("[SystemNpcStressSpawnDebugPanel2A] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        return true;
    }

    private static bool TryGetCurrentSystemNpcCount(
        out int count,
        out string currentSystemId)
    {
        count = 0;
        currentSystemId = string.Empty;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance.ServiceRegistry;

        if (!registry.TryGet<IGameSessionService>(
                out IGameSessionService gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null ||
            gameSessionService.State.Player == null)
        {
            return false;
        }

        currentSystemId =
            gameSessionService.State.Player.CurrentSystemId;

        if (string.IsNullOrWhiteSpace(currentSystemId))
            return false;

        if (!registry.TryGet<ISystemNpcRuntimeService>(
                out ISystemNpcRuntimeService runtimeService) ||
            runtimeService == null)
        {
            return false;
        }

        count =
            runtimeService.GetAliveNpcsInSystem(currentSystemId).Count;

        return true;
    }

    private static int GetNpcCount(
        ISystemNpcRuntimeService runtimeService)
    {
        if (runtimeService == null ||
            runtimeService.Npcs == null)
        {
            return 0;
        }

        return runtimeService.Npcs.Count;
    }

    private static TextMeshProUGUI CreateTextObject(
        string name,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();

        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;

        return text;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}