using Godot;
using System;

// a log that can be chopped to gain wood planks
public partial class Bush : Entity
{
    [Export] public int minBerriesToSpawn = 1;
    [Export] public int maxBerriesToSpawn = 2;
    private int numBerriesToSpawn = 0;
    [Export] public PackedScene[] berryOptions;
    private Random rnd = new Random();

    public override void _Ready()
    {
        base._Ready();
        SpawnOnUseScene = berryOptions[rnd.Next(0, berryOptions.Length)];
        numBerriesToSpawn = rnd.Next(minBerriesToSpawn, maxBerriesToSpawn + 1);
    }

    // By default, entities do not accept being used on them
    //override to accept hatchet use
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (user.IsInGroup("players"))
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

        for (int i = 0; i < numBerriesToSpawn; i++)
        {
            // Request plank spawn via RPC if not server
            if (multiplayerActive && !multiplayer.IsServer())
            {
                var error = itemManager.RpcId(1, nameof(ItemManager.RequestSpawnItem), id);
                if (error != Error.Ok)
                {
                    GD.PrintErr("Bush: Failed to request berry spawn via RPC. Error: " + error);
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
