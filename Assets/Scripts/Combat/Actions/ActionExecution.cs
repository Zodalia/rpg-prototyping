using System.Collections.Generic;

public sealed class ActionExecution
{
    public UnitState Actor { get; }
    public ActionDefinition Action { get; }
    public List<UnitState> Targets { get; }
    public int PowerModifier { get; set; } = 0;

    public ActionExecution(UnitState actor, ActionDefinition action, List<UnitState> targets)
    {
        Actor = actor;
        Action = action;
        Targets = targets;
    }
}