using UnityEngine;
using UnityEngine.UI;

//Скрипт управляет:
//    передвижением корабля по карте системы
//    картинкой корабля на карте системы
public sealed class SystemShipMarkerController : CustomMonoBehaviour
{
    [SerializeField] private ShipMarkerView shipMarkerView;
    [SerializeField] private Image shipMarkerImage;
    [SerializeField] private Image currentPositionMarkerImage;

    private SimpleEventBus _simpleEventBus;
    private ISystemTravelService _systemTravelService;
    private IHangarService _hangarService;

    private Vector2 _lastShipPosition;

    public void Initialize()
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _hangarService = Bootstrapper.Instance.ServiceRegistry.Get<IHangarService>();

        if (_systemTravelService == null)
        {
            Debug.LogError("[SystemShipMarkerController] SystemTravelService not found");
            return;
        }

        if (_hangarService == null)
        {
            Debug.LogError("[SystemShipMarkerController] HangarService not found");
            return;
        }

        SetShipImage();
        SubscribeEvents();
        RefreshPosition();

        _lastShipPosition = _systemTravelService.State.GetCurrentPosition();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (_simpleEventBus == null)
            return;

        _simpleEventBus.Subscribe<ActiveShipChangedEvent>(OnActiveShipChanged);
    }

    private void UnsubscribeEvents()
    {
        if (_simpleEventBus == null)
            return;

        _simpleEventBus.Unsubscribe<ActiveShipChangedEvent>(OnActiveShipChanged);
    }

    private void OnActiveShipChanged(ActiveShipChangedEvent evt)
    {
        SetShipImage();
    }

    private void SetShipImage()
    {
        if (shipMarkerImage != null)
            shipMarkerImage.GetComponent<Image>().sprite = _hangarService.GetActiveShipData().Icon;

        if (currentPositionMarkerImage != null)
            currentPositionMarkerImage.GetComponent<Image>().sprite = _hangarService.GetActiveShipData().Icon;
    }

    private void Update()
    {
        RefreshPosition();

        _lastShipPosition = _systemTravelService.State.GetCurrentPosition();
    }

    private void RefreshPosition()
    {
        if (_systemTravelService == null)
        {
            if (IsDebug())
                Debug.LogError("[SystemShipMarkerController] SystemTravelService is null");
            return;
        }

        if (shipMarkerView == null)
        {
            if (IsDebug())
                Debug.LogError("[SystemShipMarkerController] shipMarkerView is null");
            return;
        }

        Vector2 shipPosition = _systemTravelService.State.GetCurrentPosition();
        shipMarkerView.SetPosition(shipPosition);

        SetDirection(shipPosition - _lastShipPosition);
    }

    public void SetDirection(Vector2 movementDirection)
    {
        if (movementDirection.sqrMagnitude <= 0.001f)
            return;

        float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg;

        // Если glow нарисован "вниз", можно добавить поправку.
        if (shipMarkerImage != null)
            shipMarkerImage.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }
}