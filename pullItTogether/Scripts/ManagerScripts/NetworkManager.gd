extends Node
class_name NetworkManager

enum PeerMode { STEAM, LOCAL, NONE }

signal session_started(role) # "host" | "client"
signal session_ended()
signal peer_connected(id)
signal peer_disconnected(id)

var peer : MultiplayerPeer = null
var mode : PeerMode = PeerMode.NONE # 0=STEAM, 1=LOCAL, 2=NONE
var _signals_hooked := false
var _is_leaving := false
var _stored_initial_peer : MultiplayerPeer = null
var default_max_players: int = 4

# ---------- General ----------
func _ready() -> void:
	_stored_initial_peer = multiplayer.multiplayer_peer
	# print(multiplayer.multiplayer_peer)
	if multiplayer.has_multiplayer_peer():
		multiplayer.multiplayer_peer = null # reset on scene change
	peer = null
	mode = PeerMode.NONE
	_signals_hooked = false
	_is_leaving = false


func get_peer() -> MultiplayerPeer:
	return peer

func update_multiplayer_peer() -> void:
	multiplayer.multiplayer_peer = peer

func reset_multiplayer_peer() -> void:
	multiplayer.multiplayer_peer = _stored_initial_peer

# 0=STEAM, 1=LOCAL, 2=NONE
func set_peer_mode(peer_mode : PeerMode) -> void:
	mode = peer_mode
	match mode:
		PeerMode.STEAM:
			peer = SteamMultiplayerPeer.new()
		PeerMode.LOCAL:
			peer = ENetMultiplayerPeer.new()
		PeerMode.NONE:
			peer = null
	print_rich("Peer Mode Set To: [color=yellow]", PeerMode.keys()[mode], "[/color]")

# ---------- Steam (Expresso Bits) ----------
func host_steam() -> bool:
	#leave() # reset any existing session
	_remove_transport_peer()
	set_peer_mode(PeerMode.STEAM)
	var sp: SteamMultiplayerPeer = peer
	var err: int = sp.create_host(0)  # 0 = virtual port
	if err != OK:
		push_error("Steam create_host() failed: %s" % err)
		peer = null
		mode = PeerMode.NONE
		_signals_hooked = false
		return false
	_attach_peer()
	emit_signal("session_started", "public host")
	return true

func join_steam(host_steam_id_64 : int) -> bool:
	#leave() # reset any existing session
	_remove_transport_peer()
	set_peer_mode(PeerMode.STEAM)
	var sp: SteamMultiplayerPeer = peer
	var err: int
	if sp.has_method("join_host"):
		err = sp.join_host(host_steam_id_64, 0)
	else:
		err = sp.create_client(host_steam_id_64, 0)
	if err != OK:
		push_error("Steam client connect failed: %s" % err)
		peer = null
		mode = PeerMode.NONE
		_signals_hooked = false
		return false
	_attach_peer()
	if not await _wait_for_connection(10.0):
		return false
	emit_signal("session_started", "public client")
	return true

# ---------- Local ENet ----------
# default to localhost (same machine), change address as needed
func host_local(address : String = "127.0.0.1", port : int = 2450, max_clients : int = 4) -> bool:
	#leave() # reset any existing session
	_remove_transport_peer()
	set_peer_mode(PeerMode.LOCAL)
	peer.set_bind_ip(address) # only for Enet
	var err: int = peer.create_server(port, max_clients)
	if err != OK:
		push_error("ENet create_server() failed: %s" % err)
		peer = null
		mode = PeerMode.NONE
		_signals_hooked = false
		return false
	_attach_peer()
	emit_signal("session_started", "local host")
	print("Hosting with LAN IP: %s\nPort: %d" % [address, port])
	return true

# default to localhost (same machine), change address as needed
func join_local(address : String = "127.0.0.1", port : int = 2450) -> bool:
	#leave() # reset any existing session
	_remove_transport_peer()
	set_peer_mode(PeerMode.LOCAL)
	var err: int = peer.create_client(address, port)
	if err != OK:
		push_error("ENet create_client() failed: %s" % err)
		peer = null
		mode = PeerMode.NONE
		_signals_hooked = false
		return false
	_attach_peer()
	if not await _wait_for_connection(10.0):
		return false
	emit_signal("session_started", "local client")
	print("Connecting to IP: %s\nPort: %d" % [address, port])
	return true

