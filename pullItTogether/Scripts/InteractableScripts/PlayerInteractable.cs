using Godot;
using System;

public partial class PlayerInteractable : Node
{
    // By default, objects do not accept being used on them
    public virtual bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("Food"))
        {
            return true;
        }
        return false;
    }
    public virtual void AcceptUseFrom(CharacterBody3D user, Interactable source) { }
}
