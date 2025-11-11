using Godot;
using System;

/// PlayerInteractable represents the pickup-able player that can also be fed.
public partial class PlayerInteractable : Interactable
{

    public override void _Ready()
    {
        base._Ready();

        if (itemManager == null) InitReferences();
        if(multiplayer.HasMultiplayerPeer() && !multiplayer.IsServer())
        {
            itemManager.RpcId(1, nameof(ItemManager.SendPlayerInteractableId), multiplayer.GetUniqueId());
        }
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
            PlayerController player = GetPlayerController();
            player.GlobalPosition = followTarget.GlobalPosition;
            player.GlobalRotation = GlobalRotation;

            //GD.Print(GlobalPosition);
            // If the player regains health, instantly drop this interactable.
            if (!player.IsDowned)
                ((PlayerController)Carrier).DropObject();
        }
    }

    public override bool CanBeCarried()
    {
        return false; // Player carrying will be a next sprint thing
        //return GetPlayerController().IsDowned;
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

    public override void ToggleHighlighted(bool highlighted)
    {
        Material bodyMaterial = GetPlayerController().GetNode<MeshInstance3D>("BodyMesh").MaterialOverride;
        Material headMaterial = GetPlayerController().GetNode<MeshInstance3D>("Head/HeadMesh").MaterialOverride;
        bodyMaterial.Set("emission_enabled", highlighted);
        headMaterial.Set("emission_enabled", highlighted);
        if (highlighted)
        {
            bodyMaterial.Set("emission", Colors.Green);
            headMaterial.Set("emission", Colors.Green);
        }
    }
}
