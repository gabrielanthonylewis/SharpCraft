﻿using OpenTK;
using OpenTK.Input;
using SharpCraft.block;
using SharpCraft.gui;
using SharpCraft.item;
using SharpCraft.model;
using SharpCraft.util;
using SharpCraft.world;
using System;
using System.Linq;

namespace SharpCraft.entity
{
    public class EntityPlayerSP : Entity
    {
        private readonly float maxMoveSpeed = 0.22f;
        private float moveSpeedMult = 1;

        public float EyeHeight = 1.625f;

        private Vector2 moveSpeed;

        public bool IsRunning { get; private set; }

        public int HotbarIndex { get; private set; }

        public ItemStack[] Hotbar { get; }
        public ItemStack[] Inventory { get; }

        public bool HasFullInventory => Hotbar.All(stack => stack != null && !stack.IsEmpty) && Inventory.All(stack => stack != null && !stack.IsEmpty);

        public EntityPlayerSP(World world, Vector3 pos = new Vector3()) : base(world, pos)
        {
            SharpCraft.Instance.Camera.pos = pos + Vector3.UnitY * 1.625f;

            collisionBoundingBox = new AxisAlignedBB(new Vector3(0.6f, 1.65f, 0.6f));
            boundingBox = collisionBoundingBox.offset(pos - (Vector3.UnitX * collisionBoundingBox.size.X / 2 + Vector3.UnitZ * collisionBoundingBox.size.Z / 2));

            Hotbar = new ItemStack[9];
            Inventory = new ItemStack[27];
        }

        public override void Update()
        {
            if (SharpCraft.Instance.Focused)
                UpdateCameraMovement();

            base.Update();
        }

        public override void Render(float partialTicks)
        {
            var interpolatedPos = lastPos + (pos - lastPos) * partialTicks;

            SharpCraft.Instance.Camera.pos = interpolatedPos + Vector3.UnitY * EyeHeight;
        }

        private void UpdateCameraMovement()
        {
            if (SharpCraft.Instance.GuiScreen != null)
                return;

            var state = SharpCraft.Instance.KeyboardState;

            Vector2 dirVec = Vector2.Zero;

            var w = state.IsKeyDown(Key.W); //might use this later
            var s = state.IsKeyDown(Key.S);
            var a = state.IsKeyDown(Key.A);
            var d = state.IsKeyDown(Key.D);

            if (w) dirVec += SharpCraft.Instance.Camera.forward;
            if (s) dirVec += -SharpCraft.Instance.Camera.forward;
            if (a) dirVec += SharpCraft.Instance.Camera.left;
            if (d) dirVec += -SharpCraft.Instance.Camera.left;

            float mult = 1;

            if (IsRunning = state.IsKeyDown(Key.LShift))
                mult = 1.5f;

            if (dirVec != Vector2.Zero)
            {
                moveSpeedMult = MathHelper.Clamp(moveSpeedMult + 0.085f, 1, 1.55f);

                moveSpeed = MathUtil.Clamp(moveSpeed + dirVec.Normalized() * 0.1f * moveSpeedMult, 0, maxMoveSpeed);

                motion.Xz = moveSpeed * mult;
            }
            else
            {
                moveSpeed = Vector2.Zero;
                moveSpeedMult = 1;
            }
        }

