using System;
using UnityEngine;

namespace DreamGamesCase.PureLogic.Tests
{
    /// <summary>
    /// ASCII-row board constructor for readable tests.
    /// Char map: r=Red, g=Green, b=Blue, y=Yellow, p=Purple,
    ///           B=Box, S=Stone, V=Vase, T=TNT, C=ColorBomb,
    ///           H=HorizontalRocket, R=VerticalRocket, .=null.
    /// Rows are given top-to-bottom (visually natural). Internally,
    /// we flip so board[x, 0] is the bottom row and board[x, h-1] is the top.
    /// </summary>
    internal static class TestBoardBuilder
    {
        public static NodeModel[,] MiddleLayerFromAscii(params string[] rowsTopToBottom)
        {
            if (rowsTopToBottom == null || rowsTopToBottom.Length == 0)
                throw new ArgumentException("rows empty");

            int height = rowsTopToBottom.Length;
            int width = rowsTopToBottom[0].Length;

            for (int i = 0; i < rowsTopToBottom.Length; i++)
            {
                if (rowsTopToBottom[i].Length != width)
                    throw new ArgumentException($"row {i} width mismatch");
            }

            NodeModel[,] board = new NodeModel[width, height];

            for (int visualRow = 0; visualRow < height; visualRow++)
            {
                // y=0 should be the bottom; rowsTopToBottom[0] is the top.
                int y = (height - 1) - visualRow;
                string row = rowsTopToBottom[visualRow];

                for (int x = 0; x < width; x++)
                {
                    TileModel middle = MakeTile(row[x]);
                    board[x, y] = new NodeModel(null, middle, null);
                }
            }

            return board;
        }

        private static TileModel MakeTile(char c)
        {
            switch (c)
            {
                case '.': return null;
                case 'r': return new Matchable(TileType.Red);
                case 'g': return new Matchable(TileType.Green);
                case 'b': return new Matchable(TileType.Blue);
                case 'y': return new Matchable(TileType.Yellow);
                case 'p': return new Matchable(TileType.Purple);
                case 'B': return new Box();
                case 'S': return new Stone();
                case 'V': return new Vase();
                case 'T': return new TNT();
                case 'C': return new ColorBomb();
                case 'H': return new Rocket(TileType.HorizontalRocket);
                case 'R': return new Rocket(TileType.VerticalRocket);
                default:
                    throw new ArgumentException($"unknown tile char '{c}'");
            }
        }
    }
}
