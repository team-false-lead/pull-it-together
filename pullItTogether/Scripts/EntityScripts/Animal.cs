using Godot;
using System;
using System.Reflection.Metadata.Ecma335;

public partial class Animal : Entity
{
    [Export] public Label3D label;
    [Export] public Node BT;
    [Export] public float movementSpeed = 5f;
    [Export] public Vector3 targetPosition;
    [Export] public Node3D inventorySlot;
    [Export] public bool hasItem = false;
    [Export] public bool hasItemTarget = false;
    public Interactable itemTarget = null;

    public override void _Ready()
    {
        base._Ready();
        
        if (multiplayerActive && !multiplayer.IsServer())
        {
            if (BT != null)
            {
                BT.Call("set_active", false);
            }
        }
    }

    // By default, entities do not accept being used on them
    // need to override in child classes to ensure given items are appropriate

    // Logic for accepting use from food items
    public override void AcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (itemManager == null) InitReferences();
        var id = GetEntityId(); //get unique id, default to name

        // Request spawn via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestGiveAnimalItem), id, source.GetInteractableId());
            if (error != Error.Ok)
            {
                GD.PrintErr("Animal: Failed to request use via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles spawn directly
        {
            itemManager.DoGiveAnimalItem(id, source.GetInteractableId());
        }
    }


    public override void _PhysicsProcess(double delta)
    {
        if (multiplayerActive && !multiplayer.IsServer())
        {
            return; // Clients do not run AI logic
        }

        base._PhysicsProcess(delta);
    }

    public bool PickupItem()
    {
        if (itemTarget != null && !hasItem)
        {
            if (itemManager == null) InitReferences();
            var id = GetEntityId(); //get unique id, default to name

            // request damage wheel via RPC if not server
            if (multiplayerActive && !multiplayer.IsServer())
            {
                var error = itemManager.RpcId(1, nameof(ItemManager.RequestAnimalPickupItem), id, itemTarget.GetInteractableId());
                if (error != Error.Ok)
                {
                    GD.PrintErr("Animal: Failed to request use via RPC. Error: " + error);
                    return false;
                }
                hasItem = true;
                return hasItem;
            }
            else // Server or single-player handles spawn directly
            {
                itemManager.DoAnimalPickupItem(id, itemTarget.GetInteractableId());
                hasItem = true;
                return hasItem;
            }
        }
        return hasItem;
    }

    public Node3D GetInventorySlot()
    {
        if (inventorySlot != null)
        {
            return inventorySlot;
        }
        return null;
    }
}