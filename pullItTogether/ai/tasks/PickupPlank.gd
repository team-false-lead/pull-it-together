class_name PickupPlank
extends BTAction

func _tick(_delta: float) -> Status:
    if agent == null:
        return Status.FAILURE

    if agent.call("PickupPlank") == true:
        return Status.SUCCESS
    else:
        return Status.FAILURE