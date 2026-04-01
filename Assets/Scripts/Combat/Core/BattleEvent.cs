using System.Collections.Generic;

public abstract class BattleEvent
{
    public int TurnNumber { get; }

    protected BattleEvent(int turnNumber)
    {
        TurnNumber = turnNumber;
    }
}

public sealed class TurnStartedEvent : BattleEvent
{
    public UnitState Unit { get; }

    public TurnStartedEvent(int turnNumber, UnitState unit) : base(turnNumber)
    {
        Unit = unit;
    }
}

public sealed class TurnEndedEvent : BattleEvent
{
    public UnitState Unit { get; }

    public TurnEndedEvent(int turnNumber, UnitState unit) : base(turnNumber)
    {
        Unit = unit;
    }
}

public sealed class ActionUsedEvent : BattleEvent
{
    public UnitState Actor { get; }
    public ActionDefinition Action { get; }
    public List<UnitState> Targets { get; }

    public ActionUsedEvent(int turnNumber, UnitState actor, ActionDefinition action, List<UnitState> targets)
        : base(turnNumber)
    {
        Actor = actor;
        Action = action;
        Targets = targets;
    }
}

public sealed class DamageDealtEvent : BattleEvent
{
    public UnitState Actor { get; }
    public UnitState Target { get; }
    public int Amount { get; }
    public bool IsLethal { get; }

    public DamageDealtEvent(int turnNumber, UnitState actor, UnitState target, int amount, bool isLethal)
        : base(turnNumber)
    {
        Actor = actor;
        Target = target;
        Amount = amount;
        IsLethal = isLethal;
    }
}

public sealed class UnitDefeatedEvent : BattleEvent
{
    public UnitState Unit { get; }
    public UnitState Killer { get; }

    public UnitDefeatedEvent(int turnNumber, UnitState unit, UnitState killer = null) : base(turnNumber)
    {
        Unit = unit;
        Killer = killer;
    }
}

public sealed class StatusAppliedEvent : BattleEvent
{
    public UnitState Target { get; }
    public StatusDefinition Status { get; }
    public int Duration { get; }

    public StatusAppliedEvent(int turnNumber, UnitState target, StatusDefinition status, int duration)
        : base(turnNumber)
    {
        Target = target;
        Status = status;
        Duration = duration;
    }
}

public sealed class StatusTickEvent : BattleEvent
{
    public UnitState Unit { get; }
    public StatusDefinition Status { get; }
    public int Damage { get; }

    public StatusTickEvent(int turnNumber, UnitState unit, StatusDefinition status, int damage)
        : base(turnNumber)
    {
        Unit = unit;
        Status = status;
        Damage = damage;
    }
}

public sealed class StatusExpiredEvent : BattleEvent
{
    public UnitState Unit { get; }
    public StatusDefinition Status { get; }

    public StatusExpiredEvent(int turnNumber, UnitState unit, StatusDefinition status) : base(turnNumber)
    {
        Unit = unit;
        Status = status;
    }
}

public sealed class BattleEndedEvent : BattleEvent
{
    public string WinnerTeam { get; }

    public BattleEndedEvent(int turnNumber, string winnerTeam) : base(turnNumber)
    {
        WinnerTeam = winnerTeam;
    }
}

public sealed class ResourceChangedEvent : BattleEvent
{
    public UnitState Unit { get; }
    public ResourceDefinition Resource { get; }
    public int OldValue { get; }
    public int NewValue { get; }

    public ResourceChangedEvent(int turnNumber, UnitState unit, ResourceDefinition resource, int oldValue, int newValue)
        : base(turnNumber)
    {
        Unit = unit;
        Resource = resource;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
