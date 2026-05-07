"""
MC ArtMesh — Blender Add-on  v1.5
Marching Cubes art mesh creation workflow.

安装方法：
  将 mc_artmesh/ 文件夹打成 mc_artmesh.zip，
  Blender > Preferences > Add-ons > Install > 选择 zip > 启用

面板位置：
  3D Viewport > N-Panel > MC ArtMesh

生成结构：
  MC_ArtMesh_Ref/
    MC_Ctrl       — 线框 + 控制点球 + 接缝锚点 + 标签
    MC_IsoSurface — 等值面 ghost mesh（蓝色）
  MC_ArtMesh_Cubes/ — 顶点八分体填充 case mesh（每个 canonical case 一个）
"""

bl_info = {
    "name":        "MC ArtMesh",
    "author":      "MarchingCubes Project",
    "version":     (1, 5),
    "blender":     (3, 6, 0),
    "location":    "3D Viewport › N-Panel › MC ArtMesh",
    "description": "Marching Cubes art mesh — D4 canonical cases, octant-fill mesh, FBX export",
    "category":    "Mesh",
}

import bpy
import bmesh
import math
import os
from mathutils import Vector
from .cube_table import TRI_TABLE, get_iso_triangles

# ─────────────────────────────────────────────────────────────────────────────
# 顶点 / 棱约定（必须与 Unity CubeTable.cs 完全一致）
# ─────────────────────────────────────────────────────────────────────────────

UNITY_VERTS = [
    (0, 0, 1),  # V0
    (1, 0, 1),  # V1
    (1, 0, 0),  # V2
    (0, 0, 0),  # V3
    (0, 1, 1),  # V4
    (1, 1, 1),  # V5
    (1, 1, 0),  # V6
    (0, 1, 0),  # V7
]

EDGES = [
    (0, 1), (1, 2), (2, 3), (3, 0),
    (4, 5), (5, 6), (6, 7), (7, 4),
    (0, 4), (1, 5), (2, 6), (3, 7),
]


def u2b(ux, uy, uz):
    return (ux, uz, uy)


def b2u(bx, by, bz):
    return (bx, bz, by)


BL_VERTS = [u2b(*v) for v in UNITY_VERTS]

BL_EDGE_MIDS = tuple(
    ((BL_VERTS[a][0] + BL_VERTS[b][0]) / 2,
     (BL_VERTS[a][1] + BL_VERTS[b][1]) / 2,
     (BL_VERTS[a][2] + BL_VERTS[b][2]) / 2)
    for a, b in EDGES
)

# Collection 名称常量
REF_COL_NAME   = "MC_ArtMesh_Ref"
CTRL_COL_NAME  = "MC_Ctrl"
ISO_COL_NAME   = "MC_IsoSurface"
CUBES_COL_NAME = "MC_ArtMesh_Cubes"

# ─────────────────────────────────────────────────────────────────────────────
# D4 Canonical Case 计算
# ─────────────────────────────────────────────────────────────────────────────

_MIRROR_PERM    = [1, 0, 3, 2, 5, 4, 7, 6]
_D4_TRANSFORMS  = [
    (0, False), (90, False), (180, False), (270, False),
    (0, True),  (90, True),  (180, True),  (270, True),
]
_CANON_MAP_CACHE  = None
_CANONICALS_CACHE = None
_D4_PERMS_CACHE   = None


def _qrot_y(v, deg):
    r = math.radians(deg)
    c, s = math.cos(r), math.sin(r)
    x, y, z = v[0] - 0.5, v[1] - 0.5, v[2] - 0.5
    return (round(c*x + s*z + 0.5, 6), round(y + 0.5, 6), round(-s*x + c*z + 0.5, 6))


def _find_nearest_vert(v):
    best, best_d = 0, float('inf')
    for j, u in enumerate(UNITY_VERTS):
        d = (v[0]-u[0])**2 + (v[1]-u[1])**2 + (v[2]-u[2])**2
        if d < best_d:
            best_d, best = d, j
    return best


def _build_d4_perms():
    global _D4_PERMS_CACHE
    if _D4_PERMS_CACHE is not None:
        return _D4_PERMS_CACHE
    perms = []
    for deg, flip in _D4_TRANSFORMS:
        perm = []
        for i in range(8):
            src = _MIRROR_PERM[i] if flip else i
            rv = _qrot_y(UNITY_VERTS[src], deg)
            perm.append(_find_nearest_vert(rv))
        perms.append(tuple(perm))
    _D4_PERMS_CACHE = perms
    return perms


def _apply_perm(ci, perm):
    r = 0
    for v in range(8):
        if ci & (1 << v):
            r |= 1 << perm[v]
    return r


def get_canon_map():
    global _CANON_MAP_CACHE
    if _CANON_MAP_CACHE is not None:
        return _CANON_MAP_CACHE
    perms = _build_d4_perms()
    canon = list(range(256))
    for ci in range(256):
        for perm in perms:
            e = _apply_perm(ci, perm)
            if e < canon[ci]:
                canon[ci] = e
    _CANON_MAP_CACHE = dict(enumerate(canon))
    return _CANON_MAP_CACHE


def get_d4_canonicals():
    global _CANONICALS_CACHE
    if _CANONICALS_CACHE is not None:
        return _CANONICALS_CACHE
    canon_map = get_canon_map()
    _CANONICALS_CACHE = [i for i in range(1, 255) if canon_map[i] == i]
    return _CANONICALS_CACHE


# ─────────────────────────────────────────────────────────────────────────────
# Grid 构建
# ─────────────────────────────────────────────────────────────────────────────

GRID_COLS = 9


