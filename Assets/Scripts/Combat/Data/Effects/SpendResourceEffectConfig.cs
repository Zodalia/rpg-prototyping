using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SpendResourceEffectConfig : EffectConfig
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int amount;
    [Tooltip("Override spend priority order. Empty = use resource default.")]
    [SerializeField] private List<ResourceOwnershipScope> spendPriorityOverride;

    public ResourceDefinition Resource => resource;
    public int Amount => amount;

    public IReadOnlyList<ResourceOwnershipScope> SpendPriority =>
        spendPriorityOverride != null && spendPriorityOverride.Count > 0
            ? spendPriorityOverride
            : resource != null ? resource.AllowedScopes : null;

    public override string DisplayName => "Spend Resource";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        if (resource == null)
            return;

        foreach (var target in ResolveTargets(state, execution))
        {
            rules.SpendResource(state, target, resource, amount, SpendPriority);
        }
    }
}