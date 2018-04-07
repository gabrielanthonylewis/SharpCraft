﻿using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using SharpCraft.block;
using SharpCraft.entity;
using SharpCraft.gui;
using SharpCraft.model;
using SharpCraft.render;
using SharpCraft.texture;
using SharpCraft.util;
using SharpCraft.world;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SharpCraft.item;
using SharpCraft.render.shader;
using Bitmap = System.Drawing.Bitmap;
using Point = OpenTK.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = OpenTK.Size;

namespace SharpCraft
{
    internal class SharpCraft : GameWindow
    {
        //string _dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.sharpcraft";
        string _dir = ".\\";

        public string GameFolderDir
        {
            get
            {
                if (!Directory.Exists(_dir))
                    Directory.CreateDirectory(_dir);

                return _dir;
            }
            set
            {
                _dir = value;

                if (!Directory.Exists(_dir))
                    Directory.CreateDirectory(_dir);
            }
        }

        public WorldRenderer WorldRenderer;
        public EntityRenderer EntityRenderer;
        public ParticleRenderer ParticleRenderer;
        public SkyboxRenderer SkyboxRenderer;
        public GuiRenderer GuiRenderer;

        public KeyboardState KeyboardState;
        //public HashSet<Key> KeysDown = new HashSet<Key>();
        public MouseOverObject MouseOverObject = new MouseOverObject();
        private MouseOverObject _lastMouseOverObject = new MouseOverObject();

        public EntityPlayerSP Player;

        public World World;

        public ConcurrentDictionary<BlockPos, DestroyProgress> DestroyProgresses = new ConcurrentDictionary<BlockPos, DestroyProgress>();

        private List<MouseButton> _mouseButtonsDown = new List<MouseButton>();
        private ConcurrentQueue<Action> _glContextQueue = new ConcurrentQueue<Action>();
        private Stopwatch _frameTimer = Stopwatch.StartNew();
        private WindowState _lastWindowState;

        public static SharpCraft Instance { get; private set; }

        public Camera Camera;

        public GuiScreen GuiScreen { get; private set; }

        private Point _mouseLast;
        private float _mouseWheelLast;

        private bool _takeScreenshot;
        private bool _gamePaused;
        private bool _wasSpaceDown;
        private int _fpsCounter;
        private long _tickCounter;
        private float _sensitivity = 1;
        private float _lastPartialTicks;

        private string _glVersion;

        private static string _title;
        private static Thread _renderThread;

        private GameTimer timer = new GameTimer(60, 20);

        public SharpCraft() : base(680, 480, GraphicsMode.Default, _title, GameWindowFlags.Default, DisplayDevice.Default, 3, 3,
            GraphicsContextFlags.ForwardCompatible)
        {
            Instance = this;
            Camera = new Camera();
            _renderThread = Thread.CurrentThread;

            VSync = VSyncMode.Off;
            MakeCurrent();

            _glVersion = GL.GetString(StringName.ShadingLanguageVersion);
            Title = _title = $"SharpCraft Alpha 0.0.3 [GLSL {_glVersion}]";

            //TargetRenderFrequency = 60;

            Console.WriteLine("DEBUG: stitching textures");
            TextureManager.LoadTextures();

            Init();
        }

