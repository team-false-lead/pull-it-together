using Godot;
using System;
using System.Collections.Generic;

public partial class ItemManager : Node3D
{
	private Dictionary<NodePath, NodePath> ropeProxyByItem = new();
	private Node mapManager;
	private Node3D levelInstance;
	private bool isMultiplayerSession;

	public override void _Ready()
	{
		mapManager = GetTree().CurrentScene.GetNodeOrNull<Node>("%MapManager");
		if (mapManager != null)
		{
			levelInstance = mapManager.Get("level_instance").As<Node3D>();
			isMultiplayerSession = mapManager.Get("is_multiplayer_session").As<bool>();
		}
		else
		{
			GD.PrintErr("ItemManager: MapManager not found in scene tree!");
		}
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
		if (isMultiplayerSession && !Multiplayer.IsServer()) return; // Only the server should handle spawning

		var itemScene = ResourceLoader.Load<PackedScene>(itemScenePath);
		if (itemScene == null) return;

		var instance = itemScene.Instantiate<RigidBody3D>();

		if (instance != null)
		{
			this.AddChild(instance, true);
			instance.Position = spawnPosition;
		}
	}

	// item pickup request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request item pickup
	public void RequestPickupItem(NodePath itemPath)
	{
		long requesterId = 0;
		if (isMultiplayerSession)
		{
			if (!Multiplayer.IsServer()) return; // Only the server should handle item movement
			requesterId = Multiplayer.GetRemoteSenderId();
		}
		else
		{
			requesterId = GetTree().GetMultiplayer().GetUniqueId();
		}

		DoPickupItem(itemPath, requesterId);

	}

	public void DoPickupItem(NodePath itemPath, long requesterId)
	{
		var item = GetNodeOrNull<Interactable>(itemPath);
		if (item == null) return;

		PlayerController carrier;
		if (isMultiplayerSession)
		{
			carrier = GetPlayerById(requesterId);
		}
		else
		{
			carrier = GetLocalPlayer();
		}
		if (carrier == null) return;

		var slot = carrier.GetInventorySlot();
		if (slot == null) return;

		item.savedMask = item.CollisionMask;
		item.savedLayer = item.CollisionLayer;

		item.GetParent<Node3D>().RemoveChild(item); // Remove from current parent
		slot.AddChild(item, true); // Add to carrier's inventory slot

		CallDeferred(nameof(FinishItemAttach), item.GetPath());
	}

	private void FinishItemAttach(NodePath itemPath)
	{
		var item = GetNodeOrNull<Interactable>(itemPath);
		if (item == null) return;

		item.TopLevel = false; // Make non-top-level to inherit carrier's transform
		item.Position = Vector3.Zero;
		item.Rotation = Vector3.Zero;
		//item.Scale = Vector3.One * (1 / GetParent<Node3D>().Scale.X); // Reset scale relative to carrier

		// Prepare item for being carried
		item.Freeze = true;
		item.GravityScale = 0;
		item.LinearVelocity = Vector3.Zero;
		item.AngularVelocity = Vector3.Zero;

		// Disable collisions
		item.CollisionLayer = 0;
		item.CollisionMask = 0;
	}


	// item drop request
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request item drop
	public void RequestDropItem(NodePath itemPath, Vector3 dropPosition)
	{
		if (isMultiplayerSession && !Multiplayer.IsServer()) return; // Only the server should handle item dropping
		DoDropItem(itemPath, dropPosition);
	}

	public void DoDropItem(NodePath itemPath, Vector3 dropPosition)
	{
		var item = GetNodeOrNull<Interactable>(itemPath);
		if (item == null) return;

		item.GetParent<Node3D>().RemoveChild(item); // Remove from carrier's inventory slot
		this.AddChild(item, true); // Reattach to world interactables node

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
	public void RequestHoldRope(NodePath itemPath, String proxyScenePath)
	{
		if (isMultiplayerSession && !Multiplayer.IsServer()) return;

		long carrierId = 0;
		if (isMultiplayerSession)
		{
			if (!Multiplayer.IsServer()) return; // Only the server should handle item movement
			carrierId = Multiplayer.GetRemoteSenderId();
		}
		else
		{
			carrierId = GetTree().GetMultiplayer().GetUniqueId();
		}

		DoHoldRope(itemPath, proxyScenePath, carrierId);
	}

	public void DoHoldRope(NodePath itemPath, String proxyScenePath, long carrierId)
	{
		var item = GetNodeOrNull<RopeGrabPoint>(itemPath);
		if (item == null) return;

		PlayerController carrier;
		if (isMultiplayerSession)
		{
			carrier = GetPlayerById(carrierId);
		}
		else
		{
			carrier = GetLocalPlayer();
		}
		if (carrier == null) return;

		var slot = carrier.GetInventorySlot();
		if (slot == null) return;

		var proxyScene = ResourceLoader.Load<PackedScene>(proxyScenePath);
		if (proxyScene == null) return;
		var proxy = proxyScene.Instantiate<AnimatableBody3D>();
		if (proxy == null) return;

		slot.AddChild(proxy, true); // Add to carrier's inventory slot
		proxy.Transform = Transform3D.Identity;
		proxy.SyncToPhysics = true;
		proxy.TopLevel = true;

		ropeProxyByItem[itemPath] = proxy.GetPath();

		item.AttachToProxy(proxy, carrier);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)] // Allow any peer to request rope release
	public void RequestReleaseRope(NodePath itemPath)
	{
		if (isMultiplayerSession && !Multiplayer.IsServer()) return; // Only the server should handle item dropping
		DoReleaseRope(itemPath);
	}

	public void DoReleaseRope(NodePath itemPath)
	{
		var item = GetNodeOrNull<RopeGrabPoint>(itemPath);
		if (item == null) return;

		if (ropeProxyByItem.TryGetValue(itemPath, out var proxyPath))
		{
			var proxy = GetNodeOrNull<AnimatableBody3D>(proxyPath);
			if (proxy != null)
			{
				proxy.QueueFree();
			}
			ropeProxyByItem.Remove(itemPath);
		}

		item.DetachFromProxy();
	}
}
