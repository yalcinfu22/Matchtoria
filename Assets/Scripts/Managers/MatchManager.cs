
using Newtonsoft.Json.Bson;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class MatchManager
{
    public MatchResult FindMatches(NodeLayer layer, NodeModel[,] board)
    {
        MatchResult result = new MatchResult();
        List<Match> horizontalMatches = new List<Match>();
        List<Match> verticalMatches = new List<Match>();

        for (int x = 0; x < board.GetLength(0); x++)
        {
            verticalMatches.AddRange(VerticalMatchFinder(layer, board, x));
        }

        for (int y = 0; y < board.GetLength(1); y++)
        {
            horizontalMatches.AddRange(HorizontalMatchFinder(layer, board, y));
        }

        // find intersections (x,y) positions
        HashSet<Vector2Int> intersections = new HashSet<Vector2Int>();
        foreach (Match hMatch in horizontalMatches)
        {
            Vector2Int firstPosH = hMatch.tilePositions[0]; // x11, y1
            Vector2Int lastPosH = hMatch.tilePositions[hMatch.tilePositions.Count-1]; // x12, y1

            foreach (Match vMatch in verticalMatches)
            {

                Vector2Int firstPosV = vMatch.tilePositions[0]; // x1, y11
                Vector2Int lastPosV = vMatch.tilePositions[vMatch.tilePositions.Count-1]; // x1, y12

                // x11 <= x1 <= x12 && y11 <= y1 <= y12 then intersection exists
                if (firstPosH.x <= lastPosV.x && lastPosV.x <= lastPosH.x && firstPosV.y <= firstPosH.y && firstPosH.y <= lastPosV.y)
                {
                    intersections.Add(new Vector2Int(firstPosV.x, firstPosH.y));
                }
            }
        }

        // finder (returns matchResult)
        /*
         * find the proper match 
         * examine its positions in the intersection list
         * check the corresponding perpendicular match if it exists in the intersection list
         * delete the position from the perpendicular match list and add the remaining smaller lists (the remainder parts)
         * delete the intersection position from the intersection list
         */

        List<Match> colorBombs = FindColorBombMatches(verticalMatches, horizontalMatches, intersections);

        result.matches.AddRange(colorBombs);
        for(int i = 0; i < colorBombs.Count; i++) {

            result.specialTileTypes.Add(TileType.ColorBomb);
            result.specialTilePositions.Add(colorBombs[i].tilePositions[0]);
        }

        List<Match> TNTs = FindTNTMatches(verticalMatches, horizontalMatches, intersections);

        result.matches.AddRange(TNTs);
        for (int i = 0; i < TNTs.Count; i++) {

            result.specialTileTypes.Add(TileType.TNT);
            result.specialTilePositions.Add(TNTs[i].tilePositions[0]);
        }

        List<Match> rockets = FindRocketMatches(verticalMatches, horizontalMatches, intersections);

        result.matches.AddRange(rockets);
        for (int i = 0; i < rockets.Count; i++) {
            TileType rocketType = IsMatchHorizontal(rockets[i]) ? TileType.HorizontalRocket : TileType.VerticalRocket;
            result.specialTileTypes.Add(rocketType);
            result.specialTilePositions.Add(rockets[i].tilePositions[0]);
        }

        List<Match> remainders = RemainderCleanup(verticalMatches, horizontalMatches, intersections);

        result.matches.AddRange(remainders);
        for (int i = 0; i < remainders.Count; i++)
        {

            result.specialTileTypes.Add(TileType.None);
            result.specialTilePositions.Add(null);
        }

        return result;
    }


    public MatchResult FindMatchesAfterSwap(NodeLayer layer, NodeModel[,] board, Vector2Int pos1, Vector2Int pos2)
    {
        MatchResult result = new MatchResult();
        List<Match> horizontalMatches = new List<Match>();
        List<Match> verticalMatches = new List<Match>();

        // check which coordinate is in the same line (x or y)
        bool isHorizontalSwap = pos1.y == pos2.y;

        if (isHorizontalSwap)
        {
            // Aynı satır: 1 horizontal + 2 vertical line
            horizontalMatches.AddRange(HorizontalMatchFinder(layer, board, pos1.y));
            verticalMatches.AddRange(VerticalMatchFinder(layer, board, pos1.x));
            verticalMatches.AddRange(VerticalMatchFinder(layer, board, pos2.x));
        }
        else
        {
            // Aynı sütun: 1 vertical + 2 horizontal line
            verticalMatches.AddRange(VerticalMatchFinder(layer, board, pos1.x));
            horizontalMatches.AddRange(HorizontalMatchFinder(layer, board, pos1.y));
            horizontalMatches.AddRange(HorizontalMatchFinder(layer, board, pos2.y));
        }

        // when x is same they are at the same vertical line
        // when y is same they are at the same horizontal line
        // check that line and 0 lines perp to this line from the positions

        HashSet<Vector2Int> intersections = new HashSet<Vector2Int>();
        intersections.Add(pos1);
        intersections.Add(pos2);

        List<Match> colorBombs = FindColorBombMatches(verticalMatches, horizontalMatches, intersections);
        if (colorBombs != null)
        {
            foreach (Match match in colorBombs)
            {
                result.matches.Add(match);
                result.specialTileTypes.Add(TileType.ColorBomb);
                
                if(intersections.Contains(pos1) && match.tilePositions.Contains(pos1))
                {
                    result.specialTilePositions.Add(pos1);
                } else if(intersections.Contains(pos2) && match.tilePositions.Contains(pos2))
                {
                    result.specialTilePositions.Add(pos2);
                } else
                {
                    result.specialTilePositions.Add(match.tilePositions[0]);
                }
            }
        }

        List<Match> TNTs = FindTNTMatches(verticalMatches, horizontalMatches, intersections);
        if (TNTs != null)
        {
            foreach (Match match in TNTs)
            {
                result.matches.Add(match);
                result.specialTileTypes.Add(TileType.TNT);

                if (intersections.Contains(pos1) && match.tilePositions.Contains(pos1))
                {
                    result.specialTilePositions.Add(pos1);
                }
                else if (intersections.Contains(pos2) && match.tilePositions.Contains(pos2))
                {
                    result.specialTilePositions.Add(pos2);
                } else
                {
                    result.specialTilePositions.Add(match.tilePositions[0]);
                }
            }
        }

        List<Match> rockets = FindRocketMatches(verticalMatches, horizontalMatches, intersections);
        if (rockets != null)
        {
            foreach (Match match in rockets)
            {
                result.matches.Add(match);

                TileType rocketType = IsMatchHorizontal(match) ? TileType.HorizontalRocket : TileType.VerticalRocket;
                result.specialTileTypes.Add(rocketType);

                if (intersections.Contains(pos1) && match.tilePositions.Contains(pos1))
                {
                    result.specialTilePositions.Add(pos1);
                }
                else if (intersections.Contains(pos2) && match.tilePositions.Contains(pos2))
                {
                    result.specialTilePositions.Add(pos2);
                } else
                {
                    result.specialTilePositions.Add(match.tilePositions[0]);
                }
            }
        }

        List<Match> remainders = RemainderCleanup(verticalMatches, horizontalMatches, intersections);

        result.matches.AddRange(remainders);
        for (int i = 0; i < remainders.Count; i++)
        {

            result.specialTileTypes.Add(TileType.None);
            result.specialTilePositions.Add(null);
        }

        return result;

    }

    public List<Match> HorizontalMatchFinder(NodeLayer layer, NodeModel[,] board, int yPos)
    {
        List<Match> matches = new List<Match>();
        List<Vector2Int> matchedPositions = new List<Vector2Int>();
        TileType prev = TileType.None;
        int m = board.GetLength(0);
        int n = board.GetLength(1);

        for (int x = 0; x < m; x++)
        {
            TileType curr = TileType.None;
            if (DamagePatterns.IsInBoundary(new Vector2Int(x, yPos), m, n))
            {
                TileModel tile = board[x, yPos].GetLayer(layer);
                // Mid-tween shield: tiles still in their FallIteration animation
                // (IsMoving=true) must NOT participate in match detection — they
                // are in their post-fall model cell but pre-arrival in view.
                if (tile != null && tile is IMatchable && !(tile is IMovable mv && mv.IsMoving)) curr = tile.TileType;
            }

            if (curr == TileType.None)
            {
                if (matchedPositions.Count >= 3)
                    matches.Add(new Match(matchedPositions, prev));
                matchedPositions = new List<Vector2Int>();
                prev = TileType.None;
                continue;
            }

            if (prev == TileType.None || curr == prev)
            {
                matchedPositions.Add(new Vector2Int(x, yPos));
            }
            else
            {
                if (matchedPositions.Count >= 3)
                {
                    Match match = new Match(matchedPositions, prev);
                    matches.Add(match); // match'i ekle
                }
                matchedPositions = new List<Vector2Int>();
                matchedPositions.Add(new Vector2Int(x, yPos)); // Yeni tile'ı ekle
            }
            prev = curr;
        }

        // Loop sonunda son match'i kontrol et
        if (matchedPositions.Count >= 3)
        {
            Match match = new Match(matchedPositions, prev);
            matches.Add(match);
        }

        return matches;
    }
    public List<Match> VerticalMatchFinder(NodeLayer layer, NodeModel[,] board, int xPos)
    {
        List<Match> matches = new List<Match>();
        List<Vector2Int> matchedPositions = new List<Vector2Int>();
        TileType prev = TileType.None;
        int m = board.GetLength(0);
        int n = board.GetLength(1);
        for (int y = 0; y < n; y++)
        {
           TileType curr = TileType.None;
           if(DamagePatterns.IsInBoundary(new Vector2Int(xPos, y), m, n))
           {
              TileModel tile = board[xPos, y].GetLayer(layer);
              // See HorizontalMatchFinder: mid-tween IsMoving tiles are skipped.
              if (tile != null && tile is IMatchable && !(tile is IMovable mv && mv.IsMoving)) curr = tile.TileType;
           }

           if (curr == TileType.None)
           {
               if (matchedPositions.Count >= 3)
                   matches.Add(new Match(new List<Vector2Int>(matchedPositions), prev));
               matchedPositions = new List<Vector2Int>();
               prev = TileType.None;
               continue;
           }

           if (prev == TileType.None || curr == prev)
           {
               matchedPositions.Add(new Vector2Int(xPos, y));
           }
           else
           {
                if (matchedPositions.Count >= 3)
                {
                    Match match = new Match(new List<Vector2Int>(matchedPositions), prev);
                    matches.Add(match);
                }
                matchedPositions = new List<Vector2Int>();
                matchedPositions.Add(new Vector2Int(xPos, y));
           }
           prev = curr;
        }

        if (matchedPositions.Count >= 3)
        {
            Match match = new Match(new List<Vector2Int>(matchedPositions), prev);
            matches.Add(match);
        }

        return matches;
    }
    private List<Match> FindColorBombMatches(List<Match> verticalMatches, List<Match> horizontalMatches, HashSet<Vector2Int> intersections)
    {

        List<Match> result = new List<Match>();
        foreach (Match vMatch in verticalMatches.ToList())
        {
            if (vMatch.tilePositions.Count < 5) continue;

            result.Add(vMatch);
            verticalMatches.Remove(vMatch);

            foreach(Vector2Int vPos in vMatch.tilePositions)
            {
                if (!intersections.Contains(vPos)) continue;
                
                foreach(Match hMatch in  horizontalMatches.ToList()) // x variable y const 
                {
                    Vector2Int hFirstPos = hMatch.tilePositions[0];
                    Vector2Int hLastPos = hMatch.tilePositions[hMatch.tilePositions.Count-1];

                    // early return when y not same
                    if (hFirstPos.y != vPos.y) continue;

                    // interval check for efficiency
                    if(hFirstPos.x <= vPos.x && vPos.x <= hLastPos.x)
                    {
                        int index = hMatch.tilePositions.IndexOf(vPos);
                        if (index == -1)
                        {
                            Debug.Log("Error at MatchManager.cs at findColorBombMatches");
                            return result;
                        }

                        List<Vector2Int> before = hMatch.tilePositions.GetRange(0, index);
                        List<Vector2Int> after = hMatch.tilePositions.GetRange(index + 1, hMatch.tilePositions.Count - index - 1);

                        if (before.Count > 0)
                            horizontalMatches.Add(new Match(before, hMatch.matchableType));
                        if (after.Count > 0)
                            horizontalMatches.Add(new Match(after, hMatch.matchableType));

                        horizontalMatches.Remove(hMatch);
                        intersections.Remove(vPos);
                    }
                }
            }
        }

        // Horizontal matches >= 5 (check for color bombs in horizontal direction)
        foreach (Match hMatch in horizontalMatches.ToList())
        {
            if (hMatch.tilePositions.Count < 5) continue;

            result.Add(hMatch);
            horizontalMatches.Remove(hMatch);

            foreach (Vector2Int hPos in hMatch.tilePositions)
            {
                if (!intersections.Contains(hPos)) continue;

                foreach (Match vMatch in verticalMatches.ToList()) // y variable x const
                {
                    Vector2Int vFirstPos = vMatch.tilePositions[0];
                    Vector2Int vLastPos = vMatch.tilePositions[vMatch.tilePositions.Count - 1];

                    // early return when x not same
                    if (vFirstPos.x != hPos.x) continue;

                    // interval check for efficiency
                    if (vFirstPos.y <= hPos.y && hPos.y <= vLastPos.y)
                    {
                        int index = vMatch.tilePositions.IndexOf(hPos);
                        if (index == -1)
                        {
                            Debug.Log("Error at MatchManager.cs at findColorBombMatches (horizontal)");
                            return result;
                        }

                        Match matchToBeUpdated = new Match(vMatch);
                        List<Vector2Int> before = vMatch.tilePositions.GetRange(0, index);
                        List<Vector2Int> after = vMatch.tilePositions.GetRange(index + 1, vMatch.tilePositions.Count - index - 1);

                        if (before.Count > 0)
                            verticalMatches.Add(new Match(before, matchToBeUpdated.matchableType));
                        if (after.Count > 0)
                            verticalMatches.Add(new Match(after, matchToBeUpdated.matchableType));
                        verticalMatches.Remove(vMatch);

                        intersections.Remove(hPos);
                    }
                }
            }
        }

        return result;
    }

    private List<Match> FindTNTMatches(List<Match> verticalMatches, List<Match> horizontalMatches, HashSet<Vector2Int> intersections)
    {
        List<Match> result = new List<Match>();

        // Sort intersections: bottomleftmost first (y ascending, then x ascending)
        List<Vector2Int> sortedIntersections = intersections.OrderBy(pos => pos.y).ThenBy(pos => pos.x).ToList();

        foreach (Vector2Int intersection in sortedIntersections)
        {
            if (!intersections.Contains(intersection)) continue; // Already processed

            // Find horizontal and vertical matches containing this intersection
            Match hMatch = horizontalMatches.FirstOrDefault(m => m.tilePositions.Contains(intersection));
            Match vMatch = verticalMatches.FirstOrDefault(m => m.tilePositions.Contains(intersection));

            // Both must exist and be >= 3
            if (hMatch == null || vMatch == null) continue;
            if (hMatch.tilePositions.Count < 3 || vMatch.tilePositions.Count < 3) continue;

            List<Vector2Int> tntPositions = new List<Vector2Int>(hMatch.tilePositions);
            tntPositions.AddRange(vMatch.tilePositions);
            // Use the underlying color (not TileType.TNT) so CustomDamageMatchExplosion
            // routes through Matchable's "isMatchSource" path — matched tiles die from
            // same-color damage. TileType.TNT as source hits the special-source path
            // which returns Unaffected for already-matched tiles → no DestroySelf for any
            // of the 5 matched tiles at match time. Spawn then overwrites the intersection
            // cell's matched tile in-model, leaving an orphaned view at the TNT spawn cell.
            Match tntMatch = new Match(tntPositions, hMatch.matchableType);

            result.Add(tntMatch);

            horizontalMatches.Remove(hMatch);
            verticalMatches.Remove(vMatch);
            intersections.Remove(intersection);

            // Find all intersections in horizontal match, update vertical matches
            List<Vector2Int> hIntersections = FindIntersections(hMatch.tilePositions, intersections);
            UpdatePerpendicularDirection(verticalMatches, hIntersections);

            // Find all intersections in vertical match, update horizontal matches
            List<Vector2Int> vIntersections = FindIntersections(vMatch.tilePositions, intersections);
            UpdatePerpendicularDirection(horizontalMatches, vIntersections);

            // Merge and remove all processed intersections
            HashSet<Vector2Int> allProcessedIntersections = new HashSet<Vector2Int>(hIntersections);
            allProcessedIntersections.UnionWith(vIntersections);

            foreach (Vector2Int processedInt in allProcessedIntersections)
            {
                intersections.Remove(processedInt);
            }
        }

        return result;
    }

    // by the time we reach to the rocket matches we might have 1,2,3,4 length matches valid matches may be intersecting with non valid matches we 
    private List<Match> FindRocketMatches(List<Match> verticalMatches, List<Match> horizontalMatches, HashSet<Vector2Int> intersections)
    {
        List<Match> result = new List<Match>();

        // Process vertical rockets (4-tile vertical matches)
        List<Match> verticalMatchesCopy = verticalMatches.ToList();
        for (int i = 0; i < verticalMatchesCopy.Count; i++)
        {
            Match currMatch = verticalMatchesCopy[i];
            if (currMatch.tilePositions.Count != 4) continue;

            result.Add(currMatch);
            verticalMatches.Remove(currMatch);

            // Find all intersections in this vertical rocket match
            List<Vector2Int> vIntersections = FindIntersections(currMatch.tilePositions, intersections);

            // Update horizontal matches (cut them at intersection points)
            UpdatePerpendicularDirection(horizontalMatches, vIntersections);

            // Remove processed intersections
            foreach (Vector2Int intersection in vIntersections)
            {
                intersections.Remove(intersection);
            }
        }

        // Process horizontal rockets (4-tile horizontal matches)
        List<Match> horizontalMatchesCopy = horizontalMatches.ToList();
        for (int i = 0; i < horizontalMatchesCopy.Count; i++)
        {
            Match currMatch = horizontalMatchesCopy[i];
            if (currMatch.tilePositions.Count != 4) continue;

            result.Add(currMatch);
            horizontalMatches.Remove(currMatch);

            // Find all intersections in this horizontal rocket match
            List<Vector2Int> hIntersections = FindIntersections(currMatch.tilePositions, intersections);

            // Update vertical matches (cut them at intersection points)
            UpdatePerpendicularDirection(verticalMatches, hIntersections);

            // Remove processed intersections
            foreach (Vector2Int intersection in hIntersections)
            {
                intersections.Remove(intersection);
            }
        }

        return result;
    }

    // returns the match-3's and remainders with no intersection to prevent double damage
    private List<Match> RemainderCleanup(List<Match> verticalMatches, List<Match> horizontalMatches, HashSet<Vector2Int> intersections)
    {
        List<Match> result = new List<Match>();

        // Process length 3 matches first (normal match-3s)
        ProcessMatchesByLength(verticalMatches, horizontalMatches, intersections, result, 3);

        // Process length 2 matches (split fragments that might intersect)
        ProcessMatchesByLength(verticalMatches, horizontalMatches, intersections, result, 2);

        // Remaining matches (length 1 or any other) - just add directly
        result.AddRange(verticalMatches);
        result.AddRange(horizontalMatches);

        return result;
    }

    private void ProcessMatchesByLength(List<Match> verticalMatches, List<Match> horizontalMatches,
        HashSet<Vector2Int> intersections, List<Match> result, int targetLength)
    {
        // Process vertical matches of target length
        foreach (Match vMatch in verticalMatches.ToList())
        {
            if (vMatch.tilePositions.Count != targetLength) continue;

            result.Add(vMatch);
            verticalMatches.Remove(vMatch);

            List<Vector2Int> vIntersections = FindIntersections(vMatch.tilePositions, intersections);
            UpdatePerpendicularDirection(horizontalMatches, vIntersections);

            foreach (Vector2Int intersection in vIntersections)
            {
                intersections.Remove(intersection);
            }
        }

        // Process horizontal matches of target length
        foreach (Match hMatch in horizontalMatches.ToList())
        {
            if (hMatch.tilePositions.Count != targetLength) continue;

            result.Add(hMatch);
            horizontalMatches.Remove(hMatch);

            List<Vector2Int> hIntersections = FindIntersections(hMatch.tilePositions, intersections);
            UpdatePerpendicularDirection(verticalMatches, hIntersections);

            foreach (Vector2Int intersection in hIntersections)
            {
                intersections.Remove(intersection);
            }
        }
    }
    private List<Vector2Int> FindIntersections(List<Vector2Int> positions, HashSet<Vector2Int> intersections)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        foreach (Vector2Int pos in positions)
        {
            if (intersections.Contains(pos))
            {
                result.Add(pos);
            }
        }

        return result;
    }

    private void UpdatePerpendicularDirection(List<Match> perpendicularMatches, List<Vector2Int> intersections)
    {
        foreach (Vector2Int intersection in intersections)
        {
            foreach (Match match in perpendicularMatches.ToList())
            {
                if (!match.tilePositions.Contains(intersection)) continue;

                int index = match.tilePositions.IndexOf(intersection);

                List<Vector2Int> before = match.tilePositions.GetRange(0, index);
                List<Vector2Int> after = match.tilePositions.GetRange(index + 1, match.tilePositions.Count - index - 1);

                if (before.Count > 0)
                    perpendicularMatches.Add(new Match(before, match.matchableType));
                if (after.Count > 0)
                    perpendicularMatches.Add(new Match(after, match.matchableType));

                perpendicularMatches.Remove(match);
            }
        }
    }

    private bool IsMatchHorizontal(Match match)
    {
        return match.tilePositions[0].y == match.tilePositions[1].y;
    }
}

public class Match
{
    public List<Vector2Int> tilePositions;
    public TileType matchableType;

    // Constructor
    public Match(List<Vector2Int> positions, TileType type)
    {
        tilePositions = positions;
        matchableType = type;
    }

    public Match(Match other)
    {
        tilePositions = other.tilePositions.ToList();
        matchableType = other.matchableType;
    }
}

public class MatchResult
{
    public List<Match> matches;
    public List<TileType> specialTileTypes;
    public List<Vector2Int?> specialTilePositions;

    public MatchResult()
    {
        matches = new List<Match>();
        specialTileTypes = new List<TileType>();
        specialTilePositions = new List<Vector2Int?>();
    }
}

