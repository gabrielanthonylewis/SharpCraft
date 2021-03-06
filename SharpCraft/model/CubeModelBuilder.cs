﻿using SharpCraft.block;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;
using SharpCraft.util;

namespace SharpCraft.model
{
    public static class CubeModelBuilder
    {
        private static readonly Dictionary<FaceSides, float[]> _cube = new Dictionary<FaceSides, float[]>();

        static CubeModelBuilder()
        {
            _cube.Add(FaceSides.North, new float[]
            {
                1, 1, 0,
                1, 0, 0,
                0, 0, 0,
                0, 1, 0
            });
            _cube.Add(FaceSides.South, new float[]
            {
                0, 1, 1,
                0, 0, 1,
                1, 0, 1,
                1, 1, 1
            });
            _cube.Add(FaceSides.East, new float[]
            {
                1, 1, 1,
                1, 0, 1,
                1, 0, 0,
                1, 1, 0
            });
            _cube.Add(FaceSides.West, new float[]
            {
                0, 1, 0,
                0, 0, 0,
                0, 0, 1,
                0, 1, 1
            });
            _cube.Add(FaceSides.Up, new float[]
            {
                0, 1, 0,
                0, 1, 1,
                1, 1, 1,
                1, 1, 0
            });
            _cube.Add(FaceSides.Down, new float[]
            {
                0, 0, 1,
                0, 0, 0,
                1, 0, 0,
                1, 0, 1
            });
        }

        public static void AppendCubeModel(JsonCube cube, Dictionary<string, string> modelTextureVariables, Dictionary<string, TextureMapElement> textureMap, ref float[] vertexes, ref float[] normals, ref float[] uvs, int n)
        {
            int startIndex2 = n * 48;
            int startIndex3 = n * 72;

            int faceIndex = 0;

            foreach (var pair in cube.faces.OrderBy(p => (int)p.Key))
            {
                int uvIndex = 8 * faceIndex;

                TextureType side = pair.Key;
                JsonCubeFaceUv textureNode = pair.Value;

                //edit: textureNode.Texture can be anything. it is a variable defined by the modeller
                //textureNode.Texture isn't the name of the texture file! it is '#side', '#block', '#bottom', ... TODO - if '#' is not present, use the texture from the texture map

                string textureNameForFace = modelTextureVariables[textureNode.texture];

                if (textureMap.TryGetValue(textureNameForFace, out var tme))
                {
                    var percentageU1 = Math.Clamp(textureNode.uv[0] / 16f, 0, 1);
                    var percentageV1 = Math.Clamp(textureNode.uv[1] / 16f, 0, 1);
                    var percentageU2 = Math.Clamp(textureNode.uv[2] / 16f, 0, 1);
                    var percentageV2 = Math.Clamp(textureNode.uv[3] / 16f, 0, 1);

                    Vector2 size = tme.UVMax - tme.UVMin;

                    var minU = tme.UVMin.X + size.X * percentageU1;
                    var minV = tme.UVMin.Y + size.Y * percentageV1;
                    var maxU = tme.UVMin.X + size.X * percentageU2;
                    var maxV = tme.UVMin.Y + size.Y * percentageV2;

                    uvs[startIndex2 + uvIndex] = minU;
                    uvs[startIndex2 + uvIndex + 1] = minV;

                    uvs[startIndex2 + uvIndex + 2] = minU;
                    uvs[startIndex2 + uvIndex + 3] = maxV;

                    uvs[startIndex2 + uvIndex + 4] = maxU;
                    uvs[startIndex2 + uvIndex + 5] = maxV;

                    uvs[startIndex2 + uvIndex + 6] = maxU;
                    uvs[startIndex2 + uvIndex + 7] = minV;
                }

                Vector3 rot = Vector3.Zero;
                Vector3 ori = Vector3.Zero;

                if (cube.rotation != null)
                {
                    ori = new Vector3(cube.rotation.origin[0], cube.rotation.origin[1], cube.rotation.origin[2]) / 16f;

                    rot[(int) cube.rotation.axis] = MathHelper.DegreesToRadians(cube.rotation.angle);
                }
                
                AppendFace(side, cube.from, cube.to, rot, ori, ref vertexes, ref normals, startIndex3 + 12 * faceIndex);

                faceIndex++;
            }
        }

