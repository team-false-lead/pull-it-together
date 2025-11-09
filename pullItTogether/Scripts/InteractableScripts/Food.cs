using Godot;
using System;

/// Food is an interactable item that can be eaten by the player or cooked on a campfire.
public partial class Food : Interactable
{
    [Export] public bool isCooked = false;
    [Export] public MeshInstance3D rawMesh;
    [Export] public MeshInstance3D cookedMesh;
    [Export] public Label3D label3D;
    [Export] public float healthAddedRaw = 25f; // Amount of health restored 
    [Export] public float healthAddedCooked = 50f; // Amount of health restored when cooked
    [Export] public float potentialEnergyAddedRaw = 25f; // Amount of energy restored
    [Export] public float potentialEnergyAddedCooked = 50f; // Amount of energy restored when cooked


    public override void _PhysicsProcess(global::System.Double delta)
    {
        base._PhysicsProcess(delta);
        if (isCooked == true && cookedMesh.Visible == false)
        {
            rawMesh.Visible = false;
            cookedMesh.Visible = true;
            publicName = "Cooked " + publicName;
        }
    }
    // Logic for using the food item on self (eating)
    public override void TryUseSelf(CharacterBody3D user)
    {
        if (user == null) return;

        if (itemManager == null) InitReferences();
        var id = GetInteractableId(); //get unique id, default to name

        var targetPC = user as PlayerController;
        string targetPeerId = targetPC.GetMultiplayerAuthority().ToString();

        // Request feeding via RPC if not server
        if (multiplayerActive && !multiplayer.IsServer())
        {

            var error = itemManager.RpcId(1, nameof(ItemManager.RequestFeedTarget), id, targetPeerId);
            if (error != Error.Ok)
            {
                GD.PrintErr("FoodTemplate: Failed to request feed item via RPC. Error: " + error);
                return;
            }
        }
        else // Server or single-player handles feeding directly
        {
            itemManager.DoFeedTarget(id, targetPeerId);
        }
    }

    public override void ToggleHighlighted(bool highlighted)
    {
        MeshInstance3D activeMesh;
        if (isCooked)
            activeMesh = cookedMesh;
        else
            activeMesh = rawMesh;
    }

    // Logic for using the food item on an interactable Player (feeding)
    //public override void TryUseOnInteractable(CharacterBody3D user, Interactable target)
    //{
    //    if (user == null) return;
//
    //    if (CanUseOnInteractable(user, target) == false)
    //    {
    //        GD.Print("Food: Cannot use " + Name + " on " + target.Name);
    //        return;
    //    }
//
    //    if (itemManager == null) InitReferences();
    //    var id = GetInteractableId(); //get unique id, default to name
//
    //    var targetPC = target.GetPlayerController();
    //    long targetPeerId = targetPC.GetMultiplayerAuthority();
//
    //    // Request feed via RPC if not server
    //    if (multiplayerActive && !multiplayer.IsServer())
    //    {
    //        var error = itemManager.RpcId(1, nameof(ItemManager.RequestFeedTarget), id, targetPeerId);
    //        if (error != Error.Ok)
    //        {
    //            GD.PrintErr("FoodTemplate: Failed to request feed item via RPC. Error: " + error);
    //            return;
    //        }
    //    }
    //    else // Server or single-player handles feeding directly
    //    {
    //        itemManager.DoFeedTarget(id, targetPC);
    //    }
    //}

    //logic for cooking lives on campfire
    //public virtual void TryUseOnEntity(CharacterBody3D user, Entity target)
    //{ }
}
