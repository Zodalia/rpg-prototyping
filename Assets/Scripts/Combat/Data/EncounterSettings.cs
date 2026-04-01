using System.Collections.Generic;
using UnityEngine;

public enum EncounterMode
{
    Menu,
    Random,
}

[CreateAssetMenu(menuName = "Combat/Encounter Settings")]
public sealed class EncounterSettings : ScriptableObject
{
    [field: SerializeField] public EncounterMode Mode { get; private set; } = EncounterMode.Menu;
    [field: SerializeField] public List<BattleDefinition> AvailableBattles { get; private set; } = new();
    [field: SerializeField] public PlayerPartyDefinition PlayerParty { get; private set; }
}