# ---------- Common ----------
# helper to remove transport thingies, similar to leave but without signals or notifications, used in start/join
func _remove_transport_peer() -> void:
	# Disconnect signals
	if _signals_hooked:
		var mp: MultiplayerAPI = multiplayer
		if mp.peer_connected.is_connected(Callable(self, "_on_peer_connected")):
			mp.peer_connected.disconnect(Callable(self, "_on_peer_connected"))
		if mp.peer_disconnected.is_connected(Callable(self, "_on_peer_disconnected")):
			mp.peer_disconnected.disconnect(Callable(self, "_on_peer_disconnected"))
		if mp.connection_failed.is_connected(Callable(self, "_on_connection_failed")):
			mp.connection_failed.disconnect(Callable(self, "_on_connection_failed"))
		if mp.server_disconnected.is_connected(Callable(self, "_on_server_disconnected")):
			mp.server_disconnected.disconnect(Callable(self, "_on_server_disconnected"))
	_signals_hooked = false

	# Close peer connection
	if peer:
		if mode == PeerMode.STEAM and peer.has_method("leave_lobby"):
			peer.leave_lobby() # steam specific
		elif peer.has_method("close"):
			peer.close()
	
	multiplayer.multiplayer_peer = null
	peer = null
	mode = PeerMode.NONE
	_is_leaving = false

# leave session
func leave(notify_peers: bool = true) -> void:
	if _is_leaving:
		return
	_is_leaving = true

	# Disconnect signals
	if _signals_hooked:
		var mp: MultiplayerAPI = multiplayer
		if mp.peer_connected.is_connected(Callable(self, "_on_peer_connected")):
			mp.peer_connected.disconnect(Callable(self, "_on_peer_connected"))
		if mp.peer_disconnected.is_connected(Callable(self, "_on_peer_disconnected")):
			mp.peer_disconnected.disconnect(Callable(self, "_on_peer_disconnected"))
		if mp.connection_failed.is_connected(Callable(self, "_on_connection_failed")):
			mp.connection_failed.disconnect(Callable(self, "_on_connection_failed"))
		if mp.server_disconnected.is_connected(Callable(self, "_on_server_disconnected")):
			mp.server_disconnected.disconnect(Callable(self, "_on_server_disconnected"))
	_signals_hooked = false

	if multiplayer.has_multiplayer_peer():
		if notify_peers and multiplayer.is_server():
			rpc("_host_disconnect")
			await get_tree().process_frame # wait for rpc to send
			await get_tree().process_frame

	emit_signal("session_ended")

	# Close peer connection
	if peer:
		if mode == PeerMode.STEAM and peer.has_method("leave_lobby"):
			peer.leave_lobby() # steam specific
		elif peer.has_method("close"):
			peer.close()
	
	multiplayer.multiplayer_peer = null
	peer = null
	mode = PeerMode.NONE
	_is_leaving = false

@rpc("authority", "reliable")
func _host_disconnect() -> void:
	print("Host disconnected...")
	leave(false)

func _attach_peer() -> void:
	#connect signals
	update_multiplayer_peer()
	if _signals_hooked:
		push_warning("NetworkManager: signals already hooked. didnt call leave()?")
		return
	var mp: MultiplayerAPI = multiplayer
	mp.peer_connected.connect(_on_peer_connected)
	mp.peer_disconnected.connect(_on_peer_disconnected)
	mp.connection_failed.connect(_on_connection_failed) # also handle failed connection
	mp.server_disconnected.connect(_on_server_disconnected) # and host crash/disconnect
	_signals_hooked = true

# try wait for connection 
func _wait_for_connection(timeout: float = 10.0) -> bool:
	var timer := 0.0
	while multiplayer.has_multiplayer_peer() and multiplayer.multiplayer_peer.get_connection_status() != MultiplayerPeer.CONNECTION_CONNECTED:
		await get_tree().process_frame
		timer += get_process_delta_time()
		if timer >= timeout:
			push_error("NetworkManager: connection timed out")
			leave(false)
			return false
	return true

func _on_peer_connected(id: int) -> void:   
	if multiplayer.is_server():
		var peer_ids : PackedInt32Array = multiplayer.get_peers()
		var current_player_count := peer_ids.size() + 1 # +1 for host
		if current_player_count > default_max_players:
			print("Max players exceeded, kicking peer: %d" % id)
			if peer and peer.has_method(("disconnect_peer")):
				peer.disconnect_peer(id)
			else:
				rpc_id(id, "_host_disconnect") # should handle steam and enet
			return
	emit_signal("peer_connected", id)

func _on_peer_disconnected(id: int) -> void: 
	emit_signal("peer_disconnected", id)

func _on_connection_failed() -> void:
	push_error("NetworkManager: connection failed")
	leave()

func _on_server_disconnected() -> void:
	push_error("NetworkManager: disconnected from server")
	leave()
