extends ColorRect

@onready var piece_scene = preload("res://src/scenes/piece.tscn")
@onready var filter_path = $Margin/Filter
var slot_id := -1
var piece_child = null
signal slot_clicked(slot)

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass # Replace with function body.

func set_piece_from_data(piece_type, piece_color) -> void:
	var piece = piece_scene.instantiate()
	self.set_piece(piece)
	piece.type = piece_type
	piece.color = piece_color
	piece.load_icon(piece.type, piece.color)


func set_piece(p, free_existing=true) -> void:
	if piece_child != null:
		remove_child(piece_child)
		if free_existing:
			piece_child.free()
	if p != null:
		add_child(p)
	piece_child = p

	
func get_piece():
	return piece_child

func set_background(c) -> void:
	color = c

func set_filter(to_color = DataHandler.target_types.NONE) -> void:
	match to_color:
		DataHandler.target_types.NONE:
			filter_path.color = Color(0,0,0,0)
		DataHandler.target_types.CANTARGET:
			filter_path.color = Color(0,1,0,0.4)
		DataHandler.target_types.SELECTED:
			filter_path.color = Color(0,.5,.5,0.4)
		DataHandler.target_types.JUSTMOVED:
			filter_path.color = Color(.3,.3,.3,0.6)

func set_filter_color(to_color : Color) -> void:
	filter_path.color = to_color;

func _on_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		emit_signal("slot_clicked", self, event)
