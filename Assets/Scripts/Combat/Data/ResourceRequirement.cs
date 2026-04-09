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
    [Tooltip("If set, this requirement matches any resource with this tag instead of a specific resource.")]
    [SerializeField] private ResourceTag tag;

    public ResourceDefinition Resource => resource;
    public int Amount => amount;
    public ResourceTag Tag => tag;
    public bool IsTagBased => tag != null;

    public IReadOnlyList<ResourceOwnershipScope> SpendPriority =>
        spendPriorityOverride != null && spendPriorityOverride.Count > 0
            ? spendPriorityOverride
            : Resource != null ? Resource.AllowedScopes : null;
}