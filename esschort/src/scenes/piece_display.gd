extends VBoxContainer

@onready var move_display_slot = preload("res://src/scenes/move_display_slot.tscn")
@onready var move_description = preload("res://src/scenes/move_description.tscn")
@onready var piecename_label = $"../PieceName"
@onready var metadata_label = $"../MetadataLabel"
@onready var move_display = $MoveDisplay

var move_display_array := []
var move_description_array := []

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	for i in range(15):
		var move_display_row = []
		for j in range(15):
			var new_slot = move_display_slot.instantiate()
			move_display_row.push_back(new_slot)
			if (i + j) % 2 == 0:
				move_display_row[j].color = Color.GAINSBORO
			else:
				move_display_row[j].color = Color.CORNSILK
			move_display.add_child(new_slot)
		move_display_array.push_back(move_display_row)
		metadata_label.text = ""

var moveTypeDict = {
	-1: 'self',
	0: null,
	1: 'move_attack',
	2: 'attack',
	3: 'move',
	4: 'move_from_2nd_rank',
	5: 'resign'
}

var moveDescriptionDict = {
	1: 'Move or Attack',
	2: 'Attack Only',
	3: 'Move Only',
	4: 'Move from 2nd rank',
	5: 'Resign'
}

var piece_to_moveset = {
	DataHandler.PieceName.NONE: DataHandler.none_moves,
	DataHandler.PieceName.PAWN: DataHandler.pawn_moves,
	DataHandler.PieceName.KNIGHT: DataHandler.knight_moves,
	DataHandler.PieceName.BISHOP: DataHandler.bishop_moves,
	DataHandler.PieceName.ROOK: DataHandler.rook_moves,
	DataHandler.PieceName.QUEEN: DataHandler.queen_moves,
	DataHandler.PieceName.KING: DataHandler.king_moves,
}

var display_color = {
	-1: Color.BLACK,
	0: Color.TRANSPARENT,
	1: Color.DIM_GRAY,
	2: Color.FIREBRICK,
	3: Color.CADET_BLUE,
	4: Color.CADET_BLUE,
	5: Color.BLACK,
}

var display_symbol = {
	-1: null,
	0: null,
	1: null,
	2: null,
	3: null,
	4: load("res://assets/move_from_starting_position.png"),
	5: load('res://assets/flag.png'),
}

func setup_move_display_slot(parent_slot, move_type: int):
	var move_slot = parent_slot.get_node('Margin').get_node('MoveTypeRect')
	move_slot.color = display_color[move_type]
	move_slot.get_node('Margin').get_node('Sprite2D').texture = display_symbol[move_type]
		
func set_display_to_piece(piece_name: DataHandler.PieceName, piece_color: DataHandler.PieceColor):
	var piece_moves = piece_to_moveset[piece_name].duplicate()
	if piece_color == DataHandler.PieceColor.BLACK:
		piece_moves.reverse()
	var move_type_set = {}

	for i in range(15):
		for j in range(15):
			var move_type = piece_moves[i][j]
			move_type_set[move_type] = null
			setup_move_display_slot(move_display_array[i][j], move_type)

	var all_move_types_for_piece = move_type_set.keys()
	all_move_types_for_piece.sort()
	for move_descripion_node in move_description_array:
		move_descripion_node.free()
	move_description_array.clear()
	for move_type in all_move_types_for_piece:
		if move_type > 0:
			var move_description_node = move_description.instantiate()
			setup_move_display_slot(move_description_node.get_node('MoveDisplaySlot'), move_type)
			move_description_node.get_node('Label').text = moveDescriptionDict[move_type]
			move_description_array.push_back(move_description_node)
			add_child(move_description_node)
		metadata_label.text = "Value " + str(DataHandler.piece_cost_dict[piece_name])
		piecename_label.text = DataHandler.PieceName.keys()[piece_name]