def _compute_cube_index(grid, cx, cy, cz):
    ci = 0
    for n, (dx, dy, dz) in enumerate(UNITY_VERTS):
        if grid.get((cx+dx, cy+dy, cz+dz), 0):
            ci |= 1 << n
    return ci


def build_grid():
    canonicals = get_d4_canonicals()
    raw_grid = {}
    for n, ci in enumerate(canonicals):
        col, row = n % GRID_COLS, n // GRID_COLS
        cx, cy, cz = col * 2, 0, row * 2
        for v_idx, (dx, dy, dz) in enumerate(UNITY_VERTS):
            raw_grid[(cx+dx, cy+dy, cz+dz)] = 1 if (ci >> v_idx) & 1 else 0

    all_x = [k[0] for k in raw_grid]
    all_y = [k[1] for k in raw_grid]
    all_z = [k[2] for k in raw_grid]
    max_x, max_y, max_z = max(all_x), max(all_y), max(all_z)

    grid = {(x, y, z): raw_grid.get((x, y, z), 0)
            for x in range(max_x+1)
            for y in range(max_y+1)
            for z in range(max_z+1)}
    return grid, (max_x, max_y, max_z)


def verify_grid(grid):
    canonicals_set = set(get_d4_canonicals())
    canon_map = get_canon_map()
    all_x = [k[0] for k in grid]
    all_z = [k[2] for k in grid]
    covered = set()
    for cx in range(max(all_x)):
        for cz in range(max(all_z)):
            ci = _compute_cube_index(grid, cx, 0, cz)
            if 0 < ci < 255:
                covered.add(canon_map[ci])
    missing = canonicals_set - covered
    assert not missing, f"grid 缺少 {len(missing)} 个 canonical case: {sorted(missing)}"
    return True


# ─────────────────────────────────────────────────────────────────────────────
# 接缝锚点
# ─────────────────────────────────────────────────────────────────────────────

def _seam_anchors_world(ci, ox, oy, oz):
    pts = []
    b_ox, b_oy, b_oz = u2b(ox, oy, oz)
    for ea, eb in EDGES:
        if bool(ci & (1 << ea)) == bool(ci & (1 << eb)):
            continue
        bx1, by1, bz1 = BL_VERTS[ea]
        bx2, by2, bz2 = BL_VERTS[eb]
        pts.append(Vector((b_ox + (bx1+bx2)/2, b_oy + (by1+by2)/2, b_oz + (bz1+bz2)/2)))
    return pts


# ─────────────────────────────────────────────────────────────────────────────
# 材质
# ─────────────────────────────────────────────────────────────────────────────

def _ensure_mat(name, rgb, strength=1.5, alpha=1.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em  = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value    = (*rgb, 1.0)
    em.inputs['Strength'].default_value = strength
    nt.links.new(em.outputs['Emission'], out.inputs['Surface'])
    mat.diffuse_color = (*rgb, alpha)
    if alpha < 1.0:
        mat.blend_method = 'BLEND'
    return mat


_MAT_NAMES = ("mc_active", "mc_inactive", "mc_seam", "mc_wire", "mc_iso",
              "mc_cube_closed", "mc_cube_open", "mc_cube_top")


def _reset_mats():
    for name in _MAT_NAMES:
        if name in bpy.data.materials:
            bpy.data.materials.remove(bpy.data.materials[name])


def _ensure_mat_top(name):
    """顶面地面材质：Emission + Checker 过程纹理（棋盘格，暗土地色）。"""
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out     = nt.nodes.new('ShaderNodeOutputMaterial')
    em      = nt.nodes.new('ShaderNodeEmission')
    checker = nt.nodes.new('ShaderNodeTexChecker')
    mapping = nt.nodes.new('ShaderNodeMapping')
    coord   = nt.nodes.new('ShaderNodeTexCoord')

    # Object 坐标 → Mapping（缩放控制格子大小）→ Checker
    coord.location   = (-600, 0)
    mapping.location = (-400, 0)
    checker.location = (-160, 0)
    em.location      = ( 60,  0)
    out.location     = ( 260, 0)

    mapping.inputs['Scale'].default_value = (6, 6, 6)   # 每世界单位 6 格
    checker.inputs['Color1'].default_value = (0.60, 0.42, 0.18, 1.0)  # 土黄
    checker.inputs['Color2'].default_value = (0.32, 0.18, 0.05, 1.0)  # 深棕
    checker.inputs['Scale'].default_value  = 1.0
    em.inputs['Strength'].default_value    = 0.85

    nt.links.new(coord.outputs['Object'],   mapping.inputs['Vector'])
    nt.links.new(mapping.outputs['Vector'], checker.inputs['Vector'])
    nt.links.new(checker.outputs['Color'],  em.inputs['Color'])
    nt.links.new(em.outputs['Emission'],    out.inputs['Surface'])

    mat.diffuse_color = (0.55, 0.35, 0.10, 1.0)
    return mat


# ─────────────────────────────────────────────────────────────────────────────
# 几何辅助
# ─────────────────────────────────────────────────────────────────────────────

def _make_sphere(name, cx, cy, cz, r, subdivisions=2):
    m = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=r)
    for v in bm.verts:
        v.co.x += cx; v.co.y += cy; v.co.z += cz
    bm.to_mesh(m); bm.free(); m.update()
    return m


def _make_cube_wire(name, b_ox, b_oy, b_oz):
    verts = [(b_ox+bx, b_oy+by, b_oz+bz) for bx, by, bz in BL_VERTS]
    faces = [(3,2,1,0),(4,5,6,7),(0,1,5,4),(2,3,7,6),(1,2,6,5),(3,0,4,7)]
    m = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bvs = [bm.verts.new(p) for p in verts]
    for f in faces:
        try: bm.faces.new([bvs[i] for i in f])
        except Exception: pass
    bm.to_mesh(m); bm.free(); m.update()
    return m


