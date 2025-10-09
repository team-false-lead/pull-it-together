using Godot;
using System;

public partial class Wagon : RigidBody3D
{
    [Export] float linearXMultiplier = 0.25f;
    [Export] float angularYMultiplier = 0.75f;

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        state.LinearVelocity *= new Vector3(linearXMultiplier, 1f, 1f);
        state.AngularVelocity *= new Vector3(1f, angularYMultiplier, 1f);
        //GD.Print(state.AngularVelocity);
        base._IntegrateForces(state);
    }

}
