using UnityEngine;

public sealed class Pseudo3DSceneMarker : MonoBehaviour
{
    [SerializeField] private Transform playerShip;
    [SerializeField] private Transform cameraRig;
    [SerializeField] private Transform backLayer;
    [SerializeField] private Transform midLayer;
    [SerializeField] private Transform frontLayer;

    public Transform PlayerShip => playerShip;
    public Transform CameraRig => cameraRig;
    public Transform BackLayer => backLayer;
    public Transform MidLayer => midLayer;
    public Transform FrontLayer => frontLayer;
}