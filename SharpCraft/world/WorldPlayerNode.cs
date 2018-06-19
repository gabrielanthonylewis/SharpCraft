﻿using OpenTK;
using SharpCraft.entity;
using SharpCraft.item;
using System;

namespace SharpCraft.world
{
    [Serializable]
    internal class WorldPlayerNode
    {
        private readonly float pitch;
        private readonly float yaw;

        private readonly Vector3 pos;

        private readonly ItemStack[] hotbar;
        private readonly ItemStack[] inventory;

        private readonly float healthPercentage;

        public WorldPlayerNode(EntityPlayerSP player)
        {
            pitch = SharpCraft.Instance.Camera.pitch;
            yaw = SharpCraft.Instance.Camera.yaw;
            pos = player.Pos;
            hotbar = player.Hotbar;
            inventory = player.Inventory;
            healthPercentage = player.Health;
        }

        public EntityPlayerSP GetPlayer(World world)
        {
            EntityPlayerSP player = new EntityPlayerSP(world, pos);
            SharpCraft.Instance.Camera.pitch = pitch;
            SharpCraft.Instance.Camera.yaw = yaw;

            for (int i = 0; i < hotbar.Length; i++)
            {
                player.Hotbar[i] = hotbar[i];
            }

            for (int i = 0; i < inventory.Length; i++)
            {
                player.Inventory[i] = inventory[i];
            }

            player.Health = healthPercentage;

            return player;
        }
    }
}