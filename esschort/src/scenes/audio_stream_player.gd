extends AudioStreamPlayer

enum Sound{CAPTURE, MOVE, INVALID_MOVE};
var sounds := {
	Sound.CAPTURE: preload("res://sounds/lichess/Capture.ogg"),
	Sound.MOVE: preload("res://sounds/lichess/Move.ogg"),
	Sound.INVALID_MOVE: preload("res://sounds/lichess/OutOfBound.ogg"),
}

func playSound(soundType: Sound):
	self.stream = sounds[soundType];
	self.play()
