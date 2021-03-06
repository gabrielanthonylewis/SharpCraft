﻿using OpenTK;
using OpenTK.Graphics.OpenGL;
using SharpCraft.block;
using SharpCraft.entity;
using SharpCraft.item;
using SharpCraft.model;
using SharpCraft.render.shader;
using SharpCraft.render.shader.shaders;
using SharpCraft.texture;
using SharpCraft.util;
using SharpCraft.world;
using SharpCraft.world.chunk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GL = OpenTK.Graphics.OpenGL.GL;
using TextureTarget = OpenTK.Graphics.OpenGL.TextureTarget;

namespace SharpCraft.render
{
    internal class WorldRenderer
    {
        private readonly ModelCubeOutline _selectionOutline;

        private readonly ShaderTexturedCube _shaderTexturedCube;
        private readonly ModelRaw _destroyProgressModel;
        private ModelCustom _armModel;

        private Vector4 _selectionOutlineColor = MathUtil.Hue(0);

        private int _hue;

        private int _renderDistance;

        private Vector3 lookVec;
        private Vector3 lastLookVec;

        private Vector3 motion;
        private Vector3 lastMotion;

        private float fov;
        private float lastFov;
        private int _ticks;

        public int RenderDistance
        {
            get => _renderDistance;
            set => _renderDistance = MathHelper.Clamp(value, 3, int.MaxValue);
        }

        public WorldRenderer()
        {
            _selectionOutline = new ModelCubeOutline();
            _shaderTexturedCube = new ShaderTexturedCube();

            var cube = CubeModelBuilder.CreateCubeVertexes();

            _destroyProgressModel = ModelManager.LoadModel3ToVao(cube);
            JsonModelLoader.LoadModel("entity/player/arm", new Shader<ModelCustom>("block"));

            RenderDistance = 8;
            lastFov = fov = SharpCraft.Instance.Camera.PartialFov;
        }

        public void Update()
        {
            lastLookVec = lookVec;

            lastMotion = motion;
            lastFov = fov;

            _ticks = (_ticks + 1) % 360;

            if (SharpCraft.Instance.Player != null)
            {
                motion = SharpCraft.Instance.Player.Motion;

                fov = SharpCraft.Instance.Player.Motion.Xz.LengthFast > 0.15f && SharpCraft.Instance.Player.IsRunning
                    ? Math.Clamp(fov * 1.065f, 0, SharpCraft.Instance.Camera.TargetFov + 6)
                    : Math.Clamp(fov * 0.965f, SharpCraft.Instance.Camera.TargetFov,
                        SharpCraft.Instance.Camera.TargetFov + 6);
            }

            _selectionOutlineColor = MathUtil.Hue(_hue = (_hue + 5) % 360);

            lookVec = SharpCraft.Instance.Camera.GetLookVec();

            _armModel = JsonModelLoader.GetCustomModel("entity/player/arm");
        }

        public void Render(World world, float partialTicks)
        {
            if (world == null) return;
            world.LoadManager.LoadImportantChunks();
            world.LoadManager.BuildImportantChunks();

            GL.BindTexture(TextureTarget.Texture2D, JsonModelLoader.TEXTURE_BLOCKS);

            MouseOverObject hit = SharpCraft.Instance.MouseOverObject;

            if (hit.hit == HitType.Block)
            {
                var state = world.GetBlockState(hit.blockPos);
                if (!Equals(state, BlockRegistry.GetBlock<BlockAir>().GetState()))
                    RenderBlockSelectionOutline(state, hit.blockPos);
            }

            RenderChunks(world);
            RenderHand(partialTicks);
            RenderWorldWaypoints(world);
            RenderDestroyProgress(world);

            float partialFov = lastFov + (fov - lastFov) * partialTicks;
            Vector3 partialMotion = lastMotion + (motion - lastMotion) * partialTicks;

            SharpCraft.Instance.Camera.pitchOffset = partialMotion.Y * 0.025f;
            SharpCraft.Instance.Camera.SetFOV(partialFov);
        }

        private void RenderChunks(World world)
        {
            foreach (Chunk chunk in world.Chunks.Values)
            {
                if (!chunk.ShouldRender(RenderDistance))
                    continue;

                if (chunk.QueuedForModelBuild)
                    RenderChunkOutline(chunk);

                chunk.Render();
            }
        }

