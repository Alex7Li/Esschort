using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices; 


public record class TranspositionTableEntry(ulong fullHash, int searchDepth, MoveHelper refutationMovePacked, float eval, SearchResultType type);

// https://www.chessprogramming.org/Node_Types#PV
public enum SearchResultType { NOT_FULLY_SEARCHED = 0, AT_MOST_ALPHA = 1, AT_LEAST_BETA = 2, EXACT = 3, };



public class Hasher {
    public static int num_piece_hashes_per_color = 64 * Enum.GetNames(typeof(Helpers.Piece)).Length;
    public static ulong[] zobrist_hashes = genZobrist();
    private static ulong[] genZobrist() {
        int n_hashes = 2 * num_piece_hashes_per_color + 1;
        ulong[] hashValues = new ulong[n_hashes];
        Random rng = new Random(42);
        for (int i = 0; i < n_hashes; i++) {
            ulong rand_number = ((ulong)(rng.Next()) << 32) + (ulong)rng.Next();
            hashValues[i] = rand_number;    
        }
        return hashValues;
    }

    public static ulong updateHash(int boardIdx, uint boardData) {
        bool isWhite = Helpers.pieceIsWhiteFromData(boardData);
        Helpers.Piece piece = Helpers.pieceFromData(boardData);
        int hashIdx = boardIdx * (int)piece;
        if ((piece != Helpers.Piece.NONE) && isWhite) {
            hashIdx += num_piece_hashes_per_color;
        }
        return zobrist_hashes[hashIdx];
    }

    public static ulong getTurnChangeHash() {
        return zobrist_hashes[2 * num_piece_hashes_per_color]; 
    }

    public static ulong hashPosition(BoardState state) {
        ulong hash = 0;
        for (int i = 0; i < state.curSquareData.Count(); i++) {
            hash ^= updateHash(i, state.curSquareData[i]);
        }
        if (state.whiteToMove) {
            hash ^= zobrist_hashes[2 * num_piece_hashes_per_color];
        }
        return hash;
    }
}
public class TranspositionTable
{
    public const int num_entries = 1 << 17;
    public TranspositionTableEntry[] maxDepthTable;
    public TranspositionTableEntry[] recentTable;

    public void clear() {
        maxDepthTable = new TranspositionTableEntry[num_entries];
        recentTable = new TranspositionTableEntry[num_entries];

        for (int i = 0; i < num_entries; i++) {
            maxDepthTable[i] = null;
            recentTable[i] = null;
        }
    }

    public TranspositionTable() {
        clear();
    }

    public TranspositionTableEntry getTableEntry(ulong fullHash) {
        // prefer the max-depth table since it has the greatest depth.
        TranspositionTableEntry entry = maxDepthTable[fullHash % num_entries];
        if (entry != null && entry.fullHash == fullHash) {
            return entry;
        }
        entry = recentTable[fullHash % num_entries];
        if (entry != null && entry.fullHash == fullHash) {
            return entry;
        }
        // not in either table
        return null;
    }

    public TranspositionTableEntry updateTable(ulong fullHash, int searchDepth, MoveHelper refutationMovePacked, float eval, SearchResultType type) {
        TranspositionTableEntry newEntry = new TranspositionTableEntry(fullHash, searchDepth, refutationMovePacked, eval, type);
        int table_idx = (int)(fullHash % num_entries);
        if(!refutationMovePacked.isValid() || type == SearchResultType.NOT_FULLY_SEARCHED) {
            return newEntry; // do not save invalid moves
        }
        if (maxDepthTable[table_idx] == null) {
            maxDepthTable[table_idx] = newEntry;
        }
        if (newEntry.searchDepth > maxDepthTable[table_idx].searchDepth || 
           (newEntry.searchDepth == maxDepthTable[table_idx].searchDepth)) {
            maxDepthTable[table_idx] = newEntry;
        } else {
            recentTable[table_idx] = newEntry;
        }
        return newEntry;
    }
}
