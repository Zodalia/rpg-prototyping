using System;
using UnityEngine;

[Serializable]
public sealed class SpendResourceEffectConfig : EffectConfig
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int amount;

    public override string DisplayName => "Spend Resource";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (resource == null)
            return;

        foreach (var target in execution.Targets)
        {
            rules.SpendResource(state, target, resource.Id, amount);
        }
    }
}