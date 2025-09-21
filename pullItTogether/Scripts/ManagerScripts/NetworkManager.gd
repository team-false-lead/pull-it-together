extends Node
class_name NetworkManager

enum PeerMode { STEAM, LOCAL, NONE }

signal session_started(role)      # "host" | "client"
signal session_ended()
signal peer_connected(id)
signal peer_disconnected(id)

var peer : MultiplayerPeer = null
var mode : int = PeerMode.NONE
var _signals_hooked := false

func get_peer() -> MultiplayerPeer:
	return peer

func update_multiplayer_peer() -> void:
	multiplayer.multiplayer_peer = peer

func set_peer_mode(peer_mode : int) -> void:
	mode = peer_mode
	match mode:
		PeerMode.STEAM:
			peer = SteamMultiplayerPeer.new()
		PeerMode.LOCAL:
			peer = ENetMultiplayerPeer.new()
		PeerMode.NONE:
			peer = null
	print_rich("Peer Mode Set To: [color=yellow]", PeerMode.keys()[mode], "[/color]")

func reset_peer() -> void:
	leave()

# ---------- Steam (Expresso Bits) ----------
func host_steam() -> bool:
	set_peer_mode(PeerMode.STEAM)
	var sp: SteamMultiplayerPeer = peer
	var err: int = sp.create_host(0)  # 0 = virtual port
	if err != OK:
		push_error("Steam create_host() failed: %s" % err)
		peer = null
		return false
	_attach_peer()
	emit_signal("session_started", "public host")
	return true

func join_steam(host_steam_id_64 : int) -> bool:
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
		return false
	_attach_peer()
	emit_signal("session_started", "public client")
	return true

# ---------- Local ENet ----------
func host_local(address : String = "127.0.0.1", port : int = 2450, max_clients : int = 4) -> bool:
	set_peer_mode(PeerMode.LOCAL)
	peer.set_bind_ip(address)
	var err: int = peer.create_server(port, max_clients)
	if err != OK:
		push_error("ENet create_server() failed: %s" % err)
		peer = null
		return false
	_attach_peer()
	emit_signal("session_started", "local host")
	print("LAN IP: %s\nPort: %d" % [address, port])
	return true

func join_local(address : String = "127.0.0.1", port : int = 2450) -> bool:
	set_peer_mode(PeerMode.LOCAL)
	var err: int = peer.create_client(address, port)
	if err != OK:
		push_error("ENet create_client() failed: %s" % err)
		peer = null
		return false
	_attach_peer()
	emit_signal("session_started", "local client")
	return true

# ---------- Common ----------
func leave() -> void:
	if peer and peer.has_method("close"):
		peer.close()
	multiplayer.multiplayer_peer = null
	emit_signal("session_ended")
	peer = null
	mode = PeerMode.NONE
	_signals_hooked = false

func _attach_peer() -> void:
	multiplayer.multiplayer_peer = peer
	if not _signals_hooked:
		var mp: MultiplayerAPI = multiplayer
		mp.peer_connected.connect(_on_peer_connected)
		mp.peer_disconnected.connect(_on_peer_disconnected)
		_signals_hooked = true

func _on_peer_connected(id):   emit_signal("peer_connected", id)
func _on_peer_disconnected(id): emit_signal("peer_disconnected", id)
