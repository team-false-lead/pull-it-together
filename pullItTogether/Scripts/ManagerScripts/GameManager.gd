extends Node
class_name GameManager
## Combined Menu + Lobby + Steam callbacks
## P2P transport via Expresso Bits (SteamMultiplayerPeer)
## lobby discovery via GodotSteam

# ---------- Scene References ----------
# need to clean this up and get rid of not used, etc.
@export var network_manager: Node
@export var map_manager: Node
@export var spawn_manager: PlayerSpawnManager

@export_category("Canvas / Menus")
@export var main_canvas: CanvasLayer
@export var main_menu: Control
@export var multiplayer_select_menu: Control
@export var local_multiplayer_menu: Control
@export var public_multiplayer_menu: Control

@export_category("Local UI")
@export var local_host_button: Button
@export var local_join_button: Button
@export var local_back_to_menu_button: Button
@export var local_address_input: LineEdit
@export var local_port: LineEdit
@export var local_LAN_label: Label
@export var local_max_players: SpinBox

@export_category("Public UI")
@export var steam_status_label: Label
@export var public_refresh_button: Button
@export var public_create_lobby_button: Button
@export var public_back_button: Button
@export var public_list_container: VBoxContainer   
@export var manual_join_id: LineEdit                
@export var manual_join_button: Button

# ---------- Network Config ----------
@export_category("Network Config")
@export var lobby_prefix: String = "Pull It Together "
var user_friendly_name: String
@export var default_addr: String = "127.0.0.1"
@export var default_port: int = 2450
@export var default_max_players: int = 4
@export var app_id: String = "480"    # test AppID
var _steam_ok := false               

# ---------- Internal ----------
signal singleplayer_session_started()
signal lobby_list_updated(items: Array) 
var _gs_signals_connected := false

# ---------- Lifecycle ----------
func _enter_tree() -> void:
	if OS.get_environment("SteamAppId") == "":
		OS.set_environment("SteamAppId", app_id)
		OS.set_environment("SteamGameId", app_id)

func _ready() -> void:
	if spawn_manager:
		spawn_manager.set_spawning_enabled(false)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	
	# Menu defaults
	if main_canvas:
		main_canvas.show()
	_hide_all_menus()
	if main_menu:
		main_menu.show()
	
	# steam init
	_try_init_godotsteam()
	if _steam_ok:
		_connect_gs_signals() 
	randomize()
	
	# prints to ensure steam is setup correctly
	print("Has GodotSteam:", _has_gs())
	print("Has SteamMultiplayerPeer class:", ClassDB.class_exists("SteamMultiplayerPeer"))
	print("Has runtime DLL:", FileAccess.file_exists("res://steam_api64.dll"))
	print("AppID:", OS.get_environment("SteamAppId"))

	# Wire Public Manual Join UI, should make this its own function
	if manual_join_button and manual_join_id:
		manual_join_button.pressed.connect(func():
			var txt := manual_join_id.text.strip_edges()
			if txt.is_valid_int():
				join_steam_lobby(int(txt))
			else:
				push_error("Enter a valid SteamID64 to join.")
		)

	# Listen to network events
	if network_manager:
		if not network_manager.is_connected("session_started", Callable(self, "_on_session_started")):
			network_manager.session_started.connect(_on_session_started)
		if not network_manager.is_connected("session_ended", Callable(self, "_on_session_ended")):
			network_manager.session_ended.connect(_on_session_ended)
		# peer connected/disconnected handled in spawn manager

	# UI readiness
	_update_runtime_ui()
	await get_tree().process_frame
	_update_runtime_ui() # in case steam init was async

	# Hook signals to render the lobby list
	if not is_connected("lobby_list_updated", Callable(self, "_render_lobby_list")):
		connect("lobby_list_updated", Callable(self, "_render_lobby_list"))

func _process(_d: float) -> void:
	# Pump GodotSteam callbacks if present
	if _has_gs() and _steam_ok:
		_get_gs().run_callbacks()

