using Godot;
using System;

public partial class WoodPlank : Interactable
{

    [Export] public PackedScene campfireScene;

    public override void TryUseSelf(CharacterBody3D user)
    {
        // Logic for using the wood plank to make campfire
        if (campfireScene == null) return;

        var itemsNode = user.GetParent<Node3D>().GetParent<Node3D>().GetNode<Node3D>("%Items");
        var spawnedCampfire = campfireScene.Instantiate<Node3D>();
        //fix campfire spawn location
        spawnedCampfire.Position = user.GlobalTransform.Origin + user.GlobalTransform.Basis.Z * -2;//
        itemsNode.AddChild(spawnedCampfire);
        QueueFree();
    }

    public override void TryUseOn(CharacterBody3D user, Interactable target)
    {
        // add logic for use on wheels
    }

}
