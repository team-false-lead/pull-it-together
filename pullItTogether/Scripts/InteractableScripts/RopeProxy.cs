using Godot;
using System;

/// a proxy object for the rope to follow the player and have object in player inventory
public partial class RopeProxy : AnimatableBody3D
{
	private Node3D slot;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SyncToPhysics = true;
		slot = GetParent<Node3D>();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		if (slot != null)
		{
			GlobalTransform = slot.GlobalTransform;
		}
	}
}
