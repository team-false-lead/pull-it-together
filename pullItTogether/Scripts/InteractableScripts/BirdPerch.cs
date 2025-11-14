using Godot;
using System;

public partial class BirdPerch : Node3D
{
    [Export] public ItemDetector birdDetector;
	[Export] public bool birdInRange = false;
	[Export] public bool wagonFrontPerch = false;
	[Export] public bool wagonBackPerch = false;

	public override void _Ready()
	{
		if (birdDetector != null)
		{
			birdDetector.FilteredBodyEntered += OnBirdAdded;
			birdDetector.FilteredBodyExited += OnBirdRemoved;
		}
	}

	private void OnBirdAdded(Node3D item)
	{
		if (item is Bird)
		{
			birdInRange = true;
		}
	}

	private void OnBirdRemoved(Node3D item)
	{
		if (item is Bird && birdDetector.itemsInside.Count == 0)
		{
			birdInRange = false;
		}
	}
}
