using System;

public sealed class BattleEventBus
{
    public event Action<BattleEvent> EventRaised;

    public void Raise(BattleEvent e)
    {
        EventRaised?.Invoke(e);
    }
}
