extends Node

var armyFile = "user://savedarmies.save"
var N_ARMY_SLOTS = 5

func getDefaultArmies():
	var armies = []
	for i in range(N_ARMY_SLOTS):
		armies.append("pppppppprnbqkbnr")
	return {'armies': armies, 'curIndex': 0}

func loadAllArmies():
	if not FileAccess.file_exists(armyFile):
		return getDefaultArmies()
	var save_file = FileAccess.open(armyFile, FileAccess.READ)
	var json = JSON.new()
	var json_string = save_file.get_line()
	var parse_result = json.parse(json_string)
	if not parse_result == OK:
		print("Error parsing armies: ", json.get_error_message(), " in ", json_string, " at line ", json.get_error_line())
		return getDefaultArmies()
	return json.data

func saveCurrentArmy(army, army_index):
	var allArmies = loadAllArmies()
	allArmies['armies'][army_index] = army
	allArmies['curIndex'] = army_index
	var save_file = FileAccess.open(armyFile, FileAccess.WRITE)
	save_file.store_line(JSON.stringify(allArmies))
