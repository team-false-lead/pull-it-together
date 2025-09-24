using Godot;
using System;

//Based on Juiced Up First Person Character Controller Tutorial - Godot 3D FPS - YouTube
// https://www.youtube.com/watch?v=A3HLeyaBCq4&t=461s&ab_channel=LegionGames

/// PlayerController handles player movement, camera control, and head bobbing effect.
public partial class PlayerController : CharacterBody3D
{

	public float speed;
	public float walkSpeed = 5.0f;
	public float sprintSpeed = 8.0f;
	public float jumpVelocity = 4.5f;
	public float inertiaAirValue = 3.0f;
	public float inertiaGroundValue = 7.0f;

	[Export] public Node3D head;
	[Export] public Camera3D camera;
	[Export] public float fov = 75.0f;
	[Export] public float fovChange = 1.25f;
	public float fovChangeSpeed = 5.0f;
	[Export] public float mouseSensitivity = 0.005f;
	[Export] public float bobFrequency = 2.0f;
	[Export] public float bobAmplitude = 0.08f;
	private float bobTimer = 0.0f;

	private Interactable heldObject = null;
	[Export] public float interactRange = 3.0f;
	[Export] public uint interactMask = 3;



	public override void _EnterTree()
	{

	}

	public override void _Ready()
	{
		if (IsMultiplayerAuthority())
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
		else
		{
			Input.SetMouseMode(Input.MouseModeEnum.Visible);
		}
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

	// Called every physics frame. Delta is time since last physics frame.
	public override void _PhysicsProcess(double delta)
	{
		if (!IsLocalControlled()) return;

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

		// Handle sprint
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
		Vector2 inputDir = Input.GetVector("left", "right", "forward", "back");
		Vector3 direction = (head.Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

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

		Velocity = velocity;
		MoveAndSlide();

		// Handle interaction input
		if (Input.IsActionJustPressed("use"))
			OnUsedPressed();
		if (Input.IsActionJustPressed("drop"))
			DropObject();
		if (Input.IsActionJustPressed("pickup"))
		{
			var target = GetInteractableLookedAt();
			if (target != null)
			{
				GD.Print(target.ToString());
				PickupObject(target);
			}
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

	// Handle the "use" action input
	private void OnUsedPressed()
	{
		if (!IsLocalControlled()) return; // Only the local player can interact
		if (heldObject == null) return;

		var target = GetInteractableLookedAt();
		
		if (target != null && target != heldObject)
		{
			UseHeldObjectOn(target);
		}
		else
		{
			UseHeldObject();
		}
	}

	// Raycast to find an interactable object the player is looking at
	private Interactable GetInteractableLookedAt()
	{
		if (camera == null) return null;

		// screen center
		var vp = GetViewport();
		Vector2 center = vp.GetVisibleRect().Size * 0.5f;

		Vector3 origin = camera.ProjectRayOrigin(center);
		Vector3 dir = camera.ProjectRayNormal(center);
		Vector3 to = origin + dir * interactRange;

		var state = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(origin, to);
		query.CollisionMask = interactMask;
		// ignore self so we don't hit our own body
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var hit = state.IntersectRay(query);
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
			if (node is Interactable interactable)
				return interactable;
			node = node.GetParent();
		}
		return null;
	}

	// Methods to manage held object
	public void PickupObject(Interactable obj)
	{
		if (heldObject != null)
		{
			DropObject();
		}

		if (obj.TryPickup(this))
		{
			heldObject = obj;
		}
	}

	// Drop the currently held object
	public void DropObject()
	{
		if (heldObject != null)
		{
			heldObject.Drop(this, camera.GlobalTransform.Origin + -(camera.GlobalTransform.Basis.Z * interactRange)); // Drop in front of the player
			heldObject = null;
		}
	}

	// Use the held object on itself or on a target
	public void UseHeldObject()
	{
		if (heldObject != null)
		{
			heldObject.TryUseSelf(this);
			if (!IsInstanceValid(heldObject) || heldObject.IsQueuedForDeletion())
			{
				heldObject = null; // The held object was destroyed during use
			}
		}
	}

	// Use the held object on a target interactable
	public void UseHeldObjectOn(Interactable target)
	{
		if (heldObject != null && target != null)
		{
			heldObject.TryUseOn(this, target);
			if (!IsInstanceValid(heldObject))
			{
				heldObject = null; // The held object was destroyed during use
			}
		}
	}
}