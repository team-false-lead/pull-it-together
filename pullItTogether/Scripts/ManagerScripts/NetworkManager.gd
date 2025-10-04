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

# ---------- General ----------
func get_peer() -> MultiplayerPeer:
	return peer

func update_multiplayer_peer() -> void:
	multiplayer.multiplayer_peer = peer

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
	leave() # reset any existing session
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
	leave() # reset any existing session
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
	emit_signal("session_started", "public client")
	return true

# ---------- Local ENet ----------
# default to localhost (same machine), change address as needed
func host_local(address : String = "127.0.0.1", port : int = 2450, max_clients : int = 4) -> bool:
	leave() # reset any existing session
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
	leave() # reset any existing session
	set_peer_mode(PeerMode.LOCAL)
	var err: int = peer.create_client(address, port)
	if err != OK:
		push_error("ENet create_client() failed: %s" % err)
		peer = null
		mode = PeerMode.NONE
		_signals_hooked = false
		return false
	_attach_peer()
	emit_signal("session_started", "local client")
	print("Connecting to IP: %s\nPort: %d" % [address, port])
	return true

# ---------- Common ----------
# leave session
func leave() -> void:
	# Disconnect signals
	if _signals_hooked:
		var mp: MultiplayerAPI = multiplayer
		if mp.peer_connected.is_connected(Callable(self, "_on_peer_connected")):
			mp.peer_connected.disconnect(Callable(self, "_on_peer_connected"))
		if mp.peer_disconnected.is_connected(Callable(self, "_on_peer_disconnected")):
			mp.peer_disconnected.disconnect(Callable(self, "_on_peer_disconnected"))
	_signals_hooked = false

	# Close peer connection
	if peer and peer.has_method("close"):
		peer.close()
	multiplayer.multiplayer_peer = null
	emit_signal("session_ended")
	peer = null
	mode = PeerMode.NONE

func _attach_peer() -> void:
	#connect signals
	update_multiplayer_peer()
	if _signals_hooked:
		push_warning("NetworkManager: signals already hooked. didnt call leave()?")
		return
	var mp: MultiplayerAPI = multiplayer
	mp.peer_connected.connect(_on_peer_connected)
	mp.peer_disconnected.connect(_on_peer_disconnected)
	_signals_hooked = true

func _on_peer_connected(id: int) -> void:   
	emit_signal("peer_connected", id)

func _on_peer_disconnected(id: int) -> void: 
	emit_signal("peer_disconnected", id)
