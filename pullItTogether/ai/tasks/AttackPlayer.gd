class_name AttackPlayer
extends BTAction

func _tick(_delta: float) -> Status:
    if agent == null:
        return Status.FAILURE

    if agent.call("AttackPlayer") == true:
        return Status.SUCCESS
    else:
        return Status.FAILURE