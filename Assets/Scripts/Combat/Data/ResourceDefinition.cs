using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Resource Definition")]
public sealed class ResourceDefinition : ScriptableObject
{
    [field: SerializeField] public string Id { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
    [field: SerializeField] public Color32 IconColor { get; private set; } = new Color32(255, 255, 255, 255);
    [field: SerializeField] public bool PlayerFacing { get; private set; }
    [field: SerializeField] public int DefaultValue { get; private set; } = 0;
    [field: SerializeField] public int DecayPerTurn { get; private set; } = 0;
    [field: SerializeField] public int DefaultMaxValue { get; private set; } = -1;

    [field: SerializeField] public List<ResourceOwnershipScope> AllowedScopes { get; private set; } = new();

    // Kept for backwards compatibility / migration. Hidden from the inspector.
    [HideInInspector]
    [field: SerializeField] public ResourceOwnershipScope OwnershipScope { get; private set; } = ResourceOwnershipScope.Unit;

    private void OnValidate()
    {
        if (AllowedScopes == null || AllowedScopes.Count == 0)
        {
            AllowedScopes = new List<ResourceOwnershipScope> { OwnershipScope };
        }
    }
}