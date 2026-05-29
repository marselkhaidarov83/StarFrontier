using UnityEngine;
using UnityEngine.UI;

public class OpenGalaxyMapButtonController2A : MonoBehaviour
{
    [SerializeField] private Button openGalaxyMapButton;

    private IGameStateMachine _gameStateMachine;
    private ISceneService _sceneService;

    private void Start()
    {
        _sceneService = Bootstrapper2A.Instance.ServiceRegistry.Get<ISceneService>();
        _gameStateMachine = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameStateMachine>();

        if (openGalaxyMapButton != null)
        {
            openGalaxyMapButton.onClick.AddListener(OpenGalaxyMap);
        }
        else
        {
            Debug.LogError("OpenGalaxyMapButtonController: Button is not assigned.");
        }
    }

    private void OnDestroy()
    {
        if (openGalaxyMapButton != null)
        {
            openGalaxyMapButton.onClick.RemoveListener(OpenGalaxyMap);
        }
    }

    private void OpenGalaxyMap()
    {
        _gameStateMachine.Enter(new GalaxyState2A());
        // _sceneService.LoadScene("2A_GalaxyMapScene");
    }
}