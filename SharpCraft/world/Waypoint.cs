﻿using OpenTK;
using SharpCraft.block;

namespace SharpCraft.world
{
    public class Waypoint
    {
        public BlockPos Pos;
        public Color Color;
        public string Name;

        public Waypoint(BlockPos pos, Color color, string name)
        {
            Pos = pos;
            Color = color;
            Name = name;
        }
    }
}