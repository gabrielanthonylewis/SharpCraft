﻿using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace SharpCraft
{
    public class BetterWindow : NativeWindow
    {
        private IGraphicsContext glContext;
        private bool isExiting;

        public float LastFrameRenderTime { get; private set; }

        public BetterWindow(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device)
          : this(width, height, mode, title, options, device, 1, 0, GraphicsContextFlags.Default)
        {
        }

        public BetterWindow(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device, int major, int minor, GraphicsContextFlags flags)
          : this(width, height, mode, title, options, device, major, minor, flags, null)
        {
        }

        public BetterWindow(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device, int major, int minor, GraphicsContextFlags flags, IGraphicsContext sharedContext)
          : base(width, height, title, options, mode ?? GraphicsMode.Default, device ?? DisplayDevice.Default)
        {
            try
            {
                glContext = new GraphicsContext(mode ?? GraphicsMode.Default, WindowInfo, major, minor, flags);
                glContext.MakeCurrent(WindowInfo);
                (glContext as IGraphicsContextInternal).LoadAll();
                VSync = VSyncMode.On;
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                base.Dispose();
                throw;
            }
        }

        public override void Dispose()
        {
            try
            {
                Dispose(true);
            }
            finally
            {
                try
                {
                    if (glContext != null)
                    {
                        glContext.Dispose();
                        glContext = null;
                    }
                }
                finally
                {
                    base.Dispose();
                }
            }
            GC.SuppressFinalize(this);
        }

        public virtual void Exit()
        {
            Close();
        }

        public void MakeCurrent()
        {
            EnsureUndisposed();
            Context.MakeCurrent(WindowInfo);
        }

        public void Run()
        {
            EnsureUndisposed();

            Visible = true;
            OnResize(EventArgs.Empty);

            Stopwatch sw = Stopwatch.StartNew();

            while (true)
            {
                ProcessEvents();

                if (Exists && !IsExiting)
                {
                    OnRenderFrame();
                    LastFrameRenderTime = (float)sw.Elapsed.TotalMilliseconds;
                    sw.Restart();
                }
                else
                    break;
            }
        }

        public void SwapBuffers()
        {
            EnsureUndisposed();
            Context.SwapBuffers();
        }

        public IGraphicsContext Context
        {
            get
            {
                EnsureUndisposed();
                return glContext;
            }
        }

        public bool IsExiting
        {
            get
            {
                EnsureUndisposed();
                return isExiting;
            }
        }

        public KeyboardDevice Keyboard
        {
            get
            {
#pragma warning disable CS0612 // Type or member is obsolete
                if (InputDriver.Keyboard.Count <= 0)
                    return null;
                return InputDriver.Keyboard[0];
            }
        }

        public MouseDevice Mouse
        {
            get
            {
                if (InputDriver.Mouse.Count <= 0)
                    return null;
                return InputDriver.Mouse[0];
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }

        public VSyncMode VSync
        {
            get
            {
                EnsureUndisposed();
                GraphicsContext.Assert();
                if (Context.SwapInterval < 0)
                    return VSyncMode.Adaptive;
                return Context.SwapInterval == 0 ? VSyncMode.Off : VSyncMode.On;
            }
            set
            {
                EnsureUndisposed();
                GraphicsContext.Assert();
                switch (value)
                {
                    case VSyncMode.Off:
                        Context.SwapInterval = 0;
                        break;

                    case VSyncMode.On:
                        Context.SwapInterval = 1;
                        break;

                    case VSyncMode.Adaptive:
                        Context.SwapInterval = -1;
                        break;
                }
            }
        }

        public override WindowState WindowState
        {
            get
            {
                return base.WindowState;
            }
            set
            {
                base.WindowState = value;
                Debug.Print("Updating Context after setting WindowState to {0}", (object)value);
                if (Context == null)
                    return;
                Context.Update(WindowInfo);
            }
        }

        protected virtual void Dispose(bool manual)
        {
        }

        protected virtual void OnRenderFrame()
        {
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            glContext.Update(WindowInfo);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel)
                return;
            isExiting = true;
        }
    }
}