        private void Init()
        {
            Console.WriteLine("DEBUG: loading models");

            //TODO - merge shaders and use strings as block IDs like sharpcraft:dirt
            var shader = new Shader<ModelBlock>("block");
            var shaderUnlit = new Shader<ModelBlock>("block_unlit");

            var missingModel = new ModelBlock(EnumBlock.MISSING, shader);
            var stoneModel = new ModelBlock(EnumBlock.STONE, shader);
            var grassModel = new ModelBlock(EnumBlock.GRASS, shader);
            var dirtModel = new ModelBlock(EnumBlock.DIRT, shader);
            var cobblestoneModel = new ModelBlock(EnumBlock.COBBLESTONE, shader);
            var planksModel = new ModelBlock(EnumBlock.PLANKS, shader);
            var craftingTableModel = new ModelBlock(EnumBlock.CRAFTING_TABLE, shader, true);
            var furnaceModel = new ModelBlock(EnumBlock.FURNACE, shader, true);
            var bedrockModel = new ModelBlock(EnumBlock.BEDROCK, shader);
            var rareModel = new ModelBlock(EnumBlock.RARE, shader);
            var glassModel = new ModelBlock(EnumBlock.GLASS, shader, false, true);
            var logModel = new ModelBlock(EnumBlock.LOG, shader);
            var leavesModel = new ModelBlock(EnumBlock.LEAVES, shader, false, true);

            var xrayModel = new ModelBlock(EnumBlock.XRAY, shader);

            ModelRegistry.RegisterBlockModel(missingModel, 0);

            ModelRegistry.RegisterBlockModel(stoneModel, 0);
            ModelRegistry.RegisterBlockModel(grassModel, 0);
            ModelRegistry.RegisterBlockModel(dirtModel, 0);
            ModelRegistry.RegisterBlockModel(cobblestoneModel, 0);
            ModelRegistry.RegisterBlockModel(planksModel, 0);
            ModelRegistry.RegisterBlockModel(craftingTableModel, 0);
            ModelRegistry.RegisterBlockModel(furnaceModel, 0);

            ModelRegistry.RegisterBlockModel(bedrockModel, 0);
            ModelRegistry.RegisterBlockModel(rareModel, 0);
            ModelRegistry.RegisterBlockModel(glassModel, 0);
            ModelRegistry.RegisterBlockModel(logModel, 0);
            ModelRegistry.RegisterBlockModel(leavesModel, 0);

            ModelRegistry.RegisterBlockModel(xrayModel, 0);

            SettingsManager.Load();

            WorldRenderer = new WorldRenderer();
            EntityRenderer = new EntityRenderer();
            ParticleRenderer = new ParticleRenderer();
            SkyboxRenderer = new SkyboxRenderer();
            GuiRenderer = new GuiRenderer();

            _sensitivity = SettingsManager.GetFloat("sensitivity");
            WorldRenderer.RenderDistance = SettingsManager.GetInt("renderdistance");

            OpenGuiScreen(new GuiScreenMainMenu());

            timer.infiniteFps = true;
            timer.renderHook = Render;
        }

        public void StartGame()
        {
            var loadedWorld = WorldLoader.LoadWorld("MyWorld");

            if (loadedWorld == null)
            {
                Console.WriteLine("DEBUG: generating world");

                var playerPos = new BlockPos(MathUtil.NextFloat(-100, 100), 10, MathUtil.NextFloat(-100, 100));

                World = new World("MyWorld", "Tomlow's Fuckaround", SettingsManager.GetValue("worldseed").GetHashCode());

                Player = new EntityPlayerSP(World, new Vector3(playerPos.X, World.GetHeightAtPos(playerPos.X, playerPos.Z), playerPos.Z));

                World.AddEntity(Player);

                //Player.SetItemStackInInventory(0, new ItemStack(new ItemBlock(EnumBlock.CRAFTING_TABLE)));
                //Player.SetItemStackInInventory(1, new ItemStack(new ItemBlock(EnumBlock.FURNACE)));
                //Player.SetItemStackInInventory(2, new ItemStack(new ItemBlock(EnumBlock.COBBLESTONE)));
                //Player.SetItemStackInInventory(3, new ItemStack(new ItemBlock(EnumBlock.PLANKS)));
                //Player.SetItemStackInInventory(4, new ItemStack(new ItemBlock(EnumBlock.GLASS)));
                //Player.SetItemStackInInventory(5, new ItemStack(new ItemBlock(EnumBlock.XRAY)));
            }
            else
            {
                World = loadedWorld;
            }

            ResetMouse();

            var state = OpenTK.Input.Mouse.GetState();
            _mouseLast = new Point(state.X, state.Y);

            timer.updateHook = () =>
            {
                if (!IsDisposed && Visible)
                {
                    GetMouseOverObject();
                }

                GameLoop();
            };

            //world.setBlock(new BlockPos(player.pos), EnumBlock.RARE, 1, true); //test of block metadata, works perfectly
        }

