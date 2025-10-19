using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

// temporary script for transition areas that trigger confetti and map reload
public partial class TransitionArea : Area3D
{
    [Export]
    private Label3D label;
    [Export]
    private GpuParticles3D pinkConfetti;
    [Export]
    private GpuParticles3D yellowConfetti;
    [Export]
    private GpuParticles3D cyanConfetti;

    private List<string> playersInside;

    public int NumOfPlayersInside
    {
        get { return playersInside.Count; }
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        playersInside = new List<string>();
    }


    private void OnBodyEntered(Node3D node)
    {
        // Have to do it this way because OnBodyEntered consistently triggers twice, so this
        // prevents duplicate increments
        if (!playersInside.Contains(node.Name))
            playersInside.Add(node.Name);
    }

    private void OnBodyExited(Node3D node)
    {
        playersInside.Remove(node.Name);
    }
}
