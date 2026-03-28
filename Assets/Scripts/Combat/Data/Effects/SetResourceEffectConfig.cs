using System;
using UnityEngine;

[Serializable]
public sealed class SetResourceEffectConfig : EffectConfig
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int value;

    public override string DisplayName => "Set Resource";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (resource == null)
            return;

        foreach (var target in execution.Targets)
        {
            rules.SetResource(state, target, resource, value);
        }
    }
}