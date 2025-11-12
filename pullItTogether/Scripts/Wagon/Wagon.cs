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
    [Export] public Wheel[] wheels;
    [Export] float frictionPerWheel;
    public float wheel1 = 0;
    public float wheel2 = 0;
    public float wheel3 = 0;
    public float wheel4 = 0;
    public Vector3 localVelocity;

    // Limits sideways velocity to prevent unrealistic turning
    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        //limit angular velocity of the wagon by the specified value
        state.AngularVelocity *= new Vector3(1f, angularYMultiplier, 1f);

        // Limiting linear velocity is a bit more complicated (the simple solution
        // affects global velocity and not local velocity)
        // 
        // Local velocity is thus:
        localVelocity = state.LinearVelocity * Basis;
        // Clamp the X value of local velocity
        localVelocity *= new Vector3(linearXMultiplier, 1f, 1f);
        // Un-localize velocity to make it global
        state.LinearVelocity = localVelocity * Basis.Inverse();

        base._IntegrateForces(state);
    }

    public override void _PhysicsProcess(double delta)
    {
        PhysicsMaterial currentMaterial = PhysicsMaterialOverride;

        if (wheels[0].currentHealth <= 0)
        {
            wheel1 = frictionPerWheel;
        }
        else
        {
            wheel1 = 0f;
        }
        if (wheels[1].currentHealth <= 0)
        {
            wheel2 = frictionPerWheel;
        }
        else
        {
            wheel2 = 0f;
        }
        if (wheels[2].currentHealth <= 0)
        {
            wheel3 = frictionPerWheel;
        }
        else
        {
            wheel3 = 0f;
        }
        if (wheels[3].currentHealth <= 0)
        {
            wheel4 = frictionPerWheel;
        }
        else
        {
            wheel4 = 0f;
        }

        PhysicsMaterialOverride.Friction = 0.05f + wheel1 + wheel2 + wheel3 + wheel4;
        //GD.Print(PhysicsMaterialOverride.Friction);
        ScaleWeightPlayerCount();
    }

    public void ScaleWeightPlayerCount()
    {
        var playersArray = GetTree().GetNodesInGroup("players"); // get current players
        //GD.Print(playersArray.Count + ": " + Mass);

        if (playersArray.Count == 1 && Mass != 400f)
        {
            Mass = 400f;
        }
        else if (playersArray.Count == 2 && Mass != 550f)
        {
            Mass = 550f;
        }
        else if (playersArray.Count == 3 && Mass != 700f)
        {
            Mass = 700f;
        }
        else if (playersArray.Count == 4 && Mass != 850f)
        {
            Mass = 850f;
        }
    }
}
