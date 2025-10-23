using Godot;
using System;

// a wheel that can be repaired with wooden planks
public partial class Wheel : Entity
{
    [Export] public float currentHealth = 10f; //export to use on multiPlayer syncer
    [Export] public float maxHealth = 100f;
    [Export] public bool isBroken = false;
    [Export] public float repairAmount = 34f;
    [Export] public CollisionShape3D wheelCollision;

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
        if (isBroken == true && Visible == true)
        {
            if (wheelCollision != null)
            {
                Visible = false;
                wheelCollision.Scale = new Vector3(.25f, .25f, .25f);
            }
        }
        else if (isBroken == false && Visible == false)
        {
            if (wheelCollision != null)
            {
                Visible = true;
                wheelCollision.Scale = new Vector3(1f, 1f, 1f);

            }
        }
    }

}
