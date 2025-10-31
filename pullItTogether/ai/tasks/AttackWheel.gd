class_name AttackWheel
extends BTAction

func _tick(_delta: float) -> Status:
    if agent == null:
        return Status.FAILURE

    if agent.call("AttackWheel") == true:
        return Status.SUCCESS
    else:
        return Status.FAILURE