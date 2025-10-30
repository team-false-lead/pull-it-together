using Godot;
using System;

public partial class Beaver : Entity
{
    [Export] public Label3D label;
    [Export] public ItemDetector detector;
    public bool wagonInRange = false;
    public bool plankInRange = false;
    public bool hasPlank = false;

    public override void _Ready()
    {
        base._Ready();

        if (detector != null)
        {
            detector.BodyEntered += OnItemsAdded;
            detector.BodyExited += OnItemsRemoved;
        }
    }

    // By default, entities do not accept being used on them
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

        // Request spawn via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            //var error = itemManager.RpcId(1, nameof(ItemManager.RequestCookFood), id, source.GetInteractableId());
            var error = Error.Ok; // Placeholder for actual RPC call
            if (error != Error.Ok)
            {
                GD.PrintErr("Beaver: Failed to request use via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles spawn directly
        {
            //itemManager.DoCookFood(id, source.GetInteractableId());
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);


    }

    private void OnItemsAdded(Node3D item)
    {
        if (item.IsInGroup("wagon"))
        {
            wagonInRange = true;
            label.Text = "Wagon in range";
        }
        if (item.IsInGroup("plank"))
        {
            if (item is Interactable plankInteractable)
            {
                if (plankInteractable.Carrier == null)
                {
                    plankInRange = true;
                    label.Text = "Plank in range";
                }
                else
                {
                    plankInRange = false;
                }
            }
            
        }
    }

    private void OnItemsRemoved(Node3D item)
    {
        if (item.IsInGroup("wagon"))
        {
            wagonInRange = false;
        }
        if (item.IsInGroup("plank"))
        {
            plankInRange = false;
        }
    }
}