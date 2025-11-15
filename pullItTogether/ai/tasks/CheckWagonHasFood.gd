extends BTAction

func _tick(_delta: float) -> Status:
    if agent == null:
        return Status.FAILURE

    if agent.call("CheckWagonHasFood") == true:
        return Status.SUCCESS

    return Status.FAILURE