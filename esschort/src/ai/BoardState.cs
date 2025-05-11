using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class BoardState
{
	public enum GameState {PLAYING=0, WHITE_WIN=1, BLACK_WIN=2, DRAW=3, TWOFOLD_REPETITION=4};
	public bool whiteToMove;
	public uint[] curSquareData;
	GameState gameStatus;

	public int whiteMaterial;
	public int blackMaterial;
	public List<MoveHelper> movesPlayed;
	public List<ulong> seenPositionHashes;
	public ulong[] occupancyBitboard;
	public BoardState(string fen) {
		whiteMaterial = 0;
		blackMaterial = 0;
		curSquareData = new uint[64];
		occupancyBitboard = new ulong[2];
		gameStatus = GameState.PLAYING;
		whiteToMove = true;

		int squareIdx = 0;
		int i = 0;
		while (i < fen.Length && fen[i] != ' ') {
			char c = fen[i];
			if (c.Equals(' ') | c.Equals('/')) {}
			else if (Char.IsDigit(c)) {
				int shiftAmount = int.Parse(c.ToString());
				squareIdx += shiftAmount;
			} else {
				string square_string = fen[i].ToString();
				if (c == '(') {
					i += 1;
					square_string = "";
					while (fen[i] != ')') {
						i += 1;
						square_string += fen[i];
					}
				}
				string lower_string = square_string.ToLower();
				bool isWhite = Char.IsUpper(square_string[0]);
				Helpers.Piece piece = Helpers.FenDict[lower_string];
				curSquareData[squareIdx] = Helpers.constructPiece(piece, isWhite);
				if (isWhite) {
					occupancyBitboard[1] |= 1UL << squareIdx;
					whiteMaterial += Helpers.pieceValue[piece];
				} else {
					occupancyBitboard[0] |= 1UL << squareIdx;
					blackMaterial += Helpers.pieceValue[piece];
				}
				squareIdx += 1;

			}
			i += 1;
 		}
		while(i < fen.Length && fen[i] == ' '){
			i+=1;
		}
		if (i < fen.Length) {
			if (fen[i] == 'b') {
				whiteToMove = false;
			}
		}
		Helpers.setupHelperBitBoards();
		movesPlayed = new List<MoveHelper>();
		seenPositionHashes = new List<ulong>();
		seenPositionHashes.Add(Hasher.hashPosition(this));
	}
	public BoardState(BoardState copy_from) {
		curSquareData = new uint[64];
		Array.Copy(copy_from.curSquareData, curSquareData, 64);
		gameStatus = copy_from.gameStatus;
		whiteToMove = copy_from.whiteToMove;
		whiteMaterial = copy_from.whiteMaterial;
		blackMaterial = copy_from.blackMaterial;
		occupancyBitboard = new ulong[2];
		Array.Copy(copy_from.occupancyBitboard, occupancyBitboard, 2);
		movesPlayed = [.. copy_from.movesPlayed];
		seenPositionHashes =[.. copy_from.seenPositionHashes];

	}

	public static ulong moveOrAttackPath(Helpers.RayAttackDirection d,
		int pieceLocation, uint[] squareData, ulong blockerMask, bool canAttack=true) {
		ulong moves = Helpers.allRayAttacks[d][pieceLocation];
		ulong blockerLocations = blockerMask & moves;
		if(blockerLocations > 0) {
			int firstBlockerLocation;
			if (d > 0) {
				firstBlockerLocation = BitOperations.TrailingZeroCount(blockerLocations);
			} else {
				firstBlockerLocation = 63 - BitOperations.LeadingZeroCount(blockerLocations);
			}
			moves &= ~Helpers.allRayAttacks[d][firstBlockerLocation]; // you can't move past the blocker
			bool isAllyPiece = Helpers.pieceIsWhiteFromData(squareData[pieceLocation]) ==
				Helpers.pieceIsWhiteFromData(squareData[firstBlockerLocation]);
			if (isAllyPiece || !canAttack) {
				// Can't attack this piece
				moves ^= 1UL << firstBlockerLocation;
			}
		}
		return moves;
	}

	public static ulong targetableSquares(BoardState boardState, int fromLoc)
	{
		ulong allOccupancyBitboard = boardState.occupancyBitboard[0] |  boardState.occupancyBitboard[1];
		ulong allyOccupancyBitboard = boardState.occupancyBitboard[Convert.ToInt32(boardState.whiteToMove)];
		// Get all squares the piece at fromLoc can target,
		// assuming that it is the move of the player controlling the piece at fromLoc. (not using boardState.whiteToMove)
		ulong targetableMask = 0;
		uint[] squareData = boardState.curSquareData;
		uint fromSquareData = squareData[fromLoc];
		uint coloredPiece = Helpers.coloredPiece(fromSquareData);
		Helpers.Piece piece = Helpers.pieceFromData(fromSquareData);
		if (piece == Helpers.Piece.NONE) {
			return targetableMask;
		}

		int direction = 1;
		if (Helpers.pieceIsWhiteFromData(fromSquareData)) {
			direction *= -1;
		}
		int fromLoc0x88 = Helpers.to_0x88(fromLoc);
		switch (piece) {
			case Helpers.Piece.ROOK:
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.N, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.E, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.W, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.S, fromLoc, boardState.curSquareData, allOccupancyBitboard);
			break;
			case Helpers.Piece.BISHOP:
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.NE, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.NW, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.SE, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.SW, fromLoc, boardState.curSquareData, allOccupancyBitboard);
			break;
			case Helpers.Piece.QUEEN:
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.N, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.E, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.W, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.S, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.NE, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.NW, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.SE, fromLoc, boardState.curSquareData, allOccupancyBitboard);
				targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.SW, fromLoc, boardState.curSquareData, allOccupancyBitboard);
			break;
			case Helpers.Piece.PAWN:
				// if on the second rank, we get to move extra far :)
				if (direction == 1 && ((fromLoc0x88 & 0x70) == 0x10)) {
					targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.S2, fromLoc, boardState.curSquareData, allOccupancyBitboard, canAttack:false);
				} else if (direction == -1 && ((fromLoc0x88 & 0x70) == 0x60)) {
					targetableMask |= moveOrAttackPath(Helpers.RayAttackDirection.N2, fromLoc, boardState.curSquareData, allOccupancyBitboard, canAttack:false);
				} else {
					targetableMask |= Helpers.targetableByUnblockable(squareData, fromLoc0x88, 0x10 * direction, byMove: true, byAttack: false);
				}
				targetableMask |= Helpers.targetableByUnblockable(squareData, fromLoc0x88, 0x11 * direction, byMove: false);
				targetableMask |= Helpers.targetableByUnblockable(squareData, fromLoc0x88, (0x10 - 0x01) * direction, byMove: false);
			break;
			case Helpers.Piece.KNIGHT:
				targetableMask |= ~allyOccupancyBitboard & Helpers.unblockableMoveOrAttackBitBoards[coloredPiece][fromLoc];
			break;
			case Helpers.Piece.KING:
				targetableMask |= ~allyOccupancyBitboard & Helpers.unblockableMoveOrAttackBitBoards[coloredPiece][fromLoc];
				// There is an extra-special move to resign by taking your own king.
				// targetableMask |= 1UL << fromLoc;
			break;
			default:
				GD.Print("Invalid piece " + piece);
			break;
		}
		return targetableMask;
	}

	public ulong getAllyOccupancyBitBoard(bool whiteToMove) {
		if (whiteToMove) {
			return occupancyBitboard[1];
		} else {
			return occupancyBitboard[0];
		}
	}

	public List<int> getAllMoves(bool whiteToMove) {
		List<int> allMoves = new List<int>(100);
		ulong allyOccupancyBitboard = getAllyOccupancyBitBoard(whiteToMove);
		foreach (int fromSquare in Helpers.iterateOverSetBitInds(allyOccupancyBitboard)) {
			foreach (int toSquare in Helpers.iterateOverSetBitInds(targetableSquares(this, fromSquare))) {
				allMoves.Add(fromSquare * 64 + toSquare);
			}
		}
		return allMoves;
	}

	public int checkMaterialChangeAfterMove(int fromLoc, int toLoc) {
		// How much does moving the piece at fromloc to toloc change the material balance
		// in favor of the player making the move (owning the piece at fromLoc?)
		int delta = 0;
		Helpers.Piece fromPiece = Helpers.pieceFromData(curSquareData[fromLoc]);
		// Test basic captures
		switch (fromPiece) {
			case Helpers.Piece.PAWN:
			case Helpers.Piece.KNIGHT:
			case Helpers.Piece.BISHOP:
			case Helpers.Piece.ROOK:
			case Helpers.Piece.QUEEN:
			case Helpers.Piece.KING:
				delta += Helpers.pieceValue[Helpers.pieceFromData(curSquareData[toLoc])];
				break;
			default:
				break;
		}
		// Test for promotions
		switch (fromPiece) {
			case Helpers.Piece.PAWN:
			case Helpers.Piece.KING:
				bool curPieceIsWhite = Helpers.pieceIsWhiteFromData(curSquareData[fromLoc]);
				bool is_promotion = (toLoc < 8 && curPieceIsWhite) | (toLoc >= 56 && !curPieceIsWhite);
				if(is_promotion) {
					delta += Helpers.promotionValue[fromPiece];
				}
				break;
			default:
				break;
		}
		return delta;
	}


	public BoardUpdate makeMove(MoveHelper moveNotation, bool check_valid = false) {
		if (!moveNotation.isValid()) {
			// null move
			return new BoardUpdate(moveNotation);
		}
		int fromLoc = moveNotation.from();
		int toLoc = moveNotation.to();
		if (check_valid) {
			if (0 == (targetableSquares(this, fromLoc) & (1UL << toLoc))) {
				GD.Print("Not targetable");
				return null;
			}
		}
		BoardUpdate move = null;

		Helpers.Piece pieceType = Helpers.pieceFromData(curSquareData[fromLoc]);

		// check for promotions
		bool curPieceIsWhite = Helpers.pieceIsWhiteFromData(curSquareData[fromLoc]);
		bool is_promotion = (toLoc < 8 && curPieceIsWhite) | (toLoc >= 56 && !curPieceIsWhite);
		switch (pieceType) {
			case Helpers.Piece.PAWN:
				if(is_promotion) {
					List<SimpleUpdate> move_data = [SimpleUpdate.makeRemovePiece(fromLoc)];
					if (Helpers.pieceFromData(curSquareData[toLoc]) != Helpers.Piece.NONE){
						move_data.Add(SimpleUpdate.makeRemovePiece(toLoc));
					}
					move_data.Add(SimpleUpdate.makeAddPiece(toLoc, Helpers.constructPiece(Helpers.Piece.QUEEN, curPieceIsWhite)));
					return new BoardUpdate(moveNotation, move_data);
				}
				break;
			default:
				break;
		}

		switch (pieceType) {
			case Helpers.Piece.PAWN:
			case Helpers.Piece.KNIGHT:
			case Helpers.Piece.BISHOP:
			case Helpers.Piece.ROOK:
			case Helpers.Piece.QUEEN:
			case Helpers.Piece.KING:
				move = Helpers.MoveOrAttack(this, fromLoc, toLoc);
				break;
			default:
				GD.Print("INVALID MOVE " + fromLoc + " " + toLoc);
				break;
		}

		return move;
	}

	public int getKingIndex(bool for_white) {
		for (int fromSquare = 0; fromSquare < 64; fromSquare++) {
			Helpers.Piece p = Helpers.pieceFromData(curSquareData[fromSquare]);
			if (p == Helpers.Piece.KING && (for_white == Helpers.pieceIsWhiteFromData(curSquareData[fromSquare]))) {
				return fromSquare;
			}
		}
		return -1;
	}
}
