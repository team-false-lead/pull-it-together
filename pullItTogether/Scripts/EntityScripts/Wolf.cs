using Godot;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Generic;

public partial class Wolf : Animal
{
    [Export] public ItemDetector meatDetector;
    [Export] public ItemDetector playerDetector;
    [Export] public ItemDetector wagonDetector;
    [Export] public bool meatInRange = false;
    private List<Node3D> meatsInside = new List<Node3D>();
    [Export] public bool playerInRange = false;
    private List<Node3D> playersInside = new List<Node3D>();
    [Export] public bool wagonInRange = false;
    //hasItemTarget for meat from animal class
    [Export] public bool hasPlayerTarget = false;
    [Export] public bool hasWagonTarget = false;
    [Export] public bool inStalkRange = false;
    //itemTarget for meat from animal class
    public PlayerController playerTarget = null;
    public Wagon wagonTarget = null;
    [Export] public bool finishedAttackPlayer = false;
    [Export] public float playerDamage = 17f;
    [Export] public float maxDamageToDeal = 50f;
    [Export] public float maxWagonStalkDistance = 20f;
    [Export] public float minWagonStalkDistance = 12f; // player detector current range = 10;
    [Export] public float maxWagonStalkTime = 45f; // seconds
    [Export] public Timer stalkTimer;
    [Export] public bool finishedStalkWagon = false;
    private float totalDamageDealt = 0f;
    private bool spawnedMeat = false;

    public override void _Ready()
    {
        base._Ready();
        
        if (multiplayerActive && !multiplayer.IsServer())
        {
            if (BT != null)
            {
                BT.Call("set_active", false);
            }
        }

        if (stalkTimer != null)
        {
            stalkTimer.WaitTime = maxWagonStalkTime;
            stalkTimer.Timeout += () => 
            {
                finishedStalkWagon = true;
            };
        }

        if (meatDetector != null)
        {
            meatDetector.FilteredBodyEntered += OnItemsAdded;
            meatDetector.FilteredBodyExited += OnItemsRemoved;
        }
        if (playerDetector != null)
        {
            playerDetector.FilteredBodyEntered += OnItemsAdded;
            playerDetector.FilteredBodyExited += OnItemsRemoved;
        }
        if (wagonDetector != null)
        {
            wagonDetector.FilteredBodyEntered += OnItemsAdded;
            wagonDetector.FilteredBodyExited += OnItemsRemoved;
        }
    }

    // By default, entities do not accept being used on them
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("hatchet") || (source.IsInGroup("meat") && !hasItem))
        {
            return true;
        }
        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (wagonTarget != null)
        {
            float distanceToWagon = GlobalPosition.DistanceTo(wagonTarget.GlobalPosition);
            if (distanceToWagon <= maxWagonStalkDistance && distanceToWagon >= minWagonStalkDistance)
                inStalkRange = true;
            else
                inStalkRange = false;
        }

        if (playerInRange && playerTarget == null)
        {
            foreach (var item in playerDetector.itemsInside)
            {
                if (item is PlayerController player)
                {
                    playerTarget = player;
                    break;
                }
            }
        }

        if (playerTarget != null)
        {
            if (meatInRange || hasItem)
            {
                hasPlayerTarget = false;
            }
            else
            {
                hasPlayerTarget = true;
                targetPosition = playerTarget.GlobalPosition;
                if (playerTarget.currentHealth <= 0 || totalDamageDealt >= maxDamageToDeal)
                {
                    finishedAttackPlayer = true;
                    if (!spawnedMeat)
                    {
                        if (itemManager == null) InitReferences();
                        var id = GetEntityId(); //get unique id, default to name

                        // request spawn meat
                        if (multiplayerActive && !multiplayer.IsServer())
                        {
                            var error = itemManager.RpcId(1, nameof(ItemManager.RequestAnimalSpawnItem), id);
                            if (error != Error.Ok)
                            {
                                GD.PrintErr("Wolf: Failed to request use via RPC. Error: " + error);
                            }
                            hasItem = true;
                        }
                        else // Server or single-player handles spawn directly
                        {
                            itemManager.DoAnimalSpawnItem(id);
                            hasItem = true;
                        }
                        spawnedMeat = true;
                    }
                }
                else
                {
                    finishedAttackPlayer = false;
                }
            } 
        }
    }

    private void OnItemsAdded(Node3D item)
    {
        GD.Print("Wolf: OnItemsAdded called for " + item.Name);
        if (item.IsInGroup("wagon"))
        {
            wagonInRange = true;
            //label.Text = "Wagon in range";
            wagonTarget = item as Wagon;
            hasWagonTarget = true;
            stalkTimer.Start();
        }
        if (item.IsInGroup("players"))
        {
            if (!playersInside.Contains(item))
                playersInside.Add(item);

            if (item is PlayerController player)
            {
                playerInRange = true;
                //label.Text = "Player in range";
                playerTarget = player;
            }
        }
        if (item.IsInGroup("meat"))
        {
            if (!meatsInside.Contains(item))
                meatsInside.Add(item);

            if (item is Interactable meatInteractable)
            {
                if (meatInteractable.Carrier == null)
                {
                    meatInRange = true;
                    //label.Text = "Meat in range";
                    targetPosition = meatInteractable.GlobalPosition;
                    itemTarget = meatInteractable;
                }
                else
                {
                    meatInRange = false;
                    itemTarget = null;
                }
            }
        }
    }

    private void OnItemsRemoved(Node3D item)
    {
        GD.Print("Wolf: OnItemsRemoved called for " + item.Name);
        if (item.IsInGroup("wagon"))
        {
            wagonInRange = false;
        }
        if (item.IsInGroup("players"))
        {
            playersInside.Remove(item);
            if (playersInside.Count <= 0)
            {
                playerInRange = false;
                playerTarget = null;
                targetPosition = GlobalPosition; // stop moving toward last known player position
            }
        }
        if (item.IsInGroup("meat"))
        {
            meatsInside.Remove(item);
            if (meatsInside.Count <= 0)
            {
                meatInRange = false;
                itemTarget = null;
            }
        }
    }

    public bool AttackPlayer()
    {
        if (hasPlayerTarget && playerTarget != null && !finishedAttackPlayer)
        {
            //playerTarget.ChangeCurrentHealth(-playerDamage);
            var error = playerTarget.RpcId(playerTarget.GetMultiplayerAuthority(), nameof(PlayerController.ChangeCurrentHealth), -playerDamage);
            if (error != Error.Ok)
            {
                GD.PrintErr("Wolf: Failed to deal damage to player via RPC. Error: " + error);
                return false;
            }
            totalDamageDealt += playerDamage;
            return true;
        }
        return false;
    }

    public bool GetStalkPointAroundWagon()
    {
        if (wagonTarget == null) return false;
        
        Vector3 wagonPosition = wagonTarget.GlobalPosition;
        Vector3 toWolf = (GlobalPosition - wagonPosition).Normalized();

        // black magic for random point in closest arc around wagon
        float baseAngle = Mathf.Atan2(toWolf.Z, toWolf.X); // angle from wagon to wolf
        float arcThird = Mathf.Tau / 6f; // 60 degrees in radians (half of one third of circle)
        float angle = baseAngle + Mathf.Lerp(-arcThird, arcThird, GD.Randf()); // random angle within one third of circle

        float distance = Mathf.Lerp(minWagonStalkDistance, maxWagonStalkDistance, GD.Randf()); // random distance between min and max

        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
        targetPosition = wagonPosition + offset;
        return true;
    }
}