extends MultiplayerSpawner
class_name PlayerSpawnManager

@export var player_scene: PackedScene
@export var spawn_point: Node3D

var players: Dictionary[int, Node3D] = {}
var spawning_enabled : bool = false

var _player_color_palette := [
	Color8(63, 136, 197), # Blue
	Color8(208, 0, 0),   # Red
	Color8(255, 186, 8), # Yellow
	Color8(3, 43, 67),   # Dark Blue
]

var _assigned_colors := {}
var _free_color_indices := []

func _ready() -> void:
	# easier access via group if needed later
	if not is_in_group("player_spawner"):
		add_to_group("player_spawner")
	
	# set custom spawn function
	spawn_function = _create_player

	var map_manager = get_tree().get_root().get_node_or_null("%MapManager") as MapManager
	if map_manager:
		map_manager.map_deloaded.connect(despawn_all)

# Set spawning enabled/disabled and connect signals/spawn existing players if server
func set_spawning_enabled(enabled: bool) -> void:
	spawning_enabled = enabled
	await get_tree().process_frame # wait a frame to ensure multiplayer is ready

	if not multiplayer.has_multiplayer_peer():
		if not enabled:
			despawn_all()
		return

	if not multiplayer.is_server(): # only the server spawns players
		return

	if enabled:
		if _assigned_colors.is_empty() && _free_color_indices.is_empty():
			_init_color_pool()

		if not multiplayer.peer_connected.is_connected(_on_peer_connected):
			multiplayer.peer_connected.connect(_on_peer_connected)
		if not multiplayer.peer_disconnected.is_connected(_on_peer_disconnected):
			multiplayer.peer_disconnected.connect(_on_peer_disconnected)
		
		# spawn existing players including host
		await get_tree().process_frame
		var ids := multiplayer.get_peers()
		if not ids.has(1):
			ids.append(1)
		for id in ids:
			_assign_color_to_player(id)
			spawn_or_respawn_player(id)

	else: # disable spawning, disconnect signals
		if multiplayer.peer_connected.is_connected(_on_peer_connected):
			multiplayer.peer_connected.disconnect(_on_peer_connected)
		if multiplayer.peer_disconnected.is_connected(_on_peer_disconnected):
			multiplayer.peer_disconnected.disconnect(_on_peer_disconnected)
		# despawn all players
		despawn_all()
		_assigned_colors.clear()
		_free_color_indices.clear()

# custom spawn function for MultiplayerSpawner
func _create_player(peer_id: int) -> Node:
	if player_scene == null:
		push_error("PlayerSpawner: player_scene not assigned") 
		return null

	var player : Node3D = player_scene.instantiate()

	if multiplayer.is_server():
		var color_index : int = _assign_color_to_player(peer_id)
		var color = _player_color_palette[color_index]
		player.set("bodyColor", color)
		player.tree_entered.connect(func() -> void:
			await get_tree().process_frame       # ensure replicated on clients
			player.rpc("SetColor", color)        # ALL peers incl. owner get their own color
		, CONNECT_ONE_SHOT)

	player.name = "Player%d" % peer_id
	player.set_multiplayer_authority(peer_id)
	player.add_to_group("players")
	players[peer_id] = player

	# place and configure when added to scene tree
	player.tree_entered.connect(func():
		_place_at_spawn(player)
		_configure_local_view(player, peer_id),
	CONNECT_ONE_SHOT)

	# remove from players dict when removed from scene tree
	player.tree_exited.connect(func():
		if players.has(peer_id):
			players.erase(peer_id),
	CONNECT_ONE_SHOT)

	return player

# respawn player if exists otherwise create new and spawn
func spawn_or_respawn_player(peer_id: int) -> void:
	if not spawning_enabled:
		return
	if players.has(peer_id) and is_instance_valid(players[peer_id]):
		_place_at_spawn(players[peer_id])
		_configure_local_view(players[peer_id], peer_id)
	else:
		super.spawn(peer_id) # call MultiplayerSpawner == _spawn_player

# despawn player by peer_id if exists
func despawn_player(peer_id: int) -> void:
	if not players.has(peer_id):
		return
	var player : Node3D = players[peer_id]

	# remove multiplayer synchronizer if any to avoid warnings
	var sync:= player.get_node_or_null("MultiplayerSynchronizer") as MultiplayerSynchronizer
	if sync:
		sync.replication_config = SceneReplicationConfig.new()
		sync.queue_free()
		await get_tree().process_frame # wait a frame to avoid warnings
	
	# free player
	if is_instance_valid(player):
		player.queue_free()
	players.erase(peer_id)

# despawn all players
func despawn_all() -> void:
	for id in players.keys().duplicate():
		despawn_player(id)

# place player at spawn point and reset velocities if any
func _place_at_spawn(player: Node3D) -> void:
	player.global_position = _get_spawn_position()
	
	if "velocity" in player:
		player.velocity = Vector3.ZERO
	if "linear_velocity" in player:
		player.linear_velocity = Vector3.ZERO
	if "angular_velocity" in player:
		player.angular_velocity = Vector3.ZERO

# get spawn position from spawn_point or default above parent
func _get_spawn_position() -> Vector3:
	if spawn_point and spawn_point.is_inside_tree():
		return spawn_point.global_position
	else: # default above parent
		var parent := get_parent() as Node3D
		return parent.global_position + Vector3.UP * 3.0

# configure camera for local player
func _configure_local_view(player: Node, peer_id: int) -> void:
	var is_local := (peer_id == multiplayer.get_unique_id())
	var cam := player.get_node_or_null("Head/Camera3D") as Camera3D
	if cam:
		cam.current = is_local
	if is_local:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

# signal handlers for peer connect/disconnect
func _on_peer_connected(peer_id: int) -> void:
	if multiplayer.is_server() and spawning_enabled:
		_assign_color_to_player(peer_id)
		spawn_or_respawn_player(peer_id)

		await get_tree().process_frame
		for id in players.keys():
			var player := players[id]
			if is_instance_valid(player):
				var index : int = _assigned_colors.get(id, 0)
				var color : Color = _player_color_palette[index]
				player.rpc_id(peer_id, "SetColor", color)
		print("Peer connected: ", peer_id)

func _on_peer_disconnected(peer_id: int) -> void:
	if players.has(peer_id):
		_release_color_of_player(peer_id)
		despawn_player(peer_id)
		print("Peer disconnected: ", peer_id)

func _init_color_pool() -> void:
	_free_color_indices = []
	for i in _player_color_palette.size():
		_free_color_indices.append(i)
	_free_color_indices.sort()

func _assign_color_to_player(peer_id: int) -> int:
	if _assigned_colors.has(peer_id):
		return _assigned_colors[peer_id]
	var index : int = _take_lowest_free_color_index()
	_assigned_colors[peer_id] = index
	return index

func _take_lowest_free_color_index() -> int:
	if _free_color_indices.is_empty():
		push_warning("No free colors available to assign")
		return _player_color_palette.size() - 1 # invalid index
	var index : int = _free_color_indices[0]
	_free_color_indices.remove_at(0)
	return index

func _release_color_of_player(peer_id: int) -> void:
	if not _assigned_colors.has(peer_id):
		return
	var index : int = _assigned_colors[peer_id]
	_assigned_colors.erase(peer_id)
	_free_color_indices.append(index)
	_free_color_indices.sort()


