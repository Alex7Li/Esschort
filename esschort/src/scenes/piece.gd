extends Control

@onready var icon_path = $Icon
var type : DataHandler.PieceName
var color : int
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass # Replace with function body.


func load_icon(piece_name, piece_color) -> void:
	icon_path.texture = load(DataHandler.get_piece_asset(piece_name, piece_color))