        private void RenderWorldWaypoints(World world)
        {
            var wps = world.GetWaypoints();

            if (wps.Count == 0)
                return;

            ModelBlock model = JsonModelLoader.GetModelForBlock(BlockRegistry.GetBlock<BlockRare>().UnlocalizedName);

            float size = 0.25f;

            GL.DepthRange(0, 0.1f);
            model.Bind();

            foreach (Waypoint wp in wps)
            {
                model.Shader.UpdateGlobalUniforms();
                model.Shader.UpdateModelUniforms(model.RawModel);
                model.Shader.UpdateInstanceUniforms(MatrixHelper.CreateTransformationMatrix(wp.Pos.ToVec() + Vector3.One / 2 * (1 - size), new Vector3(45, 30, 35.264f), size), model);

                model.RawModel.Render(PrimitiveType.Quads);
            }

            model.Unbind();

            GL.DepthRange(0, 1);
        }

        private void RenderDestroyProgress(World world)
        {
            var progresses = SharpCraft.Instance.DestroyProgresses;
            if (progresses.Count == 0)
                return;

            GL.BindTexture(TextureTarget.Texture2D, TextureManager.TEXTURE_DESTROY_PROGRESS.ID);
            GL.BlendFunc(BlendingFactorSrc.DstColor, BlendingFactorDest.Zero);

            GL.BindVertexArray(_destroyProgressModel.VaoID);
            GL.EnableVertexAttribArray(0);

            _shaderTexturedCube.Bind();
            _shaderTexturedCube.UpdateGlobalUniforms();
            _shaderTexturedCube.UpdateModelUniforms(_destroyProgressModel);

            Vector3 sizeO = Vector3.One * 0.0045f;

            foreach (KeyValuePair<BlockPos, DestroyProgress> pair in progresses)
            {
                BlockState state = world.GetBlockState(pair.Key);

                if (state.Block == BlockRegistry.GetBlock<BlockAir>())
                    continue;

                Matrix4 mat = MatrixHelper.CreateTransformationMatrix(pair.Key.ToVec() - sizeO / 2 + state.Block.BoundingBox.min, state.Block.BoundingBox.size + sizeO);

                _shaderTexturedCube.UpdateInstanceUniforms(mat);
                _shaderTexturedCube.UpdateUVs(TextureManager.TEXTURE_DESTROY_PROGRESS, 0, 32 * (int)(pair.Value.PartialProgress * 8), 32);
                _destroyProgressModel.Render(PrimitiveType.Quads);
            }

            GL.DisableVertexAttribArray(0);
            GL.BindVertexArray(0);

            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            _shaderTexturedCube.Unbind();
        }

        private void RenderChunkOutline(Chunk ch)
        {
            Shader<ModelCubeOutline> shader = _selectionOutline.Shader;

            Vector3 size = new Vector3(Chunk.ChunkSize, Chunk.ChunkHeight, Chunk.ChunkSize);

            _selectionOutline.Bind();
            _selectionOutline.SetColor(Vector4.One);

            shader.UpdateGlobalUniforms();
            shader.UpdateModelUniforms(_selectionOutline.RawModel);
            shader.UpdateInstanceUniforms(MatrixHelper.CreateTransformationMatrix(ch.Pos, size), _selectionOutline);

            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            _selectionOutline.RawModel.Render(PrimitiveType.Quads);

            GL.Enable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);

            _selectionOutline.Unbind();
        }

        private void RenderBlockSelectionOutline(BlockState state, BlockPos pos)
        {
            var shader = _selectionOutline.Shader;
            AxisAlignedBB bb = state.Block.BoundingBox;

            if (bb == null)
                return;

            _selectionOutline.Bind();
            _selectionOutline.SetColor(Vector4.One);

            shader.UpdateGlobalUniforms();
            shader.UpdateModelUniforms(_selectionOutline.RawModel);
            shader.UpdateInstanceUniforms(MatrixHelper.CreateTransformationMatrix(pos.ToVec() + bb.min, bb.size + Vector3.One * 0.0025f), _selectionOutline);

            GL.LineWidth(2);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            _selectionOutline.RawModel.Render(PrimitiveType.Quads);

            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);

