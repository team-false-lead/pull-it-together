using Godot;
using System;

// a campfire that can cook food items
public partial class Campfire : Entity
{
    [Export] public int usesLeft = 3; //export to use on multiPlayer syncer

    // By default, entities do not accept being used on them
    //override to accept food items
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("food"))
        {
            return true;
        }
        return false;
    }

    // Logic for accepting use from food items
    public override void AcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (itemManager == null) InitReferences();
        var id = GetEntityId(); //get unique id, default to name

        // Request spawn via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestCookFood), id, source.GetInteractableId());
            if (error != Error.Ok)
            {
                GD.PrintErr("Campfire: Failed to request cooking via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles spawn directly
        {
            itemManager.DoCookFood(id, source.GetInteractableId());
        }
    }

}
