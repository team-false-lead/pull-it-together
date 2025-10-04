extends Node
class_name MapManager

@export var game_manager: GameManager
@export var network_manager: NetworkManager
@export var level_scene : PackedScene

var interactables_node: Node = null
var level_instance: Node = null
var _loading_map : bool = false

var is_multiplayer_session: bool = false # prob remove later for cleanup

signal map_will_reload
signal map_reloaded
signal map_deloaded

func _ready() -> void:
	game_manager.singleplayer_session_started.connect(func(): _set_multiplayer_session(false))
	network_manager.session_started.connect(func(_role): _set_multiplayer_session(true))
	network_manager.session_ended.connect(func(): _set_multiplayer_session(false))

func _set_multiplayer_session(status: bool) -> void:
	is_multiplayer_session = status

# Load the map, free existing if any.
func load_map() -> Node:
	if _loading_map:
		return level_instance
	_loading_map = true

	if level_scene == null:
		push_error("MapManager: no map assigned")
		_loading_map = false
		return null

	# free existing level if any
	if level_instance:
		deload_map()

	# instantiate new level
	level_instance = level_scene.instantiate()
	#level_instance.name = "LevelInstance" # not needed
	self.add_child(level_instance)

	# get interactables node for item manager, default to level root if not found
	interactables_node = level_instance.get_node_or_null("%Interactables")
	if interactables_node == null:
		interactables_node = level_instance

	await get_tree().process_frame
	_loading_map = false
	emit_signal("map_reloaded")
	return level_instance

# Free the current map instance if any
func deload_map() -> void:
	if level_instance and is_instance_valid(level_instance):
		level_instance.queue_free()
		await get_tree().process_frame
		level_instance = null
		interactables_node = null
		emit_signal("map_deloaded")
#removed spawner functions as they live on PlayerSpawnManager now
		
# Server RPC reloading map --------------------------------
@rpc("any_peer", "call_local") 
func request_reload_map() -> void:
	# If not the server, relay the request
	if multiplayer.has_multiplayer_peer() and not multiplayer.is_server():
		rpc_id(1, "request_reload_map")
		return
	
	emit_signal("map_will_reload")
	await get_tree().process_frame
	await load_map()
	if multiplayer.has_multiplayer_peer():
		rpc("confirm_reload")

@rpc("any_peer", "call_local") 
func confirm_reload() -> void:
	print("Map reloaded for client.")
