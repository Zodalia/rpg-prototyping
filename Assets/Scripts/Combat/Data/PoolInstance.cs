public sealed class PoolInstance
{
    public PoolDefinition Definition { get; }
    public string PoolId { get; }
    public int RemainingHarvests { get; set; }

    public bool IsDepleted => Definition.MaxHarvests >= 0 && RemainingHarvests <= 0;

    public PoolInstance(string poolId, PoolDefinition definition)
    {
        PoolId = poolId;
        Definition = definition;
        RemainingHarvests = definition.MaxHarvests;
    }

    public PoolInstance(string poolId, PoolDefinition definition, int initialHarvests)
    {
        PoolId = poolId;
        Definition = definition;
        RemainingHarvests = initialHarvests;
    }
}
