class_name LookAtWagon
extends BTAction

func _tick(_delta: float) -> Status:
	if agent == null:
		return Status.FAILURE

	var wagon = agent.get("wagonTarget")
	var target_to_look = wagon.global_transform.origin
	agent.look_at(target_to_look, Vector3.UP)
	agent.rotation_degrees.x = 90

	return Status.SUCCESS
