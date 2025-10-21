using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// ItemManager handles spawning, picking up, and dropping interactable items in the game world.
public partial class ItemManager : Node3D
{
	[Export] public NodePath placeholdersPath;

	private Dictionary<string, Interactable> interactables = new();
	private Dictionary<string, Entity> entities = new();
	private Dictionary<string, NodePath> ropeProxyByItem = new();
	private Node mapManager;
	private bool isMultiplayerSession;
	protected MultiplayerApi multiplayer => GetTree().GetMultiplayer();

	// Initialize the ItemManager, set up multiplayer, and spawn items from placeholders
	public override void _Ready()
	{
		multiplayer.PeerConnected += OnPeerConnected;
		this.ChildEnteredTree += OnChildEnteredTree;
		CallDeferred(nameof(InitializeAfterReady));
		//GD.Print($"[ItemManager], isServer={isServer}, authority={GetMultiplayerAuthority()}"); // Debug authority
	}

	// Deferred initialization to ensure the scene is fully loaded
	private void InitializeAfterReady()
	{
		// Get the MapManager node from the current scene and check multiplayer status
		mapManager = GetTree().CurrentScene.GetNodeOrNull<Node>("%MapManager");
		if (mapManager != null)
		{
			isMultiplayerSession = mapManager.Get("is_multiplayer_session").As<bool>() && multiplayer.HasMultiplayerPeer();
			mapManager.GetChild(0).ChildEnteredTree += AddPlayerInteractable;
		}
		else
		{
			GD.PrintErr("ItemManager: MapManager not found in scene tree!");
		}

		// If not in a multiplayer session, spawn items from placeholders immediately
		// If in a multiplayer session, only the server spawns items and informs clients
		// Clients remove placeholders when they join
		bool isServerOrOffline = !isMultiplayerSession || multiplayer.IsServer();
		if (isServerOrOffline)
		{
			SpawnFromPlaceholders();
			if (multiplayer.HasMultiplayerPeer())
				Rpc(nameof(ClientRemovePlaceholders)); // inform clients to remove placeholders
			ClientRemovePlaceholders(); // also remove from host
		}
		// clients will remove placeholders when they join via OnPeerConnected
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Input.IsActionJustPressed("drop")) // Q  // actually not drop but print dictionary contents for debugging
		{
			if (isMultiplayerSession && !multiplayer.IsServer()) return;
			PrintDictionaryContents();
		}
	}

	public void PrintDictionaryContents()
	{
		GD.Print("Interactables Dictionary:----------------------");
		foreach (var kvp in interactables)
		{
			GD.Print($"ID: {kvp.Key}, Item: {kvp.Value.Name}");
		}

		GD.Print("Entities Dictionary:---------------------------");
		foreach (var kvp in entities)
		{
			GD.Print($"ID: {kvp.Key}, Entity: {kvp.Value.Name}");
		}
	}

	// Clean up event connections and dictionaries when the ItemManager is removed from the scene tree
	public override void _ExitTree()
	{
		multiplayer.PeerConnected -= OnPeerConnected;
		this.ChildEnteredTree -= OnChildEnteredTree;
		interactables.Clear();
		entities.Clear();
		ropeProxyByItem.Clear();
	}

	// Spawn interactable items based on placeholder nodes in the scene
	private void SpawnFromPlaceholders()
	{
		Node3D placeholders = GetNodeOrNull<Node3D>(placeholdersPath);
		if (placeholders == null)
		{
			GD.Print("ItemManager: No placeholders path set, skipping items spawn.");
			return;
		}

		foreach (var child in placeholders.GetChildren())
		{
			if (child is not Node3D placeholder) continue; // Only process Node3D children

			string placeholderScenePath = placeholder.SceneFilePath;
			if (string.IsNullOrEmpty(placeholderScenePath)) continue; // Skip if no scene path

			var placeholderScene = ResourceLoader.Load<PackedScene>(placeholderScenePath);
			if (placeholderScene == null) { GD.PrintErr("ItemManager: Failed to load scene at path " + placeholderScenePath); continue; }

			var instance = placeholderScene.Instantiate<Node3D>();

			PreAssignId(instance);// assign IDs before adding to scene

			this.AddChild(instance, true);
			instance.GlobalTransform = placeholder.GlobalTransform;
			instance.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene
		}
	}

	// Recursively assign IDs to interactable or Entity items in a node and its children
	private void PreAssignId(Node node)
	{
		if (node is Interactable item) // assign interactable ids
		{
			AssignInteractableId(item);
		}

		if (node is Entity entity)// also assign entity ids
		{
			AssignEntityId(entity);
		}

		foreach (var child in node.GetChildren()) // recurse into children
		{
			PreAssignId(child);
		}
	}

	// Assign a unique ID to an interactable item if it doesn't already have one, and track it in the dictionary
	private void AssignInteractableId(Interactable item)
	{
		if (string.IsNullOrEmpty(item.interactableId))
		{
			item.interactableId = $"{item.Name}_{Guid.NewGuid():N}"; // Generate unique ID
																	 //item.Name = id; // breaks peer-to-peer? and it keeps auto moving this line really far right
		}

		// Ensure the ID is unique and track the item
		if (interactables.TryGetValue(item.interactableId, out var existing))
		{
			// Handle case where ID already exists but the instance is invalid (e.g., was freed)
			if (IsInstanceValid(existing))
			{
				if (existing == item) return; // already tracked
				GD.PrintErr("ItemManager: Duplicate interactable ID: " + item.interactableId);
				return;
			}
			// Replace invalid reference with the new item
			interactables[item.interactableId] = item;
			return;
		}
		// New ID, add to dictionary
		interactables[item.interactableId] = item;

		// Set up cleanup on item removal
		item.TreeExited += () =>
		{
			if (interactables.Remove(item.interactableId))
			{
				GD.Print("ItemManager: Tree Exited, Removed interactable with ID " + item.interactableId);
			}
		};
	}

	// Assign a unique ID to an entity if it doesn't already have one, and track it in the dictionary
	private void AssignEntityId(Entity entity)
	{
		if (string.IsNullOrEmpty(entity.entityId))
		{
			entity.entityId = $"{entity.Name}_{Guid.NewGuid():N}"; // Generate unique ID
		}

		// Ensure the ID is unique and track the item
		if (entities.TryGetValue(entity.entityId, out var existing))
		{
			// Handle case where ID already exists but the instance is invalid (e.g., was freed)
			if (IsInstanceValid(existing))
			{
				if (existing == entity) return; // already tracked
				GD.PrintErr("ItemManager: Duplicate entity ID: " + entity.entityId);
				return;
			}
			// Replace invalid reference with the new item
			entities[entity.entityId] = entity;
			return;
		}
		// New ID, add to dictionary
		entities[entity.entityId] = entity;

		// Set up cleanup on item removal
		entity.TreeExited += () =>
		{
			if (entities.Remove(entity.entityId))
			{
				GD.Print("ItemManager: Tree Exited, Removed entity with ID " + entity.entityId);
			}
		};
	}

	// Client-side removal of placeholder nodes
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

	// Handle new children added to the ItemManager, assign IDs if they are interactables
	private void OnChildEnteredTree(Node newChild)
	{
		if (!multiplayer.IsServer()) return;

		if (newChild is Interactable item)
		{
			// Ensure the item has a unique ID and is tracked 
			if (string.IsNullOrEmpty(item.interactableId) || !interactables.TryGetValue(item.interactableId, out var existing) || !IsInstanceValid(existing))
				AssignInteractableId(item);
		}

		if (newChild is Entity entity)
		{
			if (string.IsNullOrEmpty(entity.entityId) || !entities.TryGetValue(entity.entityId, out var existing) || !IsInstanceValid(existing))
				AssignEntityId(entity);
		}
	}

	private void AddPlayerInteractable(Node newChild)
	{
		GD.Print("ItemManager: AddPlayerInteractable called for " + newChild.Name);
		if (newChild is not CharacterBody3D player) return;
		//player.IsNodeReady();
		var PC = player as PlayerController;
		var PlayerInteractable = PC.GetOwnInteractable();
		AssignInteractableId(PlayerInteractable); // ensure their own interactable has an ID assigned
		GD.Print("ItemManager: Added player interactable with ID " + PlayerInteractable.interactableId);
		// Set up cleanup on item removal
		PlayerInteractable.TreeExited += () =>
		{
			if (interactables.Remove(PlayerInteractable.interactableId))
			{
				GD.Print("ItemManager: Tree Exited, Removed player interactable with ID " + PlayerInteractable.interactableId);
			}
		};
	}

	// Handle new peers connecting to the multiplayer session, inform them to remove placeholders
	private void OnPeerConnected(long id)
	{
		if (!multiplayer.IsServer()) return;

		GD.Print("ItemManager: Peer connected with ID " + id);
		RpcId(id, nameof(ClientRemovePlaceholders));
	}

	//find the interactable item by its unique id
	private Interactable FindInteractableById(string id)
	{
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

	private Entity FindEntityById(string id)
	{
		if (string.IsNullOrEmpty(id)) { GD.Print("ItemManager: Invalid ID"); return null; }
		if (entities.TryGetValue(id, out var entity))
		{
			if (IsInstanceValid(entity))
			{
				return entity;
			}
			else
			{
				entities.Remove(id); // Clean up invalid reference
			}
		}
		GD.Print("ItemManager: Entity with ID " + id + " not found or invalid.");
		return null;
	}

	//get the player controller script by their multiplayer id, or return singleplayer player script
	private PlayerController GetPlayerControllerById(long id)
	{
		var playersArray = GetTree().GetNodesInGroup("players");
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

	// item spawn request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request item spawn
	public void RequestSpawnItem(string requestingItemId)
	{
		GD.Print("ItemManager: RequestSpawnItem called from " + requestingItemId);
		if (isMultiplayerSession && !multiplayer.IsServer()) return; // Only the server should handle spawning
		DoSpawnItem(requestingItemId);
	}

	// perform the item spawn logic on the server
	public void DoSpawnItem(string itemId)
	{
		GD.Print("ItemManager: DoSpawnItem called for " + itemId);
		var requestingItem = FindInteractableById(itemId);
		if (requestingItem == null) { GD.Print("Requesting Item null"); return; }

		var itemToSpawnScene = requestingItem.SpawnOnUseScene;
		if (itemToSpawnScene == null) { GD.Print("SpawnOnUseScene null"); return; }

		var instance = itemToSpawnScene.Instantiate<RigidBody3D>(); // assuming all interactables and entities are RigidBody3D or derived
		PreAssignId(instance);

		this.AddChild(instance, true);
		instance.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene

		Vector3 dropPosition = GetDropPosition(requestingItem); // drop in front of the user of the item
		instance.GlobalTransform = new Transform3D(Basis.Identity, dropPosition);
		requestingItem.QueueFree(); // remove the used item
	}

	// item pickup request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request item pickup
	public void RequestPickupItem(string itemId)
	{
		GD.Print("ItemManager: RequestPickupItem called for " + itemId);
		if (isMultiplayerSession && !multiplayer.IsServer()) return; // Only the server should handle item movement

		long requesterId = multiplayer.GetRemoteSenderId();
		if (requesterId == 0) // host called
		{
			requesterId = multiplayer.GetUniqueId(); // fallback to local player in singleplayer
		}

		DoPickupItem(itemId, requesterId);
	}

	// perform the item pickup logic on the server
	public void DoPickupItem(string itemId, long requesterId)
	{
		GD.Print("ItemManager: DoPickupItem called for " + itemId);

		var item = FindInteractableById(itemId);
		if (item == null) { GD.Print("Item null"); return; }

		PlayerController carrier = GetPlayerControllerById(requesterId);
		if (carrier == null) { GD.Print("Carrier null"); return; }

		var slot = carrier.GetInventorySlot();
		if (slot == null) { GD.Print("Slot null"); return; }

		// Reparent item to the ItemManager if it's not already a child
		if (item.GetParent<Node3D>() != this)
		{
			item.GetParent<Node3D>().RemoveChild(item); // Remove from current parent
			this.AddChild(item, true); // Reattach to this node, will get assigned an ID if needed
			item.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene
        }

		// Save current collision settings
		item.savedMask = item.CollisionMask;
		item.savedLayer = item.CollisionLayer;

		// Prepare item for being carried
		//item.TopLevel = true; //items are staying on this node for server consistency (no longer moving to character slot tree), no longer need to change this
		item.Freeze = true;
		item.GravityScale = 0;
		item.LinearVelocity = Vector3.Zero;
		item.AngularVelocity = Vector3.Zero;

		// Disable collisions
		item.CollisionLayer = 0;
		item.CollisionMask = 0;

		item.StartFollowingSlot(slot);
		item.Carrier = carrier;
	}

	// item drop request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request item drop
	public void RequestDropItem(string itemId)
	{
		GD.Print("ItemManager: RequestDropItem called for " + itemId);
		if (isMultiplayerSession && !multiplayer.IsServer()) return; // Only the server should handle item dropping
		DoDropItem(itemId);
	}

	// perform the item drop logic on the server
	public void DoDropItem(string itemId)
	{
		GD.Print("ItemManager: DoDropItem called for " + itemId);

		var item = FindInteractableById(itemId);
		if (item == null) { GD.Print("Item null"); return; }

		if (item.GetParent<Node3D>() != this)
		{
			item.GetParent<Node3D>().RemoveChild(item);
			this.AddChild(item, true);
			item.SetOwner(GetTree().CurrentScene);
		}

		item.StopFollowingSlot();// called before resetting physics

		// prep item for world physics
		item.Freeze = false; // Re-enable physics interactions
		item.GravityScale = 1;
		item.LinearVelocity = Vector3.Zero;
		item.AngularVelocity = Vector3.Zero;

		// Restore collision settings
		item.CollisionMask = item.savedMask;
		item.CollisionLayer = item.savedLayer;

		Vector3 dropPosition = GetDropPosition(item);
		item.GlobalTransform = new Transform3D(item.GlobalTransform.Basis, dropPosition); // Set position in the world
		item.Carrier = null;
	}

	// Calculate a safe drop position in front of the carrier // still can fall through floor
	private Vector3 GetDropPosition(Interactable item)
	{
		PlayerController carrier = item.Carrier as PlayerController;
		if (carrier == null) return item.GlobalTransform.Origin; // fallback to current position if no carrier

		// screen center
		Viewport vp = carrier.GetViewport();
		Vector2 center = vp.GetVisibleRect().Size * 0.5f;

		//raycast from camera to drop in front of carrier
		Vector3 origin = carrier.camera.GlobalTransform.Origin;
		Vector3 dir = -carrier.camera.GlobalTransform.Basis.Z.Normalized();
		Vector3 to = origin + dir * carrier.interactRange;

		// Raycast to find a safe drop position
		var state = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(origin, to);
		query.CollisionMask = item.savedMask; // Use saved mask to avoid dropping inside other objects
		query.Exclude = new Godot.Collections.Array<Rid> { item.GetRid(), carrier.GetRid() }; // ignore self and carrier
		var hit = state.IntersectRay(query);

		// Determine drop position based on raycast result
		Vector3 dropPosition;
		Vector3 normal;

		if (hit.Count > 0) // Hit something, drop slightly above the surface
		{
			dropPosition = (Vector3)hit["position"];
			normal = ((Vector3)hit["normal"]).Normalized();
		}
		else // No hit, drop at max range
		{
			dropPosition = to; // No hit, drop at max range
			normal = Vector3.Up; // Default to up if no hit
		}

		// Offset by clearance to avoid clipping into surfaces
		float clearance = item.dropClearance;
		dropPosition += normal * clearance;

		return dropPosition;
	}

	// rope hold request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request rope holding
	public async void RequestHoldRope(string itemId)
	{
		GD.Print("ItemManager: RequestHoldRope called for " + itemId);
		if (isMultiplayerSession && !multiplayer.IsServer()) return;

		long requesterId = multiplayer.GetRemoteSenderId();
		if (requesterId == 0) // host called
		{
			requesterId = multiplayer.GetUniqueId(); // fallback to local player in singleplayer
		}

		await DoHoldRope(itemId, requesterId);
	}

	// perform the rope hold logic on the server
	public async Task DoHoldRope(string itemId, long requesterId)
	{
		GD.Print("ItemManager: DoHoldRope called for " + itemId);

		var grabPoint = FindInteractableById(itemId) as RopeGrabPoint;
		if (grabPoint == null) { GD.Print("Grab Point null"); return; }

		PlayerController carrier = GetPlayerControllerById(requesterId);
		if (carrier == null) { GD.Print("Carrier null"); return; }

		var slot = carrier.GetInventorySlot();
		if (slot == null) { GD.Print("Slot null"); return; }

		if (ropeProxyByItem.ContainsKey(grabPoint.interactableId))
		{
			GD.Print("ItemManager: Rope already held for " + itemId);
			return; // already held by someone, prevent stealing
		}

		// create proxy for player inventory and tween to slot
		var proxyScene = ResourceLoader.Load<PackedScene>(grabPoint.ropeProxyScene.ResourcePath);
		if (proxyScene == null) { GD.Print("Proxy scene null"); return; }
		var proxy = proxyScene.Instantiate<AnimatableBody3D>();
		if (proxy == null) { GD.Print("Proxy null"); return; }

		this.AddChild(proxy, true); // Add to the scene
		proxy.SetOwner(GetTree().CurrentScene); // Ensure the instance is owned by the current scene

		proxy.TopLevel = true;
		proxy.SyncToPhysics = false; //control its transform manually
		proxy.GlobalTransform = grabPoint.joint.EndCustomLocation.GlobalTransform;

		grabPoint.AttachToProxyPrep(proxy, carrier);

		var proxyScript = proxy as RopeProxy;
		proxyScript.isTweening = true;
		proxyScript.SetFollowTarget(slot);

		var targetPos = slot.GlobalPosition;

		// Tween to the inventory slot
		var tween = GetTree().CreateTween();
		tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(proxy, "global_position", targetPos, 0.25f);
		await ToSignal(tween, "finished");

		proxyScript.isTweening = false;
		proxy.SyncToPhysics = false;

		grabPoint.AttachToProxy(proxy);

		ropeProxyByItem[grabPoint.interactableId] = proxy.GetPath();
	}

	// rope release request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request rope release
	public void RequestReleaseRope(string itemId)
	{
		GD.Print("ItemManager: RequestReleaseRope called for " + itemId);
		if (isMultiplayerSession && !multiplayer.IsServer()) return; // Only the server should handle item dropping
		DoReleaseRope(itemId);
	}

	// perform the rope release logic on the server
	public void DoReleaseRope(string itemId)
	{
		GD.Print("ItemManager: DoReleaseRope called for " + itemId);
		var grabPoint = FindInteractableById(itemId) as RopeGrabPoint;
		if (grabPoint == null) return;

		if (ropeProxyByItem.TryGetValue(grabPoint.interactableId, out var proxyPath))
		{
			var proxy = GetNodeOrNull<AnimatableBody3D>(proxyPath);
			if (proxy != null)
			{
				var proxyScript = proxy as RopeProxy;
				proxyScript.ClearFollowTarget();
				proxy.QueueFree();
			}
			ropeProxyByItem.Remove(grabPoint.interactableId);
		}

		grabPoint.DetachFromProxy();
	}

	// force drop all items on all players request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] // Allow any peer to request force drop all
	public void RequestForceDropAll()
	{
		//GD.Print("ItemManager: RequestForceDropAll called");
		// only server should execute
		if (isMultiplayerSession && !multiplayer.IsServer()) return;
		ForceDropAll(); // execute locally on server or singleplayer
	}

	// force drop all items on all players
	public void ForceDropAll()
	{
		foreach (var kv in interactables)
		{
			var item = kv.Value;
			if (item == null || !IsInstanceValid(item)) continue;
			if (item.Carrier == null) continue;

			if (item is RopeGrabPoint)
			{
				DoReleaseRope(item.interactableId);
				(item.Carrier as PlayerController)?.RemoveTetherAnchor();
			}
			else
			{
				DoDropItem(item.interactableId);
			}
		}
	}

	// logic for cooking food item request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request cooking food
	public void RequestCookFood(string campfireId, string foodId)
	{
		GD.Print("ItemManager: RequestCookFood called for " + foodId);
		if (isMultiplayerSession && !multiplayer.IsServer()) return; // Only the server should handle cooking
		DoCookFood(campfireId, foodId);
	}

	public void DoCookFood(string campfireId, string foodId)
	{
		var food = FindInteractableById(foodId) as Food;
		if (food == null) { GD.Print("Food null"); return; }

		var campfire = FindEntityById(campfireId) as Campfire;
		if (campfire == null) { GD.Print("Campfire null"); return; }

		if (food.isCooked)
		{
			GD.Print("ItemManager: Food: " + food.Name + " is already cooked.");
			return;
		}

		GD.Print("Campfire: Accepted use from " + food.Name);
		if (!food.isCooked)
		{
			food.isCooked = true;
			food.label3D.Text = "(Cooked)";
			//var mat = food.currentMesh.GetActiveMaterial(0) as StandardMaterial3D;
			//if (mat == null)
			//{
			//	mat = new StandardMaterial3D(); // create new if none found
			//	food.currentMesh.SetSurfaceOverrideMaterial(0, mat);
			//}
			//else
			//{
			//	mat = mat.Duplicate() as StandardMaterial3D; // duplicate to avoid changing original
			//	food.currentMesh.SetSurfaceOverrideMaterial(0, mat);
			//}
			//mat.AlbedoColor = new Color(0.3f, 0.18f, 0.1f); // darken color to indicate cooked
		}
		//add logic for cooking food here
		//remove and replace held item with cooked version
		//assume food item has a cookedMesh assigned
		campfire.usesLeft--;
		if (campfire.usesLeft <= 0)
		{
			campfire.QueueFree(); // Remove campfire after uses are exhausted
		}
	}


	// logic for feeding food item to player request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request feeding
	public void RequestFeedTarget(string itemId, string targetPeerId)
	{
		GD.Print("ItemManager: RequestFeedTarget called for " + itemId + " to peer ID " + targetPeerId);
		GD.Print("ItemManager: isMultiplayerSession=" + isMultiplayerSession + ", IsServer=" + multiplayer.IsServer());
		if (isMultiplayerSession && !multiplayer.IsServer()) return; // Only the server should handle feeding
		DoFeedTarget(itemId, targetPeerId);
	}

	// logic for feeding food item to target player
	public void DoFeedTarget(string itemId, string targetPeerId)
	{
		var food = FindInteractableById(itemId) as Food;
		if (food == null) return;

		long targetPeerIdLong = long.Parse(targetPeerId);
		PlayerController targetPlayer = GetPlayerControllerById(targetPeerIdLong);
		if (targetPlayer == null) { GD.Print("Target Player null"); return; }

		GD.Print("Food: " + food.Name + " is cooked: " + food.isCooked);
		if (food.isCooked)
		{
			//restore cooked values
			GD.Print("Food: " + food.Name + " fed to " + targetPlayer.Name);
			GD.Print("Food: Restoring " + food.healthAddedCooked + " health and " + food.potentialEnergyAddedCooked + " energy.");
			//targetPlayer.ChangeCurrentHealth(food.healthAddedCooked);
			//targetPlayer.ChangeMaxEnergy(food.potentialEnergyAddedCooked);
			targetPlayer.RpcId(targetPeerIdLong, nameof(PlayerController.ChangeCurrentHealth), food.healthAddedCooked);
			targetPlayer.RpcId(targetPeerIdLong, nameof(PlayerController.ChangeMaxEnergy), food.potentialEnergyAddedCooked);
		}
		else
		{
			//restore raw values
			GD.Print("Food: " + food.Name + " fed to " + targetPlayer.Name);
			GD.Print("Food: Restoring " + food.healthAddedRaw + " health and " + food.potentialEnergyAddedRaw + " energy.");
			//targetPlayer.ChangeCurrentHealth(food.healthAddedRaw);
			//targetPlayer.ChangeMaxEnergy(food.potentialEnergyAddedRaw);
			targetPlayer.RpcId(targetPeerIdLong, nameof(PlayerController.ChangeCurrentHealth), food.healthAddedRaw);
			targetPlayer.RpcId(targetPeerIdLong, nameof(PlayerController.ChangeMaxEnergy), food.potentialEnergyAddedRaw);
		}

		food.QueueFree(); // remove the food item after feeding
	}

	// logic for repairing wheel request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request repairing wheel
	public void RequestRepairWheel(string wheelId, string plankId)
	{
		GD.Print("ItemManager: RequestRepairWheel called for " + wheelId);
		if (isMultiplayerSession && !multiplayer.IsServer()) return; // Only the server should handle repairing
		DoRepairWheel(wheelId, plankId);
	}

	// logic for repairing wheel
	public void DoRepairWheel(string wheelId, string plankId)
	{
		var plank = FindInteractableById(plankId) as Interactable;
		if (plank == null) { GD.Print("Plank null"); return; }

		var wheel = FindEntityById(wheelId) as Wheel;
		if (wheel == null) { GD.Print("Wheel null"); return; }

		GD.Print("Wheel: Accepted use from " + plank.Name);
		GD.Print("Wheel: Current Health " + wheel.currentHealth + "/" + wheel.maxHealth);
		wheel.currentHealth += wheel.repairAmount;
		GD.Print("Wheel: Updated Health " + wheel.currentHealth + "/" + wheel.maxHealth);
		if (wheel.currentHealth >= wheel.maxHealth)
		{
			wheel.currentHealth = wheel.maxHealth;
			GD.Print("Wheel: " + wheel.Name + " has been fully repaired!");
		}
		else
		{
			GD.Print("Wheel: " + wheel.Name + " repaired to " + wheel.currentHealth + "/" + wheel.maxHealth);
		}
		//add logic for repairing wheel here
		plank.QueueFree(); // remove the used plank
	}

	// logic for damaging wheel request 
	public void DoDamageWheel(string wheelId, int damageAmount)
	{
		//GD.Print("ItemManager: DoDamageWheel called for " + wheelId);
	}
}
