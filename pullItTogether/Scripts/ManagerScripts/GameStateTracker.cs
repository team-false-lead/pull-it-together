using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;

public partial class GameStateTracker : Node
{
    private List<PlayerController> playerControllers;
    private int numOfDownedPlayers;
    [Export] private TransitionArea transitionArea;
    protected MultiplayerApi multiplayer => GetTree().GetMultiplayer();

    public override void _Ready()
    {
        base._Ready();
        playerControllers = new List<PlayerController>();
        transitionArea.BodyEntered += (Node3D) => { CallDeferred("CheckWinState"); };
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (!multiplayer.IsServer()) return;
        if (playerControllers.Count > 1)
            CheckLossState();
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
                player.SetOutOfHealthLabelText("You couldn't Pull It Together™...");
            }
            var execute = ResetAfterSeconds(3);
        }
    }

    private void CheckWinState()
    {
        if (!multiplayer.IsServer()) return;
        if (transitionArea.NumOfPlayersInside >= playerControllers.Count)
        {
            foreach (PlayerController player in playerControllers)
            {
                player.SetOutOfHealthLabelText("Congratulations! You're the greatest Wagoneer(s)!");
            }
            var execute = ResetAfterSeconds(3);
        }
    }

    public void AddPlayerToPlayerList(PlayerController player)
    {
        if (!multiplayer.IsServer()) return;
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