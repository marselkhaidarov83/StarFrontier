public interface IPlayerAttackService
{
    string CurrentTargetNpcId { get; }

    void SetTarget(string targetNpcId);
    void ClearTarget();

    void Tick(int quantTick);
}