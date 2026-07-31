using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GalaxySectorNodeView : MonoBehaviour
{
    [SerializeField] private Image sectorImage;
    [SerializeField] private TMP_Text sectorTitle;

    private IGameSessionService _gameSessionService;
    private GalaxyRuntimeState _galaxyRuntimeState;

    private SectorConfig _sectorConfig;

    public void Initialize(
        SectorConfig config)
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _galaxyRuntimeState = _gameSessionService.State.Galaxy;

        _sectorConfig = config;
        SectorRuntimeState runtimeState = GetRuntimeState();
        bool isUnlocked =
                runtimeState != null &&
                runtimeState.IsUnlocked;

        RectTransform rectTransform =
            transform as RectTransform;

        rectTransform.anchoredPosition =
            config.MapPosition;

        rectTransform.sizeDelta =
            config.MapSize;

        if (sectorImage != null)
        {
            sectorImage.sprite = config.SectorPreviewImage;
            sectorImage.gameObject.SetActive(!isUnlocked);
        }

        if (sectorTitle != null)
        {
            sectorTitle.text = config.DisplayName;

            Color color = config.SectorTitleColor;

            if (isUnlocked)
                color.a = 0.15f;
            else
                color.a = 0.9f;

            sectorTitle.color = color;
        }
    }

    private SectorRuntimeState GetRuntimeState()
    {
        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Sectors == null)
            return null;

        return _galaxyRuntimeState.Sectors.FirstOrDefault(
            sector => sector.SectorId == _sectorConfig.Id
        );
    }
}