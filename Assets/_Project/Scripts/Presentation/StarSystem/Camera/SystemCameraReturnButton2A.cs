using UnityEngine;
using UnityEngine.UI;

public sealed class SystemCameraReturnButton2A : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private SystemCameraController2A cameraController;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        Bind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void SetCameraController(SystemCameraController2A controller)
    {
        cameraController = controller;
    }

    private void Bind()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnClicked);
    }

    private void Unbind()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }

    private void OnClicked()
    {
        if (cameraController == null)
            return;

        cameraController.ReturnToShip();
    }
}