# godot steam init setup
func _try_init_godotsteam() -> void:
	_steam_ok = false
	if not _has_gs():
		print("[Steam] GodotSteam singleton not present (plugin disabled?)")
		return
	var _gs = _get_gs()

	var r: Dictionary = _gs.steamInitEx() as Dictionary
	if r.get("status", -1) == 0:
		_steam_ok = true
	else:
		_steam_ok = false

# ---------- Menu helpers ----------
# hide everything
func _hide_all_menus() -> void:
	if main_menu: main_menu.hide()
	if multiplayer_select_menu: multiplayer_select_menu.hide()
	if local_multiplayer_menu: local_multiplayer_menu.hide()
	if public_multiplayer_menu: public_multiplayer_menu.hide()

#show main
func _show_main_menu() -> void:
	_hide_all_menus()
	if main_menu: main_menu.show()

# show multiplayer choice
func _show_multiplayer_select() -> void:
	_hide_all_menus()
	if multiplayer_select_menu: multiplayer_select_menu.show()

# show local lan menu
func show_local_menu() -> void:
	_hide_all_menus()
	if local_multiplayer_menu: local_multiplayer_menu.show()

# show public steam menu
func show_public_menu() -> void:
	_hide_all_menus()
	if public_multiplayer_menu: 
		public_multiplayer_menu.show()
	if _steam_ok:
		refresh_steam_lobby_list()
	

# ---------- Single Player ----------
func _on_single_player_pressed() -> void:
	if map_manager and map_manager.has_method("load_map"):
		await map_manager.call("load_map")
	if main_canvas: main_canvas.hide()
	if spawn_manager:
		spawn_manager.set_spawning_enabled(true)
	emit_signal("singleplayer_session_started")

# ---------- Local Network flow ----------
# Host a local game with given settings
func _on_local_host_pressed() -> void:
	var addr := default_addr
	var port := default_port
	var maxp := default_max_players
	if local_address_input and local_address_input.text.strip_edges() != "":
		addr = local_address_input.text.strip_edges()
	if local_port.text != "":
		port = int(local_port.text)
	if local_max_players: 
		maxp = int(local_max_players.value)

	if not network_manager:
		push_error("NetworkManager not assigned"); return
	if not network_manager.host_local(addr, port, maxp):
		push_error("Failed to host local ENet")
	
# Join a local game with given settings
func _on_local_join_pressed() -> void:
	var addr := default_addr
	var port := default_port
	if local_address_input and local_address_input.text.strip_edges() != "":
		addr = local_address_input.text.strip_edges()
	if local_port.text != "":
		port = int(local_port.text)
	if not network_manager:
		push_error("NetworkManager not assigned"); return
	if not network_manager.join_local(addr, port):
		push_error("Failed to join local ENet %s:%s" % [addr, port])

# ---------- Steam lobby (GodotSteam) + transport (Expresso Bits) ----------
# check if GodotSteam is available
func _has_gs() -> bool:
	return Engine.has_singleton("Steam")

# get GodotSteam singleton
func _get_gs():
	return Engine.get_singleton("Steam")

# connect GodotSteam signals if not already
func _connect_gs_signals() -> void:
	if _gs_signals_connected or not _has_gs(): return
	var GS = _get_gs()
	if not GS.is_connected("lobby_created", Callable(self, "_on_gs_lobby_created")):
		GS.connect("lobby_created", Callable(self, "_on_gs_lobby_created"))
	if not GS.is_connected("lobby_match_list", Callable(self, "_on_gs_lobby_match_list")):
		GS.connect("lobby_match_list", Callable(self, "_on_gs_lobby_match_list"))
	_gs_signals_connected = true

# generate a random lobby name with prefix "Pull It Together ####"
func _gen_lobby_name() -> String:
	var room_id := randi_range(0, 9999)
	return "%s %04d" % [lobby_prefix, room_id]

# Create a new public lobby
func create_steam_lobby() -> void:
	if not _steam_ok:
		push_error("Steam not initialized; cannot create lobby.")
		return
	var GS = _get_gs()
	GS.createLobby(2, default_max_players) # 2 = public

