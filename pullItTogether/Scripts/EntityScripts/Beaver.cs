using Godot;
using System;
using System.Reflection.Metadata.Ecma335;

public partial class Beaver : Animal
{
    //[Export] public Label3D label;
    [Export] public ItemDetector plankDetector;
    [Export] public ItemDetector wagonDetector;
    //[Export] public Node BT;
    //[Export] public bool hasItem = false;
    [Export] public bool plankInRange = false;
    //public Interactable itemTarget = null;
    [Export] public bool wagonInRange = false;
    public Wagon wagonRef = null;
    [Export] public bool hasWheelTarget = false;
    public Wheel wheelTarget = null;
    [Export] public bool wheelBroken = false;
    [Export] public float wheelDamage = 25f;
    //[Export] public float movementSpeed = 5f;
    //[Export] public Vector3 targetPosition;
    //[Export] public Node3D inventorySlot;
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
        if (source.IsInGroup("plank") && !hasItem)
        {
            return true;
        }
        return false;
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
            if (plankInRange || hasItem)
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
                            var error = itemManager.RpcId(1, nameof(ItemManager.RequestAnimalSpawnItem), id);
                            if (error != Error.Ok)
                            {
                                GD.PrintErr("Beaver: Failed to request use via RPC. Error: " + error);
                            }
                            hasItem = true;
                            inventorySlot.Rotation += new Vector3(Mathf.DegToRad(-90f), 0, Mathf.DegToRad(-90f));
                            inventorySlot.Position = new Vector3(0, -1.25f, 0);
                        }
                        else // Server or single-player handles spawn directly
                        {
                            itemManager.DoAnimalSpawnItem(id);
                            hasItem = true;
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
                    itemTarget = plankInteractable;
                }
                else
                {
                    plankInRange = false;
                    itemTarget = null;
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
        foreach (var wheel in wagonRef.wheels)
        {
            if (wheel.currentHealth > 0)
            {
                return wheel;
            }
        }
        return null; // All wheels are broken or no wheels found
    }

    //public bool PickupItem()
    //{
    //    if (plankTarget != null && !hasPlank)
    //    {
    //        if (itemManager == null) InitReferences();
    //        var id = GetEntityId(); //get unique id, default to name
//
    //        // request damage wheel via RPC if not server
    //        if (multiplayerActive && !multiplayer.IsServer())
    //        {
    //            var error = itemManager.RpcId(1, nameof(ItemManager.RequestBeaverPickupItem), id, plankTarget.GetInteractableId());
    //            if (error != Error.Ok)
    //            {
    //                GD.PrintErr("Beaver: Failed to request use via RPC. Error: " + error);
    //                return false;
    //            }
    //            hasPlank = true;
    //            return hasPlank;
    //        }
    //        else // Server or single-player handles spawn directly
    //        {
    //            itemManager.DoBeaverPickupItem(id, plankTarget.GetInteractableId());
    //            hasPlank = true;
    //            return hasPlank;
    //        }
    //    }
    //    return hasPlank;
    //}

    public bool AttackWheel()
    {
        //ApplyCentralImpulse(new Vector3(0, 2.5f, 0)); // small jump when attacking // now in BT task

        if (hasWheelTarget && wheelTarget != null && !wheelBroken)
        {
            itemManager.DoDamageWheel(wheelTarget.GetEntityId(), wheelDamage);
            return true;
        }
        return false;
    }
    
    public bool AttackPlayer()
    {
        return false;
    }
}