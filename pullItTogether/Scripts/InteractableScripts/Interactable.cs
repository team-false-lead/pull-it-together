using Godot;
using System;

public abstract partial class Interactable : RigidBody3D
{
    private uint savedMask;
    public virtual bool CanBeCarried() { return true; }
    public CharacterBody3D Carrier { get; private set; } = null;

    public virtual bool TryPickup(CharacterBody3D carrier)
    {
        if (Carrier != null || !CanBeCarried()) return false;
        
        // Attach to the carrier's inventory slot
        var slot = carrier.GetNode<Node3D>("%InventorySlot1");
        
        Freeze = true;
        GravityScale = 0;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GetParent<Node3D>().RemoveChild(this); // Remove from current parent
        slot.AddChild(this); // Add to the carrier

        TopLevel = false; // Make non-top-level to inherit carrier's transform
        //Transform = Transform3D.Identity; // Reset transform relative to carrier
        Position = Vector3.Zero;
        Scale = Vector3.One * (1/ GetParent<Node3D>().Scale.X);

        savedMask = CollisionMask;
        CollisionMask = 0; // Disable collisions while carried

        Carrier = carrier;
        return true;
    }

    public virtual void Drop(CharacterBody3D carrier, Vector3 dropPosition)
    {
        if (Carrier != carrier) return;

        GetParent<Node3D>().RemoveChild(this); // Remove from current parent
        carrier.GetParent<Node3D>().GetParent<Node3D>().GetNode<Node3D>("%Items").AddChild(this); // Add to the world items node
        GlobalTransform = new Transform3D(GlobalTransform.Basis, dropPosition); // Set position in the world

        TopLevel = true; // Make top-level to have independent transform
        Freeze = false; // Re-enable physics interactions
        GravityScale = 1; // Re-enable gravity
        CollisionMask = savedMask; // Restore collision settings

        Carrier = null;
    }
    
    // By default, objects can use themselves
    public virtual bool CanUseSelf(CharacterBody3D user) { return true; }
    public virtual void TryUseSelf(CharacterBody3D user) { }

    // By default, objects can use on each other if the target accepts it
    public virtual bool CanUseOn(CharacterBody3D user, Interactable target)
    {
        return target.CanAcceptUseFrom(user, this);
    }
    public virtual void TryUseOn(CharacterBody3D user, Interactable target)
    {
        if (CanUseOn(user, target))
        {
            target.AcceptUseFrom(user, this);
        }
    }
    
    // By default, objects do not accept being used on them
    public virtual bool CanAcceptUseFrom(CharacterBody3D user, Interactable source) { return false; }
    public virtual void AcceptUseFrom(CharacterBody3D user, Interactable source) { }

}
