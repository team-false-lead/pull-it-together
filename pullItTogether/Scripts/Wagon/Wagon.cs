using Godot;
using System;

public partial class Wagon : RigidBody3D
{
    //these values are multiplied with the wagon's linear velocity and angular velocity respectively
    //linearXMultiplier reduces the ease at which the wagon can be pulled sideways
    //angularYMultiplier reduces the ease at which the wagon can be turned around the y axis (left and right)
    //this makes the wagon move more like it has wheels
    [Export] float linearXMultiplier = 0.25f;
    [Export] float angularYMultiplier = 0.75f;

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        //multiplies the liner and angular velocities of the wagon by the specified values
        state.LinearVelocity *= new Vector3(linearXMultiplier, 1f, 1f);
        state.AngularVelocity *= new Vector3(1f, angularYMultiplier, 1f);
        base._IntegrateForces(state);
    }

}
