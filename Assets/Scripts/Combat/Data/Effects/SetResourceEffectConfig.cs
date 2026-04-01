using System;
using UnityEngine;

[Serializable]
public sealed class SetResourceEffectConfig : EffectConfig
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int value;

    public ResourceDefinition Resource => resource;
    public int Value => value;

    public override string DisplayName => "Set Resource";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (resource == null)
            return;

        foreach (var target in ResolveTargets(state, execution))
        {
            rules.SetResource(state, target, resource, value);
        }
    }
}