        public void FastMoveStack(int index)
        {
            ItemStack stack = GetItemStackInInventory(index);
            int maxStackSize = stack.Item.MaxStackSize();

            // return if there is no item to move
            if (stack == null || stack.Item == null)
                return;


            // Hotbar to Inventory
            if (index < Hotbar.Length)
            {
                // TODO: Add to Itemstack as a function(Find in stack?)
                // 1. find same object in inventory to stack
                for (int inventoryIdx = 0; inventoryIdx < Inventory.Length; inventoryIdx++)
                {
                    // empty so continue
                    if (Inventory[inventoryIdx] == null || Inventory[inventoryIdx].Item == null)
                        continue;

                    // check if same item
                    if (Inventory[inventoryIdx].Item != Hotbar[index].Item)
                        continue;

                    // check if enough space
                    if (Inventory[inventoryIdx].Count >= maxStackSize)
                        continue;

                    // if there is enough for whole inventory stack then combine
                    if (Inventory[inventoryIdx].Count + Hotbar[index].Count <= maxStackSize)
                    {
                        Inventory[inventoryIdx].Count += Hotbar[index].Count;
                        SetItemStackInHotbar(index, null);
                        return;
                    }
                    else
                    {
                        Inventory[inventoryIdx].Count -= maxStackSize - Hotbar[index].Count;
                        Hotbar[index].Count = maxStackSize;

                    }
                }

                // 2. find first free inventory spot
                for (int inventoryIdx = 0; inventoryIdx < Inventory.Length; inventoryIdx++)
                {
                    // not empty
                    if (Inventory[inventoryIdx] != null && Inventory[inventoryIdx].Item != null)
                        continue;

                    // empty slot found
                    if (Hotbar[index].Count > maxStackSize)
                    {
                        ItemStack newStack = Hotbar[index].Copy();      
                        newStack.Count = maxStackSize;
                        SetItemStackInInventory(inventoryIdx + Hotbar.Length, newStack);

                        Hotbar[index].Count -= maxStackSize;
                        continue;
                    }
                    else
                    {
                        SetItemStackInInventory(inventoryIdx + Hotbar.Length, Hotbar[index].Copy());
                        SetItemStackInHotbar(index, null);
                        return;
                    }
                 
                }
            }
            // Inventory to Hotbar
            else
            {
                int inventoryItemIdx = index - Hotbar.Length;
        
                // 1. find same object in hotbar to stack
                for (int hotBarIdx = 0; hotBarIdx < Hotbar.Length; hotBarIdx++)
                {
                    // empty so continue
                    if (Hotbar[hotBarIdx] == null || Hotbar[hotBarIdx].Item == null)
                        continue;

                    // check if same item
                    if (Hotbar[hotBarIdx].Item != Inventory[inventoryItemIdx].Item)
                        continue;

                    // check if enough space
                    if (Hotbar[hotBarIdx].Count >= maxStackSize)
                        continue;

                    // if there is enough for whole inventory stack then combine
                    if (Hotbar[hotBarIdx].Count + Inventory[inventoryItemIdx].Count <= maxStackSize)
                    {
                        Hotbar[hotBarIdx].Count += Inventory[inventoryItemIdx].Count;
                        SetItemStackInInventory(index, null);
                        return;
                    }
                    else
                    {
                        Inventory[inventoryItemIdx].Count -= maxStackSize - Hotbar[hotBarIdx].Count;
                        Hotbar[hotBarIdx].Count = maxStackSize;

                    }
                }

                // 2. find first free hotbar spot
                for (int hotBarIdx = 0; hotBarIdx < Hotbar.Length; hotBarIdx++)
                {
                    // not empty
                    if (Hotbar[hotBarIdx] != null && Hotbar[hotBarIdx].Item != null)
                        continue;

                    // empty slot found
                    if (Inventory[inventoryItemIdx].Count > maxStackSize)
                    {
                        ItemStack newStack = Inventory[inventoryItemIdx].Copy();
                        newStack.Count = maxStackSize;
                        SetItemStackInHotbar(hotBarIdx, newStack);

                        Inventory[inventoryItemIdx].Count -= maxStackSize;
                        continue;
                    }
                    else
                    {
                        SetItemStackInHotbar(hotBarIdx, Inventory[inventoryItemIdx]);
                        SetItemStackInInventory(index, null);
                        return;
                    }
                  
                }
            }
        }

        public void SetItemStackInInventory(int index, ItemStack stack)
        {
            if (index < Hotbar.Length)
                SetItemStackInHotbar(index, stack);
            else
                Inventory[index - Hotbar.Length] = stack;
            //  BEFORE: Inventory[index % Inventory.Length] = stack;
        }

        private void SetItemStackInHotbar(int index, ItemStack stack)
        {
            Hotbar[index % Hotbar.Length] = stack;
        }

        public ItemStack GetItemStackInInventory(int index)
        {
            if (index < Hotbar.Length)
                return GetItemStackInHotbar(index);

            return Inventory[index - Hotbar.Length];
        }