# Refresh the public lobby list
func refresh_steam_lobby_list() -> void:
	if not _steam_ok:
		push_error("Steam not initialized; cannot request lobby list.")
		return
	var GS = _get_gs()
	GS.requestLobbyList()
	print("refreshing lobbies...")

# Join a lobby by its lobby ID (int)
func join_steam_lobby_by_lobby_id(lobby_id: int) -> void:
	if not _has_gs():
		push_error("GodotSteam not available; cannot join by lobby id."); return
	var GS = _get_gs()
	var host_str: String = GS.getLobbyData(lobby_id, "host_id64")
	if host_str == "":
		var owner_id64: int = int(GS.getLobbyOwner(lobby_id))
		if owner_id64 == 0:
			push_error("Lobby missing host info."); return
		join_steam_lobby(owner_id64)
	else:
		var host_id64: int = int(host_str)
		join_steam_lobby(host_id64)

# Join a lobby by its host's SteamID64 (int)
func join_steam_lobby(host_steam_id_64: int) -> void:
	if not network_manager:
		push_error("NetworkManager not assigned"); return
	if not network_manager.join_steam(host_steam_id_64):
		push_error("Failed to join Steam host %s" % host_steam_id_64)

# GodotSteam signal handlers
func _on_gs_lobby_created(a, b) -> void:
	print("[Steam] lobby_created: ", a, b)

	# Determine param order (result vs lobby_id) safely
	var lobby_id := 0
	var result := 0
	if typeof(a) == TYPE_INT and typeof(b) == TYPE_INT:
		if a > 1000000:
			lobby_id = a; result = b
		else:
			result = a; lobby_id = b
	else:
		push_error("Unexpected lobby_created signature")
		return
	if result != 1:
		push_error("GodotSteam: lobby create failed (result=%s)" % result); return

	var GS = _get_gs()
	var my_id64: int = int(GS.getSteamID())
	GS.setLobbyData(lobby_id, "host_id64", str(my_id64))
	GS.setLobbyJoinable(lobby_id, true)

	user_friendly_name = _gen_lobby_name()
	print("[Steam] lobby_name: ", user_friendly_name)
	GS.setLobbyData(lobby_id, "name", user_friendly_name)
	
	# Start hosting transport
	if not network_manager:
		push_error("NetworkManager not assigned"); return
	if not network_manager.host_steam():
		push_error("Failed to start transport after lobby create"); return

# update lobby list 
func _on_gs_lobby_match_list(lobbies: Array) -> void:
	print("[Steam] lobby_match_list:", lobbies.size(), "lobbies")

	# filter by prefix and gather data
	if not _has_gs(): return
	var GS = _get_gs()
	var out: Array = []
	var prefix : String = lobby_prefix.to_lower()
	
	for lobby_id in lobbies:
		var lobby_name: String = GS.getLobbyData(lobby_id, "name")
		if lobby_name == "":
			lobby_name = "Lobby %s" % lobby_id
		
		if lobby_name.to_lower().find(prefix) == -1:
			continue
		
		var host_str: String = GS.getLobbyData(lobby_id, "host_id64")
		if host_str == "":
			host_str = str(GS.getLobbyOwner(lobby_id))
		var members: int = int(GS.getNumLobbyMembers(lobby_id))
		var max_m: int = int(GS.getLobbyMemberLimit(lobby_id))
		out.append({
			"lobby_id": int(lobby_id),
			"name": lobby_name,
			"host_id64": host_str,
			"members": members,
			"max": max_m
		})
	emit_signal("lobby_list_updated", out)

# GodotSteam lobby data updated (not used currently), lobby name/metadata changes
#func _on_gs_lobby_data_update(_success: bool, _lobby_id: int, _member_id: int) -> void:
#	pass

# GodotSteam lobby chat update (not used currently), member join/leave updates?
#func _on_lobby_chat_update(_lobby_id: int, _member_id: int, _state_change: int) -> void:
#	pass

# ---------- Transport runtime checks ----------
# check if Steam runtime DLL is present
func _steam_runtime_present() -> bool:
	return ClassDB.class_exists("SteamMultiplayerPeer") and (
		FileAccess.file_exists("res://steam_api64.dll") or
		FileAccess.file_exists("res://libsteam_api.so") or
		FileAccess.file_exists("res://libsteam_api.dylib")
	)

