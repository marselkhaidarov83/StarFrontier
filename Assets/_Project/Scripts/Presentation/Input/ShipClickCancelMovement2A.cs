using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Отменяет текущее движение при клике
/// или тапе по кораблю.
///
/// Работает как с legacy point-to-move,
/// так и с клавиатурным мостом этапа 2А.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ShipClickCancelMovement2A :
    MonoBehaviour,
    IPointerClickHandler
{
    [Header("Scene References")]
    [SerializeField]
    private ShipMarkerView2 shipMarkerView;

    [SerializeField]
    private MetaSceneKeyboardTravelBridge2A
        keyboardTravelBridge;

    [Header("Visual Feedback")]
    [SerializeField]
    private SpriteRenderer feedbackRenderer;

    [SerializeField]
    [Min(0.01f)]
    private float feedbackDuration = 0.25f;

    [SerializeField]
    private float feedbackStartScale = 0.85f;

    [SerializeField]
    private float feedbackEndScale = 1.25f;

    [SerializeField]
    [Range(0f, 1f)]
    private float feedbackStartAlpha = 0.9f;

    private IPlayerControlService _playerControlService;
    private ISystemTravelService _systemTravelService;
    private IShipMovementService _shipMovementService;
    private IPlayerAttackService _playerAttackService;

    private Coroutine _feedbackRoutine;
    private Vector3 _feedbackBaseScale = Vector3.one;
    private Color _feedbackBaseColor = Color.white;

    private void Awake()
    {
        ResolveSceneReferences();
        TryResolveServices();

        if (feedbackRenderer != null)
        {
            _feedbackBaseScale =
                feedbackRenderer.transform.localScale;

            _feedbackBaseColor =
                feedbackRenderer.color;

            feedbackRenderer.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        ResolveSceneReferences();
        TryResolveServices();
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (eventData != null &&
            eventData.button !=
            PointerEventData.InputButton.Left)
        {
            return;
        }

        /*
         * Не даём тому же клику попасть
         * в полноэкранный map handler.
         */
        eventData?.Use();

        TryResolveServices();

        _playerAttackService?.ClearSelectedTargetIfNoAssignedWeapons();

        if (_systemTravelService != null)
        {
            _systemTravelService.CancelTravel();

            Vector3 currentPosition =
                shipMarkerView != null
                    ? shipMarkerView.transform.position
                    : transform.position;

            _systemTravelService.SetCurrentPosition(
                currentPosition);
        }

        if (_playerControlService != null)
        {
            _playerControlService.SetRawMoveInput(
                Vector2.zero);
        }

        if (_shipMovementService != null)
        {
            _shipMovementService.StopImmediately();

            Vector3 currentPosition =
                shipMarkerView != null
                    ? shipMarkerView.transform.position
                    : transform.position;

            _shipMovementService.SetPosition(
                new Vector2(
                    currentPosition.x,
                    currentPosition.y));
        }

        if (keyboardTravelBridge != null)
        {
            keyboardTravelBridge
                .CancelMovementUntilInputReleased();
        }

        PlayFeedback();
    }

    private void ResolveSceneReferences()
    {
        if (shipMarkerView == null)
        {
            shipMarkerView =
                GetComponent<ShipMarkerView2>();
        }

        if (shipMarkerView == null)
        {
            shipMarkerView =
                GetComponentInParent<ShipMarkerView2>();
        }

        if (keyboardTravelBridge == null)
        {
            keyboardTravelBridge =
                FindFirstObjectByType<
                    MetaSceneKeyboardTravelBridge2A>();
        }
    }

    private bool TryResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _playerControlService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _systemTravelService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _shipMovementService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _playerAttackService);

        return _systemTravelService != null;
    }

    private void PlayFeedback()
    {
        if (feedbackRenderer == null)
            return;

        if (_feedbackRoutine != null)
        {
            StopCoroutine(_feedbackRoutine);
        }

        _feedbackRoutine =
            StartCoroutine(FeedbackRoutine());
    }

    private IEnumerator FeedbackRoutine()
    {
        feedbackRenderer.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < feedbackDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / feedbackDuration);

            float smoothT =
                Mathf.SmoothStep(0f, 1f, t);

            float currentScale =
                Mathf.Lerp(
                    feedbackStartScale,
                    feedbackEndScale,
                    smoothT);

            feedbackRenderer.transform.localScale =
                _feedbackBaseScale *
                currentScale;

            Color color = _feedbackBaseColor;

            color.a =
                Mathf.Lerp(
                    feedbackStartAlpha,
                    0f,
                    smoothT);

            feedbackRenderer.color = color;

            yield return null;
        }

        feedbackRenderer.transform.localScale =
            _feedbackBaseScale;

        feedbackRenderer.color =
            _feedbackBaseColor;

        feedbackRenderer.gameObject.SetActive(false);
        _feedbackRoutine = null;
    }

    private void OnDisable()
    {
        if (_feedbackRoutine != null)
        {
            StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
        }

        if (feedbackRenderer != null)
        {
            feedbackRenderer.transform.localScale =
                _feedbackBaseScale;

            feedbackRenderer.color =
                _feedbackBaseColor;

            feedbackRenderer.gameObject.SetActive(false);
        }
    }
}