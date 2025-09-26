using Godot;
using System;

/// a wooden plank that can be used to create a campfire and eventually fix wheels
public partial class WoodPlank : Interactable
{

    [Export] public PackedScene campfireScene;

    //spawn campfire when used on self
    public override void TryUseSelf(CharacterBody3D user)
    {
        // Logic for using the wood plank to make campfire
        if (campfireScene == null) return;

        var interactablesNode = InitWorldInteractablesNode(user);
        var spawnedCampfire = campfireScene.Instantiate<Node3D>();
        //fix campfire spawn location
        spawnedCampfire.Position = GetDropPosition(user);
        interactablesNode.AddChild(spawnedCampfire);
        QueueFree();
    }

    //add logic for using on wheels
    //public override void TryUseOnEntity(CharacterBody3D user, Entity target)
    //{
    //    // add logic for use on wheels
    //}

}
