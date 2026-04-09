using UnityEngine;

public abstract class DeathConditionStrategy : ScriptableObject
{
    public abstract bool IsDead(UnitState unit, BattleState state, CombatRules rules);
}
