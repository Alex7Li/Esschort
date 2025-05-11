#define DEBUG
#undef DEBUG
#define USECACHE
using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;

public partial class Ai : Node
{
	const float LAZY_EVAL_EXTRA_MARGIN = 200.0f;
	int MAX_DEPTH_EXTENSION = 6;

	TranspositionTable transpositionTable = new TranspositionTable();
	HashSet<ulong> seenPositions = new HashSet<ulong>(100);
	float[,,] historyHeuristic = new float[2,64,64];
	float MAX_HISTORY = 1000;
	int ONE_PLY_DEPTH = 10;
	long end_t;
	int version;
	public Ai(int version) {
		this.version = version;
	}
	public void clearState() {
		transpositionTable.clear();
		seenPositions.Clear();
		Array.Clear(historyHeuristic, 0, historyHeuristic.Length);
	}


	public float nonLazyEvalBonus(BoardState boardState) {
		double whiteMobility = 0;
		double blackMobility = 0;
		ulong whiteOccupancyBitboard = 0UL;
		ulong blackOccupancyBitboard = 0UL;
		for (int fromSquare = 0; fromSquare < 64; fromSquare++) {
			if (Helpers.pieceFromData(boardState.curSquareData[fromSquare]) != Helpers.Piece.NONE) {
				if (Helpers.pieceIsWhiteFromData(boardState.curSquareData[fromSquare])) {
					whiteOccupancyBitboard |= 1UL << fromSquare;
				} else {
					blackOccupancyBitboard |= 1UL << fromSquare;
				}
			}
		}
		float whiteKingPositionBonus = 0;
		float blackKingPositionBonus = 0;
		float pieceLocationBonus = 0;

		foreach (int fromSquare in Helpers.iterateOverSetBitInds(whiteOccupancyBitboard)) {
			whiteMobility += BitOperations.PopCount(BoardState.targetableSquares(
				boardState, fromSquare));
			Helpers.Piece atSquare = Helpers.pieceFromData(boardState.curSquareData[fromSquare]);
			uint coloredPiece = boardState.curSquareData[fromSquare] & Helpers.pieceDataMask;
			switch(atSquare) {
				case (Helpers.Piece.KING):
					float kingAdvancementBonus = Math.Clamp(((float)boardState.blackMaterial - 2560) / 8096, -.25f, .25f);
					float kingAdvancement = fromSquare / 8;
					whiteKingPositionBonus += kingAdvancementBonus * kingAdvancement;
					break;
				default:
					pieceLocationBonus += Helpers.pieceLocationBonus[coloredPiece][fromSquare];
				break;
			}
		}
		foreach (int fromSquare in Helpers.iterateOverSetBitInds(blackOccupancyBitboard)) {
			blackMobility += BitOperations.PopCount(BoardState.targetableSquares(
				boardState, fromSquare));
			Helpers.Piece atSquare = Helpers.pieceFromData(boardState.curSquareData[fromSquare]);
			uint coloredPiece = boardState.curSquareData[fromSquare] & Helpers.pieceDataMask;
			switch(atSquare) {
				case (Helpers.Piece.KING):
					float kingAdvancementBonus = Math.Clamp(((float)boardState.whiteMaterial - 2560) / 8096, -.25f, .25f);
					float kingAdvancement = 7 - fromSquare / 8; // higher is closer to other side.
					blackKingPositionBonus += kingAdvancementBonus * kingAdvancement;
					break;
				default:
					pieceLocationBonus -= Helpers.pieceLocationBonus[coloredPiece][fromSquare];
				break;
			}
		}
		// if (this.version == 1) {
		return (float)(whiteMobility - blackMobility) / 100.0f + whiteKingPositionBonus - blackKingPositionBonus + pieceLocationBonus;
		// } else {
			// return (float)(whiteMobility - blackMobility) / 100.0f;
		// }
	}

	public float evaluatePositionSimple(BoardState boardState) {
		// Evaluate the position (positive means better for white).
		// todo use quinesence search
		float totalMaterial = boardState.whiteMaterial + boardState.blackMaterial;
		float imbalance = (boardState.whiteMaterial - boardState.blackMaterial) / 100.0f;
		// imblance is magnified when there are less pieces on the board
		imbalance /= (1 + totalMaterial / 5000.0f);
		return imbalance;
	}

