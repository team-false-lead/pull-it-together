using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

// Movement Based on Juiced Up First Person Character Controller Tutorial - Godot 3D FPS - YouTube
// https://www.youtube.com/watch?v=A3HLeyaBCq4&t=461s&ab_channel=LegionGames

/// PlayerController handles player movement, looking around, and interaction with objects.
public partial class PlayerController : CharacterBody3D
{
	// Movement parameters
	public float speed;
	public float walkSpeed = 5.0f;
	public float sprintSpeed = 8.0f;
	public float jumpVelocity = 4.5f;
	public float inertiaAirValue = 3.0f;
	public float inertiaGroundValue = 7.0f;
	private bool isSprinting;

	// Camera and look parameters
	[Export] public Node3D head;
	[Export] public Camera3D camera;
	[Export] public float fov = 75.0f;
	[Export] public float fovChange = 1.25f;
	public float fovChangeSpeed = 5.0f;
	[Export] public float mouseSensitivity = 0.005f;
	[Export] public float bobFrequency = 2.0f;
	[Export] public float bobAmplitude = 0.08f;
	private float bobTimer = 0.0f;

	// Interaction parameters
	private Interactable heldObject = null;
	private bool HeldValid() => heldObject != null && IsInstanceValid(heldObject) && !heldObject.IsQueuedForDeletion() && heldObject.IsInsideTree();
	[Export] public NodePath inventorySlotPath;
	[Export] public float interactRange = 3.0f;
	//[Export] public int interactLayer = 4;

	// Collision parameters
	[Export] public Node3D collisionPusher;
	public AnimatableBody3D collisionPusherAB;
	[Export] public Interactable interactableRB;
	private uint interactMaskUint = 40;

	// Rope tether parameters when carrying rope grab point
	private Node3D tetherAnchor;
	private float maxTetherDist;
	private float tetherBuffer;
	private float tetherStrength;

	// Health and energy parameters
	[Export] private PackedScene hudScene;
	private float maxHealth = 100f;
	[Export] public float currentHealth;
	private ProgressBar healthBar;
	[Export] public float maxEnergy = 100f;
	[Export] public float currentEnergy;
	private ProgressBar energyBar;
	private ProgressBar fatigueBar;
	[Export] private float maxEnergyReductionRate;
	[Export] private float sprintingEnergyReduction;
	[Export] private float jumpingEnergyCost;
	[Export] private float energyRegen;
	[Signal] public delegate void ChangeHUDEventHandler();

	public override void _EnterTree()
	{

	}

	public override void _Ready()
	{
		// Only the local player should capture the mouse and hide self
		if (IsMultiplayerAuthority() && IsLocalControlled())
		{
			Input.SetMouseMode(Input.MouseModeEnum.Captured);

			// Hide all nodes in the "self_hide" group
			foreach (var child in GetTree().GetNodesInGroup("self_hide"))
			{
				if (child is Node3D node && IsAncestorOf(node))
				{
					node.Visible = false;
				}
			}

			// Load the player HUD
			Control HUD = (Control)hudScene.Instantiate();
			AddChild(HUD);
			// Hard-coded values for now
			healthBar = HUD.GetNode<ProgressBar>("HealthBar/HealthProgressBar");
			energyBar = HUD.GetNode<ProgressBar>("EnergyBar/EnergyProgressBar");
			fatigueBar = HUD.GetNode<ProgressBar>("EnergyBar/FatigueProgressBar");

			// Set health and energy values to their default
			currentHealth = maxHealth;
			currentEnergy = maxEnergy;
			healthBar.MaxValue = healthBar.Value = maxHealth; // double-to-float shenaningans :pensive:
			energyBar.MaxValue = energyBar.Value = fatigueBar.MaxValue = maxEnergy;
			fatigueBar.Value = 0;
		}
		else
		{
			Input.SetMouseMode(Input.MouseModeEnum.Visible);
		}

		Connect("ChangeHUD", new Callable(this, nameof(UpdateLocalHud)));

		if (collisionPusher != null)
		{
			collisionPusherAB = collisionPusher as AnimatableBody3D;
		}
		if (collisionPusherAB != null)
		{
			collisionPusherAB.SyncToPhysics = true;
		}

		//interactMaskUint = (uint)(1 << (interactLayer - 1));// Convert layer number to bitmask

		var mapManager = GetTree().CurrentScene.GetNodeOrNull<Node>("%MapManager");
		if (mapManager != null)
		{
			mapManager.Connect("map_reloaded", new Callable(this, nameof(OnMapReloaded)));
		}
	}

	// Called when the map is reloaded, checks if held object is still valid
	private void OnMapReloaded()
	{
		HandleInvalidHeldObject();
	}

	// Check if this player instance is controlled by the local user
	private bool IsLocalControlled()
	{
		return GetMultiplayerAuthority() == Multiplayer.GetUniqueId();
	}

