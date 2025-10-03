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
	[Export] public int interactLayer = 4;

	// Collision parameters
	[Export] public AnimatableBody3D collisionPusher;
	private uint interactMaskUint = 8;

	// Rope tether parameters when carrying rope grab point
	private Node3D tetherAnchor;
	private float maxTetherDist;
	private float tetherBuffer;
	private float tetherStrength;


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
		}

		if (collisionPusher != null)
		{
			collisionPusher.SyncToPhysics = true;
		}

		interactMaskUint = (uint)(1 << (interactLayer - 1));// Convert layer number to bitmask

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
	}

	// Handles movement, jumping, sprinting, head bobbing, and interaction input, probably needs to be split up later
	public override void _PhysicsProcess(double delta)
	{
		if (!IsLocalControlled()) return; // local player processes movement

		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = jumpVelocity;
		}

		// Handle sprint input and FOV change
		if (Input.IsActionPressed("sprint"))
		{
			speed = sprintSpeed;
			camera.Fov = Mathf.Lerp(camera.Fov, fov * fovChange, (float)delta * fovChangeSpeed);
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
		if (Input.IsActionJustPressed("drop"))// Q
			DropObject();
		if (Input.IsActionJustPressed("pickup")) // E
		{
			var target = GetInteractableLookedAt();
			if (target != null)
			{
				//GD.Print(target.ToString()); // debug
				PickupObject(target);
			}
		}

		// update pusher to match player position
		if (collisionPusher != null)
		{
			collisionPusher.GlobalTransform = GlobalTransform;
		}
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
		query.Exclude = new Array<Rid> { GetRid(), collisionPusher.GetRid() }; // ignore self

		var hit = state.IntersectRay(query);
		return hit;
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
	//private Entity GetEntityLookedAt()
	//{
	//	if (camera == null) return null;
	//
	//	var hit = RayCastForward();
	//	if (hit.Count == 0) return null;
	//
	//	// "collider" can be Node or RigidBody/Area/CollisionObject3D etc.
	//	if (hit.TryGetValue("collider", out var colliderVariant))
	//	{
	//		var godotObj = ((Variant)colliderVariant).AsGodotObject();
	//		if (godotObj is Node colliderNode)
	//			return FindEntity(colliderNode);
	//	}
	//
	//	return null;
	//}
	//
	//// Traverse up the node tree to find an Entity component
	//private Entity FindEntity(Node node)
	//{
	//	while (node != null)
	//	{
	//		if (node is Interactable interactable)
	//			return interactable;
	//		node = node.GetParent();
	//	}
	//	return null;
	//}

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
		}
	}

	// Drop the currently held object
	public void DropObject()
	{
		if (HandleInvalidHeldObject()) return; // if invalid item was handled return

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
		var target = GetInteractableLookedAt();
		if (target != null) //target is interactable
		{
			heldObject.TryUseOnInteractable(this, target);
			if (!IsInstanceValid(heldObject) || heldObject.IsQueuedForDeletion())
			{
				heldObject = null; // The held object was destroyed during use
			}
			return;
		}

		//check if looking at an entity second
		//if (target == null)
		//{
		//	target = GetEntityLookedAt();
		//}
		//if (target != null) //target is entity
		//{
		//	heldObject.TryUseOnEntity(this, target);
		//	if (!IsInstanceValid(heldObject) || heldObject.IsQueuedForDeletion())
		//	{
		//		heldObject = null; // The held object was destroyed during use
		//	}
		//	return;
		//}

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
}