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
	}

	// Simple head bobbing effect
	private Vector3 headBob(float timer)
	{
		Vector3 bobPos = Vector3.Zero;
		bobPos.Y = Mathf.Sin(timer * bobFrequency) * bobAmplitude;
		bobPos.X = Mathf.Cos(timer * bobFrequency * 0.5f) * bobAmplitude;
		return bobPos;
	}
}
