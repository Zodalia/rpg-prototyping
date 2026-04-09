using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ResourceRequirement
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int amount;
    [Tooltip("Override spend priority order. Empty = use resource default (AllowedScopes).")]
    [SerializeField] private List<ResourceOwnershipScope> spendPriorityOverride;

    public ResourceDefinition Resource => resource;
    public int Amount => amount;

    public IReadOnlyList<ResourceOwnershipScope> SpendPriority =>
        spendPriorityOverride != null && spendPriorityOverride.Count > 0
            ? spendPriorityOverride
            : Resource != null ? Resource.AllowedScopes : null;
}