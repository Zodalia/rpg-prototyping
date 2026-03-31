using System;
using UnityEngine;

[Serializable]
public sealed class GainResourceEffectConfig : EffectConfig
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int amount;

    public ResourceDefinition Resource => resource;
    public int Amount => amount;

    public override string DisplayName => "Gain Resource";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (resource == null)
            return;

        foreach (var target in execution.Targets)
        {
            rules.GainResource(state, target, resource, amount);
        }
    }
}