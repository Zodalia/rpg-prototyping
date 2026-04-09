using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Death Condition/All Resources Depleted")]
public sealed class AllResourcesDepletedDeathCondition : DeathConditionStrategy
{
    [field: SerializeField] public List<ResourceDefinition> Resources { get; private set; } = new();

    public override bool IsDead(UnitState unit, BattleState state, CombatRules rules)
    {
        if (Resources.Count == 0)
            return false;

        foreach (var resource in Resources)
        {
            if (resource == null)
                continue;

            if (rules.GetResourceAmount(state, unit, resource.Id) > 0)
                return false;
        }

        return true;
    }
}
