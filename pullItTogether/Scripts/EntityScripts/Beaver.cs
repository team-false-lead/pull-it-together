using Godot;
using System;
using System.Reflection.Metadata.Ecma335;

public partial class Beaver : Entity
{
    [Export] public Label3D label;
    [Export] public ItemDetector plankDetector;
    [Export] public ItemDetector wagonDetector;
    [Export] public Node BT;
    [Export] public bool hasPlank = false;
    [Export] public bool plankInRange = false;
    public Interactable plankTarget = null;
    [Export] public bool wagonInRange = false;
    public Wagon wagonRef = null;
    [Export] public bool hasWheelTarget = false;
    public Wheel wheelTarget = null;
    [Export] public bool wheelBroken = false;
    [Export] public float wheelDamage = 25f;
    [Export] public float movementSpeed = 5f;
    [Export] public Vector3 targetPosition;
    [Export] public Node3D inventorySlot;
    private bool spawnedWheel = false;

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


        if (plankDetector != null)
        {
            plankDetector.FilteredBodyEntered += OnItemsAdded;
            plankDetector.FilteredBodyExited += OnItemsRemoved;
        }
        if (wagonDetector != null)
        {
            wagonDetector.FilteredBodyEntered += OnItemsAdded;
            wagonDetector.FilteredBodyExited += OnItemsRemoved;
        }
    }

    // By default, entities do not accept being used on them
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("plank") && !hasPlank)
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
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestGiveBeaverPlank), id, source.GetInteractableId());
            if (error != Error.Ok)
            {
                GD.PrintErr("Beaver: Failed to request use via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles spawn directly
        {
            itemManager.DoGiveBeaverPlank(id, source.GetInteractableId());
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (multiplayerActive && !multiplayer.IsServer())
        {
            return; // Clients do not run AI logic
        }

        base._PhysicsProcess(delta);
        //GD.Print("beaver id: " + GetEntityId());
        //GD.Print("wagonInRange: " + wagonInRange + ", wagonRef: " + wagonRef + ", wheelTarget: " + wheelTarget);
        if (wagonInRange && wagonRef == null)
        {
            //wagonRef = wagonDetector.itemsInside[0] as Wagon;
            foreach (var item in wagonDetector.itemsInside)
            {
                if (item is Wagon wagon)
                {
                    wagonRef = wagon;
                    break;
                }
            }
        }
        if (wagonRef != null && wheelTarget == null)
        {
            wheelTarget = GetFirstWorkingWheel();
        }
        if (wheelTarget != null)
        {
            if (plankInRange || hasPlank)
            {
                hasWheelTarget = false;
            }
            else
            {
                //GD.Print("wheelHealth: " + wheelTarget.currentHealth);
                hasWheelTarget = true;
                targetPosition = wheelTarget.GlobalPosition;
                if (wheelTarget.currentHealth <= 0)
                {
                    wheelBroken = true;
                    if (!spawnedWheel)
                    {
                        if (itemManager == null) InitReferences();
                        var id = GetEntityId(); //get unique id, default to name

                        // request spawn wheel
                        if (multiplayerActive && !multiplayer.IsServer())
                        {
                            var error = itemManager.RpcId(1, nameof(ItemManager.RequestBeaverSpawnWheel), id);
                            if (error != Error.Ok)
                            {
                                GD.PrintErr("Beaver: Failed to request use via RPC. Error: " + error);
                            }
                            hasPlank = true;
                            inventorySlot.Rotation += new Vector3(Mathf.DegToRad(-90f), 0, Mathf.DegToRad(-90f));
                            inventorySlot.Position = new Vector3(0, -1.25f, 0);
                        }
                        else // Server or single-player handles spawn directly
                        {
                            itemManager.DoBeaverSpawnWheel(id);
                            hasPlank = true;
                            inventorySlot.Rotation += new Vector3(Mathf.DegToRad(-90f), 0, Mathf.DegToRad(-90f));
                            inventorySlot.Position = new Vector3(0, -1.25f, 0);
                        }
                        spawnedWheel = true;
                    }
                }
                else
                {
                    wheelBroken = false;
                }
            } 
        }
    }

    private void OnItemsAdded(Node3D item)
    {
        if (item.IsInGroup("wagon"))
        {
            wagonInRange = true;
            //label.Text = "Wagon in range";
        }
        if (item.IsInGroup("plank"))
        {
            if (item is Interactable plankInteractable)
            {
                if (plankInteractable.Carrier == null)
                {
                    plankInRange = true;
                    //label.Text = "Plank in range";
                    targetPosition = plankInteractable.GlobalPosition;
                    plankTarget = plankInteractable;
                }
                else
                {
                    plankInRange = false;
                    plankTarget = null;
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

    private Wheel GetFirstWorkingWheel()
    {
        //foreach (var wheel in wagonRef.wheels)
        //{
        //    if (wheel.currentHealth > 0)
        //    {
        //        return wheel;
        //    }
        //}
        foreach (var item in wagonDetector.itemsInside)
        {
            if (item is Wheel wheel && wheel.currentHealth > 0)
            {
                return wheel;
            }
        }
        return null; // All wheels are broken or no wheels found
    }

    public bool PickupPlank()
    {
        if (plankTarget != null && !hasPlank)
        {
            if (itemManager == null) InitReferences();
            var id = GetEntityId(); //get unique id, default to name

            // request damage wheel via RPC if not server
            if (multiplayerActive && !multiplayer.IsServer())
            {
                var error = itemManager.RpcId(1, nameof(ItemManager.RequestBeaverPickupItem), id, plankTarget.GetInteractableId());
                if (error != Error.Ok)
                {
                    GD.PrintErr("Beaver: Failed to request use via RPC. Error: " + error);
                    return false;
                }
                hasPlank = true;
                return hasPlank;
            }
            else // Server or single-player handles spawn directly
            {
                itemManager.DoBeaverPickupItem(id, plankTarget.GetInteractableId());
                hasPlank = true;
                return hasPlank;
            }
        }
        return hasPlank;
    }

    public bool AttackWheel()
    {
        ApplyCentralImpulse(new Vector3(0, 2.5f, 0)); // small jump when attacking
        wheelTarget.currentHealth -= wheelDamage;
        return true;
        //if (hasWheelTarget && wheelTarget != null && !wheelBroken)
        {

            //if (itemManager == null) InitReferences();
            //var id = GetEntityId(); //get unique id, default to name
            //
            //// request damage wheel via RPC if not server
            //if (multiplayerActive && !multiplayer.IsServer())
            //{
            //    //var error = itemManager.RpcId(1, nameof(ItemManager.RequestCookFood), id, source.GetInteractableId());
            //    var error = Error.Ok; // Placeholder for actual RPC call
            //    if (error != Error.Ok)
            //    {
            //        GD.PrintErr("Beaver: Failed to request use via RPC. Error: " + error);
            //        return false;
            //    }
            //    return true;
            //}
            //else // Server or single-player handles spawn directly
            //{
            //    //itemManager.DoCookFood(id, source.GetInteractableId());
            //    return true;
            //}
        }
        //return false;
    }
    
    public bool AttackPlayer()
    {
        return false;
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