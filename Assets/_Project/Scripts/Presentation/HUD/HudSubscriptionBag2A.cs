using System;
using System.Collections.Generic;

/// <summary>
/// Хранит действия, которые необходимо выполнить
/// при отключении HUD-виджета.
///
/// Обычно сюда добавляются вызовы Unsubscribe.
/// Все действия выполняются в обратном порядке.
/// </summary>
public sealed class HudSubscriptionBag2A
{
    private readonly List<Action>
        _unsubscribeActions = new();

    public int Count =>
        _unsubscribeActions.Count;

    public void Add(Action unsubscribeAction)
    {
        if (unsubscribeAction == null)
        {
            throw new ArgumentNullException(
                nameof(unsubscribeAction));
        }

        _unsubscribeActions.Add(
            unsubscribeAction);
    }

    public void Clear()
    {
        Exception firstException = null;

        for (int index =
                 _unsubscribeActions.Count - 1;
             index >= 0;
             index--)
        {
            try
            {
                _unsubscribeActions[index]
                    .Invoke();
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }

        _unsubscribeActions.Clear();

        if (firstException != null)
        {
            throw new InvalidOperationException(
                "One or more HUD unsubscribe " +
                "actions failed.",
                firstException);
        }
    }
}