        private ItemStack GetItemStackInHotbar(int index)
        {
            return Hotbar[index % Hotbar.Length];
        }

        public void SetItemStackInSelectedSlot(ItemStack stack)
        {
            Hotbar[HotbarIndex] = stack;
        }

        public bool CanPickUpStack(ItemStack dropped)
        {
            return Hotbar.Any(stack => stack == null || stack.IsEmpty || stack.ItemSame(dropped) && stack.Count + dropped.Count <= dropped.Item.MaxStackSize()) ||
                   Inventory.Any(stack => stack == null || stack.IsEmpty || stack.ItemSame(dropped) && stack.Count + dropped.Count <= dropped.Item.MaxStackSize());
        }

        public bool OnPickup(ItemStack dropped)
        {
            var inventorySize = Hotbar.Length + Inventory.Length;

            var lastKnownEmpty = -1;

            // Check Hotbar first
            for(int i = 0; i < Hotbar.Length; i++)
            {
                ItemStack stack = GetItemStackInInventory(i);
                if (stack == null || stack.IsEmpty)
                    continue;

                if (dropped.Item == stack.Item && stack.Count <= stack.Item.MaxStackSize())
                {
                    int toPickUp = Math.Min(stack.Item.MaxStackSize() - stack.Count, dropped.Count);

                    stack.Count += toPickUp;
                    dropped.Count -= toPickUp;
                }

                // return if fully combined
                if (dropped.IsEmpty)
                    return true;
            }

            for (var i = inventorySize - 1; i >= 0; i--)
            {
                var stack = GetItemStackInInventory(i);

                if (stack == null || stack.IsEmpty)
                {
                    lastKnownEmpty = i;
                    continue;
                }

                // Continue as already looked at Hotbar 
                if (i > Hotbar.Length)
                    continue;

                
                if (dropped.Item == stack.Item && stack.Count <= stack.Item.MaxStackSize())
                {
                    var toPickUp = Math.Min(stack.Item.MaxStackSize() - stack.Count, dropped.Count);

                    stack.Count += toPickUp;
                    dropped.Count -= toPickUp;
                }

                if (dropped.IsEmpty)
                    break;
            }

            if (lastKnownEmpty != -1)
            {
                SetItemStackInInventory(lastKnownEmpty, dropped.Copy());
                dropped.Count = 0;
            }

            return dropped.IsEmpty;
        }

        public void OnClick(MouseButton btn)
        {
            var moo = SharpCraft.Instance.MouseOverObject;

            if (moo.hit is EnumBlock)
            {
                if (btn == MouseButton.Right)
                {
                    var block = world.GetBlock(moo.blockPos);
                    var model = ModelRegistry.GetModelForBlock(block, world.GetMetadata(moo.blockPos));

                    if (model != null && model.canBeInteractedWith)
                    {
                        switch (block)
                        {
                            case EnumBlock.FURNACE:
                            case EnumBlock.CRAFTING_TABLE:
                                SharpCraft.Instance.OpenGuiScreen(new GuiScreenCrafting());
                                break;
                        }
                    }
                    else
                        PlaceBlock();
                }
                else if (btn == MouseButton.Left)
                {
                    //BreakBlock(); TODO - start breaking
                }
            }
        }

        public void BreakBlock()
        {
            var moo = SharpCraft.Instance.MouseOverObject;
            if (!(moo.hit is EnumBlock))
                return;

            var block = world.GetBlock(moo.blockPos);

            if (block == EnumBlock.AIR)
                return;

            var meta = world.GetMetadata(moo.blockPos);

            SharpCraft.Instance.ParticleRenderer.SpawnDestroyParticles(moo.blockPos, block, meta);

            world.SetBlock(moo.blockPos, EnumBlock.AIR, 0);

            var motion = new Vector3(MathUtil.NextFloat(-0.15f, 0.15f), 0.25f, MathUtil.NextFloat(-0.15f, 0.15f));

            var entityDrop = new EntityItem(world, moo.blockPos.ToVec() + Vector3.One * 0.5f, motion, new ItemStack(new ItemBlock(block), 1, meta));

            world.AddEntity(entityDrop);

            SharpCraft.Instance.GetMouseOverObject();
        }

