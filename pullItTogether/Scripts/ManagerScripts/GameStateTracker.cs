using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;

public partial class GameStateTracker : Node
{
    private List<PlayerController> playerControllers;
    private int numOfDownedPlayers;
    [Export] private TransitionArea transitionArea;
    [Export] private Node3D superstormNode;
    [Export] private Node3D interactablesNode;
    private Node3D wagonNode;
    public List<Event> currentEvents = new List<Event>();
    protected MultiplayerApi multiplayer => GetTree().GetMultiplayer();

    // Stress calculation weights
    private Timer stressTimer;
    [Export] private float stressTickInterval = 2.0f; // seconds
    [Export] private float playerStatusWeight = 0.35f;
    [Export] private float wagonStressWeight = 0.25f;
    [Export] private float superstormStressWeight = 0.30f;
    [Export] private float environmentalStressWeight = 0.10f;
    [Export] private float teamResilienceStrength = 0.50f;
    [Export] private float teamResilienceMax = 0.33f;

    //sub-weights
    [Export] private float playerHealthWeight = 0.55f;
    [Export] private float playerEnergyWeight = 0.25f;
    [Export] private float playerDownedWeight = 0.20f;
    [Export] private float majorityDownedMinStressLevel = 0.80f;
    [Export] private float wheelHealthWeight = 0.5f;
    [Export] private float wheelBrokenWeight = 0.3f;
    [Export] private float stormDistanceWeight = 1f;
    [Export] private float stormSafeDistance = 250f; // temp safe distance until we have a better way to define it (current map from storm to win point = 1300)
    [Export] private float weatherWeight = 0.65f;
    [Export] private float terrainWeight = 0.35f;

    public override void _Ready()
    {
        base._Ready();
        playerControllers = new List<PlayerController>();
        transitionArea.BodyEntered += (Node3D) => { CallDeferred("CheckWinState"); };
        interactablesNode.Connect("ItemsSpawned", new Callable(this, nameof(GetWagonReference)));

        stressTimer = new Timer
        {
            WaitTime = stressTickInterval,
            OneShot = false,
            Autostart = false
        };
        AddChild(stressTimer);
        stressTimer.Timeout += EvaluateStressTick;
    }
    
