extends MultiplayerSpawner
class_name PlayerSpawnManager

@export var player_scene: PackedScene
@export var spawn_point: Node3D

var players: Dictionary[int, Node3D] = {}

func _ready() -> void:
	if not is_in_group("player_spawner"):
		add_to_group("player_spawner")
	
	spawn_function = _spawn_player
	await get_tree().process_frame

	if is_multiplayer_authority():
		spawn(multiplayer.get_unique_id())
		multiplayer.peer_connected.connect(spawn)
		multiplayer.peer_disconnected.connect(_on_peer_disconnected)

func _spawn_player(peer_id: int) -> Node:
	if player_scene == null:
		push_error("PlayerSpawner: player_scene not assigned") 
		return null

	var player := player_scene.instantiate()
	player.set_multiplayer_authority(peer_id)
	player.add_to_group("players")
	
	players[peer_id] = player
	player.tree_exited.connect(func():
		if player.has(peer_id):
			players.erase(peer_id),
			CONNECT_ONE_SHOT)

	var target_pos := _get_spawn_position()
	
	player.tree_entered.connect(func():
		if "global_position" in player:
			player.global_position = target_pos
	, CONNECT_ONE_SHOT)
	return player

func spawn_peer(peer_id: int) -> void:
	super.spawn(peer_id)

func despawn_player(peer_id: int) -> void:
	if not players.has(peer_id):
		return
	var player := players[peer_id]
	var sync:= player.get_node_or_null("MultiplayerSynchronizer")
	if sync:
		sync.replication_config = null
	player.queue_free()
	players.erase(peer_id)

func despawn_all() -> void:
	for id in players.keys():
		despawn_player(id)

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
