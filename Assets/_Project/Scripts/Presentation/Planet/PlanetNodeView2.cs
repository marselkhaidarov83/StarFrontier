using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlanetNodeView2 : CustomMonoBehaviour
{
    [SerializeField] private GameObject orbitImage;
    [SerializeField] private SpriteRenderer planetImage;
    [SerializeField] private GameObject planetImageObject;
    [SerializeField] private TMP_Text label;

    private PlanetConfig _planet;

    public void Initialize(PlanetConfig planet)
    {
        _planet = planet;

        label.text = _planet.DisplayName;
        planetImage.sprite = _planet.PlanetSprite;

        SpriteRendererSizeUtility.SetWorldSize(
            planetImage,
            _planet.PlanetOrbit.PlanetVisualSize
        );

        planetImageObject.GetComponent<PlanetSelectableView2>().Initialize(_planet);

        PlanetOrbitMotion2 motion = planetImage.GetComponent<PlanetOrbitMotion2>();
        motion.Initialize(planet);

        if (orbitImage != null)
        {
            PlanetOrbitLineView2 planetOrbitLineView = orbitImage.GetComponent<PlanetOrbitLineView2>();
            planetOrbitLineView.Initialize(_planet.PlanetOrbit);
        }
    }
}