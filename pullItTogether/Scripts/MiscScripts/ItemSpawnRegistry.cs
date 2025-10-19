using Godot;
using System;

/// ItemSpawnRegistry automatically registers all Interactable and Entity scenes in specified folders with the ItemManager at game start.
/// so dont have to manually add scenes to the auto spawn list every new addition
public partial class ItemSpawnRegistry : MultiplayerSpawner
{
    [Export] private string interactablesFolder = "res://Scenes/Interactables/";
    [Export] private string entitiesFolder = "res://Scenes/Entities/";
    [Export] private bool includeSubfolders = true;
    private MultiplayerApi multiplayer => GetTree().GetMultiplayer();

    public override void _Ready()
    {
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
}
