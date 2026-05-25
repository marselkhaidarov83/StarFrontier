using UnityEngine;

public sealed class PlanetOrbitLineView2 : MonoBehaviour
{
    [SerializeField] private SpriteRenderer orbitLineImage;
    [SerializeField] private Sprite normalPlanetOrbit;
    [SerializeField] private Sprite selectedPlanetOrbit;

    public void Initialize(PlanetOrbitConfig orbitConfig)
    {
        if (orbitConfig == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        float diameter = orbitConfig.OrbitRadius * 2f;

        transform.position = orbitConfig.OrbitCenterOffset;

        SpriteRendererSizeUtility.SetWorldSize(
            orbitLineImage,
            diameter
        );
    }
}