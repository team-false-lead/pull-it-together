using Godot;
using System;

/// PlayerInteractable represents the pickup-able player that can also be fed.
public partial class PlayerInteractable : Interactable
{
    [Export] public PlayerController ownerPlayerController;
    [Export] public long ownerPeerId = 0;
    private bool hasSetOwnerPlayerController = false;

    public override void _Ready()
    {
        base._Ready();

        if (itemManager == null) InitReferences();
        //if(multiplayer.HasMultiplayerPeer() && !multiplayer.IsServer())
        //{
        //    itemManager.RpcId(1, nameof(ItemManager.SendPlayerInteractableId), multiplayer.GetUniqueId());
        //}
        hasSetOwnerPlayerController = SetOwnerPlayerController();
    }

    private bool SetOwnerPlayerController()
    {
        if (ownerPlayerController != null) return true;

        if (!multiplayer.HasMultiplayerPeer())
        {
            ownerPlayerController = GetTree().GetFirstNodeInGroup("players") as PlayerController;
            return true;
        }

        if (ownerPeerId == 0) { GD.Print("PlayerInteractable: no owner peer id yet"); return false; }
        foreach (var player in GetTree().GetNodesInGroup("players"))
        {
            if (player is PlayerController pc && pc.GetMultiplayerAuthority() == ownerPeerId)
            {
                ownerPlayerController = pc;
                pc.playerInteractable = this;
                GD.Print("PlayerInteractable: found owner player controller for peer id " + ownerPeerId);
                return true;
            }
        }
        return false;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SetOwnerPeerId(long peerId)
    {
        ownerPeerId = peerId;
        SetOwnerPlayerController();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed("drop")) // Q  // actually not drop but print dictionary contents for debugging
		{
			//if (multiplayer.HasMultiplayerPeer() && isMultiplayerSession && !multiplayer.IsServer()) return;
			GD.Print(GetPlayerController().Name);
			GD.Print("my id = " + interactableId);
		}

        base._PhysicsProcess(delta);
        //if (GetPlayerController().IsLocalControlled())
        //    GD.Print(GetPlayerController().Name + " " + followTarget);
        //GD.Print(interactableId);
        if (isFollowing && followTarget != null)
        {
            // When being carried, sync the player's position to their interactable's position.
            //PlayerController player = GetPlayerController();
            //ownerPlayerController.GlobalPosition = GlobalPosition;
            //ownerPlayerController.GlobalRotation = GlobalRotation;
            //GlobalPosition = followTarget.GlobalPosition;
            //GlobalRotation = followTarget.GlobalRotation;

            //GD.Print(GlobalPosition);
            // If the player regains health, instantly drop this interactable.
            if (!ownerPlayerController.IsDowned)
                ((PlayerController)Carrier).DropObject();
        }
        if (!hasSetOwnerPlayerController)
        {
            hasSetOwnerPlayerController = SetOwnerPlayerController();
        }

    }

    public override bool CanBeCarried()
    {
        //return false; // Player carrying will be a next sprint thing
        return ownerPlayerController.IsDowned;
    }

    // By default, objects do not accept being used on them
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("food"))
        {
            return true;
        }
        return false;
    }

    public override void AcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        GD.Print("PlayerInteractable: AcceptUseFrom called with source " + source.Name);
        if (itemManager == null) InitReferences();
        var sourceId = source.GetInteractableId(); //get unique id, default to name
        var thisPC = GetPlayerController();
        string targetPeerId = thisPC.GetMultiplayerAuthority().ToString();

        // Request spawn via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            GD.Print("PlayerInteractable: Sending RPC to feed target " + thisPC.Name);
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestFeedTarget), sourceId, targetPeerId);
            GD.Print("PlayerInteractable: RPC sent" + error);
            if (error != Error.Ok)
            {
                GD.PrintErr("PlayerInteractable: Failed to request feeding via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles spawn directly
        {
            GD.Print("PlayerInteractable: Directly feeding target " + thisPC.Name);
            itemManager.DoFeedTarget(sourceId, targetPeerId);
        }
    }

    public override bool CanUseSelf(CharacterBody3D user) { return false; }

    // By default, Interactables can use on each other if the target accepts it
    public override bool CanUseOnInteractable(CharacterBody3D user, Interactable target) { return false; }

    // By default, objects can use on entities other if the target accepts it
    public override bool CanUseOnEntity(CharacterBody3D user, Entity target) { return false; }

    public PlayerController GetPlayerController()
    {
        return ownerPlayerController;
    }
}