def _add_locked(col, name, mesh, mat):
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    if obj.name in bpy.context.scene.collection.objects:
        bpy.context.scene.collection.objects.unlink(obj)
    obj.hide_select   = True
    obj.lock_location = (True, True, True)
    obj.lock_rotation = (True, True, True)
    obj.lock_scale    = (True, True, True)
    return obj


def _link_col(parent, child):
    """将 child collection 挂到 parent collection 下（不挂到 scene 根）。"""
    parent.children.link(child)
    if child.name in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.unlink(child)


def _ensure_col(name, parent=None):
    """获取或创建 collection，挂到 parent（或 scene 根）下。"""
    if name in bpy.data.collections:
        col = bpy.data.collections[name]
    else:
        col = bpy.data.collections.new(name)
    if parent is not None:
        if name not in [c.name for c in parent.children]:
            _link_col(parent, col)
    else:
        if name not in [c.name for c in bpy.context.scene.collection.children]:
            bpy.context.scene.collection.children.link(col)
    return col


def _remove_col(name):
    if name in bpy.data.collections:
        bpy.data.collections.remove(bpy.data.collections[name], do_unlink=True)


# ─────────────────────────────────────────────────────────────────────────────
# 顶点八分体填充（MC_ArtMesh_Cubes 方案）
# ─────────────────────────────────────────────────────────────────────────────

# 6 面定义：(邻居格偏移, 四角 key 列表（Blender 右手坐标系 CCW 外向法线）)
_OCTANT_FACE_DEFS = [
    ((+1, 0, 0), [(1,0,0),(1,1,0),(1,1,1),(1,0,1)]),  # +X
    ((-1, 0, 0), [(0,0,0),(0,0,1),(0,1,1),(0,1,0)]),  # -X
    ((0,+1, 0), [(0,1,0),(0,1,1),(1,1,1),(1,1,0)]),   # +Y
    ((0,-1, 0), [(0,0,0),(1,0,0),(1,0,1),(0,0,1)]),   # -Y
    ((0, 0,+1), [(0,0,1),(1,0,1),(1,1,1),(0,1,1)]),   # +Z
    ((0, 0,-1), [(0,0,0),(0,1,0),(1,1,0),(1,0,0)]),   # -Z
]


_FACE_DIRS = [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]
_EPS = 1e-4


def _is_midplane(f):
    """所有顶点在某轴共享坐标 0.5 → 中平面封闭面"""
    xs = [v.co.x for v in f.verts]
    ys = [v.co.y for v in f.verts]
    zs = [v.co.z for v in f.verts]
    return (all(abs(x - 0.5) < _EPS for x in xs) or
            all(abs(y - 0.5) < _EPS for y in ys) or
            all(abs(z - 0.5) < _EPS for z in zs))


