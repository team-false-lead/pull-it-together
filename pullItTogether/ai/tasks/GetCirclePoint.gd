class_name GetStalkPoint
extends BTAction

func _tick(_delta: float) -> Status:
    if agent == null:
        return Status.FAILURE

    if agent.call("GetCirclePointAroundWagon") == true:
        return Status.SUCCESS
    else:
        return Status.FAILURE