using Godot;
using System;

/// a wooden plank that can be used to create a campfire and eventually fix wheels
public partial class WoodPlank : Interactable
{
    [Export] public PackedScene campfireScene;

    //spawn campfire when used on self
    public override void TryUseSelf(CharacterBody3D user)
    {
        if (campfireScene == null) return;

        // Spawn campfire at drop position
        //var interactablesNode = InitWorldInteractablesNode(user);
        //var spawnedCampfire = campfireScene.Instantiate<Node3D>();
        Vector3 dropPosition = GetDropPosition(user);
        String campfireScenePath = campfireScene.ResourcePath;

        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestSpawnItem), campfireScenePath, dropPosition);
            if (error != Error.Ok)
            {
                GD.PrintErr("WoodPlank: Failed to request campfire spawn via RPC. Error: " + error);
                return;
            }
        }
        else
        {
            itemManager.RequestSpawnItem(campfireScenePath, dropPosition);
        }
        

        // spawnedCampfire.Position = GetDropPosition(user);
        //interactablesNode.AddChild(spawnedCampfire);
        QueueFree(); // Remove the plank after use
    }

    //add logic for using on wheels
    //public override void TryUseOnEntity(CharacterBody3D user, Entity target)
    //{
    //    // add logic for use on wheels
    //}

}
