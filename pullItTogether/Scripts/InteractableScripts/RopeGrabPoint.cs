using Godot;
using System;
using System.Diagnostics;
using VerletRope4.Physics;
using VerletRope4.Physics.Joints;

/// <summary>
/// A rope that can be carried but not used on anything.
public partial class RopeGrabPoint : Interactable
{
    [Export] public PackedScene ropeProxyScene;
    [Export] public Node3D resetPoint;
    [Export] public VerletRopeRigid rope;
    [Export] public VerletJointRigid joint;
    [Export] public float carrierTetherBuffer = 1.2f;
    [Export] public float carrierTetherStrength = 10f;

    private uint savedRopeLayer, savedRopeMask;

    private AnimatableBody3D proxy;
    private bool isHeld = false;

    public override void _PhysicsProcess(double delta)
    {
        if (isHeld && IsInstanceValid(proxy))
        {
            Freeze = true;
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            GlobalTransform = proxy.GlobalTransform;
        }
        else if (Carrier == null && resetPoint != null)
        {
            Freeze = true;
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            GlobalTransform = resetPoint.GlobalTransform;
        }
    }

    public override bool TryPickup(CharacterBody3D carrier)
    {
        if (Carrier != null || !CanBeCarried() || ropeProxyScene == null)
            return false;

        // attach a blank proxy scene for the player inventory
        var slot = carrier.GetNode<Node3D>("%InventorySlot1");
        if (slot == null) return false;

        proxy = ropeProxyScene.Instantiate<AnimatableBody3D>();
        slot.AddChild(proxy);
        proxy.Transform = Transform3D.Identity;
        proxy.SyncToPhysics = true;

        joint.EndBody = proxy;
        joint.EndCustomLocation = proxy;
        joint.ResetJoint();

        savedMask = CollisionMask;
        savedLayer = CollisionLayer;
        CollisionLayer = 0; // Disable collisions while carried
        CollisionMask = 0; // Disable collisions while carried

        PlayerController carrierScript = carrier as PlayerController;

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

    public override void Drop(CharacterBody3D carrier)
    {
        if (Carrier != carrier) return;

        joint.EndBody = this;
        joint.EndCustomLocation = this;
        joint.ResetJoint();

        if (IsInstanceValid(proxy))
        {
            proxy.GetParent().RemoveChild(proxy);
            proxy.QueueFree();
        }
        proxy = null;

        if (resetPoint != null)
            GlobalTransform = resetPoint.GlobalTransform;

        CollisionMask = savedMask; // Restore collision settings
        CollisionLayer = savedLayer; // Restore collision settings

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