def _make_case_mesh_vf(ci, radius=0.0, segments=4, radius_top=0.0):
    """
    顶点八分体填充法（连通分量独立处理）：
      1. 六连通 BFS 将 active 八分体分成 K 个连通分量
      2. 每个分量独立 bmesh：
           - 生成八分体面（closed_layer 标记中平面封闭面）
           - remove_doubles（仅分量内，无跨分量顶点污染）
           - dissolve 共面 closed-closed 拼缝边 → 合并同平面面片
           - bevel 剩余 closed-closed 边（dissolve 后均为 90°，凸/凹均可）
           - triangulate
      3. 合并所有分量输出
    返回 (verts, faces)，坐标在 Blender [0,1]³ 内。
    """
    if not ci:
        return [], []

    active = set()
    for v in range(8):
        if ci & (1 << v):
            bx, by, bz = BL_VERTS[v]
            active.add((int(bx), int(by), int(bz)))

    if not active:
        return [], []

    # 六连通 BFS → 连通分量列表
    visited, components = set(), []
    for start in active:
        if start in visited:
            continue
        comp, stack = set(), [start]
        while stack:
            node = stack.pop()
            if node in visited:
                continue
            visited.add(node)
            comp.add(node)
            for dx, dy, dz in _FACE_DIRS:
                nb = (node[0]+dx, node[1]+dy, node[2]+dz)
                if nb in active and nb not in visited:
                    stack.append(nb)
        components.append(comp)

    all_verts, all_faces = [], []

    for comp in components:
        bm = bmesh.new()
        cl = bm.faces.layers.int.new('closed')

        for (gx, gy, gz) in comp:
            x0, x1 = gx * 0.5, (gx + 1) * 0.5
            y0, y1 = gy * 0.5, (gy + 1) * 0.5
            z0, z1 = gz * 0.5, (gz + 1) * 0.5

            corners = {
                (0,0,0): (x0,y0,z0), (1,0,0): (x1,y0,z0),
                (0,1,0): (x0,y1,z0), (1,1,0): (x1,y1,z0),
                (0,0,1): (x0,y0,z1), (1,0,1): (x1,y0,z1),
                (0,1,1): (x0,y1,z1), (1,1,1): (x1,y1,z1),
            }

            for (ndx, ndy, ndz), keys in _OCTANT_FACE_DEFS:
                nb = (gx+ndx, gy+ndy, gz+ndz)
                if nb in active:
                    continue
                nx, ny, nz = nb
                is_closed = 0 <= nx <= 1 and 0 <= ny <= 1 and 0 <= nz <= 1
                f = bm.faces.new([bm.verts.new(Vector(corners[k])) for k in keys])
                f[cl] = 1 if is_closed else 0

        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)

        if radius > 0 or radius_top > 0:
            # Step 1: dissolve 同平面 closed-closed 拼缝边
            bm.normal_update()
            bm.edges.ensure_lookup_table()
            cl = bm.faces.layers.int['closed']
            dissolve = [
                e for e in bm.edges
                if len(e.link_faces) == 2
                and e.link_faces[0][cl] and e.link_faces[1][cl]
                and e.link_faces[0].normal.dot(e.link_faces[1].normal) > 0.99
            ]
            if dissolve:
                bmesh.ops.dissolve_edges(bm, edges=dissolve, use_verts=True)

            # Step 2: 收集所有合法弧边（dissolve 后用位置判断，排除 180° 边）
            bm.normal_update()
            bm.edges.ensure_lookup_table()
            bm.faces.ensure_lookup_table()

            def _is_top_face(f):
                """向上的封闭面（地面朝向）"""
                return _is_midplane(f) and f.normal.z > 0.9

            # 顶面相邻边 vs 其余封闭-封闭边
            top_edges   = []
            other_edges = []
            for e in bm.edges:
                if len(e.link_faces) != 2:
                    continue
                f0, f1 = e.link_faces[0], e.link_faces[1]
                if not (_is_midplane(f0) and _is_midplane(f1)):
                    continue
                if abs(f0.normal.dot(f1.normal)) >= 0.99:
                    continue
                if _is_top_face(f0) or _is_top_face(f1):
                    top_edges.append(e)
                else:
                    other_edges.append(e)

            # 顶面边用 radius_top（0 = 保持直角）
            if top_edges and radius_top > 0:
                bmesh.ops.bevel(
                    bm, geom=top_edges,
                    offset=radius_top, offset_type='OFFSET',
                    segments=segments, profile=0.5,
                    affect='EDGES', clamp_overlap=True,
                )
            # 其余封闭边用 radius（重新刷新，避免第一次 bevel 影响引用）
            if other_edges and radius > 0:
                bm.normal_update()
                bm.edges.ensure_lookup_table()
                bm.faces.ensure_lookup_table()
                # 重新收集（第一次 bevel 后顶面边已被消化，仅剩非顶面边）
                other_fresh = [
                    e for e in bm.edges
                    if len(e.link_faces) == 2
                    and _is_midplane(e.link_faces[0])
                    and _is_midplane(e.link_faces[1])
                    and abs(e.link_faces[0].normal.dot(e.link_faces[1].normal)) < 0.99
                    and not (_is_top_face(e.link_faces[0]) or _is_top_face(e.link_faces[1]))
                ]
                if other_fresh:
                    bmesh.ops.bevel(
                        bm, geom=other_fresh,
                        offset=radius, offset_type='OFFSET',
                        segments=segments, profile=0.5,
                        affect='EDGES', clamp_overlap=True,
                    )

        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.verts.ensure_lookup_table()
        bm.faces.ensure_lookup_table()

        offset = len(all_verts)
        all_verts.extend(tuple(v.co) for v in bm.verts)
        all_faces.extend(tuple(v.index + offset for v in f.verts) for f in bm.faces)
        bm.free()

    return all_verts, all_faces


# ─────────────────────────────────────────────────────────────────────────────
# Properties
# ─────────────────────────────────────────────────────────────────────────────

class MCProps(bpy.types.PropertyGroup):
    output_dir: bpy.props.StringProperty(
        name="Output Dir", default="//", subtype='DIR_PATH',
        description="FBX 导出目录（// = .blend 文件同目录）",
    )
    snap_dist: bpy.props.FloatProperty(
        name="Snap Distance", default=0.15, min=0.01, max=0.5, step=1,
        description="边界顶点吸附到接缝锚点的最大距离",
    )
    coverage_text: bpy.props.StringProperty(default="")


class MCCubesProps(bpy.types.PropertyGroup):
    radius: bpy.props.FloatProperty(
        name="Side Arc Radius", default=0.08, min=0.0, max=0.24, step=1,
        description="侧面封闭边圆弧半径（0 = 直角，最大 0.24）",
    )
    radius_top: bpy.props.FloatProperty(
        name="Top Arc Radius", default=0.0, min=0.0, max=0.24, step=1,
        description="顶面（地面朝向）封闭边圆弧半径（0 = 保持直角）",
    )
    segments: bpy.props.IntProperty(
        name="Arc Segments", default=4, min=1, max=12,
        description="圆弧细分段数（越大越平滑）",
    )


# ─────────────────────────────────────────────────────────────────────────────
# Operator: Setup Reference Scene
# ─────────────────────────────────────────────────────────────────────────────