        public static void AppendFace(TextureType side, float[] from, float[] to, Vector3 rotation, Vector3 rotationOrigin,
            ref float[] vertexes, ref float[] normals, int startIndex)
        {
            FaceSides faceSide = FaceSides.Parse(side); //TextureType parsed to FaceSides, also a normal of this face
            Vector3 normal = faceSide.ToVec();
            float[] unitFace = _cube[faceSide]; //one side of the cube in unit size

            float x = from[0] / 16f;
            float y = from[1] / 16f;
            float z = from[2] / 16f;

            float sx = to[0] / 16f - x; //the size of the cube part
            float sy = to[1] / 16f - y;
            float sz = to[2] / 16f - z;

            for (var i = 0; i < unitFace.Length; i += 3)
            {
                var vertex = RotateVertex(new Vector3(
                    x + unitFace[i] * sx,
                    y + unitFace[i + 1] * sy,
                    z + unitFace[i + 2] * sz), rotation, rotationOrigin);

                vertexes[startIndex + i] = vertex.X;
                vertexes[startIndex + i + 1] = vertex.Y;
                vertexes[startIndex + i + 2] = vertex.Z;

                var nrm = RotateVertex(normal, rotation, rotationOrigin);

                normals[startIndex + i] = nrm.X;
                normals[startIndex + i + 1] = nrm.Y;
                normals[startIndex + i + 2] = nrm.Z;
            }
        }

        public static float[] CreateCubeVertexes(bool centered = false)
        {
            List<float> vertexes = new List<float>();

            void AppendVertexes(TextureType side)
            {
                var face = _cube[FaceSides.Parse(side)];

                for (var index = 0; index < face.Length; index += 3)
                {
                    var x = face[index];
                    var y = face[index + 1];
                    var z = face[index + 2];

                    if (centered)
                    {
                        x -= 0.5f;
                        y -= 0.5f;
                        z -= 0.5f;
                    }

                    vertexes.Add(x);
                    vertexes.Add(y);
                    vertexes.Add(z);
                }
            }

            AppendVertexes(TextureType.up);
            AppendVertexes(TextureType.down);
            AppendVertexes(TextureType.north);
            AppendVertexes(TextureType.south);
            AppendVertexes(TextureType.east);
            AppendVertexes(TextureType.west);

            return vertexes.ToArray();
        }

        public static float[] CreateCubeNormals()
        {
            List<float> normals = new List<float>();

            void AppendVertexes(TextureType side)
            {
                var normal = FaceSides.Parse(side);

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(normal.x);
                    normals.Add(normal.y);
                    normals.Add(normal.z);
                }
            }

            AppendVertexes(TextureType.up);
            AppendVertexes(TextureType.down);
            AppendVertexes(TextureType.north);
            AppendVertexes(TextureType.south);
            AppendVertexes(TextureType.east);
            AppendVertexes(TextureType.west);

            return normals.ToArray();
        }

        public static float[] CreateCubeUvs()
        {
            List<float> uvs = new List<float>();

            float[] faceUv =
            {
                0, 0,
                0, 1,
                1, 1,
                1, 0
            };

            for (int i = 0; i < 6; i++)
            {
                uvs.AddRange(faceUv);
            }

            return uvs.ToArray();
        }

        private static Vector3 RotateVertex(Vector3 vertex, Vector3 rotation, Vector3 origin)
        {
            if (!(rotation.Length > 0))
                return vertex;

            vertex -= origin;
            vertex = Vector3.Transform(vertex, new Quaternion(rotation.Z, rotation.Y, rotation.X));
            vertex += origin;

            return vertex;
        }
    }
}