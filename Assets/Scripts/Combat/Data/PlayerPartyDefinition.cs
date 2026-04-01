using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Player Party")]
public sealed class PlayerPartyDefinition : ScriptableObject
{
    [field: SerializeField] public List<UnitDefinition> Units { get; private set; } = new();
}
