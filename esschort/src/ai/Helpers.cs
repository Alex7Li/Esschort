using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices; 
public static class Helpers
{
	public enum Piece { NONE = 0, PAWN = 1, KNIGHT = 2, BISHOP = 3, ROOK = 4, QUEEN = 5, KING = 6 };
	public const uint pieceDataMask = 0b1_1111_1111;

	public static Dictionary<Piece, int> pieceValue = new Dictionary<Piece, int>()
	{
		{Piece.PAWN, 100},
		{Piece.KNIGHT, 310},
		{Piece.BISHOP, 325},
		{Piece.ROOK, 500},
		{Piece.QUEEN, 850},
		{Piece.KING, 400},
		{Piece.NONE, 0},
	};

	public static Dictionary<Piece, int> promotionValue = new Dictionary<Piece, int>()
	{
		{Piece.PAWN, 750},
		{Piece.KING, 6000},
	};

	public static Dictionary<string, Piece> FenDict = new Dictionary<string, Piece>()
	{
		{"p", Piece.PAWN},
		{"n", Piece.KNIGHT},
		{"b", Piece.BISHOP},
		{"r", Piece.ROOK},
		{"q", Piece.QUEEN},
		{"k", Piece.KING},
	};
	public static Dictionary<Piece, string> invFenDict = FenDict.ToDictionary(x => x.Value, x => x.Key);

	// Note the value's sign reflects if this is a positive or negative ray attack
	public enum RayAttackDirection { 
		NW = -1, N = -2, NE = -3, E = 4, N2 = -5,
		SE = 1, S = 2, SW = 3, W = -4, S2 = 5,
		};

	// rayAttacks[RayAttackDirection][currentLocation] gives all bits you could target from
	// the current square if there are no obstacles. https://www.chessprogramming.org/Classical_Approach
	public static Dictionary<RayAttackDirection, ulong[]> allRayAttacks = null;

	// unblockableMoveOrAttackBitBoards[coloredPiece][currentLocation] gives all bits you could target
	// from the current square.
	public static Dictionary<uint, ulong[]> unblockableMoveOrAttackBitBoards = null;

	public static Dictionary<uint, float[]> pieceLocationBonus = null;
	public static void setupHelperBitBoards() {
		unblockableMoveOrAttackBitBoards= new Dictionary<uint, ulong[]>();
		pieceLocationBonus = new Dictionary<uint, float[]>();
		foreach(Piece p in Enum.GetValues<Piece>()){
			for (int isWhite = 0; isWhite < 2; isWhite++) {
				ulong[] unblockableBitBoards = new ulong[64];
				for (int i = 0; i < 64; i++) {
					int from = to_0x88(i);
					List<int> directionsUnblockable = new List<int>();
					switch (p) {
						case Piece.KNIGHT:
							directionsUnblockable.Add(0x20 + 0x01);
							directionsUnblockable.Add(0x10 + 0x02);
							directionsUnblockable.Add(0x20 - 0x01);
							directionsUnblockable.Add(0x10 - 0x02);
							directionsUnblockable.Add(-0x20 + 0x01);
							directionsUnblockable.Add(-0x10 + 0x02);
							directionsUnblockable.Add(-0x20 - 0x01);
							directionsUnblockable.Add(-0x10 - 0x02);
							break;
						case Piece.KING:
							directionsUnblockable.Add(0x10 + 0x01);
							directionsUnblockable.Add(0x10 - 0x01);
							directionsUnblockable.Add(-0x10 + 0x01);
							directionsUnblockable.Add(-0x10 - 0x01);
							directionsUnblockable.Add(0x01);
							directionsUnblockable.Add(-0x01);
							directionsUnblockable.Add(0x10);
							directionsUnblockable.Add(-0x10);
							break;
					}
					foreach(int direction in directionsUnblockable){
						int dest = direction + from;
						if (0 == (0x8000_0088 & dest)) {
							unblockableBitBoards[i] |= 1UL << from_0x88(dest);
						}
					}
				}
				float[] pieceScores = new float[64];
				for (int i = 0; i < 64; i++) {
					int rank = i / 8; // your pieces start in rank 0 and 1.
					if (isWhite == 1) {
						rank = 7 - rank;
					}
					int col = i % 8;
					int colDistFromEdge = Math.Min(col, 7 - col);
					int rankDistFromEdge = Math.Min(rank, 7 - rank);
					float score = 0;
					switch (p) {
						case Piece.PAWN:
							score = -3.0f + rank;
							if (colDistFromEdge == 0) {
								score -= .5f;
							}
							score = score / 128.0f;
						break;
					}
					pieceScores[i] = score; 
				}
				uint key = Helpers.constructPiece(p, isWhite == 1);
				unblockableMoveOrAttackBitBoards.Add(key, unblockableBitBoards);
				pieceLocationBonus.Add(key, pieceScores);
			}
		}
		allRayAttacks = new Dictionary<RayAttackDirection, ulong[]>();
		foreach (RayAttackDirection d in Enum.GetValues(typeof(RayAttackDirection))) {
			ulong[] rayAttacks = new ulong[64];
			for (int i = 0; i < 64; i++) {
				int from = to_0x88(i);
				List<int> targetableOnEmptyBoard = new List<int>();
				int maxSteps = 0;
				int stepDir = 0;
				switch (d) {
					case RayAttackDirection.E:
						stepDir = 0x1; maxSteps = 7;
						break;
					case RayAttackDirection.SE:
						stepDir = 0x11; maxSteps = 7;
						break;
					case RayAttackDirection.S:
						stepDir = 0x10; maxSteps = 7;
						break;
					case RayAttackDirection.SW:
						stepDir = 0x10 - 0x01; maxSteps = 7;
						break;
					case RayAttackDirection.W:
						stepDir = -0x1; maxSteps = 7;
						break;
					case RayAttackDirection.NW:
						stepDir = -0x11; maxSteps = 7;
						break;
					case RayAttackDirection.N:
						stepDir = -0x10; maxSteps = 7;
						break;
					case RayAttackDirection.NE:
						stepDir = -0x10 + 0x01; maxSteps = 7;
						break;
					case RayAttackDirection.N2:
						stepDir = -0x10; maxSteps = 2;
						break;
					case RayAttackDirection.S2:
						stepDir = 0x10; maxSteps = 2;
						break;
					default:
						GD.PrintErr("Unsupported ray attack direction");
						break;
				}
				for (int j = 1; j <= maxSteps; j++) {
					targetableOnEmptyBoard.Add(stepDir * j);
				}
				foreach(int direction in targetableOnEmptyBoard){
					int dest = direction + from;
					if (0 == (0x8000_0088 & dest)) {
						rayAttacks[i] |= 1UL << from_0x88(dest);
					}
				}
			}
			allRayAttacks.Add(d, rayAttacks);
		}
	}
	public static string boardIndexToNotation(int boardIndex) {
		char column = (char)('a' + boardIndex % 8);
		int row =  8 - boardIndex / 8;
		return column + "" + row;
	}

