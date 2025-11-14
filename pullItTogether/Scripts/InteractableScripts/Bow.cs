using Godot;
using System;

/// a bow that can be used to shoot arrows
public partial class Bow : Interactable
{
    [Export] public int currentAmmo = 10;
    [Export] public int maxAmmo = 10;
    [Export] public Label3D ammoLabel;

    public override void _Ready()
    {
        base._Ready();
        UpdateAmmoLabel();
    }

    public override void TryUseSelf(CharacterBody3D user)
    {
        if (itemManager == null) InitReferences();
        var id = GetInteractableId(); //get unique id, default to name

        // Request feeding via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {
            var error = itemManager.RpcId(1, nameof(ItemManager.RequestFireArrow), id);
            if (error != Error.Ok)
            {
                GD.PrintErr("Bow: Failed to request fire arrow via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles firing directly
        {
            itemManager.DoFireArrow(id);
        }
    }
    
    public void UpdateAmmoLabel()
    {
        ammoLabel.Text = $"{currentAmmo} / {maxAmmo}";
    }

    public override void ToggleHighlighted(bool highlighted)
    {
        foreach (Node3D child in GetNode<Node3D>("BowMeshes").GetChildren())
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
