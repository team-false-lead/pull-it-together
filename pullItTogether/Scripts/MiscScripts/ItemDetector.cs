using Godot;
using System;
using System.Collections.Generic;

public partial class ItemDetector : Area3D
{
	[Export] CollisionShape3D collider;
	[Export] public float detectionRadius = 5.0f;
	[Export] public string[] customGroupFilters;
	public List<Node3D> itemsInside = new List<Node3D>();
	[Signal] public delegate void FilteredBodyEnteredEventHandler(Node3D body);
	[Signal] public delegate void FilteredBodyExitedEventHandler(Node3D body);


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BodyEntered += _onBodyEntered;
		BodyExited += _onBodyExited;
		if (collider != null)
		{
			var colliderShape = collider.Shape as CylinderShape3D;
			if (colliderShape != null)
			{
				colliderShape.Radius = detectionRadius;
			}
			if (collider.GetChild(0) is MeshInstance3D meshInstance)
			{
				if (meshInstance.Mesh is CylinderMesh cylinderMesh)
				{
					cylinderMesh.TopRadius = detectionRadius;
					cylinderMesh.BottomRadius = detectionRadius;
				}
			}
		}
	}

	private void _onBodyEntered(Node3D body)
	{
		if (customGroupFilters.Length > 0)
		{
			foreach (string group in customGroupFilters)
			{
				if (!string.IsNullOrEmpty(group) && body.IsInGroup(group))
				{
					itemsInside.Add(body);
					EmitSignal(SignalName.FilteredBodyEntered, body);
				}
			}
		}
		else if (body.IsInGroup("interactable") || body.IsInGroup("entity"))
		{
			itemsInside.Add(body);
			EmitSignal(SignalName.BodyEntered, body);
		}

	}

	private void _onBodyExited(Node3D body)
	{
		if (customGroupFilters.Length > 0)
		{
			foreach (string group in customGroupFilters)
			{
				if (!string.IsNullOrEmpty(group) && body.IsInGroup(group))
				{
					itemsInside.Remove(body);
					EmitSignal(SignalName.FilteredBodyExited, body);
				}
			}
		}
		else if (body.IsInGroup("interactable") || body.IsInGroup("entity"))
		{
			itemsInside.Remove(body);
			EmitSignal(SignalName.BodyExited, body);
		}
	}
}
