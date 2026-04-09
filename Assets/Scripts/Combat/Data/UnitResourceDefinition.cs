using System;
using UnityEngine;

[Serializable]
public struct UnitResourceDefinition
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int initialValue;
    [SerializeField] private bool resetToValueOnTurnStart;
    [SerializeField] private int turnStartValue;
    [Tooltip("Maximum value for this unit's pool. -1 = use resource default.")]
    [SerializeField] private int maxValue;

    public ResourceDefinition Resource => resource;
    public int InitialValue => initialValue;
    public bool ResetToValueOnTurnStart => resetToValueOnTurnStart;
    public int TurnStartValue => turnStartValue;
    public int MaxValue => maxValue == 0 ? -1 : maxValue;
}