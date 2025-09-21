using Godot;
using System;

// Syncs the player's camera to the local player.
public partial class PlayerCameraSync : Node
{
    [Export] public Camera3D Camera;

    public override void _Ready()
    {
        if (Camera != null)
        {
            Camera.Current = IsMultiplayerAuthority();
        }
    }
}
