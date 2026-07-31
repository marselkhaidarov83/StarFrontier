using System;
using System.Linq;
using TMPro;
using UnityEngine;

public class SectorLowLayerNodeView : CustomMonoBehaviour
{
    [Header("View")]
    [SerializeField] private SpriteRenderer sectorImage;
    [SerializeField] private TMP_Text titleText;

    [Header("Config")]
    [SerializeField] private SectorConfig _sectorConfig;

    [Header("System Visual")]
    [SerializeField] private Sprite sectorSprite;

    private IGameSessionService _gameSessionService;
    private GalaxyRuntimeState _galaxyRuntimeState;


    public void Initialize(SectorConfig sectorConfig)
    {
        _sectorConfig = sectorConfig;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _galaxyRuntimeState = _gameSessionService.State.Galaxy;

        transform.position = sectorConfig.MapPosition;

        SetState();
    }

    public void SetState()
    {
        if (_sectorConfig == null)
            return;

        titleText?.SetText(_sectorConfig.DisplayName);
        // SectorRuntimeState runtimeState = GetRuntimeState();
    }

    private SectorRuntimeState GetRuntimeState()
    {
        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Systems == null)
            return null;

        return _galaxyRuntimeState.Sectors.FirstOrDefault(
            sector => sector.SectorId == _sectorConfig.Id
        );
    }
}