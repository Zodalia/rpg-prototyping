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
        // Legacy string-only path: find definition, then aggregate.
        var definition = FindDefinition(state, unit, resourceId);
        if (definition != null)
            return GetResource(state, unit, definition);

        // Fallback: try each scope with override → real.
        string unitKey = UnitKey(unit.UnitId, resourceId);
        if (_overrides.TryGetValue(unitKey, out int uVal)) return uVal;
        if (unit.Resources.TryGetValue(resourceId, out var unitRes)) return unitRes.CurrentValue;

        string teamKey = TeamKey(unit.Team, resourceId);
        if (_overrides.TryGetValue(teamKey, out int tVal)) return tVal;
        if (state.TeamResources.TryGetValue(unit.Team, out var teamDict) &&
            teamDict.TryGetValue(resourceId, out var teamRes)) return teamRes.CurrentValue;

        string globalKey = GlobalKey(resourceId);
        if (_overrides.TryGetValue(globalKey, out int gVal)) return gVal;
        if (state.GlobalResources.TryGetValue(resourceId, out var globalRes)) return globalRes.CurrentValue;

        return 0;
    }

    public int GetResource(BattleState state, UnitState unit, ResourceDefinition resource)
    {
        int total = 0;
        foreach (var scope in resource.AllowedScopes)
            total += GetScopedValue(state, unit, resource.Id, scope);
        return total;
    }

    public void GainResource(BattleState state, UnitState unit, ResourceDefinition resource, int amount,
        IReadOnlyList<ResourceOwnershipScope> gainPriority = null)
    {
        var priority = gainPriority ?? resource.AllowedScopes;
        int remaining = amount;

        foreach (var scope in priority)
        {
            if (remaining <= 0) break;

            int current = GetScopedValue(state, unit, resource.Id, scope);
            int maxValue = GetMaxValue(state, unit, resource, scope);
            int space = maxValue >= 0 ? maxValue - current : int.MaxValue;
            int gain = space < remaining ? space : remaining;
            if (gain <= 0) continue;

            _overrides[ScopedKey(unit, resource.Id, scope)] = current + gain;
            remaining -= gain;
        }
    }

    public void SpendResource(BattleState state, UnitState unit, ResourceDefinition resource, int amount,
        IReadOnlyList<ResourceOwnershipScope> spendPriority = null)
    {
        var priority = spendPriority ?? resource.AllowedScopes;
        int remaining = amount;

        foreach (var scope in priority)
        {
            if (remaining <= 0) break;

            int current = GetScopedValue(state, unit, resource.Id, scope);
            if (current <= 0) continue;

            int drain = current < remaining ? current : remaining;
            _overrides[ScopedKey(unit, resource.Id, scope)] = current - drain;
            remaining -= drain;
        }
    }

    public void SetResource(BattleState state, UnitState unit, ResourceDefinition resource, int value,
        ResourceOwnershipScope scope)
    {
        _overrides[ScopedKey(unit, resource.Id, scope)] = value;
    }

    public int GetTagResource(BattleState state, UnitState unit, ResourceTag tag)
    {
        if (tag == null) return 0;

        int total = 0;
        foreach (var res in unit.Resources.Values)
            if (res.Definition != null && res.Definition.HasTag(tag))
                total += GetScopedValue(state, unit, res.Definition.Id, ResourceOwnershipScope.Unit);

        if (state.TeamResources.TryGetValue(unit.Team, out var teamDict))
            foreach (var res in teamDict.Values)
                if (res.Definition != null && res.Definition.HasTag(tag))
                    total += GetScopedValue(state, unit, res.Definition.Id, ResourceOwnershipScope.Team);

        foreach (var res in state.GlobalResources.Values)
            if (res.Definition != null && res.Definition.HasTag(tag))
                total += GetScopedValue(state, unit, res.Definition.Id, ResourceOwnershipScope.Global);

        return total;
    }

    public void SpendTagResource(BattleState state, UnitState unit, ResourceTag tag, int amount)
    {
        if (tag == null) return;

        int remaining = amount;

        // Drain Unit scope first
        foreach (var res in unit.Resources.Values)
        {
            if (remaining <= 0) break;
            if (res.Definition == null || !res.Definition.HasTag(tag)) continue;
            remaining = DrainSnapshotPool(state, unit, res.Definition.Id, ResourceOwnershipScope.Unit, remaining);
        }

        // Then Team scope
        if (remaining > 0 && state.TeamResources.TryGetValue(unit.Team, out var teamDict))
            foreach (var res in teamDict.Values)
            {
                if (remaining <= 0) break;
                if (res.Definition == null || !res.Definition.HasTag(tag)) continue;
                remaining = DrainSnapshotPool(state, unit, res.Definition.Id, ResourceOwnershipScope.Team, remaining);
            }

        // Then Global scope
        if (remaining > 0)
            foreach (var res in state.GlobalResources.Values)
            {
                if (remaining <= 0) break;
                if (res.Definition == null || !res.Definition.HasTag(tag)) continue;
                remaining = DrainSnapshotPool(state, unit, res.Definition.Id, ResourceOwnershipScope.Global, remaining);
            }
    }

    private int DrainSnapshotPool(BattleState state, UnitState unit, string resourceId,
        ResourceOwnershipScope scope, int remaining)
    {
        int current = GetScopedValue(state, unit, resourceId, scope);
        if (current <= 0) return remaining;
        int drain = current < remaining ? current : remaining;
        _overrides[ScopedKey(unit, resourceId, scope)] = current - drain;
        return remaining - drain;
    }

    private int GetScopedValue(BattleState state, UnitState unit, string resourceId,
        ResourceOwnershipScope scope)
    {
        string key = ScopedKey(unit, resourceId, scope);
        if (_overrides.TryGetValue(key, out int val))
            return val;

        return scope switch
        {
            ResourceOwnershipScope.Unit =>
                unit.Resources.TryGetValue(resourceId, out var u) ? u.CurrentValue : 0,
            ResourceOwnershipScope.Team =>
                state.TeamResources.TryGetValue(unit.Team, out var td) &&
                td.TryGetValue(resourceId, out var t) ? t.CurrentValue : 0,
            ResourceOwnershipScope.Global =>
                state.GlobalResources.TryGetValue(resourceId, out var g) ? g.CurrentValue : 0,
            _ => 0
        };
    }

    private int GetMaxValue(BattleState state, UnitState unit, ResourceDefinition resource,
        ResourceOwnershipScope scope)
    {
        // Try to read MaxValue from the real pool; fall back to the definition default.
        ResourceInstance pool = scope switch
        {
            ResourceOwnershipScope.Unit =>
                unit.Resources.TryGetValue(resource.Id, out var u) ? u : null,
            ResourceOwnershipScope.Team =>
                state.TeamResources.TryGetValue(unit.Team, out var td) &&
                td.TryGetValue(resource.Id, out var t) ? t : null,
            ResourceOwnershipScope.Global =>
                state.GlobalResources.TryGetValue(resource.Id, out var g) ? g : null,
            _ => null
        };
        return pool?.MaxValue ?? resource.DefaultMaxValue;
    }

    private static string ScopedKey(UnitState unit, string resourceId, ResourceOwnershipScope scope)
    {
        return scope switch
        {
            ResourceOwnershipScope.Unit => UnitKey(unit.UnitId, resourceId),
            ResourceOwnershipScope.Team => TeamKey(unit.Team, resourceId),
            ResourceOwnershipScope.Global => GlobalKey(resourceId),
            _ => UnitKey(unit.UnitId, resourceId)
        };
    }

    private static ResourceDefinition FindDefinition(BattleState state, UnitState unit, string resourceId)
    {
        if (unit.Resources.TryGetValue(resourceId, out var u)) return u.Definition;
        if (state.TeamResources.TryGetValue(unit.Team, out var td) &&
            td.TryGetValue(resourceId, out var t)) return t.Definition;
        if (state.GlobalResources.TryGetValue(resourceId, out var g)) return g.Definition;
        return null;
    }

    private static string UnitKey(string unitId, string resourceId) => $"U:{unitId}:{resourceId}";
    private static string TeamKey(string team, string resourceId) => $"T:{team}:{resourceId}";
    private static string GlobalKey(string resourceId) => $"G:{resourceId}";
}
