using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Resource Definition")]
public sealed class ResourceDefinition : ScriptableObject
{
    [field: SerializeField] public string Id { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }

    [field: SerializeField] public int DefaultValue { get; private set; } = 0;
    [field: SerializeField] public int DecayPerTurn { get; private set; } = 0;
    [field: SerializeField] public ResourceOwnershipScope OwnershipScope { get; private set; } = ResourceOwnershipScope.Unit;
}