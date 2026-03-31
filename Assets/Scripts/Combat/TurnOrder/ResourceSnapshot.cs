using System.Collections.Generic;
using UnityEngine;

public sealed class ResourceSnapshot
{
    private readonly Dictionary<string, int> _overrides = new();

    public HashSet<string> UnitsActedThisRound { get; }

    public ResourceSnapshot(BattleState state)
    {
        UnitsActedThisRound = new HashSet<string>(state.UnitsActedThisRound);
    }

    public int GetResource(BattleState state, UnitState unit, string resourceId)
    {
        // Unit scope: check override, then real
        string unitKey = UnitKey(unit.UnitId, resourceId);
        if (_overrides.TryGetValue(unitKey, out int uVal))
            return uVal;
        if (unit.Resources.TryGetValue(resourceId, out var unitRes))
            return unitRes.CurrentValue;

        // Team scope: check override, then real
        string teamKey = TeamKey(unit.Team, resourceId);
        if (_overrides.TryGetValue(teamKey, out int tVal))
            return tVal;
        if (state.TeamResources.TryGetValue(unit.Team, out var teamDict) &&
            teamDict.TryGetValue(resourceId, out var teamRes))
            return teamRes.CurrentValue;

        // Global scope: check override, then real
        string globalKey = GlobalKey(resourceId);
        if (_overrides.TryGetValue(globalKey, out int gVal))
            return gVal;
        if (state.GlobalResources.TryGetValue(resourceId, out var globalRes))
            return globalRes.CurrentValue;

        return 0;
    }

    public void GainResource(BattleState state, UnitState unit, ResourceDefinition resource, int amount)
    {
        int current = GetResource(state, unit, resource.Id);
        _overrides[ScopedKey(unit, resource)] = current + amount;
    }

    public void SpendResource(BattleState state, UnitState unit, ResourceDefinition resource, int amount)
    {
        int current = GetResource(state, unit, resource.Id);
        _overrides[ScopedKey(unit, resource)] = Mathf.Max(0, current - amount);
    }

    public void SetResource(BattleState state, UnitState unit, ResourceDefinition resource, int value)
    {
        _overrides[ScopedKey(unit, resource)] = value;
    }

    private string ScopedKey(UnitState unit, ResourceDefinition resource)
    {
        return resource.OwnershipScope switch
        {
            ResourceOwnershipScope.Unit => UnitKey(unit.UnitId, resource.Id),
            ResourceOwnershipScope.Team => TeamKey(unit.Team, resource.Id),
            ResourceOwnershipScope.Global => GlobalKey(resource.Id),
            _ => UnitKey(unit.UnitId, resource.Id)
        };
    }

    private static string UnitKey(string unitId, string resourceId) => $"U:{unitId}:{resourceId}";
    private static string TeamKey(string team, string resourceId) => $"T:{team}:{resourceId}";
    private static string GlobalKey(string resourceId) => $"G:{resourceId}";
}
