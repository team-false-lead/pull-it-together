extends BTAction

func _tick(_delta: float) -> Status:
    if agent == null:
        return Status.FAILURE

    if agent.call("AttackWheel") == true:
        agent.apply_central_impulse(Vector3.UP * 2.5) # small jump when attacking
        return Status.SUCCESS
    else:
        return Status.FAILURE