            _selectionOutline.Unbind();
        }

        private void RenderHand(float partialTicks)
        {
            if (SharpCraft.Instance.Player == null)
                return;

            ItemStack stack = SharpCraft.Instance.Player.GetEquippedItemStack();

            if (stack == null || stack.IsEmpty)
            {
                RenderArmModel(partialTicks);
                return;
            }

            if (stack.Item is ItemBlock itemBlock)
            {
                RenderBlockInHand(itemBlock.Block, partialTicks);
            }
        }

        private void RenderArmModel(float partialTicks)
        {
            if (_armModel == null)
                return;

            GL.DepthRange(0, 0.1f);

            if (!SharpCraft.Instance.Player.onGround)
                motion *= Vector3.UnitY;

            Vector3 partialLookVec = lastLookVec + (lookVec - lastLookVec) * partialTicks;
            Vector3 partialMotion = lastMotion + (motion - lastMotion) * partialTicks;

            float angle = MathHelper.DegreesToRadians((_ticks + partialTicks) * 20);

            float height = 0.05f * Math.Clamp(partialMotion.Xz.Length * 5, 0, 1);

            float offsetX = (float)Math.Cos(angle) * height;
            float offsetY = (float)Math.Sin(_ticks * 2 % 360 > 180 ? -angle * 2 + MathHelper.Pi : angle * 2) * height * 0.5f;

            Vector2 rotVec = new Vector2(-SharpCraft.Instance.Camera.pitch, -SharpCraft.Instance.Camera.yaw);

            Vector3 offset = new Vector3(0.55f + offsetX, -0.475f + offsetY, 0);
            offset.Y -= Math.Clamp(partialMotion.Y, -0.35f, 0.35f) / 15f;

            Matrix4 r1 = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(75)) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(15)) * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(-25));
            Matrix4 r2 = Matrix4.CreateRotationX(rotVec.X - SharpCraft.Instance.Camera.pitchOffset) * Matrix4.CreateRotationY(rotVec.Y);

            Matrix4 s = Matrix4.CreateScale(1.5f);
            Matrix4 t0 = Matrix4.CreateTranslation(Vector3.One * -0.5f);
            Matrix4 t1 = Matrix4.CreateTranslation(SharpCraft.Instance.Camera.pos + SharpCraft.Instance.Camera.GetLookVec() + partialLookVec * 0.1f);
            Matrix4 tFinal = Matrix4.CreateTranslation(offset);

            Matrix4 mat = t0 * r1 * tFinal * r2 * s * t1;

            GL.BindTexture(TextureTarget.Texture2D, _armModel.TextureID);

            _armModel.Bind();
            _armModel.Shader.UpdateGlobalUniforms();
            _armModel.Shader.UpdateModelUniforms(_armModel.RawModel);
            _armModel.Shader.UpdateInstanceUniforms(mat, _armModel);

            _armModel.RawModel.Render(PrimitiveType.Quads);

            _armModel.Unbind();

            GL.DepthRange(0, 1);
        }

        private void RenderBlockInHand(Block block, float partialTicks)
        {
            GL.DepthRange(0, 0.1f);

            ModelBlock model = JsonModelLoader.GetModelForBlock(block.UnlocalizedName);

            if (model == null)
                return;

            if (!SharpCraft.Instance.Player.onGround)
                motion *= Vector3.UnitY;

            Vector3 partialLookVec = lastLookVec + (lookVec - lastLookVec) * partialTicks;
            Vector3 partialMotion = lastMotion + (motion - lastMotion) * partialTicks;

            float angle = MathHelper.DegreesToRadians((_ticks + partialTicks) * 20);

            float height = 0.15f * Math.Clamp(partialMotion.Xz.Length * 5, 0, 1);

            float offsetX = (float)Math.Cos(angle) * height;
            float offsetY = (float)Math.Sin(_ticks * 2 % 360 > 180 ? -angle * 2 + MathHelper.Pi : angle * 2) * height * 0.5f;

            Vector2 rotVec = new Vector2(-SharpCraft.Instance.Camera.pitch, -SharpCraft.Instance.Camera.yaw);

            float itemBlockOffsetY = block.BoundingBox.size.Y / 2 - 0.5f;

            Vector3 offset = new Vector3(1.35f + offsetX, -1.25f - itemBlockOffsetY + offsetY, 0.3f);
            offset.Y -= Math.Clamp(partialMotion.Y, -0.35f, 0.35f) / 10f;

            Matrix4 r1 = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(45));
            Matrix4 r2 = Matrix4.CreateRotationX(rotVec.X - SharpCraft.Instance.Camera.pitchOffset) * Matrix4.CreateRotationY(rotVec.Y);

            Matrix4 s = Matrix4.CreateScale(0.5525f);
            Matrix4 t0 = Matrix4.CreateTranslation(Vector3.One * -0.5f);
            Matrix4 t1 = Matrix4.CreateTranslation(SharpCraft.Instance.Camera.pos + SharpCraft.Instance.Camera.GetLookVec() + partialLookVec * 0.1f);
            Matrix4 tFinal = Matrix4.CreateTranslation(offset);

            Matrix4 mat = t0 * r1 * tFinal * r2 * s * t1;

            model.Bind();

            model.Shader.UpdateGlobalUniforms();
            model.Shader.UpdateModelUniforms(model.RawModel);
            model.Shader.UpdateInstanceUniforms(mat, model);

            model.RawModel.Render(PrimitiveType.Quads);

            model.Unbind();

            GL.DepthRange(0, 1);
        }
    }
}