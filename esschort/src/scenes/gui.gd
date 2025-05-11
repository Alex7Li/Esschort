extends Control

@onready var slot_scene = preload("res://src/scenes/slot.tscn")
@onready var board_grid = $BattleContainer/VBoxContainer/AspectRatioContainer/MarginContainer/Chessboard/BoardGrid
@onready var chess_board = $BattleContainer/VBoxContainer/AspectRatioContainer/MarginContainer/Chessboard
@onready var boardState =  $GameInterface
@onready var ai_battle = $BattleContainer/DebugUI
@onready var info_board = $BattleContainer/InfoBoard/VBoxContainer/FlowContainer
@onready var audio = $AudioStreamPlayer
var grid_array := []
var selected_slot_ind : int = -1
var waiting_for_ai = false
var white_is_ai = false
var black_is_ai = false
var autoplay_next_game = false
var lastMoveDeltaBitmap = 0
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	boardState.call('setAI', 'v1', false);

	for i in range(64):
		create_slot()
	for i in range(8) :
		for j in range(8):
			if (i + j) % 2 == 0:
				grid_array[i * 8 + j].set_background(Color.BEIGE)
			else:
				grid_array[i * 8 + j].set_background(Color.BROWN)


func start_ai_thread():
	# make ai move
	waiting_for_ai = boardState.call('startAIMoveProcess')
	if (!waiting_for_ai):
		print("AI thread did not start for some rea	son")

func make_ai_move(ai_move: int):
	@warning_ignore("integer_division")
	var from = ai_move / 64;
	var to = ai_move % 64;
	move_piece(from, to)
	var result = boardState.call("gameResult")
	ai_battle.update_game_state_text(boardState.call("getFen") + "\n" + boardState.call('getPgn'))
	if result != "":
		on_game_over(result)
		ai_battle.update_game_result(result)
		if autoplay_next_game:
			setup_fen(ai_battle.random_fen())

func _process(_delta) -> void:
	if waiting_for_ai:
		var result = boardState.call("pollAiThread")
		if result != -2: # still waiting
			waiting_for_ai = false
			make_ai_move(result)


func setup_fen(fen, also_start_game=true):
	boardState.call("SetupBoard", fen)
	var board_index = 0
	var i = 0
	for j in range(64):
		if grid_array[j].get_piece():
			grid_array[j].get_piece().free()

	while i < len(fen):
		if fen[i] == ' ': break;
		if fen[i] == '/': i+=1; continue
		elif fen[i].is_valid_int(): board_index += fen[i].to_int(); i+=1;
		else:
			var square_string = fen[i];
			if fen[i] == '(':
				i += 1
				square_string = ""
				while fen[i] != ')':
					square_string += fen[i]
					i += 1
			var piece_name = square_string.to_lower()
			var piece_color = DataHandler.PieceColor.WHITE
			if piece_name[0] == square_string[0]:
				piece_color = DataHandler.PieceColor.BLACK
			var piece_type = DataHandler.fen_dict[piece_name]
			add_piece(piece_type, piece_color, board_index)
			i += 1
			board_index += 1
	var whiteToMove = boardState.call("whiteToMove")
	if whiteToMove and white_is_ai and also_start_game:
		start_ai_thread()
	if !whiteToMove and black_is_ai and also_start_game:
		start_ai_thread()


func add_piece(piece_type, piece_color, location) -> void:
	board_grid.get_child(location).set_piece_from_data(piece_type, piece_color)

func set_selected_piece(slot: int) -> void:
	grid_array[slot].set_filter(DataHandler.target_types.SELECTED)
	var pieceAt = grid_array[slot].get_piece()
	if pieceAt != null:
		info_board.set_display_to_piece(pieceAt.type, pieceAt.color)

func set_board_filter(bitmap: int) -> void:
	for i in range(64):
		if bitmap & (1 << i):
			grid_array[i].set_filter(DataHandler.target_types.CANTARGET)
		elif lastMoveDeltaBitmap & (1 << i):
			grid_array[i].set_filter(DataHandler.target_types.JUSTMOVED)
		else:
			grid_array[i].set_filter(DataHandler.target_types.NONE)

func display_piece_moves(slot_id): 
	var piece = grid_array[slot_id].get_piece()
	var targetableSquares = boardState.call("targetableSquaresWrapper", slot_id)
	var row = slot_id / 8
	var col = slot_id % 8
	for drow in range(-7, 8):
		for dcol in range(-7, 8):
			var nrow = drow + row
			var ncol = dcol + col
			if 0 <= nrow and nrow < 8 and 0 <= ncol and ncol < 8:
				var nLoc = nrow * 8 + ncol;
				var moveType = info_board.piece_to_moveset[piece.type][(drow + 7)][dcol + 7]
				var moveColor = info_board.display_color[moveType].darkened(.5)
				if targetableSquares & (1 << nLoc):
					grid_array[nLoc].set_filter_color(moveColor)
				elif moveType != 0:
					moveColor.a *= .15
					grid_array[nLoc].set_filter_color(moveColor)
				else:
					grid_array[nLoc].set_filter_color(Color.TRANSPARENT)

