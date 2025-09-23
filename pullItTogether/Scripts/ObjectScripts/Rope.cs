using Godot;
using System;

/// <summary>
/// A rope that can be carried but not used on anything.
public partial class Rope : Interactable
{
    public override void TryUseSelf(CharacterBody3D user)
    {
        //logic for adding force to pull things
    }

    public override bool CanUseOn(CharacterBody3D user, Interactable target)
    {
        return false; // Ropes cannot be used on other objects
    }
}