	// Handle mouse input for looking around
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsLocalControlled()) return;

		if (@event is InputEventMouseMotion mouseMotion)
		{
			head.RotateY(-mouseMotion.Relative.X * mouseSensitivity);
			camera.RotateX(-mouseMotion.Relative.Y * mouseSensitivity);
			camera.RotationDegrees = new Vector3(Mathf.Clamp(camera.RotationDegrees.X, -85, 85), camera.RotationDegrees.Y, camera.RotationDegrees.Z);
		}

		if (Input.IsActionPressed("sprint") && IsOnFloor() && !isSprinting)
			isSprinting = true;
		else if (!Input.IsActionPressed("sprint") && isSprinting)
			isSprinting = false;
	}

	// Handles movement, jumping, sprinting, head bobbing, and interaction input, probably needs to be split up later
	public override void _PhysicsProcess(double delta)
	{
		if (!IsLocalControlled()) return; // local player processes movement

		Vector3 velocity = Velocity;
		float maxEnergyChange = -maxEnergyReductionRate * (float)delta;
		float energyChange = 0;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = jumpVelocity;
			energyChange -= jumpingEnergyCost;
			maxEnergyChange -= jumpingEnergyCost * 0.3f;
		}

		// Handle sprint input and FOV change
		if (isSprinting)
		{
			speed = sprintSpeed;
			camera.Fov = Mathf.Lerp(camera.Fov, fov * fovChange, (float)delta * fovChangeSpeed);

			if (IsOnFloor()) // Don't decrease energy in midair
            {
                energyChange -= sprintingEnergyReduction * (float)delta;
                maxEnergyChange -= sprintingEnergyReduction * 0.3f * (float)delta;
            }
		}
		else
		{
			speed = walkSpeed;
			camera.Fov = Mathf.Lerp(camera.Fov, fov, (float)delta * fovChangeSpeed);
		}

		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.
		Vector2 inputDir = Input.GetVector("left", "right", "forward", "back"); // WASD
		Vector3 direction = (head.Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

		// intertia
		if (IsOnFloor()) // full control when on the ground
		{
			if (direction != Vector3.Zero)
			{
				velocity.X = direction.X * speed;
				velocity.Z = direction.Z * speed;
			}
			else
			{
				velocity.X = Mathf.Lerp(velocity.X, direction.X * speed, (float)delta * inertiaGroundValue);
				velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * speed, (float)delta * inertiaGroundValue);
			}
		}
		else // inertia when in the air
		{
			velocity.X = Mathf.Lerp(velocity.X, direction.X * speed, (float)delta * inertiaAirValue);
			velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * speed, (float)delta * inertiaAirValue);
		}

		// Handle head bobbing
		if (IsOnFloor() && direction != Vector3.Zero)
		{
			bobTimer += (float)delta * velocity.Length();
			camera.Position = headBob(bobTimer);
		}
		else
		{
			bobTimer = 0.0f;
			camera.Position = Vector3.Zero;
		}

		// add tether force if holding rope
		if (tetherAnchor != null)
		{
			velocity = TetherToRopeAnchor(delta, velocity);
		}

		Velocity = velocity;
		MoveAndSlide();

		// Handle interaction input
		if (Input.IsActionJustPressed("use"))// LMB
			OnUsedPressed();
		if (Input.IsActionJustPressed("pickup")) // E
		{
			if (heldObject == null)
			{
				var target = GetInteractableLookedAt();
				if (target != null)
				{
					//GD.Print(target.ToString());
					PickupObject(target);
				}
			}
			else
				DropObject();
		}

		// update pusher to match player position
		if (collisionPusherAB != null)
		{
			collisionPusherAB.GlobalTransform = GlobalTransform;
		}
		if (interactableRB != null)
		{
			interactableRB.GlobalTransform = GlobalTransform;
		}

		//get looked at object for debug and highlighting later
		var lookedAtObject = RayCastForward();
		if (lookedAtObject.Count > 0)
		{
			if (lookedAtObject.TryGetValue("collider", out var colliderVariant))
			{
			var godotObj = ((Variant)colliderVariant).AsGodotObject();
				if (godotObj is Node colliderNode)
				{
					var interactable = FindInteractable(colliderNode);
					var entity = FindEntity(colliderNode);
					//debug prints for now
					//if (interactable != null)
					//{
					//	GD.Print("Looking at interactable: " + interactable.GetInteractableId());
					//}
					//if (entity != null)
					//{
					//	GD.Print("Looking at entity: " + entity.GetEntityId());
					//}
				}
			}
		}
		

		// If the player isn't doing anything that would spend energy, regain energy
		if (energyChange == 0 && IsOnFloor())
			energyChange = energyRegen * (float)delta;

		// Update the player's current energy
		ChangeCurrentEnergy(energyChange);
		ChangeMaxEnergy(maxEnergyChange);

		// Leo's really cool health/energy/fatigue testing code
		if (Input.IsKeyPressed(Key.Kp1)) // When Numpad 1 is pressed, reduce health
			ChangeCurrentHealth(-10);
		else if (Input.IsKeyPressed(Key.Kp2)) // When Numpad 2 is pressed, restore health
			ChangeCurrentHealth(10);
		else if (Input.IsKeyPressed(Key.Kp4)) // When Numpad 4 is pressed, reduce energy
			ChangeCurrentEnergy(-10);
		else if (Input.IsKeyPressed(Key.Kp5)) // When Numpad 5 is pressed, restore energy
			ChangeCurrentEnergy(10);
		else if (Input.IsKeyPressed(Key.Kp7)) // When Numpad 7 is pressed, reduce fatigue
			ChangeMaxEnergy(-10);
		else if (Input.IsKeyPressed(Key.Kp8)) // When Numpad 8 is pressed, restore fatigue
			ChangeMaxEnergy(10);
    }

	// Simple head bobbing effect
	private Vector3 headBob(float timer)
	{
		Vector3 bobPos = Vector3.Zero;
		bobPos.Y = Mathf.Sin(timer * bobFrequency) * bobAmplitude;
		bobPos.X = Mathf.Cos(timer * bobFrequency * 0.5f) * bobAmplitude;
		return bobPos;
	}

	// disconnects wagon tether and forgets held object if invalid
	private bool HandleInvalidHeldObject()
	{
		if (HeldValid()) return false;

		RemoveTetherAnchor();
		heldObject = null;
		return true;
	}

	// get the inventory slot node for holding items
	public Node3D GetInventorySlot()
	{
		if (inventorySlotPath == null || inventorySlotPath == String.Empty) return null;
		return GetNode<Node3D>(inventorySlotPath);
	}

	// Handle the "use" action input
	private void OnUsedPressed()
	{
		if (!IsLocalControlled() || heldObject == null) return; // Only the local player can interact
		UseHeldObject();
	}

	// Raycast forward from the camera to find what the player is looking at
	public Dictionary RayCastForward()
	{
		// screen center
		var vp = GetViewport();
		Vector2 center = vp.GetVisibleRect().Size * 0.5f;

		Vector3 origin = camera.ProjectRayOrigin(center);
		Vector3 dir = camera.ProjectRayNormal(center);
		Vector3 to = origin + dir * interactRange;

		// Raycast in front of player on interact layer
		var state = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(origin, to);
		query.CollisionMask = interactMaskUint;
		query.Exclude = new Array<Rid> { GetRid(), collisionPusherAB.GetRid(), interactableRB.GetRid() }; // ignore self

		var hit = state.IntersectRay(query);
		return hit;
	}

	public Interactable GetOwnInteractable()
	{
		return interactableRB;
	}

	// Get the Interactable the player is currently looking at
	private Interactable GetInteractableLookedAt()
	{
		if (camera == null) return null;

		var hit = RayCastForward();
		if (hit.Count == 0) return null;

		// "collider" can be Node or RigidBody/Area/CollisionObject3D etc.
		if (hit.TryGetValue("collider", out var colliderVariant))
		{
			var godotObj = ((Variant)colliderVariant).AsGodotObject();
			if (godotObj is Node colliderNode)
				return FindInteractable(colliderNode);
		}

		return null;
	}

	// Traverse up the node tree to find an Interactable component
	private Interactable FindInteractable(Node node)
	{
		while (node != null)
		{
			if (node is Interactable interactable && !string.IsNullOrEmpty(interactable.interactableId))
				return interactable;
			node = node.GetParent();
		}
		return null;
	}

	// Template logic for later Entity interactions
	//// Get the Entity the player is currently looking at
	private Entity GetEntityLookedAt()
	{
		if (camera == null) return null;
	
		var hit = RayCastForward();
		if (hit.Count == 0) return null;
	
		// "collider" can be Node or RigidBody/Area/CollisionObject3D etc.
		if (hit.TryGetValue("collider", out var colliderVariant))
		{
			var godotObj = ((Variant)colliderVariant).AsGodotObject();
			if (godotObj is Node colliderNode)
				return FindEntity(colliderNode);
		}
	
		return null;
	}
	
	// Traverse up the node tree to find an Entity component
	private Entity FindEntity(Node node)
	{
		while (node != null)
		{
			if (node is Entity entity && !string.IsNullOrEmpty(entity.entityId))
				return entity;
			node = node.GetParent();
		}
		return null;
	}

	// pickup currently looked at object, drop current held object if any
	public void PickupObject(Interactable obj)
	{
		if (heldObject != null)
		{
			DropObject();
		}

		if (obj.TryPickup(this) == true)
		{
			heldObject = obj;
			//GD.Print("Picked up object: " + obj.interactableId);
		}
	}

	// Drop the currently held object
	public void DropObject()
	{
		if (HandleInvalidHeldObject()) return; // if invalid item was handled return, weird auto call on join?

		if (heldObject.TryDrop(this) == true)
		{
			heldObject = null;
		}
	}

	// Use the held object on itself or on a target
	public void UseHeldObject()
	{
		if (HandleInvalidHeldObject()) return; // if invalid item was handled return

		//check if looking at another interactable first
		var targetInteractable = GetInteractableLookedAt();
		if (targetInteractable != null) //target is interactable
		{
			heldObject.TryUseOnInteractable(this, targetInteractable);
			if (!IsInstanceValid(heldObject) || heldObject.IsQueuedForDeletion())
			{
				heldObject = null; // The held object was destroyed during use
			}
			return;
		}

		//check if looking at an entity second
		var targetEntity = GetEntityLookedAt();
		if (targetEntity != null) //target is entity
		{
			heldObject.TryUseOnEntity(this, targetEntity);
			if (!IsInstanceValid(heldObject) || heldObject.IsQueuedForDeletion())
			{
				heldObject = null; // The held object was destroyed during use
			}
			return;
		}

		//use on self if no target found
		heldObject.TryUseSelf(this);
		if (!IsInstanceValid(heldObject) || heldObject.IsQueuedForDeletion())
		{
			heldObject = null; // The held object was destroyed during use
		}
	}

	// Set up a tether anchor point for rope mechanics
	public void SetTetherAnchor(Node3D anchor, float maxDist, float buffer, float strength)
	{
		tetherAnchor = anchor;
		maxTetherDist = maxDist;
		tetherBuffer = buffer;
		tetherStrength = strength;
	}

	// Remove the tether anchor
	public void RemoveTetherAnchor()
	{
		tetherAnchor = null;
	}

	// Apply tethering force to keep player within max distance of the anchor
	private Vector3 TetherToRopeAnchor(double delta, Vector3 velocity)
	{
		Vector3 toPlayer = GlobalTransform.Origin - tetherAnchor.GlobalTransform.Origin;
		float dist = toPlayer.Length();

		//if player is past max tether distance, apply force to pull back
		if (dist > maxTetherDist)
		{
			Vector3 outwardVector = toPlayer.Normalized();

			//pull back toward maxTetherDistance with strength based on how far past max
			float distPastMax = dist - maxTetherDist;
			velocity += -(outwardVector * (tetherStrength * (float)delta * distPastMax));

			// clamp total dist to maxTetherDist with buffer, this might be causing jitters
			if (dist > maxTetherDist + tetherBuffer)
			{
				GlobalTransform = new Transform3D(GlobalTransform.Basis, tetherAnchor.GlobalTransform.Origin + outwardVector * (maxTetherDist + tetherBuffer));
			}
		}

		return velocity;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ChangeCurrentHealth(float diff)
	{
		currentHealth = Mathf.Min(currentHealth + diff, maxHealth);
		if (currentHealth <= 0)
		{
			currentHealth = 0;
			// The rest of the death code goes here
		}
		EmitSignal("ChangeHUD");
		//UpdateLocalHud();
		//healthBar.Value = currentHealth;
		//GD.Print("Current health: " + currentHealth);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ChangeCurrentEnergy(float diff)
	{
		// If the player is out of energy to spend, have the energy cost affect health instead
		if (currentEnergy <= 0 && diff < 0)
			ChangeCurrentHealth(diff);
		else
		{
			currentEnergy = Mathf.Min(currentEnergy + diff, maxEnergy);
			if (currentEnergy <= 0)
				currentEnergy = 0;
			EmitSignal("ChangeHUD");
			//UpdateLocalHud();
			//energyBar.Value = currentEnergy;
			//GD.Print("Current energy: " + currentEnergy);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ChangeMaxEnergy(float diff)
	{
		maxEnergy = maxEnergy + diff;
		if (maxEnergy <= 0)
			maxEnergy = 0;
		else if (maxEnergy > 100)
			maxEnergy = 100;
		ChangeCurrentEnergy(0); // Update energy bar
		EmitSignal("ChangeHUD");
		//UpdateLocalHud();
		//fatigueBar.Value = Mathf.Abs(maxEnergy - 100);
		//GD.Print("Current fatigue: " + Mathf.Abs(maxEnergy - 100));
		// Idk if we're doing anything else with this
	}
	
	private void UpdateLocalHud()
	{
		if (!(IsLocalControlled() && IsMultiplayerAuthority())) return; // only local player updates hud
		healthBar.Value = currentHealth;
		energyBar.Value = currentEnergy;
		fatigueBar.Value = Mathf.Abs(maxEnergy - 100);
    }
}
