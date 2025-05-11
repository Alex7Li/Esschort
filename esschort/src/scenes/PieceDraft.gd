extends ItemList

var list_elements = []

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	for piece:DataHandler.PieceName in DataHandler.PieceName.values():
		if piece == DataHandler.PieceName.NONE:
			continue
		var piece_icon = DataHandler.get_piece_asset(
			piece, DataHandler.PieceColor.WHITE)
		list_elements.append(piece)
		self.add_icon_item(load(piece_icon))

func select_piece(piece_type: int):
	for i in range(len(list_elements)):
		if list_elements[i] == piece_type:
			self.select(i)
