﻿using SharpCraft.block;
using SharpCraft.entity;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;

namespace SharpCraft.world
{
    internal class WorldLoader
    {
        public static void SaveWorld(World w)
        {
            if (w == null)
                return;

            BinaryFormatter bf = new BinaryFormatter();

            w.SaveAllChunks();

            try
            {
                WorldPlayerNode wpn = new WorldPlayerNode(SharpCraft.Instance.Player);
                WorldDataNode wdn = new WorldDataNode(w);

                using (FileStream fs = File.OpenWrite(w.SaveRoot + "/player.dat"))
                {
                    fs.Position = 0;
                    bf.Serialize(fs, wpn);
                }

                using (FileStream fs = File.OpenWrite(w.SaveRoot + "/level.dat"))
                {
                    fs.Position = 0;
                    bf.Serialize(fs, wdn);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        public static World LoadWorld(string saveName)
        {
            BinaryFormatter bf = new BinaryFormatter();

            string dir = $"{SharpCraft.Instance.GameFolderDir}saves/{saveName}";

            if (!Directory.Exists(dir))
                return null;

            World world = null;

            try
            {
                WorldDataNode wdn;
                WorldPlayerNode wpn;

                using (FileStream fs = File.OpenRead(dir + "/player.dat"))
                {
                    fs.Position = 0;
                    wpn = (WorldPlayerNode)bf.Deserialize(fs);
                }

                using (FileStream fs = File.OpenRead(dir + "/level.dat"))
                {
                    fs.Position = 0;
                    wdn = (WorldDataNode)bf.Deserialize(fs);
                }

                world = wdn.GetWorld(saveName);

                EntityPlayerSP player = wpn.GetPlayer(world);

                world.AddEntity(player);
                world.LoadChunk(new BlockPos(player.Pos).ChunkPos());
                SharpCraft.Instance.Player = player;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }

            return world;
        }
    }
}