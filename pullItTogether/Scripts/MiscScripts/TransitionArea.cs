using Godot;
using System;
using System.Collections;
using System.Threading.Tasks;

public partial class TransitionArea : Area3D
{
    [Export]
    private Label3D label;
    [Export]
    private GpuParticles3D pinkConfetti;
    [Export]
    private GpuParticles3D yellowConfetti;
    [Export]
    private GpuParticles3D cyanConfetti;

    private void OnBodyEntered(Node3D node)
    {
        // TEMPORARY CODE: Not all transition areas should change Label3D text or summon confetti
        label.Text = "You did it :)";
        SetDeferred("monitoring", false);
        pinkConfetti.Position = yellowConfetti.Position = cyanConfetti.Position = new Vector3(node.Position.X, 11, -60);
        pinkConfetti.Emitting = yellowConfetti.Emitting = cyanConfetti.Emitting = true;
        ResetAfterSeconds(3); // Warning isn't important because this is super temporary code
    }

    public async Task ResetAfterSeconds(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        GetTree().ChangeSceneToFile("res://Scenes/TestMap.tscn"); // No clue if this messes with the multiplayer stuff
    }
}
