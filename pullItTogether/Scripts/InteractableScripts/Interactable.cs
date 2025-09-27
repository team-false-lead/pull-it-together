using Godot;
using System;

/// Interactable is the base class for all objects that can be picked up, dropped, and interacted with by the player.
public abstract partial class Interactable : RigidBody3D
{
    protected uint savedLayer, savedMask;
    protected Node mapManager;
    protected Node3D worldInteractables;
    public virtual bool CanBeCarried() { return true; }
    public CharacterBody3D Carrier { get; set; } = null;

    // Attempt to pick up the object
    public virtual bool TryPickup(CharacterBody3D carrier)
    {
        if (Carrier != null || !CanBeCarried()) return false;

        // Disable physics
        Freeze = true;
        GravityScale = 0;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;

        // Attach to the carrier's inventory slot
        var slot = carrier.GetNode<Node3D>("%InventorySlot1");
        GetParent<Node3D>().RemoveChild(this); // Remove from current parent
        slot.AddChild(this); // Add to the carrier

        TopLevel = false; // Make non-top-level to inherit carrier's transform
        Position = Vector3.Zero;
        Rotation = Vector3.Zero;
        Scale = Vector3.One * (1 / GetParent<Node3D>().Scale.X); // Reset scale relative to carrier

        // Save and disable collisions
        savedMask = CollisionMask;
        savedLayer = CollisionLayer;
        CollisionLayer = 0;
        CollisionMask = 0;

        Carrier = carrier;
        return true;
    }

    // Drop the object from the carrier
    public virtual void Drop(CharacterBody3D carrier)
    {
        if (Carrier != carrier) return;

        GetParent<Node3D>().RemoveChild(this); // Remove from current parent
        // Reattach to world interactables node
        if (worldInteractables == null) worldInteractables = InitWorldInteractablesNode(carrier);
        worldInteractables.AddChild(this);

        // Place in front of the carrier
        Vector3 dropPosition = GetDropPosition(carrier);
        GlobalTransform = new Transform3D(GlobalTransform.Basis, dropPosition); // Set position in the world

        // Re-enable physics and collisions
        TopLevel = true; // Make top-level to have independent transform
        Freeze = false; // Re-enable physics interactions
        GravityScale = 1;
        CollisionMask = savedMask;
        CollisionLayer = savedLayer;

        Carrier = null;
    }

    // Find the world interactables node to reattach to when dropped
    protected Node3D InitWorldInteractablesNode(CharacterBody3D carrier)
    {
        mapManager = carrier.GetTree().CurrentScene.GetNodeOrNull<Node>("%MapManager");
        return mapManager != null ? mapManager.Get("interactables_node").As<Node3D>() : GetTree().CurrentScene as Node3D;
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