	public string getMainline(BoardState boardState, int depth) {
		if (depth < 0) {
			return "";
		}
		BoardState boardStateCopy = new BoardState(boardState);

		ulong curHash = Hasher.hashPosition(boardStateCopy);
		TranspositionTableEntry entry = transpositionTable.getTableEntry(curHash);
		if (entry == null) {
			return "...";
		}
		MoveHelper best_response = entry.refutationMovePacked;
		BoardUpdate move = boardStateCopy.makeMove(best_response, true);
		if (move == null) {
			return "... HASH FAILED";
		}
		move.apply(boardStateCopy, curHash);
		string best_response_readable = best_response.toNotation();
		return best_response_readable + " " + getMainline(boardStateCopy, depth-1);
	}

	public IEnumerable<MoveHelper> enumerateOverMoves(BoardState boardState, List<MoveHelper> refutationMoves, bool isInQuiescenseStage=false) {
		// Check the refutation move first
		ulong allyOccupancyBitboard = boardState.getAllyOccupancyBitBoard(boardState.whiteToMove);
		// ulong foeOccupancyBitboard = boardState.getAllyOccupancyBitBoard(!boardState.whiteToMove);
		if (refutationMoves != null) {
			foreach (MoveHelper refutationMove in refutationMoves) {
				// make sure the refutation move is still valid before trying
				if ((BoardState.targetableSquares(boardState, refutationMove.from()) & (1UL << refutationMove.to())) != 0) {
					yield return refutationMove;
				}
			}
		}
		List<MoveHelper> allMoves = new List<MoveHelper>();
		// Generate all of the moves
		foreach (int fromSquare in Helpers.iterateOverSetBitInds(allyOccupancyBitboard)) {
			ulong targetableSquares = BoardState.targetableSquares(boardState, fromSquare);
			foreach (int toSquare in Helpers.iterateOverSetBitInds(targetableSquares)) {
				int attackValue = boardState.checkMaterialChangeAfterMove(fromSquare, toSquare);
				if (!isInQuiescenseStage || (attackValue > 0)) {
					allMoves.Add(new MoveHelper(fromSquare, toSquare, attackValue));
				}
			}
		}
		if (isInQuiescenseStage) {
			yield return new MoveHelper(); // Null move
		}
		// Play the moves in priority order using selection sort.
		for(int movesTested = 0; movesTested < allMoves.Count(); movesTested++) {
			float bestHeuristicScore = -100000000;
			int bestInd = -1;
			for(int i = movesTested; i < allMoves.Count(); i++) {
				float heuristicScore = allMoves[i].attackValue();
				heuristicScore += this.historyHeuristic[Convert.ToInt32(boardState.whiteToMove), allMoves[i].from(), allMoves[i].to()];
				if (heuristicScore > bestHeuristicScore) {
					bestHeuristicScore = heuristicScore;
					bestInd = i;
				}
			}
			yield return allMoves[bestInd];
			allMoves[bestInd] = allMoves[movesTested]; // Remove the move at this checked index and replace it with the move at index we are about to ignore.
		}
	}

	private void updateHistory(bool whiteToMove, MoveHelper move, float bonus){
		historyHeuristic[Convert.ToInt32(whiteToMove), move.from(), move.to()] += bonus -
			historyHeuristic[Convert.ToInt32(whiteToMove), move.from(), move.to()] * Math.Abs(bonus) / MAX_HISTORY;
		if (Math.Abs(historyHeuristic[Convert.ToInt32(whiteToMove), move.from(), move.to()]) > MAX_HISTORY) {
			GD.Print(bonus);
		}
	}
	public SearchResultType flipResultType(SearchResultType refutationType){
		if (refutationType == SearchResultType.EXACT) {
			return SearchResultType.EXACT;
		} else if (refutationType == SearchResultType.AT_LEAST_BETA) {
			return SearchResultType.AT_MOST_ALPHA;
		} else if (refutationType == SearchResultType.AT_MOST_ALPHA) {
			return SearchResultType.AT_LEAST_BETA;
		}
		return SearchResultType.NOT_FULLY_SEARCHED;
	}


