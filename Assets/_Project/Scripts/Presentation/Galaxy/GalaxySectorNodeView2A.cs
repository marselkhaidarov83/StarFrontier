using TMPro;
using UnityEngine;

public class GalaxySectorNodeView2A : CustomMonoBehaviour
{
    [Header("Элементы")]
    [SerializeField] private SpriteRenderer sectorImage;
    [SerializeField] private TMP_Text sectorTitle;

    [Header("Слой отображения")]
    [SerializeField] private string sortingLayerName = "Default";

    [Header("Порядок отображения")]
    [SerializeField] private int closedImageOrder = 100;
    [SerializeField] private int closedTitleOrder = 101;
    [SerializeField] private int openedTitleOrder = -50;

    [Header("Прозрачность названия")]
    [SerializeField] private float openedTitleAlpha = 0.15f;
    [SerializeField] private float closedTitleAlpha = 0.9f;

    public void Initialize(SectorConfig config, bool isUnlocked)
    {
        if (config == null)
            return;

        transform.position = new Vector3(
            config.MapPosition.x,
            config.MapPosition.y,
            0f
        );

        // LogCustom("transform.localScale = " + transform.localScale);

        ApplyImage(config, isUnlocked);
        ApplyTitle(config, isUnlocked);
    }

    private void ApplyImage(SectorConfig config, bool isUnlocked)
    {
        if (sectorImage == null)
            return;

        sectorImage.sprite = config.SectorPreviewImage;
        sectorImage.gameObject.SetActive(!isUnlocked);

        sectorImage.sortingLayerName = sortingLayerName;
        sectorImage.sortingOrder = closedImageOrder;
    }

    private void ApplyTitle(SectorConfig config, bool isUnlocked)
    {
        if (sectorTitle == null)
            return;

        sectorTitle.text = config.Description;

        RectTransform titleRect = sectorTitle.rectTransform;
        titleRect.localPosition = Vector3.zero;

        Color titleColor = config.SectorTitleColor;
        titleColor.a = isUnlocked ? openedTitleAlpha : closedTitleAlpha;
        sectorTitle.color = titleColor;

        Renderer titleRenderer = sectorTitle.GetComponent<Renderer>();

        if (titleRenderer != null)
        {
            titleRenderer.sortingLayerName = sortingLayerName;
            titleRenderer.sortingOrder = isUnlocked
                ? openedTitleOrder
                : closedTitleOrder;
        }
    }
}