using System.Collections.Generic;
using System.Linq;

public sealed class UnitState
{
    public string UnitId { get; }
    public string Team { get; }
    public UnitDefinition Definition { get; }

    public int Hp { get; set; }
    public int TimelinePosition { get; set; }
    public bool IsAlive { get; set; } = true;

    public List<StatusInstance> Statuses { get; } = new();
    public Dictionary<string, int> Cooldowns { get; } = new();
    public Dictionary<string, ResourceInstance> Resources { get; } = new();

    public UnitState(string unitId, string team, UnitDefinition definition)
    {
        UnitId = unitId;
        Team = team;
        Definition = definition;

        Hp = definition.MaxHp;
    }

    public int GetAttack()
    {
        int value = Definition.BaseAttack;
        foreach (var status in Statuses)
            value = status.ModifyAttack(value, this);
        return value;
    }

    public int GetDefense()
    {
        int value = Definition.BaseDefense;
        foreach (var status in Statuses)
            value = status.ModifyDefense(value, this);
        return value;
    }

    public bool HasStun() => Statuses.Any(s => s.Definition.StunsUnit);
}