﻿using OpenTK;
using SharpCraft.render.shader;
using SharpCraft.render.shader.shaders;
using SharpCraft.texture;

namespace SharpCraft.gui
{
    internal class GuiScreenMainMenu : GuiScreen
    {
        private GuiTexture background;

        public GuiScreenMainMenu()
        {
            buttons.Add(new GuiButton(0, 0, 200, Vector2.One * 2) { centered = true });
            background = new GuiTexture(TextureManager.loadTexture("gui/bg"), Vector2.Zero, Vector2.One * 8);
        }

        public override void Render(Shader<Gui> shader, int mouseX, int mouseY)
        {
            drawBackground(shader, background);

            base.Render(shader, mouseX, mouseY);
        }

        protected override void buttonClicked(GuiButton btn)
        {
            switch (btn.ID)
            {
                case 0:
                    SharpCraft.Instance.CloseGuiScreen();
                    SharpCraft.Instance.StartGame();
                    break;
            }
        }

        public override void onClose()
        {
            TextureManager.destroyTexture(background.textureID);
        }
    }
}