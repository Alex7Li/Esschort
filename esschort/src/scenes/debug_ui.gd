extends VBoxContainer
@onready var text_child = $"RichTextLabel"
@onready var textedit = $"TextEdit"

var white_wins = 0
var black_wins = 0
var draws = 0
var whiteAIVersion = 'v2'
var blackAIVersion = 'v1'
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	text_child.set_text(str(white_wins) + "-" + str(black_wins))


func make_fen(white_setup: String, black_setup: String, white_to_move: bool) -> String:
	var fen = black_setup.to_lower() + '/8/8/8/8/' +  white_setup.to_upper().reverse()
	if white_to_move:
		fen += " w"
	else:
		fen += " b"
	return fen
func orderSort(a, b) -> bool:
	return a[0] < b[0]

func permute(s: String) -> String:
	var order = []
	for i in range(s.length()):
		order.append([randf(), i])

	order.sort_custom(orderSort)
	var out = ""
	for order_tuple in order:
		out += s[order_tuple[1]]
	return out
	
func random_setup() -> String:
	var back_rank = 'rrbbnnqk'
	var front_rank = 'pppppppp'
	return permute(back_rank) + '/' + front_rank

func random_fen() -> String:
	return make_fen(random_setup(), random_setup(), randf() > .5)

func update_game_state_text(fen: String) -> void:
	textedit.text = fen

func LL(x):
	return 1.0/(1.0+10.0**(-x/400.0))
	
func LLR(W,D,L,elo0,elo1):
	"""
	This function computes the log likelihood ratio of H0:elo_diff=elo0 versus
	H1:elo_diff=elo1 under the logistic elo model

	expected_score=1/(1+10**(-elo_diff/400)).

	W/D/L are respectively the Win/Draw/Loss count. It is assumed that the outcomes of
	the games follow a trinomial distribution with probabilities (w,d,l). Technically
	this is not quite an SPRT but a so-called GSPRT as the full set of parameters (w,d,l)
	cannot be derived from elo_diff, only w+(1/2)d. For a description and properties of
	the GSPRT (which are very similar to those of the SPRT) see

	http://stat.columbia.edu/~jcliu/paper/GSPRT_SQA3.pdf

	This function uses the convenient approximation for log likelihood
	ratios derived here:

	http://hardy.uhasselt.be/Toga/GSPRT_approximation.pdf

	The previous link also discusses how to adapt the code to the 5-nomial model
	discussed above.
	"""
	var N=W+D+L
	if N==0:
		return 0.0
	var w = W/N
	var d = D/N
	var s=w+d/2.0
	var m2=w+d/4.0
	var variance =m2-s**2
	var var_s=variance/N
	var s0=LL(elo0)
	var s1=LL(elo1)
	return (s1-s0)*(2*s-s0-s1)/var_s/2.0

func SPRT(W,D,L,elo0,elo1,alpha,beta):
	"""
This function sequentially tests the hypothesis H0:elo_diff=elo0 versus
the hypothesis H1:elo_diff=elo1 for elo0<elo1. It should be called after
each game until it returns either 'H0' or 'H1' in which case the test stops
and the returned hypothesis is accepted.

alpha is the probability that H1 is accepted while H0 is true
(a false positive) and beta is the probability that H0 is accepted
while H1 is true (a false negative). W/D/L are the current win/draw/loss
counts, as before.
"""
	var LLR_=LLR(W,D,L,elo0,elo1)
	var LA=log(beta/(1.0-alpha))
	var LB=log((1.0-beta)/alpha)
	if LLR_>LB:
		return 'H1'
	elif LLR_<LA:
		return 'H0'
	else:
		return '?%.2f %.2f %.2f' % [LA,LLR_, LB]

func update_game_result(result: String) -> bool:
	if result == "1-0":
		white_wins += 1
	elif result == "0-1":
		black_wins += 1
	elif result == "1/2-1/2":
		draws += 1
	else:
		print("INVALID GAME OVER")
	var sprt_result = SPRT(float(white_wins),float(draws),float(black_wins),0,10, 0.1, 0.1)
	var textout = '%s %d %s %d D %d\nsprt %s' % [whiteAIVersion, white_wins, blackAIVersion, black_wins, draws, sprt_result]
	text_child.set_text(textout)
	print(textout)
	return sprt_result[0] != '?'
