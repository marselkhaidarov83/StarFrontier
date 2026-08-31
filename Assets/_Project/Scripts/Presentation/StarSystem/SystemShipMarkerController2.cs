using UnityEngine;

// Скрипт управляет:
// - позицией изображения корабля на карте системы;
// - направлением изображения во время фактического движения.
public sealed class SystemShipMarkerController2 :
    CustomMonoBehaviour
{
    [SerializeField]
    private ShipMarkerView2 shipMarkerView2;

    [SerializeField]
    private SpriteRenderer shipMarkerImage;

    private SimpleEventBus _simpleEventBus;
    private ISystemTravelService _systemTravelService;
    private IHangarService _hangarService;
    private IShipMovementService _shipMovementService;
    private IConfigService _configService;
    private string _lastSystemId;
    private Pseudo3DDepthByY2 _pseudo3DDepth;
    private AllyConfig _lastActiveShipData;
    private Sprite _lastShipSprite;
    private float _lastShipWorldSize = -1f;

    private Vector3 _lastShipPosition;
    private bool _hasLastShipPosition;

    public void Initialize()
    {
        _simpleEventBus =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>();

        _systemTravelService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<ISystemTravelService>();

        _hangarService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IHangarService>();

        _shipMovementService =
            Bootstrapper.Instance
            .ServiceRegistry
            .Get<IShipMovementService>();

        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet<IConfigService>(
                out _configService);

        if (_systemTravelService == null)
        {
            Debug.LogError(
                "[SystemShipMarkerController2] " +
                "SystemTravelService not found.");

            return;
        }

        if (_hangarService == null)
        {
            Debug.LogError(
                "[SystemShipMarkerController2] " +
                "HangarService not found.");

            return;
        }

        SetShipImage();
        SubscribeEvents();

        /*
         * ВАЖНО:
         * сначала запоминаем фактическую стартовую позицию,
         * и только потом обновляем визуальную позицию.
         *
         * Иначе при первом RefreshPosition направление
         * вычисляется как:
         *
         * savedPosition - Vector3.zero
         *
         * и корабль поворачивается от центра карты
         * к своей сохранённой позиции.
         */
        _lastShipPosition =
            _systemTravelService
                .State
                .GetCurrentPosition();

        _hasLastShipPosition = true;

        _lastSystemId =
            _systemTravelService
                .State
                .CurrentSystemId;

        /*
         * На первом обновлении выставляется только позиция.
         * Текущее визуальное направление не изменяется.
         * Его восстанавливает MetaSceneInstaller2A из Save.
         */
        RefreshPosition(
            updateDirection: false);
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (_simpleEventBus == null)
            return;

        _simpleEventBus.Subscribe<ActiveShipChangedEvent>(
            OnActiveShipChanged);
    }

    private void UnsubscribeEvents()
    {
        if (_simpleEventBus == null)
            return;

        _simpleEventBus.Unsubscribe<ActiveShipChangedEvent>(
            OnActiveShipChanged);
    }

    private void OnActiveShipChanged(
        ActiveShipChangedEvent evt)
    {
        SetShipImage();
    }

    private void SetShipImage()
    {
        RefreshShipVisuals(
            force: true);
    }

    private void RefreshShipVisuals(
        bool force)
    {
        if (shipMarkerImage == null)
            return;

        if (_hangarService == null)
            return;

        var activeShipData =
            _hangarService.GetActiveShipData();

        if (activeShipData == null)
            return;

        Sprite activeShipSprite =
            activeShipData.CombatSprite;

        if (force ||
            _lastActiveShipData != activeShipData ||
            _lastShipSprite != activeShipSprite)
        {
            shipMarkerImage.sprite =
                activeShipSprite;

            _lastActiveShipData =
                activeShipData;

            _lastShipSprite =
                activeShipSprite;
        }

        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            float worldSize =
                _configService
                    .SystemVisualConfig
                    .GetAllyWorldSize(activeShipData);

            if (!force &&
                Mathf.Approximately(
                    _lastShipWorldSize,
                    worldSize))
            {
                return;
            }

            ApplyShipWorldSize(
                worldSize);

            _lastShipWorldSize =
                worldSize;
        }
    }

    private void ApplyShipWorldSize(
        float worldSize)
    {
        if (_pseudo3DDepth == null &&
            shipMarkerImage != null)
        {
            _pseudo3DDepth =
                shipMarkerImage
                    .GetComponentInParent<Pseudo3DDepthByY2>(
                        true);
        }

        if (_pseudo3DDepth != null)
        {
            _pseudo3DDepth.SetBaseWorldSize(
                shipMarkerImage,
                worldSize);

            return;
        }

        SpriteRendererSizeUtility.SetWorldSize(
            shipMarkerImage,
            worldSize);
    }

    private void Update()
    {
        if (_systemTravelService == null)
            return;

        RefreshShipVisuals(
            force: false);

        RefreshPosition(
            updateDirection: true);
    }

    private void RefreshPosition(
        bool updateDirection)
    {
        if (_systemTravelService == null)
        {
            if (IsDebug())
            {
                Debug.LogError(
                    "[SystemShipMarkerController2] " +
                    "SystemTravelService is null.");
            }

            return;
        }

        if (_systemTravelService.State == null)
        {
            if (IsDebug())
            {
                Debug.LogError(
                    "[SystemShipMarkerController2] " +
                    "SystemTravelState is null.");
            }

            return;
        }

        if (shipMarkerView2 == null)
        {
            if (IsDebug())
            {
                Debug.LogError(
                    "[SystemShipMarkerController2] " +
                    "ShipMarkerView2 is null.");
            }

            return;
        }

        Vector3 shipPosition =
            _systemTravelService
                .State
                .GetCurrentPosition();

        shipMarkerView2.SetPosition(
            shipPosition);

        string currentSystemId =
_systemTravelService
    .State
    .CurrentSystemId;

        bool enteredNewSystem =
            !string.IsNullOrWhiteSpace(
                currentSystemId) &&
            currentSystemId !=
                _lastSystemId;

        if (enteredNewSystem)
        {
            /*
             * Не используем разницу координат
             * между старой и новой системами.
             *
             * Берём направление, которое уже
             * рассчитано относительно солнца.
             */
            ApplyMovementFacingDirection();

            _lastSystemId =
                currentSystemId;

            _lastShipPosition =
                shipPosition;

            _hasLastShipPosition =
                true;

            return;
        }

        if (!string.IsNullOrWhiteSpace(
                currentSystemId))
        {
            _lastSystemId =
                currentSystemId;
        }

        /*
         * При первом чтении позиции направление
         * намеренно не вычисляется.
         */
        if (!_hasLastShipPosition)
        {
            _lastShipPosition =
                shipPosition;

            _hasLastShipPosition = true;

            return;
        }

        if (updateDirection)
        {
            if (!TryApplyRuntimeFacingDirection())
            {
                Vector3 movementDelta =
                    shipPosition -
                    _lastShipPosition;

                SetDirection(
                    movementDelta);
            }
        }

        /*
         * Обновляем предыдущую позицию только после того,
         * как рассчитали направление фактического движения.
         */
        _lastShipPosition =
            shipPosition;
    }

    private void ApplyMovementFacingDirection()
    {
        TryApplyRuntimeFacingDirection();
    }

    private bool TryApplyRuntimeFacingDirection()
    {
        if (_shipMovementService == null)
            return false;

        if (_shipMovementService.State == null)
            return false;

        Vector2 facingDirection =
            _shipMovementService
                .State
                .FacingDirection;

        if (facingDirection.sqrMagnitude <=
            0.0001f)
        {
            return false;
        }

        SetDirection(
            new Vector3(
                facingDirection.x,
                facingDirection.y,
                0f));

        return true;
    }

    public void SetDirection(
        Vector3 movementDirection)
    {
        /*
         * Когда корабль не движется, его направление
         * не меняется.
         *
         * Благодаря этому сохранённый поворот не заменяется
         * случайным или нулевым направлением при старте.
         */
        if (!IsFinite(movementDirection) ||
            movementDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle =
            Mathf.Atan2(
                movementDirection.y,
                movementDirection.x)
            * Mathf.Rad2Deg;

        if (shipMarkerImage != null)
        {
            shipMarkerImage
                .transform
                .localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        angle - 90f);
        }
    }

    private static bool IsFinite(
        Vector3 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y) &&
            IsFinite(value.z);
    }

    private static bool IsFinite(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}