    private void GetWagonReference()
	{
        wagonNode = GetTree().GetFirstNodeInGroup("wagon") as Node3D;
        
        if (wagonNode != null)
        {
            GD.Print("GameStateTracker: Wagon node found: " + wagonNode.Name);
            stressTimer.Start();
        }
        else
        {
            GD.PrintErr("GameStateTracker: Wagon node not found in scene tree.");
        }
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

    //--------------------------- Stress functions
    private void EvaluateStressTick()
    {
        if (!multiplayer.IsServer()) return;
        if (wagonNode == null || superstormNode == null)
        {
            GD.PrintErr("Wagon node or Superstorm node is null in EvaluateStressTick");
            return;
        }

        var playersArray = GetTree().GetNodesInGroup("players").ToArray(); // get current players
        if (playersArray.Length == 0) { GD.PrintErr("No players found in EvaluateStressTick"); return; }

        PlayerController[] pcArray = new PlayerController[playersArray.Length];
        for (int i = 0; i < playersArray.Length; i++)
        {
            if (playersArray[i] is PlayerController pc)
            {
                pcArray[i] = pc;
            }
            else
            {
                GD.PrintErr("Non-PlayerController found in players group in EvaluateStressTick");
                return;
            }
        }

        float playersStatusStressRaw = GetPlayersStatusStressLevel(pcArray);
        float wagonStressRaw = GetWagonStressLevel(pcArray);
        float stormStressRaw = GetStormStressLevel();
        float environmentalStressRaw = GetEnvironmentalStressLevel();
        float stressWeighted = (playersStatusStressRaw * playerStatusWeight) + (wagonStressRaw * wagonStressWeight) + (stormStressRaw * superstormStressWeight) + (environmentalStressRaw * environmentalStressWeight);
        //float teamResilience = GetTeamResilienceLevel(pcArray);
        //float totalStressLevel = stressWeighted - teamResilience;
        float totalStressLevel = Mathf.Clamp(stressWeighted, 0f, 1f);

        GD.Print("StressTick: Players=", playersStatusStressRaw, ", Wagon=", wagonStressRaw, ", Storm=", stormStressRaw, ", Env=", environmentalStressRaw);
        GD.Print("StressTick: TotalStressLevel=", totalStressLevel); //, ", WeightedStress=", stressWeighted, ", TeamResilience=", teamResilience);
    }

    private float GetPlayersStatusStressLevel(PlayerController[] players)
    {
        if (players == null || players.Length == 0) { GD.PrintErr("Players array is null or empty in GetPlayersStatusStressLevel"); return 0f; }

        int downedCount = 0;
        float totalHealthPercentage = 0f;
        float totalEnergyPercentage = 0f;
        float totalHpLoss = 0f;
        float totalEnergyLoss = 0f;

        foreach (var player in players)
        {
            if (player == null) { GD.PrintErr("Player is null in GetPlayersStatusStressLevel"); continue; }

            totalHealthPercentage += (player.currentHealth / 100f) * (1f / players.Length);
            totalEnergyPercentage += (player.maxEnergy / 100f) * (1f / players.Length);
            totalHpLoss += 100f - player.currentHealth;
            totalEnergyLoss += 100f - player.maxEnergy;
            if (player.currentHealth <= 0) downedCount++;
        }

        (float hpRestorePotential, float energyRestorePotential, float _) = GetTeamResilienceLevel(players, false); // false = check food, true = check repair
        float hpMitigation = totalHpLoss > 0 ? Mathf.Clamp(hpRestorePotential / totalHpLoss, 0f, 1f) : 0f;
        hpMitigation = Mathf.Clamp(hpMitigation * teamResilienceStrength, 0f, teamResilienceMax); // cap mitigation
        float hpLossPercentage = 1f - totalHealthPercentage;
        float hpStressRaw = hpLossPercentage * (1f - hpMitigation);

        float energyMitigation = totalEnergyLoss > 0 ? Mathf.Clamp(energyRestorePotential / totalEnergyLoss, 0f, 1f) : 0f;
        energyMitigation = Mathf.Clamp(energyMitigation * teamResilienceStrength, 0f, teamResilienceMax); // cap mitigation
        float energyLossPercentage = 1f - totalEnergyPercentage;
        float energyStressRaw = energyLossPercentage * (1f - energyMitigation);

        float downedPercentage = (float)downedCount / players.Length;
        float playersStressLevel = (hpStressRaw * playerHealthWeight) + (energyStressRaw * playerEnergyWeight) + (downedPercentage * playerDownedWeight);

        if (downedPercentage >= 0.5f)
        {
            playersStressLevel = Mathf.Max(playersStressLevel, majorityDownedMinStressLevel); // set to at least majority downed stress level
        }

        return playersStressLevel;
    }


    private float GetWagonStressLevel(PlayerController[] players)
    {
        if (wagonNode == null) { GD.PrintErr("Wagon is null in GetWagonStressLevel"); return 0f; }
        var wagon = wagonNode as Wagon;
        if (wagon == null) { GD.PrintErr("Wagon script is null in GetWagonStressLevel"); return 0f; }

        float totalWheelHealth = 0f;
        int totalWheelsBroken = 0;
        foreach (var wheel in wagon.wheels)
        {
            totalWheelHealth += wheel.currentHealth;
            if (wheel.currentHealth <= 0)
            {
                totalWheelsBroken++;
            }
        }
        float wheelHealthPercentage = totalWheelHealth / 400f; // max health is 100 per wheel
        float totalWheelDamage = 400f - totalWheelHealth;
        float wheelBrokenPercentage = totalWheelsBroken / 4f;

        (float _, float _, float repairPotential) = GetTeamResilienceLevel(players, true); // false = check food, true = check repair
        float damageMitigation = totalWheelDamage > 0 ? Mathf.Clamp(repairPotential / totalWheelDamage, 0f, 1f) : 0f;
        damageMitigation = Mathf.Clamp(damageMitigation * teamResilienceStrength, 0f, teamResilienceMax); // cap mitigation
        float damageLossPercentage = 1f - wheelHealthPercentage;
        float damageStressRaw = damageLossPercentage * (1f - damageMitigation);

        float totalPlanks = repairPotential / wagon.wheels[0].repairAmount; // get number of planks available for repairs
        float potentialWheelsfixedPercentage = totalWheelsBroken > 0 ? Mathf.Clamp(totalPlanks / totalWheelsBroken, 0f, 1f) : 0f;
        potentialWheelsfixedPercentage = Mathf.Clamp(potentialWheelsfixedPercentage * teamResilienceStrength, 0f, teamResilienceMax);
        float brokenStressRaw = wheelBrokenPercentage * (1f - potentialWheelsfixedPercentage);

        float wagonStressLevel = (damageStressRaw * wheelHealthWeight) + (brokenStressRaw * wheelBrokenWeight);
        return wagonStressLevel;
    }

    private float GetStormStressLevel()
    {
        if (superstormNode == null || wagonNode == null) { GD.PrintErr("Superstorm or Wagon node is null in GetStormStressLevel"); return 0f; }
        SuperstormMovement stormScript = superstormNode as SuperstormMovement;
        var stormEnterPositionZ = superstormNode.Position.Z - ((CylinderShape3D)stormScript.stormCollider.Shape).Radius; // approximate front of storm

        var distanceRaw = stormEnterPositionZ - wagonNode.GlobalPosition.Z;
        if (distanceRaw < 0f) return 1f; // wagon is inside storm
        var distanceAbs = Math.Abs(distanceRaw);
        var stormStressLevel = 0f;
        if (distanceAbs >= stormSafeDistance)
        {
            return stormStressLevel;
        }
        stormStressLevel = (stormSafeDistance - distanceAbs) / stormSafeDistance * stormDistanceWeight;
        // add storm speed?
        return stormStressLevel;
    }

    private float GetEnvironmentalStressLevel()
    {
        float weatherStress = 0f;
        float terrainStress = 0f;
        foreach (Event gameEvent in currentEvents)
        {
            if (gameEvent.Type == Event.EventType.Weather)
            {
                weatherStress = 1f;
            }
            else if (gameEvent.Type == Event.EventType.Terrain)
            {
                terrainStress = 1f;
            }
        }

        float totalEnvironmentalStress = (weatherStress * weatherWeight) + (terrainStress * terrainWeight);
        return totalEnvironmentalStress;
    }

    private (float hpMit, float energyMit, float repairMit) GetTeamResilienceLevel(PlayerController[] players, bool checkRepairInstead = false) // (float hpMit, float energyMit, float repairMit)
    {
        if (wagonNode == null) { GD.PrintErr("Wagon is null in GetTeamResilienceLevel"); return (0f, 0f, 0f); }
        var wagon = wagonNode as Wagon;
        if (wagon == null) { GD.PrintErr("Wagon script is null in GetTeamResilienceLevel"); return (0f, 0f, 0f); }
        float currentTotalHpLoss = 0f;
        float currentTotalEnergyLoss = 0f;
        float currentTotalRepairLoss = 0f;

        float totalPotentialHpValue = 0f;
        float totalPotentialEnergyValue = 0f;
        float totalPotentialRepairValue = 0f;

        //List<string> interactableIdsCounted = new List<string>();
        var counted = new HashSet<string>(); // faster lookup

        void CountFoodOnce(Food food)
        {
            if (food == null) return;
            var id = food.GetInteractableId();
            if (counted.Add(id)) // returns true if added, false if already present
            {
                if (food.isCooked)
                {
                    totalPotentialHpValue += food.healthAddedCooked;
                    totalPotentialEnergyValue += food.potentialEnergyAddedCooked;
                }
                else
                {
                    totalPotentialHpValue += food.healthAddedRaw;
                    totalPotentialEnergyValue += food.potentialEnergyAddedRaw;
                }
            }
        }

        void CountPlankOnce(WoodPlank plank)
        {
            if (plank == null) return;
            var id = plank.GetInteractableId();
            if (counted.Add(id)) // returns true if added, false if already present
            {
                totalPotentialRepairValue += wagon.wheels[0].repairAmount; //repair value lives on wheel for now
            }
        }

        foreach (var wheel in wagon.wheels)
        {
            currentTotalRepairLoss += wheel.maxHealth - wheel.currentHealth;
        }

        foreach (var item in wagon.itemDetector.itemsInside)
        {
            if (checkRepairInstead)
                CountPlankOnce(item as WoodPlank);
            else
                CountFoodOnce(item as Food);
        }

        foreach (var player in players)
        {
            if (player == null) { GD.PrintErr("Player is null in GetTeamResilienceLevel"); continue; }

            currentTotalHpLoss += 100f - player.currentHealth;
            currentTotalEnergyLoss += 100f - player.maxEnergy;

            if (checkRepairInstead)
                CountPlankOnce(player.heldObject as WoodPlank); // manual check since held objects collision isnt detected
            else
                CountFoodOnce(player.heldObject as Food);

            foreach (var item in player.itemDetector.itemsInside)
            {
                if (checkRepairInstead)
                    CountPlankOnce(item as WoodPlank);
                else
                    CountFoodOnce(item as Food);
            }
        }

        return (totalPotentialHpValue, totalPotentialEnergyValue, totalPotentialRepairValue);
        
        //float totalHPMitigation = currentTotalHpLoss > 0 ? Mathf.Clamp(totalPotentialHpValue / currentTotalHpLoss, 0f, 1f) : 0f;
        //float totalEnergyMitigation = currentTotalEnergyLoss > 0 ? Mathf.Clamp(totalPotentialEnergyValue / currentTotalEnergyLoss, 0f, 1f) : 0f;
        //float totalRepairMitigation = currentTotalRepairLoss > 0 ? Mathf.Clamp(totalPotentialRepairValue / currentTotalRepairLoss, 0f, 1f) : 0f;

        //float resilienceRaw = 1f - ((1f - totalHPMitigation) * (1f - totalEnergyMitigation) * (1f - totalRepairMitigation));
        //float resilienceLevel = resilienceRaw * teamResilienceStrength;
        //resilienceLevel = Mathf.Min(resilienceLevel, teamResilienceMax); //cap resilience level
        //return resilienceLevel;
    }
    

}