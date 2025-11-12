using Godot;
using System;
using System.Collections.Generic;

// a campfire that can cook food items
public partial class Campfire : Entity
{
    [Export] public int usesLeft = 3; //export to use on multiPlayer syncer
    [Export] public float dmgPerTick = 0.1f;
    [Export] public ItemDetector playerDetector;
    private List<Node3D> playersInside = new List<Node3D>();


    public override void _Ready()
    {
        base._Ready();

        if (playerDetector != null)
        {
            playerDetector.FilteredBodyEntered += OnItemsAdded;
            playerDetector.FilteredBodyExited += OnItemsRemoved;
        }
    }
    // By default, entities do not accept being used on them
    //override to accept food items
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("food") && source is Food food && !food.isCooked) // Can't accept use from cooked food
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

        // Request spawn via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestCookFood), id, source.GetInteractableId());
            if (error != Error.Ok)
            {
                GD.PrintErr("Campfire: Failed to request cooking via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles spawn directly
        {
            itemManager.DoCookFood(id, source.GetInteractableId());
        }
    }

    public override void ToggleHighlighted(bool highlighted)
    {
        // Annoying for loop since there are a bunch of meshes
        foreach (Node3D child in GetNode<Node3D>("SM_Campfire").GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                mesh.GetSurfaceOverrideMaterial(0).Set("emission_enabled", highlighted);
                if (highlighted)
                    mesh.GetSurfaceOverrideMaterial(0).Set("emission", Colors.Green);
            }
        }
    }
    
    public override void _PhysicsProcess(double delta)
    {
        if (playersInside != null && playersInside.Count > 0)
        {
            //GD.Print("players inside: " + playersInside.Count);
            foreach (var player in playersInside)
            {
                if (player is PlayerController playerController)
                {
                    playerController.ChangeCurrentHealth(-dmgPerTick);
                }

            }
        }
    }

    public void OnItemsAdded(Node3D body)
    {
        playersInside.Add(body);
    }

    public void OnItemsRemoved(Node3D body)
    {
        playersInside.Remove(body);
    }
}
