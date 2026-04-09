using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Pool Definition")]
public sealed class PoolDefinition : ScriptableObject
{
    [field: SerializeField] public string Id { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
    [field: SerializeField] public Color32 IconColor { get; private set; } = new Color32(255, 255, 255, 255);
    [field: SerializeField] public int MaxHarvests { get; private set; } = -1;
    [field: SerializeField] public List<PoolHarvestEntry> HarvestEntries { get; private set; } = new();
}

[Serializable]
public struct PoolHarvestEntry
{
    [SerializeField] private string harvestId;
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int amount;
    [SerializeField] private ResourceOwnershipScope ownershipScope;

    public string HarvestId => harvestId;
    public ResourceDefinition Resource => resource;
    public int Amount => amount;
    public ResourceOwnershipScope OwnershipScope => ownershipScope;
}
