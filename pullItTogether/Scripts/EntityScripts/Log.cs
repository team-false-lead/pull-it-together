using Godot;
using System;

// a log that can be chopped to gain wood planks
public partial class Log : Entity
{
    [Export] public float currentHealth = 100; //export to use on multiPlayer syncer
    [Export] public float damageToTake = 25;
    [Export] public int numPlanksToSpawn = 2;

    // By default, entities do not accept being used on them
    //override to accept hatchet use
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("food"))
        {
            return true;
        }
        return false;
    }

    // Logic for accepting use from hatchet 
    public override void AcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (itemManager == null) InitReferences();
        var id = GetEntityId(); //get unique id, default to name

        // Request chop via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestChopLog), id, source.GetInteractableId());
            if (error != Error.Ok)
            {
                GD.PrintErr("Log: Failed to request chopping via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles chop directly
        {
            itemManager.DoChopLog(id, source.GetInteractableId());
        }
    }

    public void GiveWoodPlanks()
    {
        if (itemManager == null) InitReferences();
        var id = GetEntityId(); //get unique id, default to name

        for (int i = 0; i < numPlanksToSpawn; i++)
        {
            // Request plank spawn via RPC if not server
            if (multiplayerActive && !multiplayer.IsServer())
            {
                var error = itemManager.RpcId(1, nameof(ItemManager.RequestSpawnItem), id);
                if (error != Error.Ok)
                {
                    GD.PrintErr("Log: Failed to request wood plank spawn via RPC. Error: " + error);
                    return;
                }
            }
            else // Server or single-player handles spawn directly
            {
                itemManager.RequestSpawnItem(id);
            }
        }
    }

}
