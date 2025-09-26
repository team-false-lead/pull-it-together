using Godot;
using System;

/// <summary>
/// A rope that can be carried but not used on anything.
public partial class RopeGrabPoint : Interactable
{
    [Export] public PackedScene ropeProxyScene;
    private AnimatableBody3D instantiatedPoxy = null;
    private Generic6DofJoint3D holdJoint = null;

    public override bool TryPickup(CharacterBody3D carrier)
    {
        if (Carrier != null || !CanBeCarried() || ropeProxyScene == null)
            return false;

        // attach a blank proxy scene for the player inventory
        var slot = carrier.GetNode<Node3D>("%InventorySlot1");
        if (slot == null) return false;
        instantiatedPoxy = ropeProxyScene.Instantiate<AnimatableBody3D>();
        slot.AddChild(instantiatedPoxy);

        instantiatedPoxy.Transform = Transform3D.Identity;
        instantiatedPoxy.SyncToPhysics = true;

        // pin this to the slot global position
        holdJoint = new Generic6DofJoint3D();
        slot.AddChild(holdJoint);
        holdJoint.NodeA = GetPath();
        holdJoint.NodeB = instantiatedPoxy.GetPath();

        holdJoint.GlobalTransform = new Transform3D(Basis.Identity, instantiatedPoxy.GlobalTransform.Origin);
        Scale = Vector3.One;

        Carrier = carrier;
        return true;
    }

    public override void Drop(CharacterBody3D carrier)
    {
        if (Carrier != carrier) return;

        instantiatedPoxy.GetParent().RemoveChild(instantiatedPoxy);
        holdJoint.GetParent().RemoveChild(holdJoint);
        instantiatedPoxy.QueueFree();
        holdJoint.QueueFree();
        instantiatedPoxy = null;
        holdJoint = null;

        Vector3 dropPosition = GetDropPosition(carrier);
        GlobalTransform = new Transform3D(GlobalTransform.Basis, dropPosition); // Set position in the world

        Carrier = null;
    }

    public override void TryUseSelf(CharacterBody3D user)
    {
        //logic for adding heave force to pull things
    }
}
