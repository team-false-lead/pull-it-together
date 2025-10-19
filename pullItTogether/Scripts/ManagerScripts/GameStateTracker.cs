using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;

public partial class GameStateTracker : Node
{
    private List<PlayerController> playerControllers;
    private bool isProcessing = true;

    public override void _Ready()
    {
        base._Ready();
        playerControllers = new List<PlayerController>();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        // Only update on peer side
        if (isProcessing)
        {
            CheckLossState();
        }

    }

    // TEMP: Will replace with signals for when players are downed, which should
    // improve efficiency and be less clunky compared to running a for loop every frame.
    private void CheckLossState()
    {
        //int downedPlayers = 0;
        //foreach (PlayerController player in playerControllers)
        //{
        //    if (player.IsDowned)
        //        downedPlayers++;
        //}
        //
        //if ((!Multiplayer.IsServer() && downedPlayers >= 1) || downedPlayers >= Multiplayer.GetPeers().Length)
        //{
        //    foreach (PlayerController player in playerControllers)
        //    {
        //        player.SetOutOfHealthLabelText("The wilderness claims another...");
        //    }
        //    isProcessing = false;
        //    var execute = ResetAfterSeconds(3);
        //}
    }

    public void AddPlayerToPlayerList(PlayerController player)
    {
        // Only add players to the list server-side
        if (!Multiplayer.IsServer())
            return; 
        GD.Print(player);
        if (player != null && !playerControllers.Contains(player))
            playerControllers.Add(player);
    }

    public void RemovePlayerFromPlayerList(PlayerController player)
    {
        // This method can't be host-side only, as it prevents non-hosts from calling this method
        GD.Print(player);
        if (playerControllers.Contains(player))
            playerControllers.Remove(player);
    }

    public async Task ResetAfterSeconds(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        foreach (PlayerController player in playerControllers)
        {
            player.ExitLobby();
        }
    }
}