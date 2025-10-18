using Godot;
using System;
using System.Collections.Generic;

public partial class SuperstormMovement : Area3D
{
	[Export] public float stormSpeed = 0.25f;
	[Export] public float stormDPS = 0.5f;
	[Export] public GpuParticles3D rainParticles;
	[Export] public float dropSpeed = 45f;
	[Export] public float rainDensity = 1f;
	[Export] CollisionShape3D stormCollider;
	[Export] FogVolume fogVolume;
	private ParticleProcessMaterial rainMaterial;
	private List<Node3D> playersInsideStorm = new List<Node3D>();


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BodyEntered += _onBodyEntered;
		BodyExited += _onBodyExited;

		if (rainParticles != null && stormCollider != null)
		{
			rainMaterial = (ParticleProcessMaterial)rainParticles.ProcessMaterial;
			var cylinder = (CylinderShape3D)stormCollider.Shape;

			rainMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			rainMaterial.EmissionBoxExtents = new Vector3(cylinder.Radius, 0.1f, cylinder.Radius);
			rainMaterial.Direction = Vector3.Down;
			rainMaterial.Spread = 4f;

			rainMaterial.InitialVelocityMin = dropSpeed * 0.8f;
			rainMaterial.InitialVelocityMax = dropSpeed * 1.2f;
			rainMaterial.Gravity = new Vector3(0, -9.8f, 0);

			rainParticles.GlobalPosition = GlobalPosition + new Vector3(0, cylinder.Height, 0);

			rainParticles.Lifetime = cylinder.Height / dropSpeed;
			rainParticles.Preprocess = rainParticles.Lifetime;

			float area = (float)Math.PI * cylinder.Radius * cylinder.Radius;
			rainParticles.Amount = (int)(area * rainDensity);

			rainParticles.LocalCoords = true;
			rainParticles.Emitting = true;

			rainParticles.VisibilityAabb = new Aabb(
				new Vector3(-cylinder.Radius, -cylinder.Height, -cylinder.Radius),
				new Vector3(cylinder.Radius * 2, cylinder.Height, cylinder.Radius * 2)
			);

			if (fogVolume != null)
			{
				fogVolume.Size = new Vector3(cylinder.Radius * 2, cylinder.Height / 2, cylinder.Radius * 2);
			}
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		Position += new Vector3(0, 0, -1) * (float)delta * stormSpeed;

		if (playersInsideStorm.Count > 0)
		{
			foreach (var player in playersInsideStorm)
			{
				DoStormDamage(player, stormDPS * (float)delta);
			}
		}
	}

	private void _onBodyEntered(Node3D body)
	{
		if (body.IsInGroup("players"))
		{
			playersInsideStorm.Add(body);
		}
	}

	private void _onBodyExited(Node3D body)
	{
		if (body.IsInGroup("players"))
		{
			playersInsideStorm.Remove(body);
		}
	}

	private void DoStormDamage(Node3D body, float damage)
	{
		if (body is PlayerController targetPlayer)
		{
			var targetPeerId = (long)targetPlayer.GetMultiplayerAuthority();
			targetPlayer.RpcId(targetPeerId, nameof(PlayerController.ChangeCurrentHealth), damage);
		}
	}
}
