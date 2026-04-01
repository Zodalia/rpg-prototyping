public struct TurnPreviewEntry
{
    public UnitState Unit;
    public int ResourceValue;

    public TurnPreviewEntry(UnitState unit, int resourceValue)
    {
        Unit = unit;
        ResourceValue = resourceValue;
    }
}