	public TranspositionTableEntry Search(BoardState boardState, float alpha, float beta, 
		int targetDepth, int curDepth, ulong curHash, int quiescenceExtension=0) {
		// alpha: From this position, we know that there is a move so that perfect play at this
		// depth gives a score of alpha or more.
		// beta: There was a move in a previous position which would give a score of beta that we
		// chose not to play, so if we can score higher than beta, this position isn't one reachable
		// by perfect play.
		bool isInQuiescenseStage = 0<quiescenceExtension;
		int searchDepth = targetDepth - curDepth;
		TranspositionTableEntry entry = transpositionTable.getTableEntry(curHash);
		List<MoveHelper> refutationMoves = new List<MoveHelper>();
		#if USECACHE
		if (entry != null) {
			if (entry.searchDepth >= searchDepth) {
				if (entry.type == SearchResultType.EXACT) {
					return entry;
				} else if (entry.type == SearchResultType.AT_MOST_ALPHA) {
					if (entry.eval <= alpha) {
						return entry;
					}
				} else if (entry.type == SearchResultType.AT_LEAST_BETA) {
					if (entry.eval >= beta) {
						return entry;
					}
				}
			}
		}
		#endif
		#if DEBUG
			string printPrefix = "" + curDepth / ONE_PLY_DEPTH;
			for (int i = 0; i < curDepth / ONE_PLY_DEPTH; i++) {
				printPrefix += "  ";
			}
		#endif
		if (entry != null) {
				// Add the refutation move if it is a valid move choice.
				refutationMoves.Add(entry.refutationMovePacked);
		}
		int is_white_sign = -1;
		if (boardState.whiteToMove) {
			is_white_sign = 1;
		}
		float bestMoveScore = -10_000;
		SearchResultType exitType = SearchResultType.EXACT;
		MoveHelper bestMove = new MoveHelper();
		int rootMaterial = boardState.whiteMaterial - boardState.blackMaterial;
		List<MoveHelper> movesSeenSoFar = new List<MoveHelper>();
		int ind = 0;
		bool checkedNoMoves = true;
		foreach (MoveHelper movePacked in enumerateOverMoves(boardState, refutationMoves, isInQuiescenseStage: isInQuiescenseStage)) {
			checkedNoMoves = false;
			int from = movePacked.from();
			int to = movePacked.to();
			float moveScore;
			ind += 1;
		
			BoardUpdate move = boardState.makeMove(movePacked);
			BoardUpdate invMove = move.apply(boardState, curHash);
			// int materialGain = (boardState.whiteMaterial - boardState.blackMaterial - rootMaterial) * is_white_sign;
			ulong nextMoveHash = invMove.preMoveHash;
			SearchResultType refutationType = SearchResultType.EXACT;
			switch (move.stateAfterMove) {
				case BoardState.GameState.WHITE_WIN:
					moveScore = (10_000 - boardState.movesPlayed.Count) * is_white_sign;
					break;
				case BoardState.GameState.BLACK_WIN:
					moveScore = -(10_000 - boardState.movesPlayed.Count) * is_white_sign;
					break;
				case BoardState.GameState.TWOFOLD_REPETITION:
					moveScore = -2.5f;
					break;
				case BoardState.GameState.DRAW:
					moveScore = -2.5f;
					break;
				default:
					if (isInQuiescenseStage || curDepth >= targetDepth) {
						// It's opponent to move, we assume that we will be able to improve our position
						// and the current evaluation is a lower bound for what we can get.
						moveScore = evaluatePositionSimple(boardState) * is_white_sign;
						if (moveScore + LAZY_EVAL_EXTRA_MARGIN >= alpha) {
							// Ok it's close enough to alpha let's be accurate
							moveScore += nonLazyEvalBonus(boardState) * is_white_sign;
						}
						// We just played a non-null move, so we should check for the opponent responses if there's enough compute
						if (movePacked.isValid() && quiescenceExtension < MAX_DEPTH_EXTENSION) { 
							TranspositionTableEntry searchResult = Search(boardState, -beta, -alpha, targetDepth, curDepth + ONE_PLY_DEPTH, nextMoveHash, quiescenceExtension: quiescenceExtension + 1);
							moveScore = -searchResult.eval;
							refutationType = searchResult.type;
						}
					} else {
						TranspositionTableEntry searchResult = Search(boardState, -beta, -alpha, targetDepth, curDepth + ONE_PLY_DEPTH, nextMoveHash, quiescenceExtension: 0);
						moveScore = -searchResult.eval;
						refutationType = searchResult.type;
					}
				#if DEBUG
					if (is_white_sign == 1){
						GD.Print(printPrefix + movePacked.toNotation() + " " + moveScore.ToString("0.00") + " " + alpha.ToString("0.00") + " " + beta.ToString("0.00"));
					} else {
						GD.Print(printPrefix + movePacked.toNotation() + " " + (-moveScore).ToString("0.00") + " " + (-beta).ToString("0.00") + " " + (-alpha).ToString("0.00"));
					}
				#endif
				break;
			}
			invMove.apply(boardState, nextMoveHash);
			if (refutationType == SearchResultType.NOT_FULLY_SEARCHED || DateTimeOffset.Now.ToUnixTimeMilliseconds() > this.end_t) {
				exitType = SearchResultType.NOT_FULLY_SEARCHED;
				break;
			}

			if (moveScore > bestMoveScore) {
				bestMoveScore = moveScore;
				bestMove = movePacked;
				exitType = flipResultType(refutationType);
			}
			if (moveScore >= beta) {
				exitType = SearchResultType.AT_LEAST_BETA;
				int bonus = searchDepth * (30 / ONE_PLY_DEPTH) + 5;
				// mark this move as good
				updateHistory(boardState.whiteToMove,  movePacked, bonus);
				// mark other moves we tried as not so good
				foreach (MoveHelper otherMove in movesSeenSoFar) {
					updateHistory(boardState.whiteToMove, otherMove, -bonus);
				}
				break;
			}
			movesSeenSoFar.Add(movePacked);
			if (moveScore > alpha) {
				alpha = moveScore;
			}
		}
		if (checkedNoMoves) {
			if (isInQuiescenseStage) { // no captures or interesting moves to consider
				return new TranspositionTableEntry(curHash, searchDepth, new MoveHelper(), bestMoveScore, SearchResultType.EXACT);
			} else { // Stalemate, must resign
				return new TranspositionTableEntry(curHash, searchDepth, new MoveHelper(), -10_000 + boardState.movesPlayed.Count, SearchResultType.EXACT);
			}
		}

		#if DEBUG
			string exitStr = "timeout";
			if (exitType == SearchResultType.EXACT) {
				exitStr = "=";
			} else if (exitType == SearchResultType.AT_LEAST_BETA) {
				exitStr = "opponent would not enter this line <" + beta.ToString("0.00");
			} else if (exitType == SearchResultType.AT_MOST_ALPHA) {
				exitStr = "worse than alternative searched move <" + alpha.ToString("0.00");
			}
			GD.Print(printPrefix + bestMove.toNotation() + ": " + (is_white_sign * bestMoveScore).ToString("0.00") + exitStr) ;
		#endif
		return transpositionTable.updateTable(curHash, searchDepth, bestMove, bestMoveScore, exitType);
	}

