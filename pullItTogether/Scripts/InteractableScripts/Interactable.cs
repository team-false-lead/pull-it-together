using Godot;
using System;

/// Interactable is the base class for all objects that can be picked up, dropped, and interacted with by the player.
public abstract partial class Interactable : RigidBody3D
{
    [Export]public string interactableId { get; set; } = "";
    public string GetInteractableId() => string.IsNullOrEmpty(interactableId) ? Name : interactableId;
    public uint savedLayer, savedMask;
    protected Node mapManager;
    protected bool multiplayerActive; 
    protected MultiplayerApi multiplayer => GetTree().GetMultiplayer();
    protected Node3D levelInteractablesNode;
    protected ItemManager itemManager;
    public virtual bool CanBeCarried() { return true; }
    public CharacterBody3D Carrier { get; set; } = null;

    // Attempt to pick up the object
    public virtual bool TryPickup(CharacterBody3D carrier)
    {
        if (Carrier != null || !CanBeCarried()) return false;

        if (itemManager == null) InitReferences();
        var id = GetInteractableId();
        
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestPickupItem), id);
            GD.Print("Rpc error: " + error);
            if (error != Error.Ok)
            {
                GD.PrintErr("Interactable: Failed to request item pickup via RPC. Error: " + error);
                return false;
            }
        }
        else
        {
            itemManager.DoPickupItem(id, multiplayer.GetUniqueId());
        }
        

        //host handles held logic

        Carrier = carrier;
        return true;
    }

    // Drop the object from the carrier
    public virtual bool TryDrop(CharacterBody3D carrier)
    {
        if (Carrier != carrier) return false;

        Vector3 dropPosition = GetDropPosition(carrier);

        if (itemManager == null) InitReferences();

        var id = GetInteractableId();
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestDropItem), id, dropPosition);
            if (error != Error.Ok)
            {
                GD.PrintErr("Interactable: Failed to request item drop via RPC. Error: " + error);
                return false;
            }
        }
        else
        {
            itemManager.DoDropItem(id, dropPosition);
        }

        //host handles drop logic

        Carrier = null;
        return true;
    }

    // Find the world interactables node to reattach to when dropped
    protected void InitReferences()
    {
        mapManager = GetTree().CurrentScene.GetNodeOrNull<Node>("%MapManager");
        if (mapManager != null)
        {
            //levelInstance = mapManager.Get("level_instance").As<Node3D>();
            levelInteractablesNode = mapManager.Get("interactables_node").As<Node3D>();
            itemManager = levelInteractablesNode as ItemManager;
            multiplayerActive = itemManager != null && mapManager.Get("is_multiplayer_session").As<bool>() && GetTree().GetMultiplayer().HasMultiplayerPeer();
        }
        else
        {
            GD.PrintErr("Interactable: MapManager not found in scene tree!");
        }
    }

    // Calculate a safe drop position in front of the carrier
    public Vector3 GetDropPosition(CharacterBody3D carrier)
    {
        // screen center
        var vp = carrier.GetViewport();
        Vector2 center = vp.GetVisibleRect().Size * 0.5f;

        //raycast from camera to drop in front of carrier
        PlayerController carrierScript = carrier as PlayerController;
        Vector3 origin = carrierScript.camera.ProjectRayOrigin(center);
        Vector3 dir = carrierScript.camera.ProjectRayNormal(center);
        Vector3 to = origin + dir * carrierScript.interactRange;

        // Raycast to find a safe drop position
        var state = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(origin, to);
        query.CollisionMask = savedMask; // Use saved mask to avoid dropping inside other objects
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid(), carrier.GetRid() }; // ignore self and carrier
        var hit = state.IntersectRay(query);
        Vector3 dropPosition = hit.Count > 0 ? (Vector3)hit["position"] : to; // Drop at hit point or max range
        dropPosition += Vector3.Up * 0.25f;
        
        return dropPosition;
    }

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

    //template Entity Logic for later 
    //// By default, objects can use on entities other if the target accepts it
    //public virtual bool CanUseOnEntity(CharacterBody3D user, Entity target)
    //{
    //    return target.CanAcceptUseFrom(user, this);
    //}
    //public virtual void TryUseOnEntity(CharacterBody3D user, Entity target)
    //{
    //    if (CanUseOnEntity(user, target))
    //    {
    //        target.AcceptUseFrom(user, this);
    //    }
    //}

    // By default, objects do not accept being used on them
    public virtual bool CanAcceptUseFrom(CharacterBody3D user, Interactable source) { return false; }
    public virtual void AcceptUseFrom(CharacterBody3D user, Interactable source) { }

}
