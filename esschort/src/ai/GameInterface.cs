using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;


public partial class GameInterface : Node
{
	public Ai white_AI = null;
	public Ai black_AI = null;
	public BoardState boardState;
	List<BoardUpdate> invMoves;

	BoardState.GameState gameStatus;
	public BoardUpdate UIUpdate;
	string pgn = "";
	Task<MoveHelper> getBestMoveThread = null;
	// MoveHelper threadResult = new MoveHelper();

	public override void _Ready()
	{
		ResetGameState();
	}

	public int pollAiThread()
	{
		if ((getBestMoveThread != null) && getBestMoveThread.IsCompleted) {
			MoveHelper choosenMove = getBestMoveThread.Result;
			getBestMoveThread = null;
			return choosenMove.moveIdx();
		}
		return -2;
	}

	public void ResetGameState()
	{
		if (white_AI != null) {
			white_AI.clearState();
		}
		if (black_AI != null) {
			black_AI.clearState();
		}
		invMoves = new List<BoardUpdate>();
		pgn = "";
		if (getBestMoveThread != null) {
			getBestMoveThread.Wait();
			getBestMoveThread = null;
		}
	}
	public void SetupBoard(string fen) {
		ResetGameState();
		boardState = new BoardState(fen);
		pgn = "[FEN " + fen + "]\n";
	}
	public void setAI(string ai_name, bool asWhite) {
		Ai new_ai = null;
		if (ai_name == "v1") {
			new_ai = new Ai(1);
		} else if (ai_name == "v2") {
			new_ai = new Ai(2);
		}
		if (asWhite) {
			GD.Print("Using " + ai_name + " as white AI");
			white_AI = new_ai;
		} else {
			GD.Print("Using " + ai_name + " as black AI");
			black_AI = new_ai;
		}
	}

	public string getPgn() {
		return pgn;
	}

	public string getFen() {
		int empty = 0;
		string fen = "";
		for(int i = 0; i < 64; i++) {
			Helpers.Piece p = Helpers.pieceFromData(boardState.curSquareData[i]);
			if (p == Helpers.Piece.NONE) {
				empty += 1;
			} else {
				if (empty > 0) {
					fen += empty.ToString();
					empty = 0;
				}
				string c = Helpers.invFenDict[Helpers.pieceFromData(boardState.curSquareData[i])];
				if (c.Length > 1) {
					c = '(' + c + ')';
				}
				if (Helpers.pieceIsWhiteFromData(boardState.curSquareData[i])) {
					c = c.ToUpper();
				}
				fen += c;
			}
			if ((i + 1) % 8 == 0) {
				if (empty > 0) {
					fen += empty.ToString();
					empty = 0;
				}
				fen += '/';
			}
		}
		fen += " ";
		if (boardState.whiteToMove) {
			fen += 'w';
		} else {
			fen += 'b';
		}
		return fen;
	}

	public string undoLast() {
		if (invMoves.Count == 0) {
			return "";
		}
		pgn = pgn.Substring(0, pgn.LastIndexOf(" "));
		string updateString = invMoves[invMoves.Count - 1].toUIUpdateString(boardState);
		invMoves[invMoves.Count - 1].apply(boardState, invMoves[invMoves.Count - 1].preMoveHash);
		invMoves.RemoveAt(invMoves.Count - 1);
		return updateString;
	}
	public ulong targetableSquaresWrapper(int fromLoc) {
		return BoardState.targetableSquares(boardState, fromLoc);
	}
	public string playMoveWrapper(int fromLoc, int toLoc) {
		if (Helpers.pieceFromData(boardState.curSquareData[fromLoc]) == Helpers.Piece.NONE) {
			return "";
		}
		if (boardState.whiteToMove != Helpers.pieceIsWhiteFromData(boardState.curSquareData[fromLoc])) {
			return "";
		}
		BoardUpdate move = boardState.makeMove(new MoveHelper(fromLoc, toLoc), true);
		if (move == null) {
			return "";
		} else {
			string uiUpdate = move.toUIUpdateString(boardState); // get update string before applying the move
			ulong preMoveHash = Hasher.hashPosition(boardState);
			BoardUpdate invMove = move.apply(boardState, preMoveHash);
			ulong curHash = invMove.preMoveHash;
			invMoves.Add(invMove);
			if (invMoves.Count % 2 == 1) {
				pgn += "\n " + invMoves.Count / 2 + '.';
			} else {
				pgn += ' ';
			}
			pgn += Helpers.boardIndexToNotation(fromLoc) + Helpers.boardIndexToNotation(toLoc);
			gameStatus = move.stateAfterMove;


			int positionSeenTimes = 0;
			// check if we have seen the position 3 times
			foreach(ulong hash in boardState.seenPositionHashes) {
				if (hash == curHash) {
					positionSeenTimes += 1;
				}
			}
			if (positionSeenTimes >= 3) {
				GD.Print("DRAW " + curHash);
				gameStatus = BoardState.GameState.DRAW;
			}
			return uiUpdate;
		}
	}
	
	public bool startAIMoveProcess() {
		BoardState boardStateCopy = new BoardState(boardState);
		if (getBestMoveThread != null) {
			return false; // best move is running!
		}
		Ai ai_to_use = black_AI;
		if (boardStateCopy.whiteToMove) {
			ai_to_use = white_AI;
		}
		// Thread thread = new Thread(new ThreadStart(() => {
		// 	threadResult = new MoveHelper();
		// 	threadResult = ai_to_use.getRecommendedMove(boardStateCopy, 1000);
		// }));
		// thread.Priority = ThreadPriority.Highest;
		// thread.Start();
		getBestMoveThread = Task.Run(() => {
			return ai_to_use.getRecommendedMove(boardStateCopy, 100);
			// return ai_to_use.getRecommendedMoveAtDepth(boardStateCopy, 0);
		});
		return true;
	}

	public string pieceNameFromValue(int data_at_square) {
		return Helpers.invFenDict[Helpers.pieceFromData((uint)data_at_square)];
	}
	public string gameResult() {
		if (gameStatus == BoardState.GameState.BLACK_WIN) {
			return "0-1";
		} else if (gameStatus == BoardState.GameState.WHITE_WIN) {
			return "1-0";
		} else if (gameStatus == BoardState.GameState.DRAW) {
			return "1/2-1/2";
		} else {
			return "";
		}
	}
	public bool whiteToMove() {
		return boardState.whiteToMove;
	}
}
