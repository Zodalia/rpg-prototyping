using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GainResourceEffectConfig : EffectConfig
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int amount;
    [Tooltip("Override gain priority order. Empty = use resource default.")]
    [SerializeField] private List<ResourceOwnershipScope> gainPriorityOverride;

    public ResourceDefinition Resource => resource;
    public int Amount => amount;

    public IReadOnlyList<ResourceOwnershipScope> GainPriority =>
        gainPriorityOverride != null && gainPriorityOverride.Count > 0
            ? gainPriorityOverride
            : resource != null ? resource.AllowedScopes : null;

    public override string DisplayName => "Gain Resource";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (resource == null)
            return;

        foreach (var target in ResolveTargets(state, execution))
        {
            rules.GainResource(state, target, resource, amount, GainPriority);
        }
    }
}