	public void setupCurMoveConstants(BoardState boardState){
		return;
	}
	public MoveHelper getRecommendedMoveAtDepth(BoardState boardState, int depth) {
		this.end_t = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1_000_000_000;
		ulong curHash = Hasher.hashPosition(boardState);
		int king_index = boardState.getKingIndex(boardState.whiteToMove);
		this.setupCurMoveConstants(boardState);
		TranspositionTableEntry result = Search(boardState, -1_000_000, 1_000_000, depth * ONE_PLY_DEPTH, 0, curHash);
		MoveHelper best_move = result.refutationMovePacked;
		if (best_move.isValid()) {
			string debugInfo = "Searched to depth " + depth + " best move " + best_move.toNotation() +
				" Mainline " + getMainline(boardState, depth);
			GD.Print(debugInfo);
			return best_move;
		} else {
			GD.Print("Could not find ANY moves, resigning");
			return new MoveHelper(king_index, king_index, 1);
		}
	}

	public MoveHelper getRecommendedMove(BoardState boardState, long max_ms) {
		this.end_t = DateTimeOffset.Now.ToUnixTimeMilliseconds() + max_ms;
		int targetDepth = 0;
		ulong curHash = Hasher.hashPosition(boardState);
		int king_index = boardState.getKingIndex(boardState.whiteToMove);
		MoveHelper best_move = new MoveHelper();
		this.setupCurMoveConstants(boardState);
		float eval = 0.0f;
		while(true) {
			TranspositionTableEntry result = Search(boardState, -1_000_000, 1_000_000, targetDepth * ONE_PLY_DEPTH, 0, curHash);
			if (result.refutationMovePacked.isValid()) {
				// Choose the best move found at the latest depth. It should be at least as good as the completed last depth
				// result since we will follow the mainline for the move found at the previous depth first.
				best_move = result.refutationMovePacked;
				eval = result.eval;
				if (!boardState.whiteToMove) {
					eval *= -1;
				}
			}
			// GD.Print("Depth " + targetDepth + " " + best_move.toNotation());

			if (DateTimeOffset.Now.ToUnixTimeMilliseconds() > this.end_t) {
				break;
			}
			targetDepth += 1;
		}
		if (best_move.isValid()) {
			string debugInfo = "Searched to depth " + (targetDepth - 1) + " eval: " + eval + " best move " + best_move.toNotation() +
				" Mainline " + getMainline(boardState, 8);
			GD.Print(debugInfo);
			return best_move;
		} else {
			GD.Print("Could not find ANY moves, resigning");
			return new MoveHelper(king_index, king_index, 1);
		}
	}
}
