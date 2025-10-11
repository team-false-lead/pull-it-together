using Godot;
using System;

/// a wooden plank that can be used to create a campfire and eventually fix wheels
public partial class WoodPlank : Interactable
{
    //spawn campfire when used on self
    public override void TryUseSelf(CharacterBody3D user)
    {
        if (SpawnOnUseScene == null) return;

        if (itemManager == null) InitReferences();
        var id = GetInteractableId(); //get unique id, default to name

        // Request spawn via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestSpawnItem), id);
            if (error != Error.Ok)
            {
                GD.PrintErr("WoodPlank: Failed to request campfire spawn via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles spawn directly
        {
            itemManager.DoSpawnItem(id);
        }

        //QueueFree(); // Remove this plank after use
    }

	//add logic for using on wheels
	//public override void TryUseOnEntity(CharacterBody3D user, Entity target)
	//{
	//    // add logic for use on wheels
	//}

}
