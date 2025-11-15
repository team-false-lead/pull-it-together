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

    if agent.is_in_group("bird"):
        random_offset.y = randf_range(5, 15)  # Ensure birds fly above ground level

    var random_point = agent.position + random_offset
    agent.set("targetPosition", random_point)
    
    return Status.SUCCESS