	public static int notationToBoardIndex(string notation) {
		int column = notation[0] - 'a';
		int row = int.Parse(notation[1].ToString());
		return (8 - row) * 8 + column;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Piece pieceFromData(uint data_at_square)
	{
		return (Piece)(data_at_square & 0b1111_1111);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool pieceIsWhiteFromData(uint data_at_square)
	{
		return (data_at_square & 0b1_0000_0000) > 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint coloredPiece(uint data_at_square)
	{
		return data_at_square & 0b1_1111_1111;
	}


	public static uint constructPiece(Piece piece, bool isWhite)
	{
		uint result = (uint)piece;
		if (isWhite)
		{
			result += 0b1_0000_0000;

		}
		return result;
	}
	public static ulong targetableByUnblockable(uint[] squareData, int fromLoc_0x88, int direction_0x88, bool byMove=true, bool byAttack=true) {
		int dest_0x88 = direction_0x88 + fromLoc_0x88;
		if (0 == (0x8000_0088 & (dest_0x88))) {
			int targetLocation = from_0x88(dest_0x88);
			uint fromSquareData = squareData[from_0x88(fromLoc_0x88)];
			uint toSquareData = squareData[targetLocation];
			if (pieceFromData(toSquareData) == Piece.NONE) {
				if (byMove) {
					return 1UL << targetLocation;
				}
			} else {
				if (byAttack && pieceIsWhiteFromData(fromSquareData) != pieceIsWhiteFromData(toSquareData)) {
					return 1UL << targetLocation;
				}
			}
		}
		return 0;
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int from_0x88(int id0x88) {
		return (id0x88 + (id0x88 & 7)) >> 1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int to_0x88(int id8x8) {
		return id8x8 + (id8x8 & ~7);
	}

	public static IEnumerable<int> iterateOverSetBitInds(ulong x) {
		while (x != 0){
			int trailingZeros = BitOperations.TrailingZeroCount(x);
			yield return trailingZeros;
			x = x & (x - 1); // clear lowest set bit.
		}
	}

	public static BoardUpdate MoveOrAttack(BoardState boardState, int fromIdx, int toIdx) {
		BoardUpdate move = new BoardUpdate(new MoveHelper(fromIdx, toIdx));
		if (pieceFromData(boardState.curSquareData[toIdx]) != Piece.NONE){
			move.addDeletePieceAction(toIdx);
		}
		move.addMovePieceAction(fromIdx, toIdx);
		return move;
	}
}
