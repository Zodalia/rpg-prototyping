public sealed class StatusInstance
{
    public StatusDefinition Definition { get; }
    public int RemainingTurns { get; set; }
    public TurnTickTiming TickTiming { get; }
    public TeamTrackingScope TrackingScope { get; }
    public string OwnerTeam { get; }

    public StatusInstance(StatusDefinition definition, int duration, TurnTickTiming tickTiming, TeamTrackingScope trackingScope, string ownerTeam)
    {
        Definition = definition;
        RemainingTurns = duration;
        TickTiming = tickTiming;
        TrackingScope = trackingScope;
        OwnerTeam = ownerTeam;
    }

    public int ModifyAttack(int current, UnitState unit)
    {
        return current + Definition.AttackModifier;
    }

    public int ModifyDefense(int current, UnitState unit)
    {
        return current + Definition.DefenseModifier;
    }
}