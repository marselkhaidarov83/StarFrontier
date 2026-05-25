using UnityEngine;
using UnityEngine.UI;

public sealed class PlanetSelectableView : MonoBehaviour
{
    [SerializeField] private PlanetConfig _planet;
    [SerializeField] private Button button;

    public PlanetConfig Planet => _planet;
    private SimpleEventBus _simpleEventBus;

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveAllListeners();
    }

    public void Initialize(PlanetConfig planet)
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _planet = planet;
        if (_planet == null)
        {
            Debug.LogWarning("[PlanetSelectableView] PlanetData is not assigned.");
            return;
        }

        if (button != null)
            button.onClick.RemoveAllListeners();

        if (button != null)
            button.onClick.AddListener(OnClicked);
        else
            Debug.LogError("[PlanetSelectableView] Button not found on planet prefab.");
    }

    private void OnClicked()
    {
        if (_planet == null)
        {
            Debug.LogWarning("[PlanetSelectableView] PlanetData is not assigned.");
            return;
        }

        _simpleEventBus.Publish(new PlanetSelectedEvent(_planet));
    }
}