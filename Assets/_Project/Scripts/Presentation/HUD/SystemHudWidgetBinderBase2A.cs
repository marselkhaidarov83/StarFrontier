using System;
using UnityEngine;

/// <summary>
/// Базовый MonoBehaviour для HUD-виджетов.
///
/// Гарантирует:
/// - один активный Bind;
/// - очистку подписок перед повторным Bind;
/// - очистку при Disable и Destroy;
/// - отсутствие создания gameplay-сервисов.
/// </summary>
public abstract class SystemHudWidgetBinderBase2A :
    MonoBehaviour,
    ISystemHudWidgetBinder2A
{
    [SerializeField]
    private bool isBound;

    private readonly HudSubscriptionBag2A
        _subscriptions = new();

    protected SystemHudBindingContext2A Context
    {
        get;
        private set;
    }

    public bool IsBound =>
        isBound;

    public void Bind(
        SystemHudBindingContext2A context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(
                nameof(context));
        }

        /*
         * Повторный Bind сначала освобождает
         * прежние подписки.
         */
        if (isBound ||
            Context != null ||
            _subscriptions.Count > 0)
        {
            Unbind();
        }

        Context = context;

        try
        {
            OnBind();
            isBound = true;
        }
        catch
        {
            SafeClearSubscriptions();

            Context = null;
            isBound = false;

            throw;
        }
    }

    public void Unbind()
    {
        if (!isBound &&
            Context == null &&
            _subscriptions.Count == 0)
        {
            return;
        }

        try
        {
            OnUnbind();
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception,
                this);
        }

        SafeClearSubscriptions();

        Context = null;
        isBound = false;
    }

    /// <summary>
    /// Регистрирует действие отписки.
    /// Оно будет вызвано автоматически в Unbind().
    /// </summary>
    protected void TrackSubscription(
        Action unsubscribeAction)
    {
        _subscriptions.Add(
            unsubscribeAction);
    }

    protected abstract void OnBind();

    protected virtual void OnUnbind()
    {
    }

    protected virtual void OnDisable()
    {
        Unbind();
    }

    protected virtual void OnDestroy()
    {
        Unbind();
    }

    private void SafeClearSubscriptions()
    {
        try
        {
            _subscriptions.Clear();
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception,
                this);
        }
    }
}