class MC_OT_SetupTerrain(bpy.types.Operator):
    bl_idname      = "mc.setup_terrain"
    bl_label       = "Setup Reference Scene"
    bl_description = "生成 MC_ArtMesh_Ref（MC_Ctrl + MC_IsoSurface）"

    def execute(self, context):
        grid, _ = build_grid()
        verify_grid(grid)

        # 清理旧 Ref collection
        _remove_col(REF_COL_NAME)
        _reset_mats()

        MAT_ACTIVE   = _ensure_mat("mc_active",   (1.00, 0.00, 0.00))
        MAT_INACTIVE = _ensure_mat("mc_inactive", (0.22, 0.22, 0.22))
        MAT_SEAM     = _ensure_mat("mc_seam",     (1.00, 0.88, 0.10))
        MAT_WIRE     = _ensure_mat("mc_wire",     (0.10, 0.65, 0.15))
        MAT_ISO      = _ensure_mat("mc_iso",      (0.05, 0.35, 1.00), strength=0.7, alpha=0.6)

        ref_root  = _ensure_col(REF_COL_NAME)
        ctrl_root = _ensure_col(CTRL_COL_NAME,  parent=ref_root)
        iso_root  = _ensure_col(ISO_COL_NAME,   parent=ref_root)

        canonicals = get_d4_canonicals()
        for n, ci in enumerate(canonicals):
            col_n, row_n = n % GRID_COLS, n // GRID_COLS
            cx, cy, cz   = col_n * 2, 0, row_n * 2
            b_ox, b_oy, b_oz = u2b(cx, cy, cz)

            # ── MC_Ctrl: 每个 case 一个子 collection
            ctrl_col = bpy.data.collections.new(f"case_{ci}")
            _link_col(ctrl_root, ctrl_col)

            # 标签
            lbl = bpy.data.curves.new(f"_lbl{n}", type='FONT')
            lbl.body = f"ci={ci}"; lbl.size = 0.18; lbl.align_x = 'LEFT'
            lbl_obj = bpy.data.objects.new(f"_lbl{n}", lbl)
            lbl_obj.location = (b_ox, b_oy, b_oz + 1.15)
            lbl_obj.hide_select = True
            lbl_obj.lock_location = lbl_obj.lock_rotation = lbl_obj.lock_scale = (True,True,True)
            ctrl_col.objects.link(lbl_obj)
            if lbl_obj.name in bpy.context.scene.collection.objects:
                bpy.context.scene.collection.objects.unlink(lbl_obj)

            # 绿色线框
            wire_mesh = _make_cube_wire(f"_w{n}", b_ox, b_oy, b_oz)
            wire_mesh.materials.append(MAT_WIRE)
            wire_obj = bpy.data.objects.new(f"_w{n}", wire_mesh)
            wire_obj.display_type = 'WIRE'; wire_obj.hide_select = True
            ctrl_col.objects.link(wire_obj)
            if wire_obj.name in bpy.context.scene.collection.objects:
                bpy.context.scene.collection.objects.unlink(wire_obj)

            # 控制点球
            for v in range(8):
                dx, dy, dz = UNITY_VERTS[v]
                bx, by, bz = u2b(cx+dx, cy+dy, cz+dz)
                active = bool(ci & (1 << v))
                _add_locked(ctrl_col, f"_pt{n}_{v}",
                            _make_sphere(f"_pt{n}_{v}", bx, by, bz, 0.07 if active else 0.04),
                            MAT_ACTIVE if active else MAT_INACTIVE)

            # 接缝锚点球
            for k, (ea, eb) in enumerate(EDGES):
                if bool(ci & (1 << ea)) == bool(ci & (1 << eb)): continue
                bx1,by1,bz1 = BL_VERTS[ea]; bx2,by2,bz2 = BL_VERTS[eb]
                _add_locked(ctrl_col, f"_s{n}_{k}",
                            _make_sphere(f"_s{n}_{k}",
                                         b_ox+(bx1+bx2)/2, b_oy+(by1+by2)/2, b_oz+(bz1+bz2)/2,
                                         0.05),
                            MAT_SEAM)

            # ── MC_IsoSurface: 等值面 ghost mesh
            unity_tris = get_iso_triangles(ci)
            if unity_tris:
                iso_col = bpy.data.collections.new(f"case_{ci}")
                _link_col(iso_root, iso_col)

                gm = bpy.data.meshes.new(f"_iso{n}")
                gb = bmesh.new()
                for i in range(0, len(unity_tris), 3):
                    fv = []
                    for j in range(3):
                        ux, uy, uz = unity_tris[i+j]
                        bx2, by2, bz2 = u2b(ux, uy, uz)
                        fv.append(gb.verts.new(Vector((b_ox+bx2, b_oy+by2, b_oz+bz2))))
                    try: gb.faces.new(fv)
                    except Exception: pass
                bmesh.ops.remove_doubles(gb, verts=gb.verts[:], dist=1e-5)
                gb.to_mesh(gm); gb.free(); gm.update()
                _add_locked(iso_col, f"_iso{n}", gm, MAT_ISO)

        for area in bpy.context.screen.areas:
            if area.type == 'VIEW_3D':
                for sp in area.spaces:
                    if sp.type == 'VIEW_3D':
                        sp.shading.type = 'MATERIAL'; break

        self.report({'INFO'}, f"Setup done: {len(canonicals)} canonical cases.")
        return {'FINISHED'}


# ─────────────────────────────────────────────────────────────────────────────
# Operator: Generate Cubes (MC_ArtMesh_Cubes)
# ─────────────────────────────────────────────────────────────────────────────

