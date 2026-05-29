using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GalaxySystemNodeView2A : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject currentMarker;
    [SerializeField] private GameObject lockedMarker;
    [SerializeField] private GameObject capturedMarker;

    private string _systemId;
    private Action<string> _onClicked;

    public string SystemId => _systemId;

    public void Setup(
        StarSystemConfig config,
        StarSystemState state,
        bool isCurrentSystem,
        Action<string> onClicked)
    {
        _systemId = config.Id;
        _onClicked = onClicked;

        rectTransform.anchoredPosition = config.MapPosition;

        titleText.text = config.DisplayName;

        currentMarker.SetActive(isCurrentSystem);
        lockedMarker.SetActive(!state.IsUnlocked);
        capturedMarker.SetActive(state.IsCaptured);

        button.interactable = state.IsUnlocked;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        _onClicked?.Invoke(_systemId);
    }
}