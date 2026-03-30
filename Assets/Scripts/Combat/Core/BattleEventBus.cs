using System;

public sealed class BattleEventBus
{
    public event Action<BattleEvent> EventRaised;
    public event Action<BattleEventGroup> GroupRaised;

    private BattleEventGroup _currentGroup;

    public void BeginGroup()
    {
        _currentGroup = new BattleEventGroup();
    }

    public void EndGroup()
    {
        var group = _currentGroup;
        _currentGroup = null;

        if (group != null && group.Events.Count > 0)
            GroupRaised?.Invoke(group);
    }

    public void Raise(BattleEvent e)
    {
        EventRaised?.Invoke(e);

        if (_currentGroup != null)
        {
            _currentGroup.Events.Add(e);
        }
        else
        {
            var group = new BattleEventGroup();
            group.Events.Add(e);
            GroupRaised?.Invoke(group);
        }
    }
}
