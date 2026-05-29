using UnityEngine;

public sealed class CoreArchitectureTester : MonoBehaviour
{
    private ServiceRegistry _serviceRegistry;
    private SimpleEventBus _eventBus;
    private TickService _tickService;

    private void Awake()
    {
        _serviceRegistry = new ServiceRegistry();
        _eventBus = new SimpleEventBus();
        _tickService = new TickService();

        _serviceRegistry.Register(_serviceRegistry);
        _serviceRegistry.Register(_eventBus);
        _serviceRegistry.Register(_tickService);

        _eventBus.Subscribe<CoreArchitectureTestEvent>(OnCoreArchitectureTestEvent);
        _eventBus.Publish(new CoreArchitectureTestEvent("EventBus is working."));

        var testTickable = new CoreArchitectureTestTickable();
        _tickService.Register(testTickable);
        _tickService.Start();

        UnityEngine.Debug.Log("CoreArchitectureTester: core architecture created successfully.");
    }

    private void Update()
    {
        _tickService.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        _eventBus.Unsubscribe<CoreArchitectureTestEvent>(OnCoreArchitectureTestEvent);
        _eventBus.Clear();
        _tickService.Clear();
        _serviceRegistry.Clear();
    }

    private void OnCoreArchitectureTestEvent(CoreArchitectureTestEvent eventData)
    {
        UnityEngine.Debug.Log($"CoreArchitectureTester received event: {eventData.Message}");
    }
}