        private void GameLoop()
        {
            if (GuiScreen == null && !Focused)
                OpenGuiScreen(new GuiScreenIngameMenu());

            if (_gamePaused)
                return;

            var wheelValue = Mouse.WheelPrecise;

            if (Player != null) // && GuiScreen == null)
            {
                if (AllowInput())
                {
                    if (wheelValue < _mouseWheelLast)
                        Player.SelectNextItem();
                    else if (wheelValue > _mouseWheelLast)
                        Player.SelectPreviousItem();

                    if (World?.GetChunk(new BlockPos(Player.pos).ChunkPos()) == null)
                        Player.motion = Vector3.Zero;

                    var lmb = _mouseButtonsDown.Contains(MouseButton.Left);
                    var rmb = _mouseButtonsDown.Contains(MouseButton.Right);

                    if (lmb || rmb)
                    {
                        _tickCounter++;

                        var lastPos = _lastMouseOverObject.blockPos;

                        if (lmb && _lastMouseOverObject.hit is EnumBlock)
                        {
                            ParticleRenderer.SpawnDiggingParticle(_lastMouseOverObject);

                            if (MouseOverObject.hit != null && _lastMouseOverObject.hit == MouseOverObject.hit &&
                                lastPos == MouseOverObject.blockPos)
                            {
                                if (!DestroyProgresses.TryGetValue(lastPos, out var progress))
                                    DestroyProgresses.TryAdd(lastPos,
                                        progress = new DestroyProgress(lastPos, Player));
                                else
                                    progress.Progress++;

                                if (progress.Destroyed)
                                    DestroyProgresses.TryRemove(progress.Pos, out var removed);
                            }
                            else ResetDestroyProgress(Player);
                        }

                        _lastMouseOverObject = MouseOverObject;

                        if (_tickCounter % 4 == 0)
                        {
                            GetMouseOverObject();

                            if (rmb)
                                Player.PlaceBlock();
                        }
                    }
                    else
                    {
                        _tickCounter = 0;
                        ResetDestroyProgress(Player);
                    }
                }
                else
                    ResetDestroyProgress(Player);
            }

            _mouseWheelLast = wheelValue;

            World?.Update(Player, WorldRenderer.RenderDistance);

            WorldRenderer?.Update();
            SkyboxRenderer.Update();

            ParticleRenderer?.TickParticles();
        }

        private void RenderScreen()
        {
            float partialTicks = GetPartialTicksForRender();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.DepthClamp);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            if (World != null)
            {
                WorldRenderer.Render(World, partialTicks);
                ParticleRenderer.Render(partialTicks);
                EntityRenderer.Render(partialTicks);
                SkyboxRenderer.Render(partialTicks);
            }

            //render other gui
            if (Player != null)
            {
                GuiRenderer.renderCrosshair();
                GuiRenderer.renderHUD();
            }

            //render gui screen
            if (GuiScreen != null)
            {
                CursorVisible = true;
                GuiRenderer.render(GuiScreen);
            }

            GL.Flush();
        }

