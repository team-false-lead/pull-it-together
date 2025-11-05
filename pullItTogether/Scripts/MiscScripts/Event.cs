using Godot;
using System;

public abstract partial class Event : Node
{
    public enum EventType
    {
        Weather,
        Terrain,
        Recovery,
        Animal,
        Human,
        Wacky
    }

    [Export] public EventType Type = EventType.Weather;
}
