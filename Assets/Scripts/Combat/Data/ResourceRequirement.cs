using System;
using UnityEngine;

[Serializable]
public struct ResourceRequirement
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField] private int amount;

    public ResourceDefinition Resource => resource;
    public int Amount => amount;
}