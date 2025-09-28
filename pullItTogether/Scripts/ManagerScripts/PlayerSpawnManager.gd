extends MultiplayerSpawner
class_name PlayerSpawnManager

@export var player_scene: PackedScene
@export var spawn_point: Node3D

var players := {}

func _ready() -> void:
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
	players[peer_id] = player

	var target_pos := _get_spawn_position()
	
	player.tree_entered.connect(func():
		if "global_position" in player:
			player.global_position = target_pos
	, CONNECT_ONE_SHOT)
	return player

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
