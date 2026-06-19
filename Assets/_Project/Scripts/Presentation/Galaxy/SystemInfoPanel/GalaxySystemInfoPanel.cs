using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GalaxySystemInfoPanel : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text pathText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text flyButtonText;

    [Header("Button")]
    [SerializeField] private Button flyButton;

    private string _nextSystemId;
    private Action<string> _onFlyClicked;

    public void Initialize(Action<string> onFlyClicked)
    {
        _onFlyClicked = onFlyClicked;

        if (flyButton != null)
            flyButton.onClick.AddListener(OnFlyButtonClicked);

        Hide();
    }

    public void Show(
        StarSystemConfig selectedSystem,
        StarSystemRuntimeState runtimeState,
        string currentSystemId,
        List<string> path,
        int totalPathCost,
        string nextSystemId,
        bool canStartTravel,
        string failReasonText)
    {
        if (selectedSystem == null)
        {
            Hide();
            return;
        }

        _nextSystemId = nextSystemId;

        bool isCurrent = selectedSystem.Id == currentSystemId;
        bool isDiscovered = runtimeState == null || runtimeState.IsDiscovered;

        if (titleText != null)
            titleText.text = isDiscovered ? selectedSystem.DisplayName : "???";

        if (statusText != null)
        {
            if (!isDiscovered)
                statusText.text = "Статус: скрыта";
            else if (isCurrent)
                statusText.text = "Статус: текущая система";
            else
                statusText.text = "Статус: доступна на карте";
        }

        if (pathText != null)
            pathText.text = BuildPathText(path);

        if (costText != null)
        {
            if (isCurrent)
                costText.text = "Вы уже находитесь в этой системе";
            else if (path == null || path.Count == 0)
                costText.text = "Маршрут не найден";
            else
                costText.text = "Стоимость маршрута: " + totalPathCost;
        }

        if (flyButtonText != null)
            flyButtonText.text = "Лететь";

        if (flyButton != null)
        {
            flyButton.gameObject.SetActive(!isCurrent && isDiscovered);
            flyButton.interactable = canStartTravel;
        }

        if (costText != null && !string.IsNullOrEmpty(failReasonText))
            costText.text = failReasonText;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private string BuildPathText(List<string> path)
    {
        if (path == null || path.Count == 0)
            return "Маршрут: не найден";

        if (path.Count == 1)
            return "Маршрут: текущая система";

        return "Маршрут: " + string.Join(" → ", path);
    }

    private void OnFlyButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(_nextSystemId))
            return;

        _onFlyClicked?.Invoke(_nextSystemId);
    }
}