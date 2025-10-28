using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// ItemSpawnRegistry automatically registers all Interactable and Entity scenes in specified folders with the ItemManager at game start.
/// so dont have to manually add scenes to the auto spawn list every new addition
public partial class ItemSpawnRegistry : MultiplayerSpawner
{
    [Export] private string interactablesFolder = "res://Scenes/Interactables/";
    [Export] private string entitiesFolder = "res://Scenes/Entities/";
    [Export] private bool includeSubfolders = true;
    private MultiplayerApi multiplayer => GetTree().GetMultiplayer();
    private ItemManager itemManager;

    public override void _Ready()
    {
        itemManager = this.GetParent<ItemManager>();
        RegisterFolderScenes(interactablesFolder, includeSubfolders);
        RegisterFolderScenes(entitiesFolder, includeSubfolders);
        GD.Print("ItemSpawnRegistry: Registered all interactable and entity scenes.");
    }

    // Register all .tscn scenes in the specified folder (and subfolders if recursive is true)
    private void RegisterFolderScenes(string folderPath, bool recursive)
    {
        if (string.IsNullOrEmpty(folderPath)) return;

        using var dir = DirAccess.Open(folderPath); // 'using' to ensure proper disposal
        if (dir == null)
        {
            GD.PrintErr("ItemSpawnRegistry: Directory does not exist: " + folderPath);
            return;
        }

        if (!folderPath.StartsWith("res://")) // Ensure path is valid
        {
            GD.PrintErr("ItemSpawnRegistry: Folder path must start with 'res://': " + folderPath);
            return;
        }

        // Register all .tscn files in the current directory
        foreach (var fileName in dir.GetFiles())
        {
            if (fileName.EndsWith(".tscn"))
            {
                var fullPath = folderPath + (folderPath.EndsWith("/") ? "" : "/") + fileName; // Ensure proper path formatting
                this.AddSpawnableScene(fullPath); // Register the scene
            }
        }

        if (recursive)
        {
            foreach (var subdir in dir.GetDirectories())
            {
                var subfolderPath = folderPath + (folderPath.EndsWith("/") ? "" : "/") + subdir;
                RegisterFolderScenes(subfolderPath, true);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ClientSpawnItem(string scenePath, string itemId, Transform3D transform, int authorityPeerId)
    {
        // Check if item with the same ID already exists
        foreach (var child in GetChildren())
        {
            if (child is Interactable childInteractable && childInteractable.interactableId == itemId)
            {
                // Item already exists, no need to spawn again
                return;
            }
            else if (child is Entity childEntity && childEntity.entityId == itemId)
            {
                // Item already exists, no need to spawn again
                return;
            }
        }

        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PrintErr("ItemSpawnRegistry: Failed to load scene for spawning: " + scenePath + " for item ID: " + itemId);
            return;
        }

        var instance = scene.Instantiate<Node3D>();
        instance.GlobalTransform = transform;
        instance.SetMultiplayerAuthority(authorityPeerId);
        this.AddChild(instance, true); // Spawn the instance
        instance.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene

        if (instance is Interactable instanceInteractable)
        {
            instanceInteractable.interactableId = itemId;
            itemManager.AssignInteractableId(instanceInteractable);
        }
        else if (instance is Entity instanceEntity)
        {
            instanceEntity.entityId = itemId;
            itemManager.AssignEntityId(instanceEntity);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ClientDespawnItem(string itemId)
    {
        foreach (var child in GetChildren())
        {
            if (child is Interactable childInteractable && childInteractable.interactableId == itemId)
            {
                childInteractable.SetMultiplayerAuthority(0); // Reset authority before freeing
                var syncer = childInteractable.GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer");
                if (syncer != null)
                {
                    syncer.ReplicationConfig = null; // Clear replication config to avoid issues
                    syncer.SetMultiplayerAuthority(0);
                    syncer.SetPhysicsProcess(false);
                    syncer.SetProcess(false);
                }
                //await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                childInteractable.QueueFree();
                return;
            }
            else if (child is Entity childEntity && childEntity.entityId == itemId)
            {
                childEntity.SetMultiplayerAuthority(0); // Reset authority before freeing
                var syncer = childEntity.GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer");
                if (syncer != null)
                {
                    syncer.ReplicationConfig = null; // Clear replication config to avoid issues
                    syncer.SetMultiplayerAuthority(0);
                    syncer.SetPhysicsProcess(false);
                    syncer.SetProcess(false);
                }
                //await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                childEntity.QueueFree();
                return;
            }
        }
    }
}
