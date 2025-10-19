using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;

public partial class GameStateTracker : Node
{
    private List<PlayerController> playerControllers;
    private int numOfDownedPlayers;
    private bool isProcessing = true;

    public override void _Ready()
    {
        base._Ready();
        playerControllers = new List<PlayerController>();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    /// <summary>
    /// Called when a player runs out of health. If every player is out of health,
    /// the game is lost, and every player is sent to the main menu after three seconds.
    /// </summary>
    private void CheckLossState()
    {
        int numOfDownedPlayers = 0;
        foreach (PlayerController playerController in playerControllers)
        {
            if (playerController.IsDowned)
                numOfDownedPlayers++;
        }

        if (numOfDownedPlayers >= playerControllers.Count)
        {
            foreach (PlayerController player in playerControllers)
            {
                player.SetOutOfHealthLabelText("The wilderness claims another...");
            }
            isProcessing = false;
            var execute = ResetAfterSeconds(3);
        }
    }

    public void AddPlayerToPlayerList(PlayerController player)
    {
        if (player != null && !playerControllers.Contains(player))
        {
            GD.Print(player);
            playerControllers.Add(player);
            player.OnDowned += CheckLossState;
        }
    }

    public void RemovePlayerFromPlayerList(PlayerController player)
    {
        if (playerControllers.Contains(player))
        {
            GD.Print(player);
            playerControllers.Remove(player);
            player.OnDowned -= CheckLossState;
        }
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