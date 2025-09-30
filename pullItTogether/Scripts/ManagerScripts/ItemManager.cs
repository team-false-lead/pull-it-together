using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// ItemManager handles spawning, picking up, and dropping interactable items in the game world.
public partial class ItemManager : Node3D
{
	[Export] public NodePath placeholdersPath;
	[Export] public NodePath spawnerPath;
	[Export] public bool removePlaceholdersOnSpawn = true;

	private MultiplayerSpawner spawner;
	private Node spawnerParent;

	private static string NewIDFor(Node node) => $"{node.Name}_{Guid.NewGuid():N}";
	private Dictionary<String, Interactable> interactables = new();
	private Dictionary<string, NodePath> ropeProxyByItem = new();
	private Node mapManager;
	//private Node3D levelInstance;
	private bool isMultiplayerSession;
	protected MultiplayerApi multiplayer => GetTree().GetMultiplayer();

	public override void _Ready()
	{
		mapManager = GetTree().CurrentScene.GetNodeOrNull<Node>("%MapManager");
		if (mapManager != null)
		{
			//levelInstance = mapManager.Get("level_instance").As<Node3D>();
			isMultiplayerSession = mapManager.Get("is_multiplayer_session").As<bool>() && multiplayer.HasMultiplayerPeer();
		}
		else
		{
			GD.PrintErr("ItemManager: MapManager not found in scene tree!");
		}

		spawner = GetNodeOrNull<MultiplayerSpawner>(spawnerPath);
		if (spawner != null)
		{
			var path = spawner.SpawnPath;
			if (path == null || path.ToString() == "" || path == ".")
			{
				spawnerParent = spawner;
			}
			else
			{
				spawnerParent = spawner.GetNodeOrNull<Node>(path) ?? spawner;
			}
		}
		else
		{
			GD.PrintErr("ItemManager: Spawner not found at path " + spawnerPath);
			spawnerParent = this;
		}

		bool isServer = !isMultiplayerSession || multiplayer.IsServer();
		if (isServer)
		{
			SetMultiplayerAuthority(multiplayer.GetUniqueId());
			SpawnFromPlaceholders();
			Rpc(nameof(ClientRemovePlaceholders));
			ClientRemovePlaceholders();
		}
		else
		{
			CallDeferred(nameof(ClientRemovePlaceholders));
		}

		multiplayer.PeerConnected += OnPeerConnected;
		spawnerParent.ChildEnteredTree += OnChildEnteredTree;

		GD.Print($"[ItemManager], isServer={isServer}, authority={GetMultiplayerAuthority()}");
	}

