﻿using System;
using OpenTK;
using SharpCraft.model;
using SharpCraft.render.shader.uniform;

namespace SharpCraft.render.shader.module
{
	public class ShaderModule3D<T> : ShaderModule<T>
	{
		private UniformMat4 Projection;
		private UniformMat4 Transform;
		private UniformMat4 View;

		public ShaderModule3D(Shader<T> parent) : base(parent)
		{
		}

		public override void InitUniforms()
		{
			try
			{
				Projection = new UniformMat4(Parent.GetUniformId("projectionMatrix"));
			}
			catch
			{}
			try
			{
				View = new UniformMat4(Parent.GetUniformId("viewMatrix"));
			}
			catch
			{}
			try
			{
				Transform = new UniformMat4(Parent.GetUniformId("transformationMatrix"));
			}
			catch
			{}
		}

		public override void UpdateGlobalUniforms()
		{
			Projection?.Update(SharpCraft.Instance.Camera.Projection);
			View?.Update(SharpCraft.Instance.Camera.View);
		}

		public override void UpdateInstanceUniforms(Matrix4 transform, T renderable)
		{
			UpdateGlobalUniforms();
			Transform?.Update(transform);
		}
	}
}