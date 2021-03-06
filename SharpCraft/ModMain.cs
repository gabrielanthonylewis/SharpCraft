﻿namespace SharpCraft
{
    internal abstract class ModMain
    {
        public ModInfo ModInfo { get; protected set; }

        protected ModMain(ModInfo modInfo)
        {
            ModInfo = modInfo;
        }

        public abstract void OnItemsAndBlocksRegistry(RegistryEventArgs args);
    }
}