func create_slot():
	var new_slot = slot_scene.instantiate()
	new_slot.slot_id = grid_array.size()
	new_slot.slot_clicked.connect(_on_slot_clicked)
	grid_array.push_back(new_slot)
	board_grid.add_child(new_slot)


func on_game_over(result: String):
	print("GAME OVER ", result)
	pass
	#setup_fen("8/8/8/8/8/8/8/8")
	#boardState.call("ResetBoard")

func _on_slot_clicked(slot, event: InputEventMouseButton) -> void:
	if event != null and not (event.button_index == MOUSE_BUTTON_LEFT and event.pressed):
		return
	if waiting_for_ai:
		return
	else:
		if grid_array[slot.slot_id].get_piece() != null:
			if selected_slot_ind == -1:
				selected_slot_ind = slot.slot_id
				if grid_array[slot.slot_id].get_piece() != null:
					display_piece_moves(selected_slot_ind)
					set_selected_piece(selected_slot_ind)
					return

	if selected_slot_ind == -1 or waiting_for_ai:
		return
	# TODO check that it is your move/not processsing
	set_board_filter(0)
	var made_move = move_piece(selected_slot_ind, slot.slot_id)	
	selected_slot_ind = -1

	if made_move:
		var result = boardState.call("gameResult");
		if result != "":
			on_game_over(result)
			return
	else:
		if grid_array[slot.slot_id].get_piece():
			_on_slot_clicked(slot, null)
		else:
			audio.playSound(audio.Sound.INVALID_MOVE)

func apply_UI_Update(UIUpdate):
	var soundType = audio.Sound.MOVE
	lastMoveDeltaBitmap = 0
	for submove in UIUpdate.split(';'):
		var move_type = submove[0]
		var move_data = submove.right(-1).split('_')
		if move_type == "R":
			var index_to_drop = int(move_data[0])
			board_grid.get_child(index_to_drop).set_piece(null)
			lastMoveDeltaBitmap |= 1 << index_to_drop;
			soundType = audio.Sound.CAPTURE
		elif move_type == 'M':
			var move_from = int(move_data[0])
			var move_to = int(move_data[1])
			var move_from_piece = board_grid.get_child(move_from).get_piece()
			board_grid.get_child(move_from).set_piece(null, false)
			board_grid.get_child(move_to).set_piece(move_from_piece)
			lastMoveDeltaBitmap |= 1 << move_to;
			lastMoveDeltaBitmap |= 1 << move_from;
		elif move_type == 'A':
			var piece_color;
			var index_to_add = int(move_data[0])
			if move_data[1] == 'w':
				piece_color = DataHandler.PieceColor.WHITE
			else:
				assert(move_data[1] == 'b')
				piece_color = DataHandler.PieceColor.BLACK
			var piece_type = DataHandler.fen_dict[str(move_data[2])]
			add_piece(piece_type, piece_color, index_to_add)
			lastMoveDeltaBitmap |= 1 << index_to_add;			
	set_board_filter(0)
	audio.playSound(soundType)


func move_piece(from: int, to: int) -> bool:
	var UIUpdate = boardState.call("playMoveWrapper", from, to)
	if UIUpdate == "":
		return false
	apply_UI_Update(UIUpdate)
	ai_battle.update_game_state_text(boardState.call("getFen") + "\n" + boardState.call('getPgn'))
	if boardState.call('gameResult') == '': # still in progress
		var white_to_move = boardState.call("whiteToMove")
		if white_to_move and white_is_ai:
			start_ai_thread()
		elif not white_to_move and black_is_ai:
			start_ai_thread()
	return true


func _on_button_pressed() -> void:
	# Setup the board for human v computer
	boardState.call('setAI', 'v1', false);
	boardState.call('setAI', 'v1', true);
	white_is_ai = false;
	black_is_ai = true;
	var loaded_data = SaveState.loadAllArmies()
	var human_army = loaded_data['armies'][loaded_data['curIndex']]

	var loaded_fen = ai_battle.make_fen(human_army.reverse(), ai_battle.random_setup(), true)
	#loaded_fen = 'r2krnbq/pp1ppnpp/3b1p2/2p5/2BPP3/1PN2NP1/P1P2P1P/R1BQK2R/ b'
	setup_fen(loaded_fen)
	ai_battle.update_game_state_text(boardState.call("getFen"))


func _on_ai_battle_button_pressed() -> void:
	# Setup the board for computer v computer
	boardState.call('setAI', ai_battle.whiteAIVersion, true);
	boardState.call('setAI', ai_battle.blackAIVersion, false);
	print("WHITE v1 VS BLACK v2")
	white_is_ai = true;
	black_is_ai = true;
	autoplay_next_game = false
	#setup_fen(ai_battle.random_fen())
	#setup_fen("nkbnrbrq/pppppppp/8/8/8/8/PPPPPPPP/RQBNRKNB b")
	setup_fen("k1r5/8/pprb3Q/p3p1p1/4P3/5P2/3P2PP/R1BNRKNn/ w")

func _on_undo_pressed() -> void:
	var UIUpdate = boardState.call('undoLast');
	if UIUpdate != "":
		apply_UI_Update(UIUpdate)


func _on_go_to_army_setup_button_pressed() -> void:
	get_tree().change_scene_to_file("res://src/scenes/ArmySetup.tscn")
