using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlanetNodeView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject orbitImage;
    [SerializeField] private Image planetImage;
    [SerializeField] private TMP_Text label;

    private PlanetConfig _planet;
    private System.Action<string> _onClick;

    public void Initialize(PlanetConfig planet, System.Action<string> onClick, Vector2 center)
    {
        _planet = planet;
        _onClick = onClick;

        label.text = _planet.DisplayName;
        planetImage.sprite = _planet.PlanetSprite;

        RectTransform imageRect = planetImage.GetComponent<RectTransform>();
        imageRect.sizeDelta = new Vector2(_planet.PlanetOrbit.PlanetVisualSize, _planet.PlanetOrbit.PlanetVisualSize);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        if (button != null)
        {
            PlanetOrbitMotion motion = button.GetComponent<PlanetOrbitMotion>();
            Vector2 orbitCenter = planet.PlanetOrbit.OrbitCenterOffset;
            motion.Initialize(
                orbitCenter,
                planet
                );
        }

        if (orbitImage != null)
        {
            PlanetOrbitLineView planetOrbitLineView = orbitImage.GetComponent<PlanetOrbitLineView>();
            planetOrbitLineView.Initialize(_planet.PlanetOrbit);
        }
    }

    private void OnClick()
    {
        // _onClick?.Invoke(_planetId);
    }
}