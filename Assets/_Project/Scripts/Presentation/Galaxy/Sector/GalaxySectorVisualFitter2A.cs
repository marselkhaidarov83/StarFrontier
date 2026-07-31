using System.Collections.Generic;
using UnityEngine;
using TMPro;

public sealed class GalaxySectorVisualFitter2A : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Camera mapCamera;
    [SerializeField] private Transform sectorNodesRoot;
    [SerializeField] private RectTransform topHudRoot;

    [Header("Заполнение по ширине")]
    [SerializeField] private bool fillScreenWidth = true;

    [Tooltip("Небольшой дополнительный выход картинки за края экрана.")]
    [SerializeField] private float horizontalOverlap = 0.02f;

    [Header("Заполнение сверху")]
    [SerializeField] private bool extendTopSectorToHud = true;

    [Tooltip("Небольшой заход верхней картинки под интерфейс.")]
    [SerializeField] private float topOverlap = 0.02f;

    private readonly Dictionary<SpriteRenderer, BaseVisualState> _baseStates = new();
    private readonly Dictionary<Transform, Vector3> _baseTitleLocalPositions = new();

    private struct BaseVisualState
    {
        public Vector3 LocalPosition;
        public Vector3 LocalScale;

        public BaseVisualState(
            Vector3 localPosition,
            Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalScale = localScale;
        }
    }

    public void FitNow()
    {
        if (mapCamera == null)
        {
            Debug.LogError(
                "[GalaxySectorVisualFitter2A] Map Camera не назначена."
            );

            return;
        }

        if (sectorNodesRoot == null)
        {
            Debug.LogError(
                "[GalaxySectorVisualFitter2A] Sector Nodes Root не назначен."
            );

            return;
        }

        SpriteRenderer[] sectorImages =
            sectorNodesRoot.GetComponentsInChildren<SpriteRenderer>(true);

        if (sectorImages == null || sectorImages.Length == 0)
            return;

        CacheBaseStates(sectorImages);
        RestoreBaseStates(sectorImages);

        if (fillScreenWidth)
            FillSectorImagesByScreenWidth(sectorImages);

        if (extendTopSectorToHud)
            ExtendTopSectorImage(sectorImages);
    }

    private void CacheBaseStates(SpriteRenderer[] sectorImages)
    {
        foreach (SpriteRenderer sectorImage in sectorImages)
        {
            if (sectorImage == null)
                continue;

            if (!_baseStates.ContainsKey(sectorImage))
            {
                _baseStates.Add(
                    sectorImage,
                    new BaseVisualState(
                        sectorImage.transform.localPosition,
                        sectorImage.transform.localScale
                    )
                );
            }

            TMP_Text sectorTitle = FindSectorTitle(sectorImage);

            if (sectorTitle != null &&
                !_baseTitleLocalPositions.ContainsKey(sectorTitle.transform))
            {
                _baseTitleLocalPositions.Add(
                    sectorTitle.transform,
                    sectorTitle.transform.localPosition
                );
            }
        }
    }

    private void RestoreBaseStates(SpriteRenderer[] sectorImages)
    {
        foreach (SpriteRenderer sectorImage in sectorImages)
        {
            if (sectorImage == null)
                continue;

            if (_baseStates.TryGetValue(
                    sectorImage,
                    out BaseVisualState baseState))
            {
                sectorImage.transform.localPosition =
                    baseState.LocalPosition;

                sectorImage.transform.localScale =
                    baseState.LocalScale;
            }

            TMP_Text sectorTitle = FindSectorTitle(sectorImage);

            if (sectorTitle != null &&
                _baseTitleLocalPositions.TryGetValue(
                    sectorTitle.transform,
                    out Vector3 baseTitlePosition))
            {
                sectorTitle.transform.localPosition =
                    baseTitlePosition;
            }
        }
    }

    private TMP_Text FindSectorTitle(SpriteRenderer sectorImage)
    {
        if (sectorImage == null)
            return null;

        Transform sectorRoot = sectorImage.transform.parent;

        if (sectorRoot == null)
            return null;

        return sectorRoot.GetComponentInChildren<TMP_Text>(true);
    }

    private void CenterSectorTitle(SpriteRenderer sectorImage)
    {
        TMP_Text sectorTitle = FindSectorTitle(sectorImage);

        if (sectorTitle == null)
            return;

        Vector3 titlePosition = sectorTitle.transform.position;
        Vector3 imageCenter = sectorImage.bounds.center;

        titlePosition.x = imageCenter.x;
        titlePosition.y = imageCenter.y;

        // Z оставляем исходным, чтобы не сломать порядок отображения.
        sectorTitle.transform.position = titlePosition;
    }

    private void FillSectorImagesByScreenWidth(
        SpriteRenderer[] sectorImages)
    {
        float mapPlaneZ = sectorNodesRoot.position.z;
        float cameraDistance = Mathf.Abs(
            mapCamera.transform.position.z - mapPlaneZ
        );

        Vector3 leftWorldPoint =
            mapCamera.ScreenToWorldPoint(
                new Vector3(
                    0f,
                    Screen.height * 0.5f,
                    cameraDistance
                )
            );

        Vector3 rightWorldPoint =
            mapCamera.ScreenToWorldPoint(
                new Vector3(
                    Screen.width,
                    Screen.height * 0.5f,
                    cameraDistance
                )
            );

        float targetWorldWidth =
            rightWorldPoint.x -
            leftWorldPoint.x +
            horizontalOverlap * 2f;

        foreach (SpriteRenderer sectorImage in sectorImages)
        {
            if (sectorImage == null || sectorImage.sprite == null)
                continue;

            float currentWorldWidth =
                GetSpriteWorldWidth(sectorImage);

            if (currentWorldWidth <= 0.001f)
                continue;

            float widthMultiplier =
                targetWorldWidth / currentWorldWidth;

            Vector3 localScale =
                sectorImage.transform.localScale;

            localScale.x *= widthMultiplier;

            sectorImage.transform.localScale = localScale;
        }
    }

    private void ExtendTopSectorImage(
        SpriteRenderer[] sectorImages)
    {
        if (topHudRoot == null)
            return;

        SpriteRenderer topSectorImage =
            FindTopVisibleSectorImage(sectorImages);

        if (topSectorImage == null ||
            topSectorImage.sprite == null)
        {
            return;
        }

        float hudBottomScreenY =
            GetTopHudBottomScreenY();

        float mapPlaneZ =
            topSectorImage.transform.position.z;

        float cameraDistance = Mathf.Abs(
            mapCamera.transform.position.z - mapPlaneZ
        );

        Vector3 hudBottomWorldPoint =
            mapCamera.ScreenToWorldPoint(
                new Vector3(
                    Screen.width * 0.5f,
                    hudBottomScreenY,
                    cameraDistance
                )
            );

        float targetTopWorldY =
            hudBottomWorldPoint.y + topOverlap;

        float currentTopWorldY =
            topSectorImage.bounds.max.y;

        float requiredExtension =
            targetTopWorldY - currentTopWorldY;

        // На эталонном разрешении картинку не уменьшаем.
        if (requiredExtension <= 0f)
            return;

        float currentWorldHeight =
            GetSpriteWorldHeight(topSectorImage);

        if (currentWorldHeight <= 0.001f)
            return;

        float targetWorldHeight =
            currentWorldHeight + requiredExtension;

        float heightMultiplier =
            targetWorldHeight / currentWorldHeight;

        Vector3 localScale =
            topSectorImage.transform.localScale;

        localScale.y *= heightMultiplier;

        topSectorImage.transform.localScale = localScale;

        // Увеличиваем только вверх:
        // нижний край картинки остаётся на старом месте.
        Vector3 worldPosition =
            topSectorImage.transform.position;

        worldPosition.y += requiredExtension * 0.5f;

        topSectorImage.transform.position = worldPosition;

        CenterSectorTitle(topSectorImage);
    }

    private SpriteRenderer FindTopVisibleSectorImage(
        SpriteRenderer[] sectorImages)
    {
        SpriteRenderer result = null;
        float highestPoint = float.MinValue;

        foreach (SpriteRenderer sectorImage in sectorImages)
        {
            if (sectorImage == null)
                continue;

            if (!sectorImage.enabled)
                continue;

            if (!sectorImage.gameObject.activeInHierarchy)
                continue;

            float topPoint = sectorImage.bounds.max.y;

            if (topPoint <= highestPoint)
                continue;

            highestPoint = topPoint;
            result = sectorImage;
        }

        return result;
    }

    private float GetTopHudBottomScreenY()
    {
        Vector3[] corners = new Vector3[4];
        topHudRoot.GetWorldCorners(corners);

        Canvas canvas =
            topHudRoot.GetComponentInParent<Canvas>();

        Camera canvasCamera = null;

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvasCamera = canvas.worldCamera;
        }

        float bottomY = float.MaxValue;

        foreach (Vector3 corner in corners)
        {
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint(
                    canvasCamera,
                    corner
                );

            bottomY = Mathf.Min(
                bottomY,
                screenPoint.y
            );
        }

        return bottomY;
    }

    private float GetSpriteWorldWidth(
        SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null ||
            spriteRenderer.sprite == null)
        {
            return 0f;
        }

        return spriteRenderer.sprite.bounds.size.x *
               Mathf.Abs(
                   spriteRenderer.transform.lossyScale.x
               );
    }

    private float GetSpriteWorldHeight(
        SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null ||
            spriteRenderer.sprite == null)
        {
            return 0f;
        }

        return spriteRenderer.sprite.bounds.size.y *
               Mathf.Abs(
                   spriteRenderer.transform.lossyScale.y
               );
    }
}