using System;
using UnityEngine;

[Serializable]
public sealed class ResourceModifierEffectConfig : EffectConfig
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int powerPerResource = 1;

    public override string DisplayName => "Resource Modifier";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (resource == null)
            return;

        int resourceAmount = rules.GetResourceAmount(state, execution.Actor, resource.Id);
        execution.PowerModifier += resourceAmount * powerPerResource;
    }
}