using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Передаёт удержание UI-кнопки
/// в существующий PlayerInputBridge2A.
///
/// Не вызывает InteractionService напрямую.
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractionTouchButton2A :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [SerializeField]
    private PlayerInputBridge2A
        inputBridge;

    [SerializeField]
    private Button button;

    private bool _pressed;

    private void Awake()
    {
        if (button == null)
        {
            button =
                GetComponent<Button>();
        }
    }

    public void OnPointerDown(
        PointerEventData eventData)
    {
        if (button != null &&
            !button.interactable)
        {
            return;
        }

        if (inputBridge == null)
        {
            Debug.LogError(
                "[InteractionTouchButton2A] " +
                "PlayerInputBridge2A is not assigned.",
                this);

            return;
        }

        if (_pressed)
            return;

        _pressed = true;

        inputBridge
            .PressInteractFromTouch();
    }

    public void OnPointerUp(
        PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(
        PointerEventData eventData)
    {
        Release();
    }

    private void OnDisable()
    {
        Release();
    }

    private void Release()
    {
        if (!_pressed)
            return;

        _pressed = false;

        if (inputBridge != null)
        {
            inputBridge
                .ReleaseInteractFromTouch();
        }
    }
}