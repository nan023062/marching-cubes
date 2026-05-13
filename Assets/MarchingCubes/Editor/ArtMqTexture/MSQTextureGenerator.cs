#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MarchingSquareTerrain.Editor
{
    public static class MSQTextureGenerator
    {
        const int TileSize = 64;
        const int GridSize = 4;
        const int AtlasSize = TileSize * GridSize;
        const int BaseTexSize = 64;
        const string ResourceDir = "Assets/MarchingSquares/Sample/Resources/";
        const string ShaderName = "MarchingSquares/SplatmapTerrain";
        const string CliffShaderName = "MarchingSquares/CliffWall";

        struct Palette
        {
            public string name;
            public Color fillA, fillB;
            public float edgeNoise;
        }

        static readonly Palette[] Palettes =
        {
            new Palette { name = "dirt",      fillA = new Color(0.55f,0.42f,0.16f), fillB = new Color(0.63f,0.50f,0.22f), edgeNoise = 0.15f },
            new Palette { name = "grass",     fillA = new Color(0.13f,0.55f,0.13f), fillB = new Color(0.22f,0.68f,0.20f), edgeNoise = 0.18f },
            new Palette { name = "rock",      fillA = new Color(0.40f,0.40f,0.40f), fillB = new Color(0.55f,0.55f,0.52f), edgeNoise = 0.12f },
            new Palette { name = "snow",      fillA = new Color(0.85f,0.88f,0.95f), fillB = new Color(0.95f,0.96f,1.00f), edgeNoise = 0.15f },
            new Palette { name = "corrupted", fillA = new Color(0.30f,0.12f,0.35f), fillB = new Color(0.45f,0.18f,0.50f), edgeNoise = 0.22f },
        };

        [MenuItem("Assets/Create/MarchingSquares/Gen Splatmap Textures")]
        public static void GenerateSplatmapTextures()
        {
            var baseArray = new Texture2DArray(BaseTexSize, BaseTexSize, Palettes.Length, TextureFormat.RGBA32, false);
            baseArray.filterMode = FilterMode.Point;
            baseArray.wrapMode = TextureWrapMode.Repeat;

            var overlayArray = new Texture2DArray(AtlasSize, AtlasSize, Palettes.Length, TextureFormat.RGBA32, false);
            overlayArray.filterMode = FilterMode.Point;
            overlayArray.wrapMode = TextureWrapMode.Clamp;

            for (int i = 0; i < Palettes.Length; i++)
            {
                ref readonly var p = ref Palettes[i];

                var baseTex = CreateSeamlessBase(p, i);
                baseArray.SetPixels(baseTex.GetPixels(), i);
                Object.DestroyImmediate(baseTex);

                var overlayTex = CreateMSAtlas(p, i);
                overlayArray.SetPixels(overlayTex.GetPixels(), i);
                Object.DestroyImmediate(overlayTex);

                Debug.Log($"Generated: {p.name}");
            }

            baseArray.Apply();
            overlayArray.Apply();

            string baseArrayPath = ResourceDir + "terrain_base.asset";
            string overlayArrayPath = ResourceDir + "terrain_overlay.asset";

            SaveOrReplace(baseArray, baseArrayPath);
            SaveOrReplace(overlayArray, overlayArrayPath);

            AssetDatabase.Refresh();

            string matPath = ResourceDir + "SplatmapTerrain.mat";
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader '{ShaderName}' not found.");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetTexture("_BaseArray", AssetDatabase.LoadAssetAtPath<Texture2DArray>(baseArrayPath));
            mat.SetTexture("_OverlayArray", AssetDatabase.LoadAssetAtPath<Texture2DArray>(overlayArrayPath));
            mat.SetFloat("_Tiling", 1f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            Debug.Log($"Material: {matPath}");

            var cliffTex = CreateCliffTexture();
            string cliffTexPath = ResourceDir + "cliff_wall.asset";
            SaveOrReplace(cliffTex, cliffTexPath);

            AssetDatabase.Refresh();

            string cliffMatPath = ResourceDir + "CliffWall.mat";
            var cliffShader = Shader.Find(CliffShaderName);
            if (cliffShader == null)
            {
                Debug.LogError($"Shader '{CliffShaderName}' not found.");
                return;
            }

            var cliffMat = AssetDatabase.LoadAssetAtPath<Material>(cliffMatPath);
            if (cliffMat == null)
            {
                cliffMat = new Material(cliffShader);
                AssetDatabase.CreateAsset(cliffMat, cliffMatPath);
            }
            else
            {
                cliffMat.shader = cliffShader;
            }

            cliffMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(cliffTexPath));
            EditorUtility.SetDirty(cliffMat);
            AssetDatabase.SaveAssets();

            Debug.Log($"Cliff Material: {cliffMatPath}");

            var cliff1 = CreateCliffMesh(1);
            SaveOrReplace(cliff1, ResourceDir + "cliff_level1.asset");
            var cliff2 = CreateCliffMesh(2);
            SaveOrReplace(cliff2, ResourceDir + "cliff_level2.asset");
            AssetDatabase.SaveAssets();

            Debug.Log("Done! Assign SplatmapTerrain.mat + CliffWall.mat.");
        }

        static void SaveOrReplace<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                Object.DestroyImmediate(asset);
            }
            else
            {
                AssetDatabase.CreateAsset(asset, path);
            }
        }

        static Mesh CreateCliffMesh(int levels)
        {
            const int segX = 8;
            int segY = 8 * levels;
            float depth = 0.25f;
            float seed = levels * 13.7f;

            int vertCount = (segX + 1) * (segY + 1);
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segX * segY * 6];

            for (int j = 0; j <= segY; j++)
            {
                for (int i = 0; i <= segX; i++)
                {
                    int idx = j * (segX + 1) + i;
                    float u = (float)i / segX;
                    float v = (float)j / segY;

                    float edgeFalloff = Mathf.Sin(u * Mathf.PI);
                    float vertFalloff = Mathf.Sin(v * Mathf.PI);
                    float falloff = edgeFalloff * vertFalloff;

                    float n1 = Mathf.PerlinNoise(u * 4f + seed, v * 4f * levels + seed);
                    float n2 = Mathf.PerlinNoise(u * 8f + seed + 50f, v * 8f * levels + seed + 50f);
                    float d = (n1 * 0.7f + n2 * 0.3f - 0.3f) * depth * falloff;

                    verts[idx] = new Vector3(u, v * levels, d);
                    uvs[idx] = new Vector2(u, v * levels);
                }
            }

            int tri = 0;
            for (int j = 0; j < segY; j++)
            {
                for (int i = 0; i < segX; i++)
                {
                    int v00 = j * (segX + 1) + i;
                    int v10 = v00 + 1;
                    int v01 = v00 + (segX + 1);
                    int v11 = v01 + 1;
                    tris[tri++] = v00; tris[tri++] = v01; tris[tri++] = v10;
                    tris[tri++] = v10; tris[tri++] = v01; tris[tri++] = v11;
                }
            }

            var mesh = new Mesh
            {
                vertices = verts,
                uv = uvs,
                triangles = tris
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Texture2D CreateCliffTexture()
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            float seed = 77.7f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float n = TileableNoise(u, v, 3f, seed);
                    float detail = TileableNoise(u, v, 6f, seed + 40f);
                    float val = n * 0.7f + detail * 0.3f;
                    float r = Mathf.Lerp(0.32f, 0.55f, val);
                    tex.SetPixel(x, y, new Color(r, r * 0.95f, r * 0.88f));
                }
            }
            tex.Apply();
            return tex;
        }

        static float TileableNoise(float u, float v, float freq, float seed)
        {
            float twoPi = Mathf.PI * 2f;
            float cx = Mathf.Cos(u * twoPi) * freq;
            float sx = Mathf.Sin(u * twoPi) * freq;
            float cy = Mathf.Cos(v * twoPi) * freq;
            float sy = Mathf.Sin(v * twoPi) * freq;
            return (Mathf.PerlinNoise(cx + seed, cy + seed) +
                    Mathf.PerlinNoise(sx + seed + 31.7f, sy + seed + 31.7f)) * 0.5f;
        }

        static Texture2D CreateSeamlessBase(in Palette p, int index)
        {
            var tex = new Texture2D(BaseTexSize, BaseTexSize, TextureFormat.RGBA32, false);
            float seed = index * 37.7f;

            for (int y = 0; y < BaseTexSize; y++)
            {
                for (int x = 0; x < BaseTexSize; x++)
                {
                    float u = (float)x / BaseTexSize;
                    float v = (float)y / BaseTexSize;

                    float val = TileableNoise(u, v, 2f, seed);
                    float detail = TileableNoise(u, v, 4f, seed + 50f);
                    val = val * 0.6f + detail * 0.4f;

                    Color c = Color.Lerp(p.fillA, p.fillB, val);
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        static Texture2D CreateMSAtlas(in Palette p, int index)
        {
            var tex = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
            var transparent = new Color(0, 0, 0, 0);
            float baseSeed = index * 17.31f;

            for (int idx = 0; idx < 16; idx++)
            {
                int ox = (idx % GridSize) * TileSize;
                int oy = (idx / GridSize) * TileSize;

                float cBL = (idx & 8) != 0 ? 1f : 0f;
                float cTL = (idx & 4) != 0 ? 1f : 0f;
                float cTR = (idx & 2) != 0 ? 1f : 0f;
                float cBR = (idx & 1) != 0 ? 1f : 0f;

                float seed = baseSeed + idx * 17.31f;

                for (int y = 0; y < TileSize; y++)
                {
                    for (int x = 0; x < TileSize; x++)
                    {
                        float u = (float)x / (TileSize - 1);
                        float v = (float)y / (TileSize - 1);

                        float w = Mathf.Lerp(
                            Mathf.Lerp(cBL, cBR, u),
                            Mathf.Lerp(cTL, cTR, u), v);

                        float edge = (TileableNoise(u, v, 1.5f, seed + 100f) - 0.5f) * p.edgeNoise;
                        bool isFill = w + edge > 0.5f;

                        if (isFill)
                        {
                            float detail = TileableNoise(u, v, 1f, seed);
                            Color c = Color.Lerp(p.fillA, p.fillB, detail);
                            c.a = 1f;
                            tex.SetPixel(ox + x, oy + y, c);
                        }
                        else
                        {
                            tex.SetPixel(ox + x, oy + y, transparent);
                        }
                    }
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
#endif
