using System.Collections.Generic;

public sealed class ActionExecution
{
    public UnitState Actor { get; }
    public ActionDefinition Action { get; }
    public List<UnitState> Targets { get; }
    public PoolInstance TargetPool { get; }
    public int PowerModifier { get; set; } = 0;

    public ActionExecution(UnitState actor, ActionDefinition action, List<UnitState> targets)
    {
        Actor = actor;
        Action = action;
        Targets = targets;
    }

    public ActionExecution(UnitState actor, ActionDefinition action, PoolInstance targetPool)
    {
        Actor = actor;
        Action = action;
        TargetPool = targetPool;
        Targets = new List<UnitState>();
    }
}