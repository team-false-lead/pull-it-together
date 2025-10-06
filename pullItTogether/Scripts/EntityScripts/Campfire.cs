using Godot;
using System;

public partial class Campfire : Entity
{
    [Export] private int usesLeft = 3; //export to use on multiPlayer syncer
    
    // By default, entities do not accept being used on them
    //override to accept food items
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("Food"))
        {
            return true;
        }
        return false;
    }

    // Logic for accepting use from food items
    public override void AcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        GD.Print("Campfire: Accepted use from " + source.Name);
        //add logic for cooking food here
        //remove and replace held item with cooked version
        //assume food item has a cookedMesh assigned
        usesLeft--;
        if (usesLeft <= 0)
        {
            QueueFree(); // Remove campfire after uses are exhausted
        }
    }

}
