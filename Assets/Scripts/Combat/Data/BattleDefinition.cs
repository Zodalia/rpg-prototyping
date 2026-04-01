using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Battle Definition")]
public sealed class BattleDefinition : ScriptableObject
{
    [field: SerializeField] public string Id { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }

    [field: SerializeField] public List<UnitDefinition> Enemies { get; private set; } = new();
    [field: SerializeField] public TurnOrderStrategy TurnOrderStrategy { get; private set; }
    [field: SerializeField] public List<TurnEffectDefinition> GlobalTurnEffects { get; private set; } = new();
    [field: SerializeField] public List<GlobalResourceEntry> StartingGlobalResources { get; private set; } = new();
}

[Serializable]
public struct GlobalResourceEntry
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int initialValue;

    public ResourceDefinition Resource => resource;
    public int InitialValue => initialValue;
}
