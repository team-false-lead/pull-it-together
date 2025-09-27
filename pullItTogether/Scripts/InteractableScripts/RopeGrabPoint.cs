using Godot;
using System;
using System.Diagnostics;
using VerletRope4.Physics;
using VerletRope4.Physics.Joints;

/// <summary>
/// A rope grab point that can be picked up and used to tether the player to a point
public partial class RopeGrabPoint : Interactable
{
    [Export] public PackedScene ropeProxyScene;
    [Export] public Node3D resetPoint;
    [Export] public VerletRopeRigid rope;
    [Export] public VerletJointRigid joint;
    [Export] public float carrierTetherBuffer = 1.1f;
    [Export] public float carrierTetherStrength = 10f;

    private uint savedRopeLayer, savedRopeMask;

    private AnimatableBody3D proxy;
    private bool isHeld = false;

    public override void _PhysicsProcess(double delta)
    {
        // if held, follow the proxy in the inventory slot
        if (isHeld && IsInstanceValid(proxy))
        {
            Freeze = true;
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            GlobalTransform = proxy.GlobalTransform;
        }
        // if not held and has a reset point, go back to it
        else if (Carrier == null && resetPoint != null)
        {
            Freeze = true;
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            GlobalTransform = resetPoint.GlobalTransform;
        }
    }

    // override pickup to attach a proxy to the player inventory
    public override bool TryPickup(CharacterBody3D carrier)
    {
        if (Carrier != null || !CanBeCarried() || ropeProxyScene == null)
            return false;

        var slot = carrier.GetNode<Node3D>("%InventorySlot1");
        if (slot == null) return false;

        // create proxy and attach to player slot
        proxy = ropeProxyScene.Instantiate<AnimatableBody3D>();
        slot.AddChild(proxy);
        proxy.Transform = Transform3D.Identity;
        proxy.SyncToPhysics = true;

        // attach rope joint to proxy
        joint.EndBody = proxy;
        joint.EndCustomLocation = proxy;
        joint.ResetJoint();

        // disable collisions between player and rope/grab point while carrying
        savedMask = CollisionMask;
        savedLayer = CollisionLayer;
        CollisionLayer = 0;
        CollisionMask = 0;

        PlayerController carrierScript = carrier as PlayerController;

        // disable collisions between player and rope while carrying
        savedRopeLayer = rope.CollisionLayer;
        savedRopeMask = rope.CollisionMask;
        uint carrierLayerBit = carrierScript.collisionPusher.CollisionLayer;
        rope.CollisionLayer = 0;
        rope.CollisionMask = savedRopeMask & ~carrierLayerBit; // drop player collision but keep world and object

        isHeld = true;
        Carrier = carrier;
        carrierScript.SetTetherAnchor(joint.StartCustomLocation, rope.RopeLength, carrierTetherBuffer, carrierTetherStrength);
        return true;
    }

    // override drop to remove proxy and re-enable collisions
    public override void Drop(CharacterBody3D carrier)
    {
        if (Carrier != carrier) return;

        // detach rope joint from proxy and reattach to self
        joint.EndBody = this;
        joint.EndCustomLocation = this;
        joint.ResetJoint();

        // remove and free proxy
        if (IsInstanceValid(proxy))
        {
            proxy.GetParent().RemoveChild(proxy);
            proxy.QueueFree();
        }
        proxy = null;

        if (resetPoint != null)
            GlobalTransform = resetPoint.GlobalTransform;

        // Restore collision settings
        CollisionMask = savedMask; 
        CollisionLayer = savedLayer; 
        rope.CollisionMask = savedRopeMask;
        rope.CollisionLayer = savedRopeLayer;

        isHeld = false;
        PlayerController carrierScript = carrier as PlayerController;
        carrierScript.RemoveTetherAnchor();
        Carrier = null;
    }

    public override void TryUseSelf(CharacterBody3D user)
    {
        return; //logic for adding heave force to pull things
    }
}