class MC_OT_GenerateCubes(bpy.types.Operator):
    bl_idname      = "mc.generate_cubes"
    bl_label       = "Generate Cubes"
    bl_description = "生成顶点八分体填充 case mesh → MC_ArtMesh_Cubes"

    def execute(self, context):
        cubes_props = context.scene.mc_cubes_props
        _remove_col(CUBES_COL_NAME)
        # index 0: 深蓝  封闭面（中平面 + 圆弧面）
        # index 1: 浅灰  侧/底开放面（cube 边界，拼接后自然封闭）
        # index 2: 棋盘  顶面（z=1，地面朝向，过程纹理标识上下不对称）
        MAT_CLOSED = _ensure_mat("mc_cube_closed", (0.04, 0.12, 0.55), strength=1.1)
        MAT_OPEN   = _ensure_mat("mc_cube_open",   (0.60, 0.60, 0.60), strength=0.7)
        MAT_TOP    = _ensure_mat_top("mc_cube_top")

        cubes_root = _ensure_col(CUBES_COL_NAME)
        canonicals = get_d4_canonicals()
        EPS = 1e-4

        for n, ci in enumerate(canonicals):
            col_n, row_n = n % GRID_COLS, n // GRID_COLS
            cx, cy, cz   = col_n * 2, 0, row_n * 2
            b_ox, b_oy, b_oz = u2b(cx, cy, cz)

            verts_local, faces_local = _make_case_mesh_vf(
                ci, radius=cubes_props.radius, segments=cubes_props.segments,
                radius_top=cubes_props.radius_top)
            if not verts_local:
                continue

            verts_world = [(v[0]+b_ox, v[1]+b_oy, v[2]+b_oz) for v in verts_local]
            mesh = bpy.data.meshes.new(f"cube_{ci}")
            mesh.from_pydata(verts_world, [], faces_local)
            mesh.update()  # 先计算法线，用于顶面识别

            mesh.materials.append(MAT_CLOSED)  # index 0
            mesh.materials.append(MAT_OPEN)    # index 1
            mesh.materials.append(MAT_TOP)     # index 2

            for poly in mesh.polygons:
                pvs = [verts_world[vi] for vi in poly.vertices]
                # 开放面：严格落在 cube 任意边界平面上
                is_open = (
                    all(abs(v[0] - b_ox      ) < EPS for v in pvs) or
                    all(abs(v[0] - b_ox - 1.0) < EPS for v in pvs) or
                    all(abs(v[1] - b_oy      ) < EPS for v in pvs) or
                    all(abs(v[1] - b_oy - 1.0) < EPS for v in pvs) or
                    all(abs(v[2] - b_oz      ) < EPS for v in pvs) or
                    all(abs(v[2] - b_oz - 1.0) < EPS for v in pvs)
                )
                if is_open:
                    poly.material_index = 1
                elif poly.normal.z > 0.9:
                    # 顶面：向上的封闭面（法线+Z，地面朝向，棋盘纹理标识）
                    poly.material_index = 2
                else:
                    poly.material_index = 0

            mesh.update()

            obj = bpy.data.objects.new(f"cube_{ci}", mesh)
            cubes_root.objects.link(obj)
            if obj.name in bpy.context.scene.collection.objects:
                bpy.context.scene.collection.objects.unlink(obj)
            obj.hide_select   = True
            obj.lock_location = (True, True, True)
            obj.lock_rotation = (True, True, True)
            obj.lock_scale    = (True, True, True)

        self.report({'INFO'}, f"Generated {len(canonicals)} case meshes → {CUBES_COL_NAME}")
        return {'FINISHED'}


# ─────────────────────────────────────────────────────────────────────────────
# Operator: Check Coverage
# ─────────────────────────────────────────────────────────────────────────────

class MC_OT_CheckCoverage(bpy.types.Operator):
    bl_idname      = "mc.check_coverage"
    bl_label       = "Check Coverage"
    bl_description = "检查当前 active 对象覆盖了多少 canonical case"

    def execute(self, context):
        canonicals = get_d4_canonicals()
        canon_map  = get_canon_map()
        grid, (nx, ny, nz) = build_grid()
        props = context.scene.mc_props

        obj = context.active_object
        if obj is None or obj.type != 'MESH' or len(obj.data.vertices) == 0:
            props.coverage_text = "0 / %d  (select a mesh)" % len(canonicals)
            self.report({'WARNING'}, "请先选中一个有顶点的 Mesh 对象。")
            return {'FINISHED'}

        obj.data.calc_loop_triangles()
        world_verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
        covered = set()
        for tri in obj.data.loop_triangles:
            vs = [world_verts[i] for i in tri.vertices]
            bx = (vs[0].x+vs[1].x+vs[2].x)/3
            by = (vs[0].y+vs[1].y+vs[2].y)/3
            bz = (vs[0].z+vs[1].z+vs[2].z)/3
            ux, uy, uz = b2u(bx, by, bz)
            ci = _compute_cube_index(grid, int(math.floor(ux)), int(math.floor(uy)), int(math.floor(uz)))
            if 0 < ci < 255:
                covered.add(canon_map[ci])

        total = len(canonicals); n_cov = len(covered)
        props.coverage_text = f"{n_cov} / {total}"
        missing = [ci for ci in canonicals if ci not in covered]
        if missing:
            self.report({'WARNING'}, f"{n_cov}/{total} covered. Missing: {missing}")
        else:
            self.report({'INFO'}, f"All {total} canonical cases covered!")
        return {'FINISHED'}


# ─────────────────────────────────────────────────────────────────────────────
# Operator: Extract & Export FBX
# ─────────────────────────────────────────────────────────────────────────────

# ─────────────────────────────────────────────────────────────────────────────
# Operator: Export Generated Meshes（直接导出生成 mesh，用于 Unity 快速测试）
# ─────────────────────────────────────────────────────────────────────────────

