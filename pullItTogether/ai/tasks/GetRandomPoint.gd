class_name GetRandomPoint
extends BTAction

@export var patrol_radius: float = 5.0

func _tick(_delta: float) -> Status:
    if agent == null:
        return Status.FAILURE
    
    var random_offset = Vector3(
        randf_range(-patrol_radius, patrol_radius),
        0,
        randf_range(-patrol_radius, patrol_radius)
    )
    var random_point = agent.position + random_offset
    agent.set("targetPosition", random_point)
    
    return Status.SUCCESS