using Godot;
using System;

/// PlayerInteractable represents the (later) pickupable player that can be fed.
public partial class PlayerInteractable : Interactable
{
    public override bool CanBeCarried() { return false; }

    // By default, objects do not accept being used on them
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("Food"))
        {
            return true;
        }
        return false;
    }

    public override void AcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        GD.Print("PlayerInteractable: AcceptUseFrom called with source " + source.Name);
        if (itemManager == null) InitReferences();
        var sourceId = GetInteractableId(); //get unique id, default to name
        var thisPC = GetPlayerController();
        long targetPeerId = thisPC.GetMultiplayerAuthority();

        // Request spawn via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestFeedTarget), sourceId, targetPeerId);
            if (error != Error.Ok)
            {
                GD.PrintErr("PlayerInteractable: Failed to request feeding via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles spawn directly
        {
            itemManager.DoFeedTarget(sourceId, thisPC);
        }
    }

    public override bool CanUseSelf(CharacterBody3D user) { return false; }

    // By default, Interactables can use on each other if the target accepts it
    public override bool CanUseOnInteractable(CharacterBody3D user, Interactable target) { return false; }

    // By default, objects can use on entities other if the target accepts it
    public override bool CanUseOnEntity(CharacterBody3D user, Entity target) { return false; }

}