class MC_OT_ExportGeneratedMeshes(bpy.types.Operator):
    bl_idname      = "mc.export_generated"
    bl_label       = "Export Generated as FBX"
    bl_description = "将当前圆角参数生成的 case mesh 直接导出为 case_{n}.fbx（Unity 测试用）"

    def execute(self, context):
        props       = context.scene.mc_props
        cubes_props = context.scene.mc_cubes_props
        canonicals  = get_d4_canonicals()

        output_dir = bpy.path.abspath(props.output_dir)
        os.makedirs(output_dir, exist_ok=True)

        count = 0
        for ci in canonicals:
            verts_local, faces_local = _make_case_mesh_vf(
                ci,
                radius=cubes_props.radius,
                segments=cubes_props.segments,
                radius_top=cubes_props.radius_top,
            )
            if not verts_local:
                continue

            # 临时 mesh 放在场景原点，坐标即本地 [0,1]³
            mesh = bpy.data.meshes.new(f"_exp_{ci}")
            mesh.from_pydata(verts_local, [], faces_local)
            mesh.update()

            # ── 顶面识别并分类 ──────────────────────────────────────────────
            EPS_e = 1e-4
            # index 0=封闭(蓝) 1=开放侧/底(灰) 2=顶面封闭(棕)
            MAT_EXP_CLOSED = _ensure_mat("mc_cube_closed", (0.04, 0.12, 0.55), strength=1.1)
            MAT_EXP_OPEN   = _ensure_mat("mc_cube_open",   (0.60, 0.60, 0.60), strength=0.7)
            MAT_EXP_TOP    = _ensure_mat_top("mc_cube_top")
            mesh.materials.append(MAT_EXP_CLOSED)
            mesh.materials.append(MAT_EXP_OPEN)
            mesh.materials.append(MAT_EXP_TOP)

            for poly in mesh.polygons:
                pvs = [verts_local[vi] for vi in poly.vertices]
                is_open = (
                    all(abs(v[0]) < EPS_e for v in pvs) or
                    all(abs(v[0] - 1.0) < EPS_e for v in pvs) or
                    all(abs(v[1]) < EPS_e for v in pvs) or
                    all(abs(v[1] - 1.0) < EPS_e for v in pvs) or
                    all(abs(v[2]) < EPS_e for v in pvs) or
                    all(abs(v[2] - 1.0) < EPS_e for v in pvs)
                )
                if is_open:
                    poly.material_index = 1
                elif poly.normal.z > 0.9:
                    poly.material_index = 2
                else:
                    poly.material_index = 0

            # ── 写入顶点色（FBX 可携带，Unity 任意 Vertex Color shader 可读）──
            # 颜色对应：index0→蓝  index1→灰  index2→棕
            VCOL = {0: (0.04, 0.12, 0.55, 1.0),
                    1: (0.60, 0.60, 0.60, 1.0),
                    2: (0.55, 0.35, 0.10, 1.0)}
            try:
                vc = mesh.color_attributes.new(
                    name="Col", type='BYTE_COLOR', domain='CORNER')
                for poly in mesh.polygons:
                    c = VCOL[poly.material_index]
                    for li in poly.loop_indices:
                        vc.data[li].color = c
            except Exception:
                try:  # Blender < 3.3 fallback
                    vc = mesh.vertex_colors.new(name="Col")
                    for poly in mesh.polygons:
                        c = VCOL[poly.material_index]
                        for li in poly.loop_indices:
                            vc.data[li].color = c
                except Exception:
                    pass

            mesh.update()

            exp_obj = bpy.data.objects.new(f"case_{ci}", mesh)
            bpy.context.scene.collection.objects.link(exp_obj)
            bpy.ops.object.select_all(action='DESELECT')
            exp_obj.select_set(True)
            bpy.context.view_layer.objects.active = exp_obj

            fbx_path = os.path.join(output_dir, f"case_{ci}.fbx")
            bpy.ops.export_scene.fbx(
                filepath=fbx_path, use_selection=True,
                axis_forward='-Z', axis_up='Y',
                apply_scale_options='FBX_SCALE_NONE',
                use_mesh_modifiers=True, mesh_smooth_type='FACE',
                add_leaf_bones=False,
            )

            bpy.context.scene.collection.objects.unlink(exp_obj)
            bpy.data.objects.remove(exp_obj)
            bpy.data.meshes.remove(mesh)
            count += 1

        self.report({'INFO'}, f"Exported {count} / {len(canonicals)} cases → {output_dir}")
        return {'FINISHED'}


