using Godot;
using System;


/// Entity is the base class for all objects that can be interacted with by the player but are not carried.
public abstract partial class Entity : Node3D
{
    [Export] public PackedScene SpawnOnUseScene; // Optional scene to spawn when used
    [Export] public string entityId = "";
    [Export] public string publicName = "";

    public string scenePath = "";
    protected Node mapManager;
    protected bool multiplayerActive;
    protected MultiplayerApi multiplayer => GetTree().GetMultiplayer();
    protected Node3D levelInteractablesNode;
    protected ItemManager itemManager;

    //checks for multiplayer synchronizer on ready
    //if no id and not server, warn that waiting for id replication
    public override void _Ready()
    {
        var sync = GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer");
        if (sync == null)
        {
            GD.PrintErr("Entity: MultiplayerSynchronizer not found on " + Name);
        }

        InitReferences();
    }

    // Initialize references to MapManager and ItemManager
    protected void InitReferences()
    {
        mapManager = GetTree().CurrentScene.GetNodeOrNull<Node>("%MapManager");
        if (mapManager != null)
        {
            levelInteractablesNode = mapManager.Get("interactables_node").As<Node3D>();
            itemManager = levelInteractablesNode as ItemManager;
            multiplayerActive = mapManager.Get("is_multiplayer_session").As<bool>() && multiplayer.HasMultiplayerPeer();
        }
        else
        {
            GD.PrintErr("Entity: MapManager not found in scene tree!");
        }
    }

    // Get the unique ID of this entity, defaulting to its node name if not set
    public string GetEntityId()
    {
        if (string.IsNullOrEmpty(entityId))
        {
            return Name;
        }
        return entityId;
    }

    // By default, entities do not accept being used on them
    public virtual bool CanAcceptUseFrom(CharacterBody3D user, Interactable source) { return false; }
    public virtual void AcceptUseFrom(CharacterBody3D user, Interactable source) { }


    public virtual void ToggleHighlighted(bool highlighted) { }
}
