class_name MoveTo
extends BTAction

@export var acceptance_radius: float = 1
@export var slowdown_radius: float = 2

func _tick(_delta: float) -> Status:
	if agent == null:
		return Status.FAILURE

	var current_position = agent.call("GetInventorySlot").global_transform.origin #agent.global_transform.origin
	var target_position = agent.get("targetPosition")
	var distance_to_target = current_position.distance_to(target_position)
	#print("Distance to target: ", distance_to_target)
	if distance_to_target <= acceptance_radius:
		agent.linear_velocity = Vector3.ZERO
		return Status.SUCCESS

	var speed = agent.get("movementSpeed")
	if distance_to_target <= slowdown_radius:
		speed /= 2
	
	var direction = (target_position - current_position).normalized()
	var desired_velocity = direction * speed
	agent.linear_velocity = desired_velocity

	if agent.linear_velocity.length() > 0.1:
		agent.look_at(current_position + agent.linear_velocity, Vector3.UP)
		agent.rotation_degrees.x = 90

	return Status.RUNNING
