using System.Linq;
using TMPro;
using UnityEngine;

public class GalaxySectorNodeView2 : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sectorImage;
    [SerializeField] private TMP_Text sectorTitle;

    private IGameSessionService _gameSessionService;
    private GalaxyRuntimeState _galaxyRuntimeState;
    [SerializeField] private SectorConfig _sectorConfig;

    public void Initialize(SectorConfig config)
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _galaxyRuntimeState = _gameSessionService.State.Galaxy;

        _sectorConfig = config;
        SectorRuntimeState runtimeState = GetRuntimeState();
        bool isUnlocked =
                runtimeState != null &&
                runtimeState.IsUnlocked;

        transform.position = new Vector3(
            config.MapPosition.x,
            config.MapPosition.y,
            1f
        );

        // transform.localScale = new Vector3(
        //     config.MapSize.x,
        //     config.MapSize.y,
        //     1f
        // );

        if (sectorImage != null)
        {
            sectorImage.sprite = config.SectorPreviewImage;
            sectorImage.gameObject.SetActive(!isUnlocked);
        }

        if (sectorTitle != null)
        {
            sectorTitle.text = config.DisplayName;

            Color color = config.SectorTitleColor;
            color.a = isUnlocked ? 0.15f : 0.9f;
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