# update UI elements based on Steam runtime status
func _update_runtime_ui() -> void:
	var is_ready := _steam_runtime_present() && _steam_ok
	if steam_status_label:
		steam_status_label.text = "Steam: Ready" if is_ready else "Steam: Not Ready"
		# green if ready, red if not
		steam_status_label.add_theme_color_override("font_color", Color(0.32, 0.78, 0.37) if is_ready else Color(0.9, 0.25, 0.25))
	if public_create_lobby_button:
		public_create_lobby_button.disabled = not is_ready
	if public_refresh_button:
		public_refresh_button.disabled = not is_ready
	if manual_join_button:
		manual_join_button.disabled = not is_ready

# ---------- Network events ----------
#load map and enable spawning on session start
func _on_session_started(role: String) -> void:
	if map_manager and map_manager.has_method("load_map"):
		await map_manager.call("load_map")
	if main_canvas: main_canvas.hide()
	if spawn_manager:
		spawn_manager.set_spawning_enabled(true)
	print("Session started as: ", role)

# disable spawning and show menu on session end
func _on_session_ended() -> void:
	if map_manager and map_manager.has_method("deload_map"):
		await map_manager.call("deload_map") # reset map to initial state
	if spawn_manager:
		spawn_manager.set_spawning_enabled(false)
	if main_canvas: main_canvas.show()
	_show_main_menu()

# peer connected/disconnected handled in spawn manager

# ---------- Lobby list rendering ----------
func _render_lobby_list(items: Array) -> void:
	if not public_list_container:
		return

	# Clear old entries
	for c in public_list_container.get_children():
		c.queue_free()

	# Build entries
	for item_var in items:
		var item: Dictionary = item_var

		var lobby_name: String = (item.get("name", "Lobby") as String)
		var members: int = int(item.get("members", 0))
		var max_m: int = int(item.get("max", 0))
		var lobby_id: int = int(item.get("lobby_id", 0))

		var row := HBoxContainer.new()
		row.size_flags_horizontal = Control.SIZE_EXPAND_FILL

		var lbl := Label.new()
		lbl.text = "%s  (%d/%d)" % [lobby_name, members, max_m]
		lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(lbl)

		var join_btn := Button.new()
		join_btn.text = "Join"
		join_btn.pressed.connect(func():
			join_steam_lobby_by_lobby_id(lobby_id)
		)
		row.add_child(join_btn)

		public_list_container.add_child(row)
		
# ---------- Get Local LAN IP ----------
# Display LAN IP and copy to clipboard
func _display_local_lan_IP() -> void:
	var message := ""
	var ip := _get_lan_ipv4()
	
	if ip == "":
		message = "No LAN IPv4 found."
	else:
		DisplayServer.clipboard_set(ip)
		message = "LAN IP: %s\n (Copied to Clipboard)" % [ip]

	if local_LAN_label:
		local_LAN_label.text = message
	else:
		print(message)

# get a local LAN IPv4 address 
func _get_lan_ipv4() -> String:
	# 192.168.*, 10.*, or 172.16–31.*
	var fallback := ""
	for address in IP.get_local_addresses():
		if _is_private_ipv4(address) and not address.begins_with("127.") and not address.begins_with("169.254."):
			if address.begins_with("192.168."):
				return address
			if fallback == "":
				fallback = address
	return fallback

# check if an IPv4 address is in private range
func _is_private_ipv4(address: String) -> bool:
	if address.count(".") !=3 or not address.is_valid_ip_address():
		return false
	var parts := address.split(".")
	var part0 := int(parts[0])
	var part1 := int(parts[1])
	# 10.0.0.0/8
	if part0 == 10:
		return true
	# 192.168.0.0/16
	if part0 == 192 and part1 == 168:
		return true
	# 172.16.0.0 – 172.31.255.255
	if part0 == 172 and part1 >= 16 and part1 <= 31:
		return true
	return false
