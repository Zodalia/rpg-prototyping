using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Status Definition")]
public sealed class StatusDefinition : ScriptableObject
{
    [field: SerializeField] public string Id { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
    [field: SerializeField] public Color32 IconColor { get; private set; }

    [field: SerializeField] public int AttackModifier { get; private set; } = 0;
    [field: SerializeField] public int DefenseModifier { get; private set; } = 0;
    [field: SerializeField] public int DamagePerTick { get; private set; } = 0;
    [field: SerializeField] public bool StunsUnit { get; private set; } = false;
    [field: SerializeField] public int DefaultDuration { get; private set; } = 1;

    [field: SerializeField] public TurnTickTiming TickTiming { get; private set; } = TurnTickTiming.TurnEnd;
    [field: SerializeField] public TeamTrackingScope TrackingScope { get; private set; } = TeamTrackingScope.Self;
}