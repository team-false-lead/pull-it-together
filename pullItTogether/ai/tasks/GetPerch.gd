class_name GetPerch
extends BTAction

@export var wagon_perch : bool = false

func _tick(_delta: float) -> Status:
    if agent == null:
        return Status.FAILURE

    if wagon_perch:
        if agent.call("GetClosestWagonPerch") == true:
            return Status.SUCCESS
    else:
        if agent.call("GetRandomWorldPerch") == true:
            return Status.SUCCESS

    return Status.FAILURE