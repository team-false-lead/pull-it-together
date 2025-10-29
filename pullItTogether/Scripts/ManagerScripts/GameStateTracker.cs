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
        //if (!multiplayer.IsServer()) return;
        //if (playerControllers.Count > 1)
        //    CheckLossState();
    }

    /// <summary>
    /// Called when a player runs out of health. If every player is out of health,
    /// the game is lost, and every player is sent to the main menu after three seconds.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void CheckLossState(int peerId, float peerCurrentHealth)
    {
        //if (!multiplayer.IsServer()) return;
        GD.Print("Checking loss state...");
        numOfDownedPlayers = 0;
        //foreach (PlayerController playerController in playerControllers) // this is a cached list so it may be out of date
        //{
        //    if (playerController.IsDowned)
        //        numOfDownedPlayers++;
        //}

        var playersArray = GetTree().GetNodesInGroup("players"); // get current players
        foreach (var player in playersArray)
        {
            if (player is PlayerController pc)
            {
                GD.Print("CheckLossState: PlayerController ", pc.Name, " has health ", pc.currentHealth); // the caller says they have 10? when they only send the rpc when 0??
                if (pc.GetMultiplayerAuthority() == peerId) //manualy update the health of the caller cause their downed for sure
                {
                    GD.Print("CheckLossState: Updating health for player ", pc.Name, " to given value: ", peerCurrentHealth);
                    pc.currentHealth = peerCurrentHealth;
                }
                if (pc.IsDowned)
                {
                    GD.Print("CheckLossState: Player ", pc.Name, " is downed.");
                    numOfDownedPlayers++;
                }
            }
        }

        //GD.Print("Number of downed players: ", numOfDownedPlayers);
        //GD.Print("Total number of players: ", playersArray.Count);
        if (numOfDownedPlayers >= playersArray.Count)
        {
            foreach (var player in playersArray)
            {
                if (player is PlayerController pc)
                {
                    pc.RpcId(pc.GetMultiplayerAuthority(), nameof(PlayerController.SetOutOfHealthLabelText), "You couldn't Pull It Together™...");
                    //pc.SetOutOfHealthLabelText("You couldn't Pull It Together™...");
                }
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
                if (player is PlayerController pc)
                {
                    pc.RpcId(pc.GetMultiplayerAuthority(), nameof(PlayerController.SetOutOfHealthLabelText), "Congratulations! You're the greatest Wagoneer(s)");
                }
            }
            var execute = ResetAfterSeconds(3);
        }
    }

    public void AddPlayerToPlayerList(int peerId)
    {
        if (!multiplayer.IsServer()) return;
        var playerController = GetPlayerControllerById(peerId);
        if (playerController != null && !playerControllers.Contains(playerController))
        {
            GD.Print("GameStateTracker: Adding player ", playerController.Name);
            playerControllers.Add(playerController);
            //playerController.OnDowned += CheckLossState;
            //playerController.Connect(PlayerController.SignalName.OnDowned, new Callable(this, nameof(CheckLossState)));
        }
    }

    public void RemovePlayerFromPlayerList(int peerId)
    {
        if (!multiplayer.IsServer()) return;
        var playerController = GetPlayerControllerById(peerId);
        if (playerControllers.Contains(playerController))
        {
            GD.Print("GameStateTracker: Removing player ", playerController.Name);
            playerControllers.Remove(playerController);
            //player.OnDowned -= CheckLossState;
            //playerController.Disconnect(PlayerController.SignalName.OnDowned, new Callable(this, nameof(CheckLossState)));
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

    //copied this from item manager
    //get the player controller script by their multiplayer id, or return singleplayer player script
    private PlayerController GetPlayerControllerById(long id)
    {
        var playersArray = GetTree().GetNodesInGroup("players"); // get current players
        if (playersArray.Count == 1) return playersArray[0] as PlayerController; // singleplayer shortcut

        foreach (var player in playersArray)
        {
            if (player is PlayerController pc && pc.GetMultiplayerAuthority() == id)
            {
                return pc;
            }
        }
        return null;
    }
}