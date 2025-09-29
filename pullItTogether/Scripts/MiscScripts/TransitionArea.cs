using Godot;
using System;
using System.Collections;
using System.Threading.Tasks;

public partial class TransitionArea : Area3D
{
    [Export]
    private Label3D label;

    private void OnBodyEntered(Node3D node)
    {
        label.Text = "You did it :)";
        SetDeferred("monitoring", false);
        ResetAfterSeconds(3); // Warning isn't important because this is super temporary code
    }

    public async Task ResetAfterSeconds(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        GetTree().ChangeSceneToFile("res://Scenes/TestMap.tscn"); // No clue if this messes with the multiplayer stuff
    }
}
