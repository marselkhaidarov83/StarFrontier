using UnityEngine;
using UnityEngine.UI;

public class GalaxySystemInfoPanel : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text dangerText;
    [SerializeField] private Text developmentText;
    [SerializeField] private Text stabilityText;
    [SerializeField] private Button travelButton;

    private string _selectedSystemId;
    private GalaxyMapRuntimeView _owner;

    public void Initialize(GalaxyMapRuntimeView owner)
    {
        _owner = owner;

        if (travelButton != null)
            travelButton.onClick.AddListener(OnTravelClicked);
    }

    public void Show(
        string systemId,
        string displayName,
        bool isDiscovered,
        bool isVisited,
        bool isCurrent,
        int danger,
        int development,
        int stability)
    {
        _selectedSystemId = systemId;

        if (titleText != null)
            titleText.text = isDiscovered ? displayName : "???";

        if (statusText != null)
        {
            if (!isDiscovered)
                statusText.text = "Статус: скрыта";
            else if (isCurrent)
                statusText.text = "Статус: текущая";
            else if (isVisited)
                statusText.text = "Статус: посещена";
            else
                statusText.text = "Статус: открыта";
        }

        if (dangerText != null)
            dangerText.text = "Опасность: " + danger;

        if (developmentText != null)
            developmentText.text = "Развитие: " + development;

        if (stabilityText != null)
            stabilityText.text = "Стабильность: " + stability;

        if (travelButton != null)
            travelButton.gameObject.SetActive(isDiscovered && !isCurrent);

        gameObject.SetActive(true);
    }

    private void OnTravelClicked()
    {
        if (_owner != null)
            _owner.TryTravelToSelectedSystem(_selectedSystemId);
    }
}
