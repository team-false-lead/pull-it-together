using Godot;
using System;

/// Interactable is the base class for all objects that can be picked up, dropped, and interacted with by the player.
public abstract partial class Interactable : RigidBody3D
{
    [Export] public PackedScene SpawnOnUseScene; // Optional scene to spawn when used
    [Export] public float dropClearance = 0.5f; // per item drop clearance roughly based on size
    [Export] public string interactableId = "";
    public uint savedLayer, savedMask;
    public Vector3 savedScale;
    protected Node mapManager;
    protected bool multiplayerActive; 
    protected MultiplayerApi multiplayer => GetTree().GetMultiplayer();
    protected Node3D levelInteractablesNode;
    protected ItemManager itemManager;
    public virtual bool CanBeCarried() { return true; }
    public CharacterBody3D Carrier { get; set; } = null;

    private Node3D followTarget;
    private bool isFollowing;
    [Export] private float movementPenalty = 1.0f;

    public float MovementPenalty
    {
        get { return movementPenalty; }
    }

    //checks for multiplayer synchronizer on ready
    //if no id and not server, warn that waiting for id replication
    public override void _Ready()
    {
        var sync = GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer");
        if (sync == null)
        {
            GD.PrintErr("Interactable: MultiplayerSynchronizer not found on " + Name);
        }

        // debug warning
        //if (string.IsNullOrEmpty(interactableId) && !multiplayer.IsServer())
        //{
        //    GD.Print("Warning: Interactable " + Name + " waiting for ID replication.");
        //}

        // Save initial collision layers and masks
        savedLayer = CollisionLayer;
        savedMask = CollisionMask;
        savedScale = Scale;
    }

    // Start following a target inventory slot
    public void StartFollowingSlot(Node3D slot)
    {
        followTarget = slot;
        isFollowing = true;
    }

    // Stop following the inventory slot
    public void StopFollowingSlot()
    {
        followTarget = null;
        isFollowing = false;
    }

    // Follow the target slot if set, maintaining current rotation and scale
    public override void _PhysicsProcess(double delta)
    {
        if (isFollowing && followTarget != null)
        {
            //GlobalTransform = followTarget.GlobalTransform; // Snap to target position and rotation
            //Scale = savedScale; // Maintain original scale
            GlobalPosition = followTarget.GlobalPosition;
            GlobalRotation = followTarget.GlobalRotation;
        }
        //else reset to normal physics behavior
    }

    // Attempt to pick up the object
    public virtual bool TryPickup(CharacterBody3D carrier)
    {
        if (Carrier != null || !CanBeCarried()) return false;

        if (itemManager == null) InitReferences();
        var id = GetInteractableId(); //get unique id, default to name

        // Request pickup via RPC if not server, 
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestPickupItem), id); // Request pickup via RPC, 1 is server ID
            if (error != Error.Ok)
            {
                GD.PrintErr("Interactable: Failed to request item pickup via RPC. Error: " + error);
                return false; //failed pickup request
            }
        }
        else // Server or single-player handles pickup directly
        {
            itemManager.DoPickupItem(id, multiplayer.GetUniqueId());
        }

        //server handles pickup logic

        Carrier = carrier;
        return true;
    }

    // Drop the object from the carrier
    public virtual bool TryDrop(CharacterBody3D carrier)
    {
        if (Carrier != carrier) return false;

        if (itemManager == null) InitReferences();
        var id = GetInteractableId(); //get unique id, default to name

        // Calculate drop position in front of carrier
        //Vector3 dropPosition = GetDropPosition(carrier);

        // Request drop via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestDropItem), id);// Request drop via RPC, 1 is server ID
            if (error != Error.Ok)
            {
                GD.PrintErr("Interactable: Failed to request item drop via RPC. Error: " + error);
                return false; //failed drop request
            }
        }
        else // Server or single-player handles drop directly
        {
            itemManager.DoDropItem(id);
        }

        //server handles drop logic

        Carrier = null;
        return true;
    }

    // Initialize references to MapManager and ItemManager
    protected void InitReferences()
    {
        mapManager = GetTree().CurrentScene.GetNodeOrNull<Node>("%MapManager");
        if (mapManager != null)
        {
            levelInteractablesNode = mapManager.Get("interactables_node").As<Node3D>();
            itemManager = levelInteractablesNode as ItemManager;
            multiplayerActive = mapManager.Get("is_multiplayer_session").As<bool>() && multiplayer.HasMultiplayerPeer();
        }
        else
        {
            GD.PrintErr("Interactable: MapManager not found in scene tree!");
        }
    }

    // Get the unique ID of this interactable, defaulting to its node name if not set
    public string GetInteractableId()
    {
        if (string.IsNullOrEmpty(interactableId))
        {
            return Name;
        }
        return interactableId;
    }

    // Get the PlayerController parent of this interactable, if any
    public PlayerController GetPlayerController()
    {
        var pc = this.GetParent<PlayerController>();
        if (pc == null) return null;
        return pc;
    }

    //get drop position moved to item manager for multiplayer sync

    // By default, objects can use themselves
    public virtual bool CanUseSelf(CharacterBody3D user) { return true; }
    public virtual void TryUseSelf(CharacterBody3D user) { }

    // By default, Interactables can use on each other if the target accepts it
    public virtual bool CanUseOnInteractable(CharacterBody3D user, Interactable target)
    {
        return target.CanAcceptUseFrom(user, this);
    }
    public virtual void TryUseOnInteractable(CharacterBody3D user, Interactable target)
    {
        if (CanUseOnInteractable(user, target))
        {
            target.AcceptUseFrom(user, this);
        }
    }

    // By default, objects can use on entities other if the target accepts it
    public virtual bool CanUseOnEntity(CharacterBody3D user, Entity target)
    {
        return target.CanAcceptUseFrom(user, this);
    }
    public virtual void TryUseOnEntity(CharacterBody3D user, Entity target)
    {
        if (CanUseOnEntity(user, target))
        {
            target.AcceptUseFrom(user, this);
        }
    }

    // By default, objects do not accept being used on them
    public virtual bool CanAcceptUseFrom(CharacterBody3D user, Interactable source) { return false; }
    public virtual void AcceptUseFrom(CharacterBody3D user, Interactable source) { }

}
