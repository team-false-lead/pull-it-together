using Godot;
using System;
using System.Diagnostics;
using static Godot.WebSocketPeer;

// a wheel that can be repaired with wooden planks
public partial class Wheel : Entity
{
    [Export] public float currentHealth; //export to use on multiPlayer syncer
    [Export] public float maxHealth = 100f;
    //[Export] public bool isBroken = false;
    [Export] public float repairAmount = 34f;
    [Export] public float damageAmountMax = 0.75f;
    [Export] public float damageAmountMin = 0f;
    [Export] public CollisionShape3D wheelCollision;
    public float speed;
    public float currentYVel;
    public float prevYVel;
    public Wagon wagonScript;
    public Random rnd = new Random();
    public override void _Ready()
    {
        wagonScript = GetTree().GetFirstNodeInGroup("wagon") as Wagon;
        if (itemManager == null) InitReferences();
    }

    // By default, entities do not accept being used on them
    //override to accept food items
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("plank"))
        {
            return true;
        }
        return false;
    }

    // Logic for accepting use from food items
    public override void AcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (itemManager == null) InitReferences();
        var id = GetEntityId(); //get unique id, default to name

        // Request repair via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestRepairWheel), id, source.GetInteractableId());
            if (error != Error.Ok)
            {
                GD.PrintErr("Wheel: Failed to request repairing via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles repair directly
        {
            itemManager.DoRepairWheel(id, source.GetInteractableId());
        }
    }

    // if not held, reset to reset point
    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (itemManager == null) InitReferences();
        //if (!multiplayerActive || (multiplayerActive && multiplayer.IsServer()))
        //{
        //    itemManager.DoDamageWheel(GetEntityId(), damageAmount);
        //}
        prevYVel = currentYVel;

        if (Visible == true)
        {
            speed = wagonScript.localVelocity.Z;

            if (currentHealth >= 75 && RotationDegrees.Z != 90)
            {
                RotationDegrees = new Vector3(Rotation.X, Rotation.Y, 90);
            }
            if (currentHealth < 75 && currentHealth >= 50 && RotationDegrees.Z != 93)
            {
                RotationDegrees = new Vector3(Rotation.X, Rotation.Y, 93);
            }
            if (currentHealth < 50 && currentHealth >= 25 && RotationDegrees.Z != 96)
            {
                RotationDegrees = new Vector3(Rotation.X, Rotation.Y, 96);
            }
            if (currentHealth < 25 && RotationDegrees.Z != 99)
            {
                RotationDegrees = new Vector3(Rotation.X, Rotation.Y, 99);
            }


            Rotation += new Vector3(speed * 0.015f, 0f, 0f);


            if (MathF.Abs(speed) > 0.25f)
            {
                itemManager.DoDamageWheel(GetEntityId(), MathF.Abs(speed) * (float)(rnd.NextDouble() * (damageAmountMax - damageAmountMin) * delta));
            }

            ImpulseDamage(delta);

            //if(currentHealth < 10)
            //GD.Print(currentHealth);
        }


        if (currentHealth <= 0)
        {
            if (wheelCollision != null)
            {
                Visible = false;
                wheelCollision.Scale = new Vector3(.4f, .4f, .4f);
            }
        }
        else if (currentHealth > 0)
        {
            if (wheelCollision != null)
            {
                Visible = true;
                wheelCollision.Scale = new Vector3(1f, 1f, 1f);

            }
        }
    }

    public void ImpulseDamage(double delta)
    {
        currentYVel = wagonScript.localVelocity.Y;

        float velocityDelta = Math.Abs(prevYVel - currentYVel);

        if(velocityDelta > .5)
        {
            Debug.Print("Damage: " + velocityDelta * 5);

        }

        if (velocityDelta > 0.5f)
        {
            itemManager.DoDamageWheel(GetEntityId(), velocityDelta * 5);
        }

    }

}
