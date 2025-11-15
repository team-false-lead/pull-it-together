using Godot;
using System;

/// an arrow that can be used with a bow
public partial class Arrow : Interactable
{
    [Export] public float speed = 25f;
    [Export] public float linearDamp = 0.05f;
    [Export] public float angularDamp = 4f;
    public override bool CanBeCarried() { return false; }

    public override void _Ready()
    {
        if (multiplayerActive && !multiplayer.IsServer()) return;
        base._Ready();

        ContactMonitor = true;
        MaxContactsReported = 1;

        Vector3 dir = -GlobalTransform.Basis.Z.Normalized();
        LinearVelocity = dir * speed;
        LinearDamp = linearDamp;
        AngularDamp = angularDamp;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (multiplayerActive && !multiplayer.IsServer()) return;

        // Rotate arrow to face its velocity direction
        if (LinearVelocity.Length() > 0.1f)
        {
            GlobalTransform = new Transform3D(Basis.LookingAt(LinearVelocity.Normalized(), Vector3.Up), GlobalTransform.Origin);
        }

        if (GetContactCount() > 0)
        {
            // Stop the arrow on first collision
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            Freeze = true;
            Node3D collider = GetCollidingBodies()[0];
            if (collider is Beaver beaver)
            {
                beaver.TakeDamage(beaver.damageToTake);
            }
            StartDespawnTimer();
            SetPhysicsProcess(false);
        }
    }

    public void StartDespawnTimer()
    {
        SceneTreeTimer timer = GetTree().CreateTimer(3f);
        timer.Timeout += () =>
        {
            itemManager.DoDespawnItem(GetInteractableId());
        };
    }



}