        public void PlaceBlock()
        {
            var moo = SharpCraft.Instance.MouseOverObject;
            if (!(moo.hit is EnumBlock))
                return;

            var stack = GetEquippedItemStack();

            if (!(stack?.Item is ItemBlock itemBlock))
                return;

            var pos = moo.blockPos.Offset(moo.sideHit);
            var blockAtPos = world.GetBlock(pos);

            var heldBlock = itemBlock.GetBlock();
            var blockBb = ModelRegistry.GetModelForBlock(heldBlock, world.GetMetadata(pos))
                .boundingBox.offset(pos.ToVec());

            if (blockAtPos != EnumBlock.AIR || world.GetIntersectingEntitiesBBs(blockBb).Count > 0)
                return;

            var posUnder = pos.Offset(FaceSides.Down);

            var blockUnder = world.GetBlock(posUnder);
            var blockAbove = world.GetBlock(pos.Offset(FaceSides.Up));

            if (blockUnder == EnumBlock.GRASS && heldBlock != EnumBlock.GLASS)
                world.SetBlock(posUnder, EnumBlock.DIRT, 0);
            if (blockAbove != EnumBlock.AIR && blockAbove != EnumBlock.GLASS &&
                heldBlock == EnumBlock.GRASS)
                world.SetBlock(pos, EnumBlock.DIRT, 0);
            else
                world.SetBlock(pos, heldBlock, stack.Meta);

            stack.Count--;

            SharpCraft.Instance.GetMouseOverObject();
        }

        public void PickBlock()
        {
            var moo = SharpCraft.Instance.MouseOverObject;

            if (moo.hit is EnumBlock clickedBlock)
            {
                var clickedMeta = world.GetMetadata(moo.blockPos);

                if (clickedBlock != EnumBlock.AIR)
                {
                    for (int i = 0; i < Hotbar.Length; i++)
                    {
                        var stack = Hotbar[i];

                        if (stack?.Item?.InnerItem == clickedBlock && stack.Meta == clickedMeta)
                        {
                            SetSelectedSlot(i);
                            return;
                        }

                        if (stack?.IsEmpty == true)
                        {
                            var itemBlock = new ItemBlock(clickedBlock);
                            var itemStack = new ItemStack(itemBlock, 1, world.GetMetadata(moo.blockPos));

                            SetItemStackInHotbar(i, itemStack);
                            SetSelectedSlot(i);
                            return;
                        }
                    }

                    SetItemStackInSelectedSlot(new ItemStack(new ItemBlock(clickedBlock), 1,
                        world.GetMetadata(moo.blockPos)));
                }
            }
        }

        public void DropHeldItem()
        {
            ThrowStack(GetEquippedItemStack(), 1);
        }

        public void DropHeldStack()
        {
            ThrowStack(GetEquippedItemStack());
        }

        public void ThrowStack(ItemStack stack)
        {
            if (stack == null)
                return;

            ThrowStack(stack, stack.Count);
        }

        public void ThrowStack(ItemStack stack, int count)
        {
            if (stack == null || stack.IsEmpty)
                return;

            var ammountToThrow = Math.Min(count, stack.Count);

            var toThrow = stack.Copy(1);
            toThrow.Count = ammountToThrow;

            world?.AddEntity(new EntityItem(world, SharpCraft.Instance.Camera.pos - Vector3.UnitY * 0.35f,
                SharpCraft.Instance.Camera.GetLookVec() * 0.75f + Vector3.UnitY * 0.1f, toThrow));

            stack.Count -= ammountToThrow;
        }

        public ItemStack GetEquippedItemStack()
        {
            return Hotbar[HotbarIndex];
        }

        public void SetSelectedSlot(int index)
        {
            HotbarIndex = index % 9;
        }

        public void SelectNextItem()
        {
            HotbarIndex = (HotbarIndex + 1) % 9;
        }

        public void SelectPreviousItem()
        {
            if (HotbarIndex <= 0)
                HotbarIndex = 8;
            else
                HotbarIndex = HotbarIndex - 1;
        }
    }
}