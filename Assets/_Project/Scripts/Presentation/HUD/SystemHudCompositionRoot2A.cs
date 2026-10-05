using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Единственная точка подключения System HUD
/// к production-сервисам.
///
/// Composition root:
/// - не создаёт сервисы;
/// - получает IServiceRegistry от Bootstrapper;
/// - проверяет обязательные зависимости;
/// - передаёт один binding context всем HUD-виджетам;
/// - выполняет rollback при ошибке;
/// - вызывает Unbind при отключении и уничтожении.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class SystemHudCompositionRoot2A :
    CustomMonoBehaviour
{
    [Header("Binder discovery")]

    [Tooltip(
        "Корень, внутри которого находятся " +
        "HUD widget binders.")]
    [SerializeField]
    private Transform bindersRoot;

    [Tooltip(
    "Дополнительные HUD roots. " +
    "Используется, когда верхний и нижний HUD " +
    "находятся в разных Canvas.")]
    [SerializeField]
    private List<Transform> additionalBindersRoots =
    new List<Transform>();

    [SerializeField]
    private bool autoDiscoverChildBinders = true;

    [SerializeField]
    private bool includeInactiveBinders = false;

    [Tooltip(
        "Необязательный явный список binders. " +
        "Дубли автоматически исключаются.")]
    [SerializeField]
    private List<MonoBehaviour>
        explicitBinders = new();

    [Header("Startup")]

    [Min(0.1f)]
    [SerializeField]
    private float registryWaitTimeoutSeconds = 5f;

    [SerializeField]
    private bool validateRequiredServices = true;

    [SerializeField]
    private bool requireAtLeastOneBinder = true;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logLifecycle = true;

    [SerializeField]
    private bool isBound;

    [SerializeField]
    private int boundBinderCount;

    [SerializeField]
    private int registryServiceCount;

    [SerializeField]
    private string lastError = "";

    private readonly List<
        ISystemHudWidgetBinder2A>
        _boundBinders = new();

    private Coroutine _bindingRoutine;

    public bool IsBound =>
        isBound;

    public int BoundBinderCount =>
        boundBinderCount;

    private void Awake()
    {
        if (bindersRoot == null)
        {
            bindersRoot = transform;
        }
    }

    private void OnEnable()
    {
        BeginBinding();
    }

    private void OnDisable()
    {
        StopBindingRoutine();
        UnbindAll();
    }

    private void OnDestroy()
    {
        StopBindingRoutine();
        UnbindAll();
    }

    [ContextMenu("Bind HUD Now")]
    public void BeginBinding()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "HUD binding is available only " +
                "in Play Mode.",
                this);

            return;
        }

        StopBindingRoutine();

        _bindingRoutine =
            StartCoroutine(
                BindWhenRegistryReady());
    }

    [ContextMenu("Unbind HUD Now")]
    public void UnbindAll()
    {
        for (int index =
                 _boundBinders.Count - 1;
             index >= 0;
             index--)
        {
            try
            {
                _boundBinders[index]
                    .Unbind();
            }
            catch (Exception exception)
            {
                Debug.LogException(
                    exception,
                    this);
            }
        }

        _boundBinders.Clear();

        isBound = false;
        boundBinderCount = 0;

        if (logLifecycle &&
            Application.isPlaying)
        {
            LogCustom(
                "[2A-S03-03-T02] " +
                "System HUD unbound.");
        }
    }

    private IEnumerator BindWhenRegistryReady()
    {
        float deadline =
            Time.realtimeSinceStartup +
            registryWaitTimeoutSeconds;

        IServiceRegistry registry = null;

        while (!TryGetRegistry(out registry))
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                lastError =
                    "ServiceRegistry was not ready " +
                    $"within " +
                    $"{registryWaitTimeoutSeconds:0.0} " +
                    "seconds.";

                Debug.LogError(
                    "[2A-S03-03-T02] " +
                    lastError,
                    this);

                _bindingRoutine = null;
                yield break;
            }

            yield return null;
        }

        BindAll(registry);

        _bindingRoutine = null;
    }

    private void BindAll(
        IServiceRegistry registry)
    {
        UnbindAll();

        lastError = "";

        try
        {
            SystemHudBindingContext2A context =
                new(registry);

            if (validateRequiredServices)
            {
                ValidateRequiredServices(
                    context);
            }

            List<ISystemHudWidgetBinder2A>
                binders =
                    CollectBinders();

            if (requireAtLeastOneBinder &&
                binders.Count == 0)
            {
                throw new InvalidOperationException(
                    "No HUD widget binders were " +
                    "found under the configured root.");
            }

            foreach (
                ISystemHudWidgetBinder2A binder
                in binders)
            {
                binder.Bind(context);

                if (!binder.IsBound)
                {
                    throw new InvalidOperationException(
                        $"HUD binder " +
                        $"{binder.GetType().Name} " +
                        "did not enter bound state.");
                }

                _boundBinders.Add(binder);
            }

            registryServiceCount =
                registry.Count;

            boundBinderCount =
                _boundBinders.Count;

            isBound = true;

            if (logLifecycle)
            {
                LogCustom(
                    "[2A-S03-03-T02] " +
                    "System HUD bound. " +
                    $"Binders: {boundBinderCount}; " +
                    $"Registry services: " +
                    $"{registryServiceCount}.");
            }
        }
        catch (Exception exception)
        {
            lastError = exception.Message;

            /*
             * Rollback уже подключённых binders,
             * чтобы после частичной ошибки
             * не остались активные подписки.
             */
            UnbindAll();

            Debug.LogException(
                exception,
                this);
        }
    }

    private void ValidateRequiredServices(
        SystemHudBindingContext2A context)
    {
        RequireService<SimpleEventBus>(
            context);

        RequireService<IConfigService>(
            context);

        RequireService<
            ISystemGameplayStateService>(
                context);

        RequireService<IPlayerControlService>(
            context);
    }

    private static void RequireService<TService>(
        SystemHudBindingContext2A context)
        where TService : class
    {
        if (!context.TryGet<TService>(
                out TService service) ||
            service == null)
        {
            throw new InvalidOperationException(
                $"Required HUD service " +
                $"{typeof(TService).Name} " +
                "is not registered.");
        }
    }

    private List<ISystemHudWidgetBinder2A>
    CollectBinders()
    {
        List<ISystemHudWidgetBinder2A> result =
            new List<ISystemHudWidgetBinder2A>();

        HashSet<ISystemHudWidgetBinder2A> unique =
            new HashSet<ISystemHudWidgetBinder2A>();

        foreach (MonoBehaviour component
                 in explicitBinders)
        {
            TryAddBinder(
                component,
                result,
                unique,
                true);
        }

        if (!autoDiscoverChildBinders)
        {
            return result;
        }

        HashSet<Transform> searchRoots =
            new HashSet<Transform>();

        Transform primaryRoot =
            bindersRoot != null
                ? bindersRoot
                : transform;

        searchRoots.Add(primaryRoot);

        foreach (Transform additionalRoot
                 in additionalBindersRoots)
        {
            if (additionalRoot != null)
            {
                searchRoots.Add(additionalRoot);
            }
        }

        foreach (Transform searchRoot
                 in searchRoots)
        {
            MonoBehaviour[] components =
                searchRoot.GetComponentsInChildren
                    <MonoBehaviour>(true);

            foreach (MonoBehaviour component
                     in components)
            {
                TryAddBinder(
                    component,
                    result,
                    unique,
                    false);
            }
        }

        return result;
    }

    private void TryAddBinder(
        MonoBehaviour component,
        List<ISystemHudWidgetBinder2A> result,
        HashSet<ISystemHudWidgetBinder2A>
            unique,
        bool isExplicit)
    {
        if (component == null ||
            component == this)
        {
            return;
        }

        if (!includeInactiveBinders &&
            !component.isActiveAndEnabled)
        {
            return;
        }

        if (component is not
            ISystemHudWidgetBinder2A binder)
        {
            if (isExplicit)
            {
                Debug.LogError(
                    component.name +
                    " does not implement " +
                    nameof(ISystemHudWidgetBinder2A) +
                    ".",
                    component);
            }

            return;
        }

        if (unique.Add(binder))
        {
            result.Add(binder);
        }
    }

    private static bool TryGetRegistry(
        out IServiceRegistry registry)
    {
        registry = null;

        if (Bootstrapper.Instance == null)
        {
            return false;
        }

        registry =
            Bootstrapper.Instance
                .ServiceRegistry;

        return registry != null;
    }

    private void StopBindingRoutine()
    {
        if (_bindingRoutine == null)
        {
            return;
        }

        StopCoroutine(_bindingRoutine);
        _bindingRoutine = null;
    }
}