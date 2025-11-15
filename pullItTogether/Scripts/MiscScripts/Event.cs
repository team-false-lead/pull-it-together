using Godot;
using System;

public partial class Event : Node3D
{
    public int cooldownValue = 2;
    public enum EventType
    {
        Weather,
        Terrain,
        Recovery,
        Animal,
        Human,
        Wacky
    }

    [Export] public EventType eventType = EventType.Weather;
    [Export] private float estimatedStressReduction = 0.05f;
    [Export] public int currentCooldown = 0;

    public override void _Ready()
    {

    }
}
