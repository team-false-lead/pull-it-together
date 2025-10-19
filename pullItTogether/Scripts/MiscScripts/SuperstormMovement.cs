using Godot;
using System;
using System.Collections.Generic;

public partial class SuperstormMovement : Area3D
{
	[Export] public float stormSpeed = 0.25f;
	[Export] public float stormDPS = -2.5f;
	[Export] public GpuParticles3D rainParticles;
	[Export] public float dropSpeed = 45f;
	[Export] public float rainDensity = 1f;
	[Export] CollisionShape3D stormCollider;
	[Export] FogVolume fogVolume;
	[Export] Node3D cloudsEffector;
	[Export] public float effectorParallaxMaxMultiplier = 400f;
	[Export] public Node3D interactablesNode;
	public Node3D wagon;
	private ParticleProcessMaterial rainMaterial;
	private List<Node3D> playersInsideStorm = new List<Node3D>();
	private Vector3 lastStormPos;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BodyEntered += _onBodyEntered;
		BodyExited += _onBodyExited;
		GetTree().GetMultiplayer().PeerDisconnected += OnPeerDisconnected;

		lastStormPos = GlobalPosition;

		interactablesNode.Connect("ItemsSpawned", new Callable(this, nameof(GetWagonReference)));

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
			//rainMaterial.Gravity = new Vector3(0, -9.8f, 0);

			rainParticles.GlobalPosition = GlobalPosition + new Vector3(0, cylinder.Height / 2, 0);

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
		if (wagon != null)
		{
			//GD.Print($"Storm Z: {GlobalPosition.Z}, Wagon Z: {wagon.GlobalPosition.Z}");
			float toWagon = wagon.GlobalPosition.Z - GlobalPosition.Z;
			toWagon = Mathf.Min(toWagon, 0f); // only consider when wagon is ahead
			//GD.Print($"Distance to wagon: {toWagon}");
			if (Position.Z < wagon.Position.Z)
			{
				Position = new Vector3(Position.X, Position.Y, wagon.Position.Z);
			}
			UpdateEffectorPosition(toWagon);
		}
		PhysicsServer3D.AreaSetTransform(GetRid(), GlobalTransform);

		//GD.Print($"Players inside storm: {playersInsideStorm.Count}");
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
			
			if (Multiplayer == null || Multiplayer.MultiplayerPeer == null) return;

			if (body is PlayerController targetPlayer && Multiplayer.IsServer())
			{
				if (targetPlayer.IsQueuedForDeletion() || !targetPlayer.IsInsideTree())
				{
					//GD.PrintErr($"Entered: Cannot update HUD storm text for {body.Name} as it is not valid.");
					return;
				}

				var targetPeerId = (long)targetPlayer.GetMultiplayerAuthority();
				if (targetPeerId == 0) return; // avoid invalid peer ID
                var hudError = targetPlayer.RpcId(targetPeerId, nameof(PlayerController.UpdateHudStormText), true);
				if (hudError != Error.Ok)
				{
					GD.PrintErr($"RPC Error when updating HUD storm text for {body.Name}: {hudError}");
				}
            }
			
		}
	}

	private void _onBodyExited(Node3D body)
	{
		if (body.IsInGroup("players"))
		{
			playersInsideStorm.Remove(body);

			if (Multiplayer == null || Multiplayer.MultiplayerPeer == null) return;

			if (body is PlayerController targetPlayer && Multiplayer.IsServer())
			{
				if (targetPlayer.IsQueuedForDeletion() || !targetPlayer.IsInsideTree())
				{
					//GD.PrintErr($"Exit: Cannot update HUD storm text for {body.Name} as it is not valid.");
					return;
				}

				var targetPeerId = (long)targetPlayer.GetMultiplayerAuthority();
				if (targetPeerId == 0) return; // avoid invalid peer ID
				var hudError = targetPlayer.RpcId(targetPeerId, nameof(PlayerController.UpdateHudStormText), false);
				if (hudError != Error.Ok)
				{
					GD.PrintErr($"RPC Error when updating HUD storm text for {body.Name}: {hudError}");
				}
			}
		}
	}
	
	private void OnPeerDisconnected(long peerId)
	{
		// Clean up any players associated with the disconnected peer
		foreach (var body in new List<Node3D>(playersInsideStorm))
		{
			if (body is PlayerController targetPlayer)
			{
				if ((long)targetPlayer.GetMultiplayerAuthority() == peerId)
				{
					playersInsideStorm.Remove(body);
				}
			}
		}
	}

	private void DoStormDamage(Node3D body, float damage)
	{
		if (!Multiplayer.IsServer()) return;

		//GD.Print($"Dealing {damage} damage to {body.Name}");
		if (body is PlayerController targetPlayer)
		{
			if (targetPlayer.IsQueuedForDeletion() || !targetPlayer.IsInsideTree())
			{
				GD.PrintErr($"Cannot deal storm damage to {body.Name} as it is not valid.");
				return;
			}
			var targetPeerId = (long)targetPlayer.GetMultiplayerAuthority();
			//GD.Print($"Target Peer ID: {targetPeerId}");
			var error = targetPlayer.RpcId(targetPeerId, nameof(PlayerController.ChangeCurrentHealth), damage);
			if (error != Error.Ok)
			{
				GD.PrintErr($"RPC Error when dealing storm damage to {body.Name}: {error}");
			}
		}
	}

	private void UpdateEffectorPosition(float toWagon)
	{
		if (cloudsEffector == null || wagon == null) return;

		Vector3 stormDeltaPos = GlobalPosition - lastStormPos;

		Vector3 effectorDistanceToWagon = wagon.GlobalPosition - cloudsEffector.GlobalPosition;
		float effectorDistance = effectorDistanceToWagon.Length();

		float closeRatio = 1f - Mathf.Clamp(toWagon / effectorDistance, 0f, 1f);
		float parallaxMultiplier = Mathf.Lerp(1f, effectorParallaxMaxMultiplier, closeRatio);

		Vector3 parallaxOffset = stormDeltaPos * parallaxMultiplier;

		cloudsEffector.Position += parallaxOffset;
		if (cloudsEffector.Position.Z < GlobalPosition.Z)
		{
			cloudsEffector.Position = new Vector3(cloudsEffector.Position.X, cloudsEffector.Position.Y, GlobalPosition.Z);
		}
		lastStormPos = GlobalPosition;
	}

	private void GetWagonReference()
	{
		wagon = GetTree().GetFirstNodeInGroup("wagon") as Node3D;
	}
}
