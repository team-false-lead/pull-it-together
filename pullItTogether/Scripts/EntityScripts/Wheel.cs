using Godot;
using System;
using static Godot.WebSocketPeer;

// a wheel that can be repaired with wooden planks
public partial class Wheel : Entity
{
    [Export] public float currentHealth; //export to use on multiPlayer syncer
    [Export] public float maxHealth = 100f;
    //[Export] public bool isBroken = false;
    [Export] public float repairAmount = 34f;
    [Export] public float damageAmount = 0.1f;
    [Export] public CollisionShape3D wheelCollision;
    public float speed;
    public Wagon wagonScript;

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
        if (itemManager == null) InitReferences();
        //if (!multiplayerActive || (multiplayerActive && multiplayer.IsServer()))
        //{
        //    itemManager.DoDamageWheel(GetEntityId(), damageAmount);
        //}
        if(currentHealth > 76)
        {
            //RotationDegrees = new Vector3(Rotation.X, Rotation.Y, 90);
        }

        if (Visible == true)
        {
            speed = wagonScript.localVelocity.Z;
            Rotation += new Vector3(speed * 0.015f, 0f, 0f);
            if (MathF.Abs(speed) > 0.25f)
            {
                itemManager.DoDamageWheel(GetEntityId(), MathF.Abs(speed) * 0.01f);
            }

            GD.Print(currentHealth);
        }


        if (currentHealth <= 0 && Visible == true)
        {
            if (wheelCollision != null)
            {
                Visible = false;
                wheelCollision.Scale = new Vector3(.25f, .25f, .25f);
            }
        }
        else if (currentHealth > 0 && Visible == false)
        {
            if (wheelCollision != null)
            {
                Visible = true;
                wheelCollision.Scale = new Vector3(1f, 1f, 1f);

            }
        }
    }

}
