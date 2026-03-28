using System.Collections.Generic;
using System.Linq;

public sealed class BattleState
{
    public List<UnitState> Units { get; } = new();
    public UnitState ActiveUnit { get; set; }
    public int TurnNumber { get; set; } = 1;
    public bool IsBattleOver { get; set; }
    public string WinnerTeam { get; set; }
    public List<string> Log { get; } = new();

    public IEnumerable<UnitState> LivingUnits => Units.Where(u => u.IsAlive);

    public IEnumerable<UnitState> GetLivingTeam(string team) =>
        Units.Where(u => u.IsAlive && u.Team == team);

    public IEnumerable<UnitState> GetEnemiesOf(UnitState unit) =>
        Units.Where(u => u.IsAlive && u.Team != unit.Team);

    public IEnumerable<UnitState> GetAlliesOf(UnitState unit) =>
        Units.Where(u => u.IsAlive && u.Team == unit.Team);
}