extends BTAction

func _tick(_delta: float) -> Status:
	if agent == null:
		return Status.FAILURE

	if agent.call("PickupItem") == true:
		return Status.SUCCESS
	else:
		return Status.FAILURE
