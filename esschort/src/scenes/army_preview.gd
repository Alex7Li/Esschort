extends GridContainer

@onready var slot_scene = preload("res://src/scenes/slot.tscn")

var slots = []
var MAX_ARMY_VALUE = 500

func _ready() -> void:
	for i in range(16):
		var slot = slot_scene.instantiate()
		self.add_child(slot)
		slots.append(slot)
		slot.slot_id = i

func get_army_value():
	var total_value = 0
	for i in range(16):
		var piece_at_slot = slots[i].get_piece()
		if piece_at_slot != null:
			total_value += DataHandler.piece_cost_dict[piece_at_slot.type]
	return total_value

func errors():
	var failure_string = ""
	var king_count = 0
	for i in range(16):
		var piece_at_slot = slots[i].get_piece()
		if piece_at_slot.type == DataHandler.PieceName.KING:
			king_count += 1
	if king_count != 1:
		failure_string += "Expected 1 king but got " + str(king_count) + "\r\n"
	var value = get_army_value()
	if value > MAX_ARMY_VALUE:
		failure_string += "Army value is greater than the maximum of " + str(MAX_ARMY_VALUE)  + "\r\n"
	return failure_string

func setupArmy(army):
	for i in range(16):
		slots[i].set_piece_from_data(DataHandler.fen_dict[army[i]], DataHandler.PieceColor.WHITE)