	//spawn items at the positions of the placeholders in the scene
	private void SpawnFromPlaceholders()
	{
		if (placeholdersPath == null) return;
		Node3D placeholders = GetNodeOrNull<Node3D>(placeholdersPath);

		foreach (var child in placeholders.GetChildren())
		{
			if (child is not Node3D placeholder) continue;

			String scenePath = placeholder.SceneFilePath;
			if (String.IsNullOrEmpty(scenePath)) continue;

			var scene = ResourceLoader.Load<PackedScene>(scenePath);
			if (scene == null) { GD.PrintErr("ItemManager: Failed to load scene at path " + scenePath); continue; }

			var instance = scene.Instantiate<Node3D>();

			PreAssignIdIfInteractable(instance);
			if (instance is Interactable item)
			{
				AssignId(item);
				//interactables[item.interactableId] = item;
			}

			spawnerParent.AddChild(instance, true);
			foreach (var c in instance.GetChildren())
			{
				if (c is Interactable interactable)
					AssignId(interactable);
			}
			instance.GlobalTransform = placeholder.GlobalTransform;
			instance.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene

			foreach (var childNode in instance.GetChildren())
			{
				if (childNode is Interactable ropeGrabPoint)
				{
					AssignId(ropeGrabPoint);
					interactables[ropeGrabPoint.interactableId] = ropeGrabPoint;
				}
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientRemovePlaceholders()
	{
		if (placeholdersPath == null) return;
		Node3D placeholders = GetNodeOrNull<Node3D>(placeholdersPath);
		if (placeholders != null)
		{
			placeholders.QueueFree();
		}
	}

	private void PreAssignIdIfInteractable(Node node)
	{
		if (node is Interactable item && string.IsNullOrEmpty(item.interactableId))
		{
			item.interactableId = NewIDFor(item);
		}
		foreach (var child in node.GetChildren())
		{
			PreAssignIdIfInteractable(child);
			//if (child is Interactable childInteractable && string.IsNullOrEmpty(childInteractable.interactableId))
			//{
			//	childInteractable.interactableId = NewIDFor(childInteractable);
			//}
		}
	}

	private void AssignId(Interactable item)
	{
		if (string.IsNullOrEmpty(item.interactableId))
		{
			item.interactableId = $"{item.Name}_{Guid.NewGuid():N}"; // Generate unique ID
																	 //item.Name = id; // breaks peer-to-peer?
		}

		interactables[item.interactableId] = item;

		item.TreeExited += () =>
		{
			if (interactables.ContainsKey(item.interactableId))
			{
				interactables.Remove(item.interactableId);
				GD.Print("ItemManager: Tree Exited, Removed interactable with ID " + item.interactableId);
			}
		};
	}

	private void OnChildEnteredTree(Node newChild)
	{
		if (!multiplayer.IsServer()) return;

		if (newChild is Interactable item)
		{
			AssignId(item);
			interactables[item.interactableId] = item;
		}
	}

	private void OnPeerConnected(long id)
	{
		if (!multiplayer.IsServer()) return;

		GD.Print("ItemManager: Peer connected with ID " + id);
		RpcId(id, nameof(ClientRemovePlaceholders));
	}

	private Interactable FindInteractableById(string id)
	{
		//if (!multiplayer.IsServer()) return null; //already should be server if calling
		if (string.IsNullOrEmpty(id)) { GD.Print("ItemManager: Invalid ID"); return null; }
		if (interactables.TryGetValue(id, out var item))
		{
			if (IsInstanceValid(item))
			{
				return item;
			}
			else
			{
				interactables.Remove(id); // Clean up invalid reference
			}

		}
		GD.Print("ItemManager: Interactable with ID " + id + " not found or invalid.");
		return null;
	}

	//get the player controller script by their multiplayer id
	private PlayerController GetPlayerById(long id)
	{
		foreach (var player in GetTree().GetNodesInGroup("players"))
		{
			if (player is PlayerController pc && pc.GetMultiplayerAuthority() == id)
			{
				return pc;
			}
		}
		return null;
	}

	private PlayerController GetLocalPlayer()
	{
		foreach (var player in GetTree().GetNodesInGroup("players"))
		{
			if (player is PlayerController pc)
			{
				return pc;
			}
		}
		return null;
	}

	// host-side item spawn request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request item spawn
	public void RequestSpawnItem(String itemScenePath, Vector3 spawnPosition)
	{
		GD.Print("ItemManager: RequestSpawnItem called for " + itemScenePath);
		if (isMultiplayerSession && !Multiplayer.IsServer()) return; // Only the server should handle spawning

		var itemScene = ResourceLoader.Load<PackedScene>(itemScenePath);
		if (itemScene == null) { GD.Print("Item scene null"); return; }

		var instance = itemScene.Instantiate<RigidBody3D>();
		PreAssignIdIfInteractable(instance);
		spawnerParent.AddChild(instance, true);

		if (instance is Interactable item)
		{
			AssignId(item);
			//interactables[item.interactableId] = item;
		}

		foreach (var child in instance.GetChildren())
		{
			if (child is Interactable interactable)
				AssignId(interactable);
		}
		instance.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene
		instance.GlobalTransform = new Transform3D(Basis.Identity, spawnPosition);

		//if (instance != null)
		//{
		//	if (instance is Interactable item)
		//	{
		//		AssignId(item);
		//		interactables[item.interactableId] = item;
		//	}
		//}
	}

	// item pickup request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request item pickup
	public void RequestPickupItem(string itemId)
	{
		GD.Print("ItemManager: RequestPickupItem called for " + itemId);
		long requesterId;
		if (isMultiplayerSession)
		{
			if (!Multiplayer.IsServer()) return; // Only the server should handle item movement
			requesterId = Multiplayer.GetRemoteSenderId();
		}
		else
		{
			requesterId = multiplayer.GetUniqueId();
		}

		DoPickupItem(itemId, requesterId);

	}

	public void DoPickupItem(string itemId, long requesterId)
	{
		GD.Print("ItemManager: DoPickupItem called for " + itemId);
		var item = FindInteractableById(itemId);
		if (item == null) { GD.Print("Item null"); return; }

		PlayerController carrier;
		if (isMultiplayerSession)
		{
			carrier = GetPlayerById(requesterId);
		}
		else
		{
			carrier = GetLocalPlayer();
		}
		if (carrier == null) { GD.Print("Carrier null"); return; }

		var slot = carrier.GetInventorySlot();
		if (slot == null) { GD.Print("Slot null"); return; }

		if (item.GetParent<Node3D>() != spawnerParent)
		{
			item.GetParent<Node3D>().RemoveChild(item); // Remove from current parent
			spawnerParent.AddChild(item, true); // Reattach to world interactables node
			foreach (var child in item.GetChildren())
			{
				if (child is Interactable interactable)
					AssignId(interactable);
			}
			item.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene
		}

		item.savedMask = item.CollisionMask;
		item.savedLayer = item.CollisionLayer;

		//item.GetParent<Node3D>().RemoveChild(item); // Remove from current parent
		//slot.AddChild(item, true); // Add to carrier's inventory slot

		//CallDeferred(nameof(FinishItemAttach), item.GetPath());

		//item.TopLevel = false; // Make non-top-level to inherit carrier's transform
		//item.Position = Vector3.Zero;
		//item.Rotation = Vector3.Zero;
		//item.Scale = Vector3.One * (1 / GetParent<Node3D>().Scale.X); // Reset scale relative to carrier

		// Prepare item for being carried
		item.TopLevel = true; // Make non-top-level to inherit carrier's transform
		item.Freeze = true;
		item.GravityScale = 0;
		item.LinearVelocity = Vector3.Zero;
		item.AngularVelocity = Vector3.Zero;

		// Disable collisions
		item.CollisionLayer = 0;
		item.CollisionMask = 0;

		item.GlobalTransform = slot.GlobalTransform;
		item.StartFollowingSlot(slot);
	}

	// Finalize item attachment after being added to carrier's inventory slot
	//private void FinishItemAttach(NodePath itemPath)
	//{
	//
	//	var item = GetNodeOrNull<Interactable>(itemPath);
	//	if (item == null) return;
	//
	//	item.TopLevel = false; // Make non-top-level to inherit carrier's transform
	//	item.Position = Vector3.Zero;
	//	item.Rotation = Vector3.Zero;
	//	//item.Scale = Vector3.One * (1 / GetParent<Node3D>().Scale.X); // Reset scale relative to carrier
	//
	//	// Prepare item for being carried
	//	item.Freeze = true;
	//	item.GravityScale = 0;
	//	item.LinearVelocity = Vector3.Zero;
	//	item.AngularVelocity = Vector3.Zero;
	//
	//	// Disable collisions
	//	item.CollisionLayer = 0;
	//	item.CollisionMask = 0;
	//}


	// item drop request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request item drop
	public void RequestDropItem(string itemId, Vector3 dropPosition)
	{
		GD.Print("ItemManager: RequestDropItem called for " + itemId);
		if (isMultiplayerSession && !Multiplayer.IsServer()) return; // Only the server should handle item dropping
		DoDropItem(itemId, dropPosition);
	}

	public void DoDropItem(string itemId, Vector3 dropPosition)
	{
		GD.Print("ItemManager: DoDropItem called for " + itemId);
		var item = FindInteractableById(itemId);
		if (item == null) { GD.Print("Item null"); return; }

		item.StopFollowingSlot();
		if (item.GetParent<Node3D>() != spawnerParent)
		{
			item.GetParent<Node3D>().RemoveChild(item);
			spawnerParent.AddChild(item, true);
			foreach (var child in item.GetChildren())
			{
				if (child is Interactable interactable)
					AssignId(interactable);
			}
			item.SetOwner(GetTree().CurrentScene);
		}

		//item.GetParent<Node3D>().RemoveChild(item); // Remove from carrier's inventory slot
		//spawnerParent.AddChild(item, true); // Reattach to world interactables node

		item.TopLevel = true; // Make top-level to have independent transform
		item.Freeze = false; // Re-enable physics interactions
		item.GravityScale = 1;
		item.CollisionMask = item.savedMask;
		item.CollisionLayer = item.savedLayer;

		item.LinearVelocity = Vector3.Zero;
		item.AngularVelocity = Vector3.Zero;

		item.GlobalTransform = new Transform3D(item.GlobalTransform.Basis, dropPosition); // Set position in the world
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request rope holding
	public async void RequestHoldRope(string itemId, String proxyScenePath)
	{
		GD.Print("ItemManager: RequestHoldRope called for " + itemId);
		if (isMultiplayerSession && !Multiplayer.IsServer()) return;

		long carrierId;
		if (isMultiplayerSession)
		{
			if (!Multiplayer.IsServer()) return; // Only the server should handle item movement
			carrierId = Multiplayer.GetRemoteSenderId();
		}
		else
		{
			carrierId = multiplayer.GetUniqueId();
		}

		await DoHoldRope(itemId, proxyScenePath, carrierId);
	}

	public async Task DoHoldRope(string itemId, String proxyScenePath, long carrierId)
	{
		GD.Print("ItemManager: DoHoldRope called for " + itemId);
		var item = FindInteractableById(itemId) as RopeGrabPoint;
		if (item == null) { GD.Print("Item null"); return; }

		PlayerController carrier;
		if (isMultiplayerSession)
		{
			carrier = GetPlayerById(carrierId);
		}
		else
		{
			carrier = GetLocalPlayer();
		}
		if (carrier == null) { GD.Print("Carrier null"); return; }

		var slot = carrier.GetInventorySlot();
		if (slot == null) { GD.Print("Slot null"); return; }

		var proxyScene = ResourceLoader.Load<PackedScene>(proxyScenePath);
		if (proxyScene == null) { GD.Print("Proxy scene null"); return; }
		var proxy = proxyScene.Instantiate<AnimatableBody3D>();
		if (proxy == null) { GD.Print("Proxy null"); return; }

		proxy.TopLevel = true;
		proxy.SyncToPhysics = false; //control its transform manually
		proxy.GlobalTransform = item.joint.EndCustomLocation.GlobalTransform;

		spawnerParent.AddChild(proxy, true); // Add to the scene
		proxy.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene

		item.AttachToProxyPrep(proxy, carrier);

		RopeProxy proxyScript = proxy as RopeProxy;
		proxyScript.isTweening = true;
		proxyScript.SetFollowTarget(slot);

		var targetPos = slot.GlobalTransform;
		var tween = GetTree().CreateTween();

		tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(proxy, "global_transform", targetPos, 0.25f);
		await ToSignal(tween, "finished");

		proxyScript.isTweening = false;
		proxy.SyncToPhysics = false;
		//proxy.Reparent(slot, keepGlobalTransform: false);
		//ropeProxyByItem[item.interactableId] = proxy.GetPath();
		////slot.AddChild(proxy, true); // Add to carrier's inventory slot
		//proxy.TopLevel = true;
		//proxy.Transform = Transform3D.Identity;
		//proxy.SyncToPhysics = true;

		item.AttachToProxy(proxy);

		ropeProxyByItem[item.interactableId] = proxy.GetPath();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request rope release
	public void RequestReleaseRope(string itemId)
	{
		GD.Print("ItemManager: RequestReleaseRope called for " + itemId);
		if (isMultiplayerSession && !Multiplayer.IsServer()) return; // Only the server should handle item dropping
		DoReleaseRope(itemId);
	}

	public void DoReleaseRope(string itemId)
	{
		GD.Print("ItemManager: DoReleaseRope called for " + itemId);
		var item = FindInteractableById(itemId) as RopeGrabPoint;
		if (item == null) return;

		if (ropeProxyByItem.TryGetValue(item.interactableId, out var proxyPath))
		{
			var proxy = GetNodeOrNull<AnimatableBody3D>(proxyPath);
			var proxyScript = proxy as RopeProxy;
			if (proxy != null)
			{
				proxyScript.ClearFollowTarget();
				proxy.QueueFree();
			}
			ropeProxyByItem.Remove(item.interactableId);
		}

		item.DetachFromProxy();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)] // Allow any peer to request force drop all
	public void RequestForceDropAll()
	{
		if (isMultiplayerSession && !Multiplayer.IsServer())
		{
			RpcId(1, nameof(RequestForceDropAll));
			return;
		}
		RequestForceDropAll();
	}

	public void ForceDropAll()
	{
		foreach (var kv in new List<KeyValuePair<string, Interactable>>(interactables))
		{
			var item = kv.Value;
			if (item == null || !IsInstanceValid(item)) continue;
			if (item.Carrier == null) continue;

			var pos = item.GlobalTransform.Origin;
			if (item is RopeGrabPoint)
			{
				DoReleaseRope(item.interactableId);
			}
			else
			{
				DoDropItem(item.interactableId, pos);
			}
		}
	}
}
