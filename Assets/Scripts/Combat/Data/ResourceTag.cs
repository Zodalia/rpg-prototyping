using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Resource Tag")]
public sealed class ResourceTag : ScriptableObject
{
    [field: SerializeField] public string Id { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }
}
