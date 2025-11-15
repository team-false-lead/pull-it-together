using Godot;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Generic;
using System.Linq;

public partial class Bird : Animal
{

    [Export] public ItemDetector wagonDetector;
    [Export] public bool hasWagonTarget = false;
    [Export] public bool wagonInRange = false;
    [Export] public bool wagonHasFood = false;
    [Export] public ItemDetector foodDetector;
    // hasItemTarget for food from animal class
    [Export] public bool itemInRange = false;
    //hasItem for food from animal class
    [Export] public ItemDetector perchDetector;
    [Export] public bool isOnPerch = false;
    [Export] public bool isOnWagonPerch = false;
    [Export] public float circleRadius = 10f;
    [Export] public float circleHeight = 7f;
    [Export] public bool finishedCircling = false;
    [Export] public float maxCircleTime = 10f; // seconds
    [Export] public Timer circleTimer;

    public Wagon wagonTarget = null;
    public Node3D perchTarget = null;
    public List<Node3D> worldPerches = new List<Node3D>();
    public List<Node3D> wagonPerches = new List<Node3D>();
    public int checkedWagon = 0;

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

        if (circleTimer != null)
        {
            circleTimer.WaitTime = maxCircleTime;
            circleTimer.Timeout += () => 
            {
                finishedCircling = true;
            };
        }

        if (wagonDetector != null)
        {
            wagonDetector.FilteredBodyEntered += OnItemsAdded;
            wagonDetector.FilteredBodyExited += OnItemsRemoved;
        }
        if (foodDetector != null)
        {
            foodDetector.FilteredBodyEntered += OnItemsAdded;
            foodDetector.FilteredBodyExited += OnItemsRemoved;
        }
        if (perchDetector != null)
        {
            perchDetector.FilteredBodyEntered += OnPerchAdded;
            perchDetector.FilteredBodyExited += OnPerchRemoved;
        }
    }

    // By default, entities do not accept being used on them
    public override bool CanAcceptUseFrom(CharacterBody3D user, Interactable source)
    {
        if (source.IsInGroup("food") && !hasItem && !source.IsInGroup("meat"))
        {
            return true;
        }
        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        //if (wagonTarget != null)
        //{
        //    float distanceToWagon = GlobalPosition.DistanceTo(wagonTarget.GlobalPosition);
        //    if (distanceToWagon <= maxWagonStalkDistance && distanceToWagon >= minWagonStalkDistance)
        //        inStalkRange = true;
        //    else
        //        inStalkRange = false;
        //}
    }

    private void OnItemsAdded(Node3D item)
    {
        if (item.IsInGroup("wagon"))
        {
            wagonInRange = true;
            //label.Text = "Wagon in range";
            wagonTarget = item as Wagon;
            hasWagonTarget = true;
            wagonPerches = wagonTarget.perchPoints.ToList();
            circleTimer.Start();
        }
        if (item.IsInGroup("food") && !item.IsInGroup("meat"))
        {
            itemInRange = true;
            //label.Text = "Food in range";
            itemTarget = item as Interactable;
        }
    }

    private void OnItemsRemoved(Node3D item)
    {
        if (item.IsInGroup("wagon"))
        {
            wagonInRange = false;
        }
        if (item.IsInGroup("food") && !item.IsInGroup("meat"))
        {
            if (foodDetector.itemsInside.Count == 0)
            {
                itemInRange = false;
            }
        }
    }

    private void OnPerchAdded(Node3D item)
    {
        if (wagonTarget != null)
        {
            if(wagonPerches.Contains(item))
            {
                return;
            }
        }
        else
        {
            worldPerches.Add(item);
        }
    }

    private void OnPerchRemoved(Node3D item)
    {
        //dont remove wagon perches
        if (worldPerches.Contains(item))
        {
            worldPerches.Remove(item);
        }
    }

    public bool GetRandomWorldPerch()
    {
        if (worldPerches == null || worldPerches.Count == 0) return false;

        perchTarget = worldPerches[(int)(GD.Randi() % worldPerches.Count)];
        targetPosition = perchTarget.GlobalPosition;
        return true;
    }

    public bool GetClosestWagonPerch()
    {
        if (wagonTarget == null) return false;
        if (wagonPerches == null || wagonPerches.Count == 0) return false;

        float closestDistance = 100f;
        foreach(var perch in wagonPerches)
        {
            Vector3 perchDistance = GlobalPosition - perch.GlobalPosition;
            if (perchDistance.Length() < closestDistance)
            {
                closestDistance = perchDistance.Length();
                perchTarget = perch;
            }
        }

        targetPosition = perchTarget.GlobalPosition;
        return true;
    }

    public bool GetCirclePointAroundWagon()
    {
        if (wagonTarget == null) return false;
        
        Vector3 wagonPosition = wagonTarget.GlobalPosition;
        float angle = GD.Randf() * Mathf.Tau;
        
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * circleRadius;
        targetPosition = wagonPosition + offset + new Vector3(0, circleHeight, 0);
        return true;
    }

    public bool OnReachedPerch()
    {
        if (perchTarget == null) return false;

        isOnPerch = true;
        if (wagonTarget != null)
        {
            foreach (var perch in wagonTarget.perchPoints)
            {
                if (perchTarget == perch) {isOnWagonPerch = true; break; }
            }
        }
        return true;
    }

    public bool CheckWagonHasFood()
    {
        if (wagonTarget == null) return false;

        checkedWagon++;
        foreach (var item in foodDetector.itemsInside)
        {
            if (item.IsInGroup("food") && !item.IsInGroup("meat")) // && CheckFoodIsInWagon(item)) // pending tools branch
            {
                wagonHasFood = true;
                itemTarget = item as Interactable;
                return wagonHasFood;
            }
        }

        return wagonHasFood;
    }

    public bool CheckFoodIsInWagon(Node3D item)
    {
        if (wagonTarget == null) return false;

        if (item is Interactable itemInteractable && itemInteractable.Carrier == null) //&& itemInteractable.inWagon == true // pending tools branch
        {
            return true;
        }
        return false;
    }

    public bool ClearWagonTarget()
    {
        wagonTarget = null;
        hasWagonTarget = false;
        wagonHasFood = false;
        wagonPerches.Clear();
        return true;
    }
}