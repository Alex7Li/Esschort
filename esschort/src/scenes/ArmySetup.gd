extends Control
@onready var draft = $HBoxContainer/VBoxContainer/PieceDraft
@onready var preview = $HBoxContainer/VBoxContainer/ArmyPreview
@onready var info_board = $HBoxContainer/InfoBoard/VBoxContainer/FlowContainer
@onready var value_display = $HBoxContainer/VBoxContainer2/ValueDisplay
@onready var quick_swap = $HBoxContainer/VBoxContainer2/QuickSwap

var last_selected_ind = 0
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	for i in range(16):
		preview.slots[i].slot_clicked.connect(_on_slot_clicked)
	var selected_ind = 0
	if len(quick_swap.get_selected_items()):
		selected_ind = quick_swap.get_selected_items()[0]
	var saved_armies = SaveState.loadAllArmies()
	var army_index = saved_armies['curIndex']
	var start_army = saved_armies['armies'][army_index]
	preview.setupArmy(start_army)
	quick_swap.select(army_index)
	last_selected_ind = army_index
	value_display.text = "Value " +  str(preview.get_army_value())

func save() -> void:
	var army = ''
	for i in range(16):
		army += DataHandler.inv_fen_dict[preview.slots[i].get_piece().type]
	SaveState.saveCurrentArmy(army, last_selected_ind)


func _on_slot_clicked(slot, event: InputEventMouseButton) -> void:
	if not (event.pressed):
		return
	info_board.set_display_to_piece(slot.get_piece().type, DataHandler.PieceColor.WHITE)
	if event.button_index == MOUSE_BUTTON_LEFT:
		draft.select_piece(slot.get_piece().type)
	elif event.button_index == MOUSE_BUTTON_RIGHT:
		var selected_inds = draft.get_selected_items()
		if len(selected_inds) > 0:
			var selected_piece_type = draft.list_elements[selected_inds[0]]
			slot.set_piece_from_data(
				selected_piece_type, DataHandler.PieceColor.WHITE)
			#if not event.shift_pressed:
				#draft.deselect_all()
				#info_board.set_display_to_piece(DataHandler.PieceName.NONE, DataHandler.PieceColor.WHITE)
			value_display.text = "Value " +  str(preview.get_army_value()) + " " + preview.errors()
	

func _on_piece_draft_item_selected(index: int) -> void:
	var selected_piece_type = draft.list_elements[index]
	info_board.set_display_to_piece(selected_piece_type, DataHandler.PieceColor.WHITE)


func _on_gui_input(event: InputEvent) -> void:
	if event.is_action_pressed("mouse_left"):
		draft.deselect_all()
		info_board.set_display_to_piece(DataHandler.PieceName.NONE, DataHandler.PieceColor.WHITE)


func _on_battle_button_pressed() -> void:
	if (!preview.errors()):
		save()
	get_tree().change_scene_to_file("res://src/scenes/gui.tscn")


func _on_quick_swap_item_selected(index: int) -> void:
	save()
	last_selected_ind = index
	var saved_armies = SaveState.loadAllArmies()
	var start_army = saved_armies['armies'][index]
	preview.setupArmy(start_army)
	value_display.text = "Value " +  str(preview.get_army_value())
