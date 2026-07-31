/// <summary>
/// Общий контракт для компонентов HUD,
/// которые получают зависимости через composition root.
///
/// Компонент:
/// - не создаёт сервисы;
/// - не хранит отдельную копию gameplay State;
/// - освобождает все подписки в Unbind().
/// </summary>
public interface ISystemHudWidgetBinder2A
{
    bool IsBound { get; }

    void Bind(SystemHudBindingContext2A context);

    void Unbind();
}