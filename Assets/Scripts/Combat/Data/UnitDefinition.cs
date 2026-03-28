using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Unit Definition")]
public sealed class UnitDefinition : ScriptableObject
{
    [field: SerializeField] public string Id { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }

    [field: SerializeField] public int MaxHp { get; private set; } = 20;
    [field: SerializeField] public int Speed { get; private set; } = 10;

    [field: SerializeField] public int BaseAttack { get; private set; } = 5;
    [field: SerializeField] public int BaseDefense { get; private set; } = 2;

    [field: SerializeField] public List<UnitResourceDefinition> Resources { get; private set; } = new();
    [field: SerializeField] public List<ActionDefinition> Actions { get; private set; } = new();
}