        private void GetMouseOverObject()
        {
            var radius = 5.5f;

            var final = new MouseOverObject();

            var dist = float.MaxValue;

            var camPos = Vector3.One * 0.5f + Camera.pos;

            for (var z = -radius; z <= radius; z++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        var vec = camPos;
                        vec.X += x;
                        vec.Y += y;
                        vec.Z += z;

                        var f = (vec - Camera.pos).LengthFast;

                        if (f <= radius + 0.5f)
                        {
                            var pos = new BlockPos(vec);
                            var block = World.GetBlock(pos);

                            if (block != EnumBlock.AIR)
                            {
                                var model = ModelRegistry.GetModelForBlock(block, World.GetMetadata(pos));
                                var bb = model.boundingBox.offset(pos.ToVec());

                                var hitSomething = RayHelper.rayIntersectsBB(Camera.pos,
                                    Camera.GetLookVec(), bb, out var hitPos, out var normal);

                                if (hitSomething)
                                {
                                    FaceSides sideHit = FaceSides.Null;

                                    if (normal.X < 0)
                                        sideHit = FaceSides.West;
                                    else if (normal.X > 0)
                                        sideHit = FaceSides.East;
                                    if (normal.Y < 0)
                                        sideHit = FaceSides.Down;
                                    else if (normal.Y > 0)
                                        sideHit = FaceSides.Up;
                                    if (normal.Z < 0)
                                        sideHit = FaceSides.North;
                                    else if (normal.Z > 0)
                                        sideHit = FaceSides.South;

                                    var p = new BlockPos(hitPos - normal * 0.5f); ;

                                    if (sideHit == FaceSides.Null)
                                        continue;

                                    var l = Math.Abs((Camera.pos - (p.ToVec() + Vector3.One * 0.5f)).Length);

                                    if (l < dist)
                                    {
                                        dist = l;

                                        final.hit = block;
                                        final.hitVec = hitPos;
                                        final.blockPos = p;
                                        final.normal = normal;
                                        final.sideHit = sideHit;

                                        final.boundingBox = bb;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            MouseOverObject = final;
        }

        private void ResetDestroyProgress(EntityPlayerSP player)
        {
            foreach (var progress in DestroyProgresses.Values)
            {
                if (progress.Player == player)
                    DestroyProgresses.TryRemove(progress.Pos, out var removed);
            }
        }

        private void ResetMouse()
        {
            var middle = PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
            OpenTK.Input.Mouse.SetPosition(middle.X, middle.Y);
        }

        public void RunGlTasks()
        {
            if (_glContextQueue.Count == 0)
                return;

            while (_glContextQueue.Count > 0)
            {
                if (_glContextQueue.TryDequeue(out var func))
                    func?.Invoke();
            }

            GL.Flush();
        }

        private void HandleMouseMovement()
        {
            var state = Mouse.GetState();

            var point = new Point(state.X, state.Y);

            if (AllowInput())
            {
                var delta = new Point(_mouseLast.X - point.X, _mouseLast.Y - point.Y);

                Camera.yaw -= delta.X / 1000f * _sensitivity;
                Camera.pitch -= delta.Y / 1000f * _sensitivity;

                //ResetMouse();

                if (KeyboardState.IsKeyDown(Key.Space) && !_wasSpaceDown && Player.onGround)
                {
                    _wasSpaceDown = true;
                    Player.motion.Y = 0.475F;
                }
                else if ((!KeyboardState.IsKeyDown(Key.Space) || Player.onGround) && _wasSpaceDown)
                    _wasSpaceDown = false;
            }

            _mouseLast = point;

            if (_frameTimer.ElapsedMilliseconds >= 1000)
            {
                _frameTimer.Restart();

                Title = $"{_title} - FPS: {_fpsCounter}";
                _fpsCounter = 0;
            }
        }

        public void RunGlContext(Action a)
        {
            if (_renderThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId) a();
            else _glContextQueue.Enqueue(a);
        }

        public float GetPartialTicksForRender()
        {
            return _gamePaused ? _lastPartialTicks : _lastPartialTicks = timer.GetPartialTicks();
        }

        private bool AllowInput()
        {
            return GuiScreen == null && !(CursorVisible = !Focused);
        }

        public void OpenGuiScreen(GuiScreen guiScreen)
        {
            if (guiScreen == null)
            {
                CloseGuiScreen();
                return;
            }

            GuiScreen = guiScreen;

            if (guiScreen.DoesGuiPauseGame)
                _gamePaused = true;

            var middle = new Point(ClientRectangle.Width / 2, ClientRectangle.Height / 2);
            middle = PointToScreen(middle);

            OpenTK.Input.Mouse.SetPosition(middle.X, middle.Y);
        }

        public void CloseGuiScreen()
        {
            if (GuiScreen == null)
                return;

            if (GuiScreen.DoesGuiPauseGame)
                _gamePaused = false;

            GuiScreen.OnClose();
            GuiScreen = null;

            CursorVisible = false;
        }

        private void CaptureScreen()
        {
            Bitmap bmp = new Bitmap(ClientSize.Width, ClientSize.Height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (bmp)
            {
                BitmapData bData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadWrite, bmp.PixelFormat);

                GL.ReadBuffer(ReadBufferMode.Back);
                // read the data directly into the bitmap's buffer (bitmap is stored in BGRA)
                GL.ReadPixels(0, 0, ClientSize.Width, ClientSize.Height, OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                    PixelType.UnsignedByte, bData.Scan0);

                bmp.UnlockBits(bData);
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

                var time = DateTime.UtcNow;

                var dir = $"{GameFolderDir}\\screenshots";
                var file = $"{dir}\\{time.ToShortDateString()}_{time.TimeOfDay.Hours}.{time.TimeOfDay.Minutes}.{time.TimeOfDay.Seconds}.png";

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    bmp.Save(fs, ImageFormat.Png);
                }
            }
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            timer.CheckRender();
        }

        private void Render()
        {
            RunGlTasks();

            HandleMouseMovement();

            timer.CheckUpdate();
            Camera.UpdateViewMatrix();

            RenderScreen();

            _fpsCounter++;

            if (_takeScreenshot)
            {
                _takeScreenshot = false;

                CaptureScreen();
            }

            SwapBuffers();
            ProcessEvents(false);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.IsPressed)
            {
                if (GuiScreen == null)
                {
                    GetMouseOverObject();

                    //pickBlock
                    if (e.Button == MouseButton.Middle)
                        Player?.PickBlock();

                    //place/interact
                    if (e.Button == MouseButton.Right || e.Button == MouseButton.Left)
                        Player?.OnClick(e.Button);
                }
                else
                {
                    var state = OpenTK.Input.Mouse.GetCursorState();
                    var point = PointToClient(new Point(state.X, state.Y));

                    GuiScreen.OnMouseClick(point.X, point.Y);
                }

                if (!_mouseButtonsDown.Contains(e.Button))
                    _mouseButtonsDown.Add(e.Button);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            _mouseButtonsDown.Remove(e.Button);

            if (e.Button == MouseButton.Left && AllowInput())
                ResetDestroyProgress(Player);
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Escape:
                    if (GuiScreen is GuiScreenMainMenu || KeyboardState.IsKeyDown(Key.Escape))
                        return;

                    if (GuiScreen != null)
                        CloseGuiScreen();
                    else
                        OpenGuiScreen(new GuiScreenIngameMenu());
                    break;
                case Key.E:
                    if (KeyboardState.IsKeyDown(Key.E))
                        return;

                    if (GuiScreen is GuiScreenInventory)
                    {
                        CloseGuiScreen();
                        return;
                    }

                    if (GuiScreen == null)
                        OpenGuiScreen(new GuiScreenInventory());
                    break;
                case Key.F2:
                    _takeScreenshot = true;
                    break;
                case Key.F11:
                    if (WindowState != WindowState.Fullscreen)
                    {
                        _lastWindowState = WindowState;
                        WindowState = WindowState.Fullscreen;
                    }
                    else
                        WindowState = _lastWindowState;
                    break;
            }

            if (AllowInput())
            {
                switch (e.Key)
                {
                    case Key.Q:
                        if (KeyboardState.IsKeyDown(Key.Q))
                            return;

                        if (e.Control)
                            Player.DropHeldStack();
                        else
                            Player?.DropHeldItem();
                        break;

                    case Key.R:
                        if (!e.Control || KeyboardState.IsKeyDown(Key.R))
                            return;

                        Shader.ReloadAll();
                        SettingsManager.Load();
                        TextureManager.Reload();

                        WorldRenderer.RenderDistance = SettingsManager.GetInt("renderdistance");
                        _sensitivity = SettingsManager.GetFloat("sensitivity");

                        if (e.Shift)
                            World?.DestroyChunkModels();

                        break;
                }

                if (GuiScreen == null)
                {
                    for (var i = 0; i < 9; i++)
                    {
                        if (e.Key == Key.Number1 + i)
                        {
                            Player?.SetSelectedSlot(i);

                            break;
                        }
                    }
                }

                if (e.Key == (Key.LAlt | Key.F4))
                    Exit();
            }

            KeyboardState = e.Keyboard;
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            base.OnKeyUp(e);

            KeyboardState = e.Keyboard;
        }

        protected override void OnResize(EventArgs e)
        {
            if (ClientSize.Width < 640)
                ClientSize = new Size(640, ClientSize.Height);
            if (ClientSize.Height < 480)
                ClientSize = new Size(ClientSize.Width, 480);

            GL.Viewport(ClientRectangle);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, ClientRectangle.Width, ClientRectangle.Height, 0, Camera.NearPlane, Camera.FarPlane);

            Camera.UpdateProjectionMatrix();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Shader.DestroyAll();
            ModelManager.cleanup();
            TextureManager.Destroy();

            if (World != null)
                WorldLoader.SaveWorld(World);

            base.OnClosing(e);
        }
    }

    internal class SettingsManager
    {
        private static Dictionary<string, string> _settings = new Dictionary<string, string>();

        static SettingsManager()
        {
            _settings.Add("sensitivity", "1");
            _settings.Add("renderdistance", "8");
            _settings.Add("worldseed", "yeet");
        }

        public static void Load()
        {
            var file = SharpCraft.Instance.GameFolderDir + "/settings.txt";

            if (File.Exists(file))
            {
                var data = File.ReadLines(file);

                foreach (var line in data)
                {
                    var parsed = line.Trim().Replace(" ", "").ToLower();
                    var split = parsed.Split('=');

                    if (split.Length < 2)
                        continue;

                    var variable = split[0];
                    var value = split[1];

                    if (_settings.ContainsKey(variable))
                    {
                        _settings.Remove(variable);
                        _settings.Add(variable, value);
                    }
                }
            }

            Save();
        }

        public static void Save()
        {
            var file = SharpCraft.Instance.GameFolderDir + "/settings.txt";

            var sb = new StringBuilder();

            var keys = _settings.Keys.ToArray();

            for (var index = 0; index < keys.Length - 1; index++)
            {
                var key = keys[index];

                sb.AppendLine($"{key}={GetValue(key)}");
            }

            var last = _settings.Last();

            sb.Append($"{last.Key}={GetValue(last.Key)}");

            File.WriteAllText(file, sb.ToString());
        }

        public static string GetValue(string variable)
        {
            return _settings[variable];
        }

        public static int GetInt(string variable)
        {
            return int.Parse(GetValue(variable));
        }

        public static float GetFloat(string variable)
        {
            return float.Parse(GetValue(variable));
        }

        public static bool GetBool(string variable)
        {
            return bool.Parse(GetValue(variable));
        }
    }

    internal class Start
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ThreadPool.SetMinThreads(0, 0);
            ThreadPool.SetMaxThreads(Environment.ProcessorCount, Environment.ProcessorCount);

            using (var game = new SharpCraft())
            {
                game.Run(30.0);
            }
        }
    }
}