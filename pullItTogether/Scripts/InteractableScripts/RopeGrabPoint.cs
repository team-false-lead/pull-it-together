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
    [Export] public VerletRopeSimulated simRope;
    [Export] public VerletJointSimulated simJoint;
    [Export] public float carrierTetherBuffer = 1.1f;
    [Export] public float carrierTetherStrength = 10f;

    private uint savedRopeLayer, savedRopeMask;

    private AnimatableBody3D proxy;
    private bool isHeld = false;

    public override void _Ready()
    {
        if (resetPoint != null)
        {
            GlobalTransform = resetPoint.GlobalTransform;
        }

        Freeze = true; // start frozen
        GravityScale = 0;

        joint.EndBody = this;
        joint.EndCustomLocation = this;
        simJoint.EndBody = this;
        simJoint.EndCustomLocation = this;
        CallDeferred(nameof(DeferredResetJoint));
        //GD.Print(joint.EndBody);

        // Save initial collision layers and masks
        savedLayer = CollisionLayer;
        savedMask = CollisionMask;

        if (!multiplayer.IsServer()) // temp until fix for rope jitter and update refresh
        {
            DisableRopeVisual();
        }
    }

    // if not held, reset to reset point
    public override void _PhysicsProcess(double delta)
    {
        // if held, follow the proxy in the inventory slot
        if (!isHeld && resetPoint != null)
        {
            GlobalTransform = resetPoint.GlobalTransform;
            Freeze = true;
            GravityScale = 0;
        }
    }

    //temp until fix for rope jitter and update refresh
    private void DisableRopeVisual()
    {
        if (rope != null)
        {
            rope.Visible = false;
            foreach (var segment in rope.GetChildren())
            {
                if (segment is MeshInstance3D mesh)
                {
                    mesh.Visible = false;
                }
            }
        }
    }

    // prepare to attach to proxy - disable collisions, hide, freeze, set rope collisions
    public void AttachToProxyPrep(Node3D proxyNode, CharacterBody3D carrier)
    {
        proxy = proxyNode as AnimatableBody3D;

        savedRopeLayer = rope.CollisionLayer;
        savedRopeMask = rope.CollisionMask;
        CollisionLayer = 0; // disable collisions
        CollisionMask = 0;

        Visible = false;
        Freeze = true;
        GravityScale = 0;


        PlayerController carrierScript = carrier as PlayerController;
        var slotPath = carrierScript.GetInventorySlot().GetPath();
        uint carrierLayerBit = carrierScript.collisionPusherAB.CollisionLayer;
        //rope.CollisionLayer = 0;
        rope.CollisionMask = savedRopeMask & ~carrierLayerBit & ~savedLayer; // drop player and self from rope collisions
        //carrierScript.SetTetherAnchor(joint.StartCustomLocation, rope.RopeLength, carrierTetherBuffer, carrierTetherStrength);
        carrierScript.RpcId(carrierScript.GetMultiplayerAuthority(), nameof(PlayerController.RequestSetTetherAnchorPath), simJoint.StartCustomLocation.GetPath(), simRope.RopeLength, carrierTetherBuffer, carrierTetherStrength);
        Rpc(nameof(ClientMoveSimRope), slotPath);

        Carrier = carrier;
        isHeld = true;
    }

    // attach to the proxy - set joint end body to proxy and reset joint
    public void AttachToProxy(AnimatableBody3D proxyNode)
    {
        proxy = proxyNode;

        if (joint != null && simJoint != null && proxy != null)
        {
            joint.EndBody = proxy;
            joint.EndCustomLocation = proxy;
            simJoint.EndBody = proxy;
            simJoint.EndCustomLocation = proxy;
            CallDeferred(nameof(DeferredResetJoint));
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ClientMoveSimRope(NodePath slotPath)
    {
        if (multiplayer.IsServer()) return; // server does not need to move, it already shoud know
        var end = GetNode<Node3D>(slotPath);
        if (end != null && simJoint != null)
        {
            //simJoint.EndBody = end;
            simJoint.EndCustomLocation = end;
            DeferredResetJoint();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ClientClearSimRope()
    {
        if (multiplayer.IsServer()) return; // server does not need to move, it already shoud know
        if (simJoint != null)
        {
            simJoint.EndBody = this;
            simJoint.EndCustomLocation = this;
            DeferredResetJoint();
        }
    }

    // detach from proxy - reset joint, restore collisions, show, freeze, reset position
    public void DetachFromProxy()
    {
        Rpc(nameof(ClientClearSimRope));
        joint.EndBody = this;
        joint.EndCustomLocation = this;
        simJoint.EndBody = this;
        simJoint.EndCustomLocation = this;
        CallDeferred(nameof(DeferredResetJoint));

        // Restore collision setting
        rope.CollisionMask = savedRopeMask;
        rope.CollisionLayer = savedRopeLayer;
        CollisionLayer = savedLayer;
        CollisionMask = savedMask;

        Visible = true;
        Freeze = true;
        GravityScale = 0;
        if (resetPoint != null)
        {
            GlobalTransform = resetPoint.GlobalTransform;
        }

        PlayerController carrierScript = Carrier as PlayerController;
        //carrierScript.RemoveTetherAnchor();
        carrierScript.RpcId(carrierScript.GetMultiplayerAuthority(), nameof(PlayerController.RequestClearTether));

        isHeld = false;
        Carrier = null;
    }

    // need to reset the joint after changing any settings
    private void DeferredResetJoint()
    {
        if (joint != null)
        {
            joint.ResetJoint();
        }
        if (simJoint != null)
        {
            simJoint.ResetJoint();
        }
    }

    // override pickup to attach a proxy to the player inventory
    public override bool TryPickup(CharacterBody3D carrier)
    {
        if (Carrier != null || !CanBeCarried() || ropeProxyScene == null)
            return false;

        if (itemManager == null) InitReferences();
        var id = GetInteractableId();

        // Request hold via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestHoldRope), id);
            if (error != Error.Ok)
            {
                GD.PrintErr("RopeGrabPoint: Failed to request hold rope via RPC. Error: " + error);
                return false;
            }
        }
        else // Server or single-player handles hold directly
        {
            var execute = itemManager.DoHoldRope(id, multiplayer.GetUniqueId());
        }

        return true;
    }

    // override drop to remove proxy and re-enable collisions
    public override bool TryDrop(CharacterBody3D carrier)
    {
        //if (Carrier != carrier) return false; // allow drop even if not carrier, in case of desync

        if (itemManager == null) InitReferences();
        var id = GetInteractableId();

        // Request drop via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestReleaseRope), id);
            if (error != Error.Ok)
            {
                GD.PrintErr("RopeGrabPoint: Failed to request rope release via RPC. Error: " + error);
                return false;
            }
        }
        else // Server or single-player handles drop directly
        {
            itemManager.DoReleaseRope(id);
        }

        return true;
    }

    public override void TryUseSelf(CharacterBody3D user)
    {
        return; //logic for adding heave force to pull things
    }
}
