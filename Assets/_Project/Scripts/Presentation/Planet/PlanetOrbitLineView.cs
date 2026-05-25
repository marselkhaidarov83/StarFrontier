using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlanetOrbitLineView : MonoBehaviour
{
    [SerializeField] private RectTransform orbitLineRect;
    [SerializeField] private Image orbitLineImage;
    [SerializeField] private Sprite normalPlanetOrbit;
    [SerializeField] private Sprite selectedPlanetOrbit;

    private void Reset()
    {
        orbitLineRect = GetComponent<RectTransform>();
        orbitLineImage = GetComponent<Image>();
    }

    public void Initialize(PlanetOrbitConfig orbitConfig)
    {
        if (orbitConfig == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (orbitLineRect == null)
            orbitLineRect = GetComponent<RectTransform>();

        float diameter = orbitConfig.OrbitRadius * 2f;

        orbitLineRect.anchorMin = new Vector2(0.5f, 0.5f);
        orbitLineRect.anchorMax = new Vector2(0.5f, 0.5f);
        orbitLineRect.pivot = new Vector2(0.5f, 0.5f);

        orbitLineRect.anchoredPosition = orbitConfig.OrbitCenterOffset;
        orbitLineRect.sizeDelta = new Vector2(diameter, diameter);

        if (orbitLineImage != null)
            orbitLineImage.raycastTarget = false;
    }
}