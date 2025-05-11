using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public struct MoveHelper {
	int data;
	public const int VALID_BIT = 1 << 13;
	// 2 ** (31-14) gives a max move value of 131_072 centipawns
	public const int ATTACK_BIT = 1 << 14;

	public MoveHelper(){
		this.data = 0;
	}
	public MoveHelper(int from, int to, int captureValue=0) {
		this.data = from * 64 + to + VALID_BIT + captureValue * ATTACK_BIT;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int to() {
		return this.data & 0x3f;
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int from() {
		return (this.data >> 6) & 0x3f;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int moveIdx() {
		return this.data & 0xfff;
	}
	public bool isValid() {
		return VALID_BIT == (this.data & VALID_BIT);
	}
	public int attackValue() {
		return this.data / ATTACK_BIT;
	}

	public static string boardIndexToNotation(int boardIndex) {
		char column = (char)('a' + boardIndex % 8);
		int row =  8 - boardIndex / 8;
		return column + "" + row;
	}

	public string toNotation() {
		if (this.attackValue() != 0) {
			return  boardIndexToNotation(this.from())  + "x" + boardIndexToNotation(this.to());
		} else {
			return boardIndexToNotation(this.from()) + boardIndexToNotation(this.to());
		}
	}

	public MoveHelper reverseMove() {
		return new MoveHelper(this.to(), this.from(), attackValue());
	}
}

public struct SimpleUpdate {
	public const int MoveType_NONE = 0;
	public const int MoveType_MOVE_PIECE = 1;
	public const int MoveType_REMOVE_PIECE = 2;
	public const int MoveType_ADD_PIECE = 3;

	int data;
	public SimpleUpdate(int data) {
		this.data = data;
	}

	public int getMoveType() {
		return this.data & 0x3f;
	}
	public static SimpleUpdate makeMovePiece(int from, int to){
		return new SimpleUpdate(MoveType_MOVE_PIECE + (to << 6) + (from << 12));
	}
	public int getMovePieceTo() {
		return (this.data >> 6) & 0x3f;
	}
	public int getMovePieceFrom() {
		return (this.data >> 12) & 0x3f;
	}

	public static SimpleUpdate makeRemovePiece(int at){
		return new SimpleUpdate(MoveType_REMOVE_PIECE + (at << 6));
	}
	public int getRemovePieceAt() {
		return (this.data >> 6) & 0x3f;
	}

	public static SimpleUpdate makeAddPiece(int at, uint piece_data){
		return new SimpleUpdate(MoveType_ADD_PIECE + (at << 6) + ((int)piece_data << 12));
	}

	public int getAddPieceAt() {
		return (this.data >> 6) & 0x3f;
	}
	public uint getAddPieceData() {
		return (uint)(this.data >> 12) & Helpers.pieceDataMask;
	}

}

public class BoardUpdate {
	// List of [squareindex, newsquaredata], indicating 
	// which squares have been affected by this move.
	public List<SimpleUpdate> changes;

	public BoardState.GameState stateAfterMove = BoardState.GameState.PLAYING;
	public ulong preMoveHash = 0; // will be populated after calling apply()
	public bool isInverse;
	public MoveHelper move;
	public BoardUpdate(MoveHelper moveBase, bool isInverseMove = false) {
		changes = new List<SimpleUpdate>();
		isInverse = isInverseMove;
		move = moveBase;
	}

	public BoardUpdate(MoveHelper moveBase, List<SimpleUpdate> initUpdates, bool isInverseMove = false) {
		changes = initUpdates;
		isInverse = isInverseMove;
		move = moveBase;
	}

	public void addMovePieceAction(int from, int to) {
		// Move a piece at from to an empty square.
		changes.Add(SimpleUpdate.makeMovePiece(from, to)); 
	}
	public void addAddPieceAction(int at, uint piece_data) {
		// Create a piece at the target square. Piece data is assumed to be masked
		// with pieceDataMask already.
		changes.Add(SimpleUpdate.makeAddPiece(at, piece_data)); 
	}
	public void addDeletePieceAction(int at) {
		changes.Add(SimpleUpdate.makeRemovePiece(at));
	}

	private string _toUIUpdateString() {
		string updateString = "";
		foreach (SimpleUpdate delta in changes) {
			if (updateString != "") {
				updateString += ";";
			}
			switch (delta.getMoveType()) {
				case (int)SimpleUpdate.MoveType_MOVE_PIECE: {
					int from = delta.getMovePieceFrom();
					int to = delta.getMovePieceTo();
					updateString += 'M' + from.ToString() + "_" + to.ToString();
					break;
				}
				case (int)SimpleUpdate.MoveType_REMOVE_PIECE: {
					int at = delta.getRemovePieceAt();
					updateString += 'R' + at.ToString();
					break;
				}
				case (int)SimpleUpdate.MoveType_ADD_PIECE: {
					int at = delta.getAddPieceAt();
					uint piece_data = (uint) delta.getAddPieceData();
					string piece_color = "b";
					if (Helpers.pieceIsWhiteFromData(piece_data)) {
						piece_color = "w";
					}
					updateString += 'A' + at.ToString() + '_' + piece_color + "_"  + Helpers.invFenDict[Helpers.pieceFromData(piece_data)];
					break;
				}
				default:
				GD.Print("Invalid move type " + delta.getMoveType());
				break;
			}
		}
		return updateString;
	}
	public string toUIUpdateString(BoardState boardState) {
		// Get the UI Update string. SquareData must be in the state right before the move is applied for this to work.
		// To apply validity checks, play the move. Then play it's inverse to get back to the original state.
		BoardUpdate nextMoveState = this.apply(boardState, preMoveHash);
		BoardUpdate curMoveState = nextMoveState.apply(boardState, nextMoveState.preMoveHash);
		return curMoveState._toUIUpdateString();
	}

	public void updateStateAfterMove(bool towards_white){
		if (towards_white) {
			if (stateAfterMove == BoardState.GameState.BLACK_WIN) {
				stateAfterMove = BoardState.GameState.DRAW;
			} else if (stateAfterMove != BoardState.GameState.DRAW) {
				stateAfterMove = BoardState.GameState.WHITE_WIN;
			}
		} else {
			if (stateAfterMove == BoardState.GameState.WHITE_WIN) {
				stateAfterMove = BoardState.GameState.DRAW;
			} else  if (stateAfterMove != BoardState.GameState.DRAW) {
				stateAfterMove = BoardState.GameState.BLACK_WIN;
			}

		}
	}

	public BoardUpdate apply(BoardState state, ulong curHash) {
		// Apply all of the operations specified in the move in order, and update state
		// to be after this move is over. 
		// Returns a move that represents the inverse of
		// this move, pruned to remove no-ops from the original move.
		uint[] squareData = state.curSquareData;
		BoardUpdate inv_move = new BoardUpdate(move.reverseMove(),!isInverse);
		state.whiteToMove = !state.whiteToMove;
		curHash ^= Hasher.getTurnChangeHash();
		foreach (SimpleUpdate delta in changes) {
			switch (delta.getMoveType()) {
				case (int)SimpleUpdate.MoveType_MOVE_PIECE: {
					int from = delta.getMovePieceFrom();
					int to = delta.getMovePieceTo();
					// Check that from is nonempty and to is empty
					if ((Helpers.pieceFromData(squareData[to]) == Helpers.Piece.NONE) && (Helpers.pieceFromData(squareData[from]) != Helpers.Piece.NONE)) {
						inv_move.addMovePieceAction(to, from);
						int bitBoardidx = Convert.ToInt32(Helpers.pieceIsWhiteFromData(squareData[from]));
						state.occupancyBitboard[bitBoardidx] ^= 1UL << to;
						state.occupancyBitboard[bitBoardidx] ^= 1UL << from;

						curHash ^= Hasher.updateHash(to, squareData[to]);
						curHash ^= Hasher.updateHash(from, squareData[from]);
						squareData[to] |= (squareData[from] & Helpers.pieceDataMask) | (squareData[to] & ~Helpers.pieceDataMask);
						squareData[from] = squareData[from] & ~Helpers.pieceDataMask;
						curHash ^= Hasher.updateHash(to, squareData[to]);
						curHash ^= Hasher.updateHash(from, squareData[from]);
						if (Helpers.pieceFromData(squareData[to]) == Helpers.Piece.KING) {
							// Moved a king to the end of the board, it's winning
							if (Helpers.pieceIsWhiteFromData(squareData[to])) {
								if (to < 8) {
									updateStateAfterMove(true);
								}
							} else {
								if (to >= 56) {
									updateStateAfterMove(false);
								}
							}
						}
					}
					break;
				}
				case SimpleUpdate.MoveType_REMOVE_PIECE: {
					int at = delta.getRemovePieceAt();
					// Check that there is a piece on this square
					if (Helpers.pieceFromData(squareData[at]) != Helpers.Piece.NONE) {
						inv_move.addAddPieceAction(at, squareData[at] & Helpers.pieceDataMask);
						Helpers.Piece takenPiece = Helpers.pieceFromData(squareData[at]);
						bool takenPieceIsWhite = Helpers.pieceIsWhiteFromData(squareData[at]);
						// Check if a player has won
						if (takenPiece == Helpers.Piece.KING) {
							updateStateAfterMove(!takenPieceIsWhite);
						}
						curHash ^= Hasher.updateHash(at, squareData[at]);
						squareData[at] = squareData[at] & ~Helpers.pieceDataMask;
						curHash ^= Hasher.updateHash(at, squareData[at]);

						if (takenPieceIsWhite){
							state.occupancyBitboard[1] ^= 1UL << at;
							state.whiteMaterial -= Helpers.pieceValue[takenPiece];
						}else {
							state.occupancyBitboard[0] ^= 1UL << at;
							state.blackMaterial -= Helpers.pieceValue[takenPiece];
						}
					}
					break;
				}
				case SimpleUpdate.MoveType_ADD_PIECE: {
					int at = delta.getAddPieceAt();
					// Check that there is no piece on this square
				
					if (Helpers.pieceFromData(squareData[at]) == Helpers.Piece.NONE) {
						uint piece_data = delta.getAddPieceData();
						inv_move.addDeletePieceAction(at);
						curHash ^= Hasher.updateHash(at, squareData[at]);
						squareData[at] = (squareData[at] & ~Helpers.pieceDataMask) | (piece_data);
						curHash ^= Hasher.updateHash(at, squareData[at]);

						Helpers.Piece generatedPiece = Helpers.pieceFromData(squareData[at]);
						bool generatedPieceIsWhite = Helpers.pieceIsWhiteFromData(squareData[at]);
						if (generatedPieceIsWhite) {
							state.occupancyBitboard[1] ^= 1UL << at;
							state.whiteMaterial += Helpers.pieceValue[generatedPiece];
						} else {
							state.occupancyBitboard[0] ^= 1UL << at;
							state.blackMaterial += Helpers.pieceValue[generatedPiece];
						}
					}
					break;
				}
				default:
				GD.Print("Invalid move type " + delta.getMoveType());
				break;
			}
		}
		if (isInverse) {
			state.movesPlayed.RemoveAt(state.movesPlayed.Count - 1);
			state.seenPositionHashes.RemoveAt(state.seenPositionHashes.Count - 1);
		} else {
			state.movesPlayed.Add(move);
			// Check last few hashes for a twofold repetition. We don't care to search further, it's not likely
			// to cause an issue.
			foreach(ulong hash in state.seenPositionHashes.TakeLast(7)) {
				if (hash == curHash) {
					if (stateAfterMove == BoardState.GameState.PLAYING) {
						stateAfterMove = BoardState.GameState.TWOFOLD_REPETITION;
					}
				}
			}
			state.seenPositionHashes.Add(curHash);
		}
		inv_move.preMoveHash = curHash;
		inv_move.changes.Reverse();
		return inv_move;
	}
}