# ─────────────────────────────────────────────────────────────────────────────
class MC_OT_ExtractCases(bpy.types.Operator):
    bl_idname      = "mc.extract_cases"
    bl_label       = "Extract & Export FBX"
    bl_description = "从当前 active 对象切割 canonical case mesh 并导出 FBX"

    def execute(self, context):
        props      = context.scene.mc_props
        canonicals = get_d4_canonicals()
        canon_map  = get_canon_map()
        grid, (nx, ny, nz) = build_grid()

        obj = context.active_object
        if obj is None or obj.type != 'MESH':
            self.report({'ERROR'}, "请先选中 source mesh（如 ProceduralTerrain 或美术自建模型）。")
            return {'CANCELLED'}
        if len(obj.data.vertices) == 0:
            self.report({'ERROR'}, f"'{obj.name}' 没有顶点。")
            return {'CANCELLED'}

        output_dir = bpy.path.abspath(props.output_dir)
        os.makedirs(output_dir, exist_ok=True)

        snap_dist = props.snap_dist
        EPS = 1e-4

        obj.data.calc_loop_triangles()
        world_verts = [obj.matrix_world @ v.co for v in obj.data.vertices]

        cell_tris = {}
        for tri in obj.data.loop_triangles:
            vs = [world_verts[i] for i in tri.vertices]
            bx = (vs[0].x+vs[1].x+vs[2].x)/3
            by = (vs[0].y+vs[1].y+vs[2].y)/3
            bz = (vs[0].z+vs[1].z+vs[2].z)/3
            ux, uy, uz = b2u(bx, by, bz)
            key = (int(math.floor(ux)), int(math.floor(uy)), int(math.floor(uz)))
            cell_tris.setdefault(key, []).append(tri)

        canonicals_set     = set(canonicals)
        exported_canonical = {}
        exported, skipped  = [], []

        for cx in range(nx):
            for cy in range(ny):
                for cz in range(nz):
                    ci = _compute_cube_index(grid, cx, cy, cz)
                    if ci == 0 or ci == 255: continue
                    canonical_ci = canon_map[ci]
                    if canonical_ci not in canonicals_set: continue
                    if canonical_ci in exported_canonical: continue

                    tris = cell_tris.get((cx, cy, cz), [])
                    if not tris:
                        skipped.append(canonical_ci); continue

                    b_ox, b_oy, b_oz = u2b(cx, cy, cz)
                    anchors = _seam_anchors_world(ci, cx, cy, cz)

                    vert_map, new_verts, new_faces = {}, [], []
                    for tri in tris:
                        tri_idx = []
                        for li in tri.vertices:
                            if li not in vert_map:
                                wco = world_verts[li].copy()
                                on_b = (abs(wco.x-b_ox)<EPS or abs(wco.x-(b_ox+1))<EPS or
                                        abs(wco.y-b_oy)<EPS or abs(wco.y-(b_oy+1))<EPS or
                                        abs(wco.z-b_oz)<EPS or abs(wco.z-(b_oz+1))<EPS)
                                if on_b and anchors:
                                    nearest = min(anchors, key=lambda a: (wco-a).length_squared)
                                    if (wco-nearest).length < snap_dist:
                                        wco = nearest.copy()
                                wco.x -= b_ox; wco.y -= b_oy; wco.z -= b_oz
                                vert_map[li] = len(new_verts); new_verts.append(wco)
                            tri_idx.append(vert_map[li])
                        new_faces.append(tri_idx)

                    mesh = bpy.data.meshes.new(f"_exp_{canonical_ci}")
                    mesh.from_pydata(new_verts, [], new_faces)
                    mesh.update()
                    exp_obj = bpy.data.objects.new(f"case_{canonical_ci}", mesh)
                    bpy.context.scene.collection.objects.link(exp_obj)
                    bpy.ops.object.select_all(action='DESELECT')
                    exp_obj.select_set(True)
                    bpy.context.view_layer.objects.active = exp_obj

                    fbx_path = os.path.join(output_dir, f"case_{canonical_ci}.fbx")
                    bpy.ops.export_scene.fbx(
                        filepath=fbx_path, use_selection=True,
                        axis_forward='-Z', axis_up='Y',
                        apply_scale_options='FBX_SCALE_NONE',
                        use_mesh_modifiers=True, mesh_smooth_type='FACE',
                        add_leaf_bones=False,
                    )
                    bpy.context.scene.collection.objects.unlink(exp_obj)
                    bpy.data.objects.remove(exp_obj)
                    bpy.data.meshes.remove(mesh)
                    exported_canonical[canonical_ci] = True
                    exported.append(canonical_ci)

        msg = f"Exported {len(exported)}/{len(canonicals)} cases → {output_dir}"
        if skipped:
            msg += f"  |  No mesh: {skipped}"
        self.report({'INFO'}, msg)
        return {'FINISHED'}


# ─────────────────────────────────────────────────────────────────────────────
# Panel
# ─────────────────────────────────────────────────────────────────────────────

class MC_PT_Panel(bpy.types.Panel):
    bl_idname      = "MC_PT_Panel"
    bl_label       = "MC ArtMesh"
    bl_space_type  = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category    = 'MC ArtMesh'

    def draw(self, context):
        layout      = self.layout
        props       = context.scene.mc_props
        cubes_props = context.scene.mc_cubes_props

        # 1. Reference Scene
        box = layout.box()
        box.label(text="1. Reference Scene", icon='MESH_GRID')
        box.operator("mc.setup_terrain", icon='SCENE_DATA')
        box.label(text="MC_ArtMesh_Ref / MC_Ctrl + MC_IsoSurface", icon='INFO')

        layout.separator()

        # 2. Case Meshes
        box = layout.box()
        box.label(text="2. Case Meshes", icon='MESH_CUBE')
        box.prop(cubes_props, "radius")
        box.prop(cubes_props, "radius_top")
        box.prop(cubes_props, "segments")
        box.operator("mc.generate_cubes", icon='MESH_UVSPHERE')
        box.label(text="→ MC_ArtMesh_Cubes  深蓝=封闭  浅灰=开放", icon='INFO')

        layout.separator()

        # 3. Coverage
        box = layout.box()
        box.label(text="3. Coverage Check", icon='VIEWZOOM')
        row = box.row()
        row.operator("mc.check_coverage", icon='CHECKMARK')
        if props.coverage_text:
            row.label(text=props.coverage_text)
        box.label(text="Select source mesh first", icon='INFO')

        layout.separator()

        # 4b. Quick Export（直接导出生成 mesh，测试用）
        box = layout.box()
        box.label(text="4. Quick Export (Test)", icon='EXPORT')
        box.prop(props, "output_dir")
        box.operator("mc.export_generated", icon='EXPORT')
        box.label(text="直接导出生成 mesh → case_{n}.fbx", icon='INFO')

        layout.separator()

        # 5. Export from art mesh
        box = layout.box()
        box.label(text="5. Export FBX (Art Mesh)", icon='EXPORT')
        box.prop(props, "snap_dist")
        box.operator("mc.extract_cases", icon='EXPORT')
        box.label(text="Select source mesh, then Export", icon='INFO')


# ─────────────────────────────────────────────────────────────────────────────
# Register / Unregister
# ─────────────────────────────────────────────────────────────────────────────

_CLASSES = [
    MCProps,
    MCCubesProps,
    MC_OT_SetupTerrain,
    MC_OT_GenerateCubes,
    MC_OT_ExportGeneratedMeshes,
    MC_OT_CheckCoverage,
    MC_OT_ExtractCases,
    MC_PT_Panel,
]


def register():
    for cls in _CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.mc_props       = bpy.props.PointerProperty(type=MCProps)
    bpy.types.Scene.mc_cubes_props = bpy.props.PointerProperty(type=MCCubesProps)


def unregister():
    for cls in reversed(_CLASSES):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.mc_props
    del bpy.types.Scene.mc_cubes_props
