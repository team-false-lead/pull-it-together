using Godot;
using System;

public partial class TransitionArea : Area3D
{
    [Export]
    private Label3D label;

    private void OnBodyEntered(Node3D node)
    {
        label.Text = "Your did it :)";
        GetTree().CallDeferred("change_scene_to_file", "res://Scenes/TestMap.tscn"); // No clue if this messes with the multiplayer stuff
    }
}
