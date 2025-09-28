using Godot;
using System;

// Syncs the controlled camera to the local player.
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
