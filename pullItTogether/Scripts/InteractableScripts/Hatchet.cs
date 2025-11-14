using Godot;
using System;

/// a hatchet that can be used to chop logs
public partial class Hatchet : Interactable
{
    //play chop animation when used on self
    public override void TryUseSelf(CharacterBody3D user)
    {
        //temp chop animation
        PlayChopAnimation();
    }

    public void PlayChopAnimation()
    {
        //temp chop animation
        Tween tween = CreateTween();
        Vector3 originalRotation = this.RotationDegrees;
        Vector3 chopRotation = originalRotation + new Vector3(-75, 0, 0);
        tween.TweenProperty(this, "rotation_degrees", chopRotation, 0.1f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.TweenProperty(this, "rotation_degrees", originalRotation, 0.1f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public override void ToggleHighlighted(bool highlighted)
    {
        foreach (Node3D child in GetNode<Node3D>("HatchetMeshes").GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                mesh.GetSurfaceOverrideMaterial(0).Set("emission_enabled", highlighted);
                if (highlighted)
                    mesh.GetSurfaceOverrideMaterial(0).Set("emission", Colors.Yellow);
            }
        }
    }
}
