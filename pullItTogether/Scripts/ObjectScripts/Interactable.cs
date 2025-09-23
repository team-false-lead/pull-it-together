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
        Carrier = carrier;
        GetParent<Node3D>().RemoveChild(this); // Remove from current parent
        carrier.GetNode<Node3D>("Inventory").AddChild(this); // Add to the carrier
        Position = Vector3.Zero; // Reset position relative to carrier
        Rotation = Vector3.Zero; // Reset rotation relative to carrier
        Scale = Vector3.One; // Reset scale relative to carrier

        LinearVelocity = Vector3.Zero; // Stop any existing motion
        AngularVelocity = Vector3.Zero;
        GravityScale = 0; // Disable gravity while carried
        Freeze = true; // Prevent physics interactions while carried
        savedMask = CollisionMask;
        CollisionMask = 0; // Disable collisions while carried
        return true;
    }

    public virtual void Drop(CharacterBody3D carrier)
    {
        if (Carrier != carrier) return;
        carrier.GetNode<Node3D>("Inventory").RemoveChild(this); // Remove from current parent
        Carrier.GetParent<Node3D>().AddChild(this); // Add to the world
        Carrier = null;

        GravityScale = 1; // Re-enable gravity
        Freeze = false; // Re-enable physics interactions
        CollisionMask = savedMask; // Restore collision settings
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
