﻿using SharpCraft.render.shader;

namespace SharpCraft.model
{
    public class ModelCustom : ModelBaked<ModelCustom>
    {
        public int TextureID { get; }

        public ModelCustom(int textureId, IModelRaw rawModel, Shader<ModelCustom> shader) : base(rawModel, shader)
        {
            TextureID = textureId;
        }
    }
}
