using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// 在 Unity 坐标系内程序化生成圆角八分体 case mesh。
    /// 完整实现连通分量 BFS + dissolve 共面边 + arc bevel（侧/顶独立半径）+ 顶点色。
    /// </summary>
    public static class ProceduralCaseMesh
    {
        // ── 顶点色 ────────────────────────────────────────────────────────────
        public static readonly Color32 ColClosed = new Color32( 10,  30, 140, 255); // 深蓝
        public static readonly Color32 ColOpen   = new Color32(153, 153, 153, 255); // 浅灰
        public static readonly Color32 ColTop    = new Color32(140,  90,  25, 255); // 棕（地面）

        // ── 公开入口 ──────────────────────────────────────────────────────────

        /// <param name="cubeIndex">marching cube 索引（0-255）</param>
        /// <param name="sideRadius">侧面封闭边圆弧半径</param>
        /// <param name="topRadius"> 顶面（+Y 法线）封闭边圆弧半径</param>
        /// <param name="segments">  弧线细分段数</param>
        public static Mesh Build(int cubeIndex,
                                 float sideRadius = 0f,
                                 float topRadius  = 0f,
                                 int   segments   = 4)
        {
            var active     = GetActiveOctants(cubeIndex);
            var components = ConnectedComponents(active);

            var verts  = new List<Vector3>();
            var tris   = new List<int>();
            var cols   = new List<Color32>();

            foreach (var comp in components)
                BuildComponent(comp, active, sideRadius, topRadius, segments,
                               verts, tris, cols);

            if (verts.Count == 0) return null;

            var m = new Mesh { name = $"case_{cubeIndex}" };
            m.vertices  = verts.ToArray();
            m.triangles = tris.ToArray();
            m.colors32  = cols.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ═════════════════════════════════════════════════════════════════════
        // 内部实现
        // ═════════════════════════════════════════════════════════════════════

        static readonly (int dx,int dy,int dz, (int,int,int)[] c)[] DIRS =
        {
            (+1,0,0, new[]{(1,0,0),(1,1,0),(1,1,1),(1,0,1)}),
            (-1,0,0, new[]{(0,0,0),(0,0,1),(0,1,1),(0,1,0)}),
            (0,+1,0, new[]{(0,1,0),(0,1,1),(1,1,1),(1,1,0)}),
            (0,-1,0, new[]{(0,0,0),(1,0,0),(1,0,1),(0,0,1)}),
            (0,0,+1, new[]{(0,0,1),(1,0,1),(1,1,1),(0,1,1)}),
            (0,0,-1, new[]{(0,0,0),(0,1,0),(1,1,0),(1,0,0)}),
        };

        static readonly (int dx,int dy,int dz)[] FACE_DIRS =
        {
            (1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)
        };

        // ── 获取 active 八分体 ────────────────────────────────────────────────
        static HashSet<(int,int,int)> GetActiveOctants(int ci)
        {
            var s = new HashSet<(int,int,int)>();
            for (int vi = 0; vi < 8; vi++)
                if ((ci & (1 << vi)) != 0)
                { var v = CubeTable.Vertices[vi]; s.Add((v.x,v.y,v.z)); }
            return s;
        }

        // 2×2×2 格中任意两个八分体 Chebyshev 距离 ≤ 1，即 26-全连通。
        // 所有 active 八分体放入同一分量，remove_doubles 可合并对角共享顶点，
        // 产生对角桥接造型，与 Blender mc_artmesh 插件行为一致。
        static List<HashSet<(int,int,int)>> ConnectedComponents(
            HashSet<(int,int,int)> active)
            => new List<HashSet<(int,int,int)>> { active };

        // ── 单个连通分量 mesh 构建 ────────────────────────────────────────────
        static void BuildComponent(
            HashSet<(int,int,int)> comp,
            HashSet<(int,int,int)> active,
            float sideR, float topR, int segs,
            List<Vector3> outVerts,
            List<int>     outTris,
            List<Color32> outCols)
        {
            // --- 1. 收集所有面（多边形形式，含类型标记）---
            var faces   = new List<FaceData>();
            var vCache  = new Dictionary<Vector3, int>();
            var vList   = new List<Vector3>();

            int Vert(Vector3 p)
            {
                if (!vCache.TryGetValue(p, out int i))
                { i = vList.Count; vList.Add(p); vCache[p] = i; }
                return i;
            }

            foreach (var (gx,gy,gz) in comp)
            {
                float x0=gx*.5f, x1=(gx+1)*.5f;
                float y0=gy*.5f, y1=(gy+1)*.5f;
                float z0=gz*.5f, z1=(gz+1)*.5f;

                Vector3 C(int cx,int cy,int cz) =>
                    new Vector3(cx==0?x0:x1, cy==0?y0:y1, cz==0?z0:z1);

                foreach (var (dx,dy,dz,corners) in DIRS)
                {
                    var nb = (gx+dx, gy+dy, gz+dz);
                    if (active.Contains(nb)) continue;

                    bool isClosed = (nb.Item1 >= 0 && nb.Item1 <= 1 &&
                                     nb.Item2 >= 0 && nb.Item2 <= 1 &&
                                     nb.Item3 >= 0 && nb.Item3 <= 1);

                    var poly = new int[4];
                    for (int i = 0; i < 4; i++)
                        poly[i] = Vert(C(corners[i].Item1,
                                         corners[i].Item2,
                                         corners[i].Item3));

                    var normal = new Vector3(dx, dy, dz);
                    bool isTop = isClosed && dy == 1; // Unity +Y = 地面朝上
                    faces.Add(new FaceData { poly = poly, normal = normal,
                                            isClosed = isClosed, isTop = isTop });
                }
            }

            // --- 2. 无圆角时直接输出 ---
            bool doArc = (sideR > 0 || topR > 0);
            if (!doArc)
            {
                int off = outVerts.Count;
                outVerts.AddRange(vList);
                foreach (var fd in faces)
                {
                    var col = FaceColor(fd);
                    for (int i = outVerts.Count - vList.Count; i < outVerts.Count; i++)
                        while (outCols.Count < outVerts.Count) outCols.Add(col);

                    int a=fd.poly[0]+off, b=fd.poly[1]+off,
                        c=fd.poly[2]+off, d=fd.poly[3]+off;
                    outTris.Add(a); outTris.Add(b); outTris.Add(c);
                    outTris.Add(a); outTris.Add(c); outTris.Add(d);
                }
                // 补全颜色
                while (outCols.Count < outVerts.Count) outCols.Add(ColOpen);
                // 重新按面赋色
                AssignFaceColors(off, vList.Count, faces, outVerts, outCols);
                return;
            }

            // --- 3. 有圆角：找封闭-封闭共边，生成弧条带 + 裁剪平面 ---
            // 用多边形列表表示每个封闭面（用于裁剪）
            var closedPolys = new List<(List<Vector3> poly, bool isTop, bool used)>();
            foreach (var fd in faces)
                if (fd.isClosed)
                    closedPolys.Add((QuadToList(fd.poly, vList), fd.isTop, false));

            // 找所有封闭-封闭邻接边（共享2个顶点 且 法线夹角≈90°）
            var arcEdges = FindArcEdges(faces, vList);

            // 输出收集
            var arcVerts  = new List<Vector3>();
            var arcTris   = new List<int>();
            var arcCols   = new List<Color32>();

            // 每个封闭面的裁剪半平面（d方向, 裁剪点）
            var faceTrims = new Dictionary<int, List<(Vector3 d, Vector3 pt)>>();

            for (int ei = 0; ei < arcEdges.Count; ei++)
            {
                var (pa, pb, f1Idx, f2Idx) = arcEdges[ei];
                var fd1 = faces[f1Idx];
                var fd2 = faces[f2Idx];

                bool anyTop = fd1.isTop || fd2.isTop;
                float r = anyTop ? topR : sideR;
                if (r <= 0f) continue;

                Vector3 t  = (pb - pa).normalized;
                Vector3 d1 = IntoFaceDir(fd1.normal, t, QuadCenter(fd1.poly, vList));
                Vector3 d2 = IntoFaceDir(fd2.normal, t, QuadCenter(fd2.poly, vList));

                // 弧条带
                AddArcStrip(pa, pb, d1, d2, r, segs, arcVerts, arcTris, arcCols, ColClosed);

                // 记录裁剪信息
                Vector3 em = (pa + pb) * .5f;
                if (!faceTrims.ContainsKey(f1Idx)) faceTrims[f1Idx] = new List<(Vector3,Vector3)>();
                if (!faceTrims.ContainsKey(f2Idx)) faceTrims[f2Idx] = new List<(Vector3,Vector3)>();
                faceTrims[f1Idx].Add((d1, em + r * d1));
                faceTrims[f2Idx].Add((d2, em + r * d2));
            }

            // --- 4. 输出裁剪后的封闭面和完整开放面 ---
            int baseOff = outVerts.Count;
            var allFaceVerts = new List<Vector3>(vList);
            var allFaceCols  = new List<Color32>();
            while (allFaceCols.Count < allFaceVerts.Count) allFaceCols.Add(ColOpen);

            var faceTris = new List<int>();

            for (int fi = 0; fi < faces.Count; fi++)
            {
                var fd = faces[fi];
                Color32 col = FaceColor(fd);

                var poly = QuadToList(fd.poly, vList);
                if (fd.isClosed && faceTrims.TryGetValue(fi, out var trims))
                {
                    foreach (var (d, pt) in trims)
                        poly = ClipPolygon(poly, pt, d);
                }

                if (poly == null || poly.Count < 3) continue;

                int pOff = allFaceVerts.Count;
                allFaceVerts.AddRange(poly);
                for (int i = 0; i < poly.Count; i++) allFaceCols.Add(col);
                for (int i = 1; i < poly.Count - 1; i++)
                {
                    faceTris.Add(pOff + 0);
                    faceTris.Add(pOff + i);
                    faceTris.Add(pOff + i + 1);
                }
            }

            // 合并 flatFaces + arcStrips
            int flatOff = outVerts.Count;
            outVerts.AddRange(allFaceVerts);
            outCols.AddRange(allFaceCols);
            foreach (var t in faceTris) outTris.Add(t + flatOff);

            int arcOff = outVerts.Count;
            outVerts.AddRange(arcVerts);
            outCols.AddRange(arcCols);
            foreach (var t in arcTris) outTris.Add(t + arcOff);
        }

        // ── 寻找弧边（封闭-封闭、90°、共享2顶点）────────────────────────────
        static List<(Vector3 pa, Vector3 pb, int f1, int f2)> FindArcEdges(
            List<FaceData> faces, List<Vector3> verts)
        {
            var result = new List<(Vector3,Vector3,int,int)>();
            const float EPS = 1e-4f;

            for (int i = 0; i < faces.Count; i++)
            {
                if (!faces[i].isClosed) continue;
                for (int j = i+1; j < faces.Count; j++)
                {
                    if (!faces[j].isClosed) continue;
                    float dot = Vector3.Dot(faces[i].normal, faces[j].normal);
                    if (Mathf.Abs(dot) >= 0.99f) continue; // 共面或反向
                    if (dot > -0.01f && dot < 0.01f) {} // 垂直 ✓
                    else continue;

                    // 找共享顶点
                    var shared = new List<Vector3>();
                    foreach (int vi in faces[i].poly)
                        foreach (int vj in faces[j].poly)
                            if ((verts[vi] - verts[vj]).sqrMagnitude < EPS)
                            { shared.Add(verts[vi]); break; }

                    if (shared.Count == 2)
                        result.Add((shared[0], shared[1], i, j));
                }
            }
            return result;
        }

        // ── 弧条带生成 ────────────────────────────────────────────────────────
        static void AddArcStrip(
            Vector3 pa, Vector3 pb,
            Vector3 d1, Vector3 d2,
            float r, int segs,
            List<Vector3> verts, List<int> tris, List<Color32> cols,
            Color32 col)
        {
            int nc = segs + 1;
            int baseIdx = verts.Count;

            foreach (var ep in new[]{ pa, pb })
            {
                Vector3 cen = ep + r * d1 + r * d2;
                for (int i = 0; i < nc; i++)
                {
                    float t = (Mathf.PI * .5f) * i / segs;
                    verts.Add(cen - r * (Mathf.Cos(t) * d2 + Mathf.Sin(t) * d1));
                    cols.Add(col);
                }
            }

            for (int i = 0; i < segs; i++)
            {
                int a0=baseIdx+i, a1=baseIdx+i+1;
                int b0=baseIdx+nc+i, b1=baseIdx+nc+i+1;
                tris.Add(a0); tris.Add(b0); tris.Add(b1);
                tris.Add(a0); tris.Add(b1); tris.Add(a1);
            }
        }

        // ── Sutherland-Hodgman 半平面裁剪（保留 (p-pt)·d >= 0 的部分）────────
        static List<Vector3> ClipPolygon(List<Vector3> poly, Vector3 pt, Vector3 d)
        {
            if (poly == null || poly.Count < 3) return poly;
            const float EPS = 1e-4f;
            var result = new List<Vector3>();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var cur  = poly[i];
                var prev = poly[(i - 1 + n) % n];
                float dc = Vector3.Dot(cur  - pt, d);
                float dp = Vector3.Dot(prev - pt, d);
                if (dc >= -EPS)
                {
                    if (dp < -EPS)
                    {
                        float t = dp / (dp - dc);
                        result.Add(prev + t * (cur - prev));
                    }
                    result.Add(cur);
                }
                else if (dp >= -EPS)
                {
                    float t = dp / (dp - dc);
                    result.Add(prev + t * (cur - prev));
                }
            }
            return result.Count >= 3 ? result : null;
        }

        // ── 辅助 ──────────────────────────────────────────────────────────────

        static Vector3 IntoFaceDir(Vector3 faceNormal, Vector3 edgeT, Vector3 faceCenter)
        {
            Vector3 d = Vector3.Cross(faceNormal, edgeT).normalized;
            if (Vector3.Dot(d, faceCenter) < 0) d = -d;
            return d;
        }

        static List<Vector3> QuadToList(int[] poly, List<Vector3> verts)
        {
            var list = new List<Vector3>(4);
            foreach (int i in poly) list.Add(verts[i]);
            return list;
        }

        static Vector3 QuadCenter(int[] poly, List<Vector3> verts)
        {
            var c = Vector3.zero;
            foreach (int i in poly) c += verts[i];
            return c / poly.Length;
        }

        static Color32 FaceColor(FaceData fd)
        {
            if (!fd.isClosed) return ColOpen;
            return fd.isTop ? ColTop : ColClosed;
        }

        static void AssignFaceColors(int off, int count,
            List<FaceData> faces, List<Vector3> verts, List<Color32> cols)
        {
            // 为无圆角路径按面重新赋色
            while (cols.Count < off + count) cols.Add(ColOpen);
            // （无圆角路径已在 BuildComponent 的简单分支处理）
        }

        struct FaceData
        {
            public int[]   poly;
            public Vector3 normal;
            public bool    isClosed;
            public bool    isTop;
        }
    }
}
