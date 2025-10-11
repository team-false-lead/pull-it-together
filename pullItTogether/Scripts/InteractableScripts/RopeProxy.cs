using Godot;
using System;

/// a proxy object for the rope to follow the player and have object in player inventory
public partial class RopeProxy : AnimatableBody3D
{
	private Node3D followTarget;
	public bool isTweening = false;
	private MultiplayerApi multiplayer => GetTree().GetMultiplayer();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SyncToPhysics = false; // we control its transform manually in item manager
		TopLevel = true;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		//tween controlled in item manager
		if (!multiplayer.IsServer() || isTweening || followTarget == null || !IsInstanceValid(followTarget)) return;
		var targetTransform = followTarget.GlobalTransform;
		targetTransform.Basis = GlobalTransform.Basis; // Keep current rotation and scale
		GlobalTransform = targetTransform; // Snap to target position
	}

	public void SetFollowTarget(Node3D target)
	{
		followTarget = target;
	}

	public void ClearFollowTarget()
	{
		followTarget = null;
	}
}
