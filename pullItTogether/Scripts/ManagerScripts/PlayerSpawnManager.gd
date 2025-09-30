extends MultiplayerSpawner
class_name PlayerSpawnManager

@export var player_scene: PackedScene
@export var spawn_point: Node3D

var players: Dictionary[int, Node3D] = {}
var spawning_enabled := true

func _ready() -> void:
	if not is_in_group("player_spawner"):
		add_to_group("player_spawner")
	
	spawn_function = _spawn_player
	await get_tree().process_frame

	if is_multiplayer_authority() and spawning_enabled:
		spawn(multiplayer.get_unique_id())
	
	if spawning_enabled:
		multiplayer.peer_connected.connect(_on_peer_connected)
		multiplayer.peer_disconnected.connect(_on_peer_disconnected)

func set_spawning_enabled(enabled: bool) -> void:
	spawning_enabled = enabled

func _spawn_player(peer_id: int) -> Node:
	if player_scene == null:
		push_error("PlayerSpawner: player_scene not assigned") 
		return null

	var player : Node3D = player_scene.instantiate()
	player.name = "Player%d" % peer_id
	player.set_multiplayer_authority(peer_id)
	player.add_to_group("players")
	
	players[peer_id] = player
	player.tree_exited.connect(func():
		if players.has(peer_id):
			players.erase(peer_id),
	CONNECT_ONE_SHOT)

	var target_pos := _get_spawn_position()
	
	player.tree_entered.connect(func():
		if "global_position" in player:
			player.global_position = target_pos, 
	CONNECT_ONE_SHOT)
	
	_configure_local_view(player, peer_id)
	return player

func _configure_local_view(player: Node, peer_id: int) -> void:
	var is_local := (peer_id == multiplayer.get_unique_id())
	var cam := player.get_node_or_null("Head/Camera3D") as Camera3D
	if cam:
		cam.current = is_local
	if is_local:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func spawn_peer(peer_id: int) -> void:
	if spawning_enabled:
		super.spawn(peer_id)

func despawn_player(peer_id: int) -> void:
	if not players.has(peer_id):
		return
	var player : Node3D = players[peer_id]
	var sync:= player.get_node_or_null("MultiplayerSynchronizer") as MultiplayerSynchronizer
	if sync:
		sync.replication_config = SceneReplicationConfig.new()
	await get_tree().process_frame
	player.queue_free()
	players.erase(peer_id)

func despawn_all() -> void:
	for id in players.keys():
		await despawn_player(id)

func _on_peer_connected(peer_id: int) -> void:
	if spawning_enabled:
		spawn(peer_id)

func _on_peer_disconnected(peer_id: int) -> void:
	if players.has(peer_id):
		players[peer_id].queue_free()
		players.erase(peer_id)

func _get_spawn_position() -> Vector3:
	if spawn_point and spawn_point.is_inside_tree():
		return spawn_point.global_position
	else:
		var parent := get_parent() as Node3D
		return Vector3(parent.global_position.x, parent.global_position.y + 5, parent.global_position.z)
