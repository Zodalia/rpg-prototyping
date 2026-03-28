using System;
using UnityEngine;

[Serializable]
public struct UnitResourceDefinition
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int initialValue;
    [SerializeField] private bool resetToValueOnTurnStart;
    [SerializeField] private int turnStartValue;

    public ResourceDefinition Resource => resource;
    public int InitialValue => initialValue;
    public bool ResetToValueOnTurnStart => resetToValueOnTurnStart;
    public int TurnStartValue => turnStartValue;
}