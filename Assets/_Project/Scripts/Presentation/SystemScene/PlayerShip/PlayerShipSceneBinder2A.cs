using UnityEngine;

/// <summary>
/// Простой scene-binder визуального корабля.
///
/// В S3-15 он только применяет финальный спрайт,
/// если он назначен вручную в Inspector.
///
/// Не обращается к Bootstrapper.
/// Не обращается к IConfigService.
/// Не использует SystemVisualConfig.
/// Не двигает корабль.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerShipSceneBinder2A : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private PlayerShipView2A playerShipView;

    [Header("Optional Final Graphics")]
    [Tooltip("Временное поле для проверки финальной графики корабля.")]
    [SerializeField] private Sprite finalShipSpriteOverride;

    private void Awake()
    {
        ResolveViewIfNeeded();
    }

    private void Start()
    {
        ApplyConfiguredGraphics();
    }

    public bool ApplyConfiguredGraphics()
    {
        ResolveViewIfNeeded();

        if (playerShipView == null)
            return false;

        if (finalShipSpriteOverride == null)
            return false;

        playerShipView.SetFinalShipSprite(
            finalShipSpriteOverride);

        playerShipView.ConfigureSorting();

        return true;
    }

    public void SetFinalShipSpriteOverride(Sprite sprite)
    {
        finalShipSpriteOverride = sprite;

        if (playerShipView != null && sprite != null)
        {
            playerShipView.SetFinalShipSprite(sprite);
        }
    }

    private void ResolveViewIfNeeded()
    {
        if (playerShipView != null)
            return;

        playerShipView =
            GetComponentInChildren<PlayerShipView2A>(
                true);
    }
}