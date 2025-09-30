extends Node
class_name MapManager

@export var game_manager: Node
@export var level_scene : PackedScene
#@export var container_path : NodePath
@export var interactables_node = null

@export var level_instance: Node = null
var _spawner: Node = null
var _loading := false

var is_multiplayer_session: bool = true

signal map_reloaded

func _ready() -> void:
	game_manager.connect("singleplayer_session_started", Callable(self, "_set_multiplayer_false"))

func _set_multiplayer_false() -> void:
	is_multiplayer_session = false

# Server rebuild level instance instead
#func reload_current_map() -> Node:
#	return await load_map()

func load_map() -> Node:
	if _loading:
		return level_instance
	_loading = true
	
	#var parent := get_node_or_null(container_path)
	#if parent == null: 
	#parent = self

	#var scene: PackedScene = level
	if level_scene == null:
		push_error("MapManager: no map assigned")
		_loading = false
		return null

	if level_instance and is_instance_valid(level_instance):
		_disable_syncers(level_instance)
		await get_tree().process_frame
		level_instance.queue_free()
		level_instance = null
		for c in self.get_children():
			c.queue_free()
		await get_tree().process_frame
		#_spawner = null

	level_instance = level_scene.instantiate()
	level_instance.name = "LevelInstance"
	self.add_child(level_instance)
	interactables_node = level_instance.get_node_or_null("%Interactables")
	if interactables_node == null:
		interactables_node = level_instance

	await get_tree().process_frame
	
	_spawner = _find_spawner(level_instance)
	if _spawner == null:
		push_error("MapManager: PlayerSpawner not found in loaded level.")
		
	_loading = false
	emit_signal("map_reloaded")
	return level_instance

func _find_spawner(root: Node) -> Node:
	var n := root.find_child("PlayerSpawner", true, false)
	if n: return n
	for node in get_tree().get_nodes_in_group("player_spawner"):
		if root.is_ancestor_of(node):
			return node
	for c in root.get_children():
		if c is MultiplayerSpawner or c.get_class() == "PlayerSpawner":
			return c
		var deep := _find_spawner(c)
		if deep: return deep
	return null

# Only the host should spawn; defer until ready.
func spawn_player(peer_id: int) -> void:
	if not multiplayer.is_server():
		return
	var tries := 0
	while (_spawner == null or not _spawner.is_inside_tree() or not multiplayer.has_multiplayer_peer()) and tries < 60:
		await get_tree().process_frame
		if _spawner == null and level_instance:
			_spawner = _find_spawner(level_instance)
		tries += 1
	if _spawner and _spawner.has_method("spawn"):
		_spawner.spawn(peer_id)

func despawn_player(peer_id: int) -> void:
	if not multiplayer.is_server():
		return
	if _spawner and _spawner.is_inside_tree() and _spawner.has_method("despawn_player"):
		_spawner.despawn_player(peer_id)
	else:
		push_warning("MapManager: despawn_player() called but spawner not ready")
		
# Server RPC --------------------------------
@rpc("any_peer", "call_local") 
func request_reload_map() -> void:
	if multiplayer.has_multiplayer_peer() and not multiplayer.is_server():
		rpc_id(1, "request_reload_map")
		return
	await  get_tree().process_frame
	await _server_reload_current_map()

func _get_all_players() -> Array:
	var ids := multiplayer.get_peers()
	if not ids.has(1):
		ids.append(1) # host
	return ids
	
func _ensure_spawner() -> void:
	if _spawner == null and level_instance:
		_spawner = _find_spawner(level_instance)
	
#func _check_players_exist() -> bool:
#	return level_instance != null and is_instance_valid(level_instance) and level_instance.find_child("PlayersContainer", true, false) != null

func _safe_despawn_all() -> void:
	_ensure_spawner()
	if _spawner and _spawner.is_inside_tree() and _spawner.has_method("despawn_all"):
		_spawner.despawn_all()
	else:
		for id in _get_all_players():
			despawn_player(id)

func _disable_syncers(node: Node) -> void:
	for child in node.get_children():
		_disable_syncers(child)
		if child is MultiplayerSynchronizer:
			var syncer := child as MultiplayerSynchronizer
			syncer.replication_enabled = false
			syncer.replication_config = null
			syncer.root_path = NodePath("")

func _safe_spawn_all() -> void:
	for id in _get_all_players():
		spawn_player(id)

func _server_reload_current_map() -> void:
	_ensure_spawner()
	if level_instance and is_instance_valid(level_instance):
		_disable_syncers(level_instance)
	await get_tree().process_frame
	
	_safe_despawn_all()
	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().process_frame # just incase
	
	await load_map()
	await get_tree().process_frame
	
	_safe_spawn_all()
	rpc("confirm_reload")

@rpc("authority") func confirm_reload() -> void:
	print("Map reloaded for client.")
