"""
ME ArtMesh — Blender Add-on module  v1.0
Marching Edges（面建造）美术网格工作流。

面槽编码（与 FaceBuilder.cs 完全一致）：
  12-bit mask:
    bits 0-3:  X 面（YZ 平面）法线=X: [+Y+Z, -Y+Z, -Y-Z, +Y-Z]
    bits 4-7:  Y 面（XZ 平面）法线=Y: [+X+Z, -X+Z, -X-Z, +X-Z]
    bits 8-11: Z 面（XY 平面）法线=Z: [+X+Y, -X+Y, -X-Y, +X-Y]
  对称群：绕 Y 轴旋转 90°/180°/270°（仅旋转，无翻转）
  Canonical 总数：1044

工作流：
  1. 运行「构建参考网格」查看全部 1044 个 canonical case 的面槽布局
  2. 选择某个 case 单独预览（输入 case_index → 显示单 case）
  3. 在 ME_ArtMesh collection 中创建对应美术网格，命名为 me_case_N
  4. 运行「导出 FBX」→ me_case_N.fbx（供 Unity ArtFaceMeshEditor 导入）
"""

import bpy
import bmesh
import math
import os
import re
from mathutils import Vector

# ─────────────────────────────────────────────────────────────────────────────
# 坐标系转换
# Unity: Y 上 Z 前  →  Blender: Z 上 Y 前
# ─────────────────────────────────────────────────────────────────────────────

def u2b(ux, uy, uz): return (ux, uz, uy)
def b2u(bx, by, bz): return (bx, bz, by)


# ─────────────────────────────────────────────────────────────────────────────
# 12 个面槽（Unity 坐标，以顶点为中心 0,0,0）
# xFace[vx,j,k]: YZ 平面, x=vx, y:[j,j+1], z:[k,k+1]
# yFace[i,vy,k]: XZ 平面, y=vy, x:[i,i+1], z:[k,k+1]
# zFace[i,j,vz]: XY 平面, z=vz, x:[i,i+1], y:[j,j+1]
# ─────────────────────────────────────────────────────────────────────────────

_SLOTS_U = (
    # X 组 (法线=X, YZ 平面)
    ((0, 0, 0), (0, 1, 0), (0, 1, 1), (0, 0, 1)),      # X0  bit 0  +Y+Z
    ((0,-1, 0), (0, 0, 0), (0, 0, 1), (0,-1, 1)),      # X1  bit 1  -Y+Z
    ((0,-1,-1), (0, 0,-1), (0, 0, 0), (0,-1, 0)),      # X2  bit 2  -Y-Z
    ((0, 0,-1), (0, 1,-1), (0, 1, 0), (0, 0, 0)),      # X3  bit 3  +Y-Z
    # Y 组 (法线=Y, XZ 平面)
    ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),      # Y0  bit 4  +X+Z
    ((-1, 0, 0),(0, 0, 0), (0, 0, 1),(-1, 0, 1)),      # Y1  bit 5  -X+Z
    ((-1, 0,-1),(0, 0,-1), (0, 0, 0),(-1, 0, 0)),      # Y2  bit 6  -X-Z
    ((0, 0,-1), (1, 0,-1), (1, 0, 0), (0, 0, 0)),      # Y3  bit 7  +X-Z
    # Z 组 (法线=Z, XY 平面)
    ((0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)),      # Z0  bit 8  +X+Y
    ((-1, 0, 0),(0, 0, 0), (0, 1, 0),(-1, 1, 0)),      # Z1  bit 9  -X+Y
    ((-1,-1, 0),(0,-1, 0), (0, 0, 0),(-1, 0, 0)),      # Z2  bit 10 -X-Y
    ((0,-1, 0), (1,-1, 0), (1, 0, 0), (0, 0, 0)),      # Z3  bit 11 +X-Y
)

# 转换为 Blender 坐标
SLOTS_B = tuple(tuple(u2b(*v) for v in quad) for quad in _SLOTS_U)

# 面槽颜色 RGBA (X=红, Y=绿, Z=蓝)
SLOT_RGBA = (
    (0.90, 0.22, 0.22, 0.65),
    (0.90, 0.22, 0.22, 0.65),
    (0.90, 0.22, 0.22, 0.65),
    (0.90, 0.22, 0.22, 0.65),
    (0.22, 0.80, 0.32, 0.65),
    (0.22, 0.80, 0.32, 0.65),
    (0.22, 0.80, 0.32, 0.65),
    (0.22, 0.80, 0.32, 0.65),
    (0.25, 0.45, 0.92, 0.65),
    (0.25, 0.45, 0.92, 0.65),
    (0.25, 0.45, 0.92, 0.65),
    (0.25, 0.45, 0.92, 0.65),
)


# ─────────────────────────────────────────────────────────────────────────────
# 12-bit 旋转置换（与 FaceBuilder.cs Rotate90CW 完全一致）
# ─────────────────────────────────────────────────────────────────────────────

def _rot90cw(m):
    r = 0
    if m &    1: r |= 1 << 8    # X0 → Z0
    if m &    2: r |= 1 << 11   # X1 → Z3
    if m &    4: r |= 1 << 10   # X2 → Z2
    if m &    8: r |= 1 << 9    # X3 → Z1
    if m &   16: r |= 1 << 7    # Y0 → Y3
    if m &   32: r |= 1 << 4    # Y1 → Y0
    if m &   64: r |= 1 << 5    # Y2 → Y1
    if m &  128: r |= 1 << 6    # Y3 → Y2
    if m &  256: r |= 1 << 3    # Z0 → X3
    if m &  512: r |= 1 << 0    # Z1 → X0
    if m & 1024: r |= 1 << 1    # Z2 → X1
    if m & 2048: r |= 1 << 2    # Z3 → X2
    return r


# ─────────────────────────────────────────────────────────────────────────────
# Canonical map（懒加载，与 FaceBuilder.EnsureLookup 完全等价）
# ─────────────────────────────────────────────────────────────────────────────

_ME_CANON_LIST  = None   # 1044 canonical 12-bit masks（升序）
_ME_CANON_INDEX = None   # canonical_mask → 序号 (0-1043)
_ME_CANON_OF    = None   # 任意 mask → canonical mask


def _init():
    global _ME_CANON_LIST, _ME_CANON_INDEX, _ME_CANON_OF
    if _ME_CANON_LIST is not None:
        return

    min_mask = list(range(4096))
    for m in range(4096):
        cur = m
        for _ in range(3):
            cur = _rot90cw(cur)
            if cur < min_mask[m]:
                min_mask[m] = cur

    _ME_CANON_OF    = min_mask
    canon_set       = sorted(set(min_mask))
    _ME_CANON_LIST  = canon_set
    _ME_CANON_INDEX = {v: i for i, v in enumerate(canon_set)}


def me_canonicals():
    """返回 1044 个 canonical 12-bit mask（升序列表）。"""
    _init()
    return _ME_CANON_LIST


def me_canon_idx(mask):
    """任意 12-bit mask → canonical 序号 (0-1043)。"""
    _init()
    return _ME_CANON_INDEX.get(_ME_CANON_OF[mask & 0xFFF], -1)


# ─────────────────────────────────────────────────────────────────────────────
# Collection 工具
# ─────────────────────────────────────────────────────────────────────────────

ME_REF_COL  = "ME_ArtMesh_Ref"
ME_MESH_COL = "ME_ArtMesh"

GRID_COLS = 33
GRID_STEP = 3.0   # Blender 单位


def _ensure_col(name, parent=None):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(col)
    return col


def _add_to_col(col, obj):
    for c in bpy.data.collections:
        if obj.name in c.objects:
            c.objects.unlink(obj)
    sc = bpy.context.scene.collection
    if obj.name in sc.objects:
        sc.objects.unlink(obj)
    col.objects.link(obj)


def _clear_col(col):
    for obj in list(col.objects):
        col.objects.unlink(obj)
        if obj.users == 0:
            bpy.data.objects.remove(obj, do_unlink=True)


# ─────────────────────────────────────────────────────────────────────────────
# 材质
# ─────────────────────────────────────────────────────────────────────────────

def _mat(name, rgba):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out  = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = rgba
    bsdf.inputs['Alpha'].default_value      = rgba[3]
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    if rgba[3] < 1.0:
        mat.blend_method  = 'BLEND'
        mat.shadow_method = 'NONE'
    mat.diffuse_color = rgba
    return mat


# ─────────────────────────────────────────────────────────────────────────────
# 几何辅助
# ─────────────────────────────────────────────────────────────────────────────

def _slot_quad_mesh(name, corners_b, shrink=0.88):
    """单个面槽可视化 quad（轻微缩小，避免相邻面 z-fighting）。"""
    cx = sum(v[0] for v in corners_b) / 4
    cy = sum(v[1] for v in corners_b) / 4
    cz = sum(v[2] for v in corners_b) / 4
    verts = [(cx + (v[0] - cx) * shrink,
              cy + (v[1] - cy) * shrink,
              cz + (v[2] - cz) * shrink) for v in corners_b]
    m  = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bvs = [bm.verts.new(v) for v in verts]
    bm.faces.new(bvs)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(m)
    bm.free()
    m.update()
    return m


def _wire_cube_mesh(name, ox_b, oy_b, oz_b):
    """1×1×1 线框立方体（display_type=WIRE 对象）。"""
    verts_u = [(0,0,1),(1,0,1),(1,0,0),(0,0,0),
               (0,1,1),(1,1,1),(1,1,0),(0,1,0)]
    verts = [u2b(ox_b + vx, oy_b + vy, oz_b + vz) for vx, vy, vz in verts_u]
    faces = [(0,1,2,3),(4,5,6,7),(0,1,5,4),(2,3,7,6),(1,2,6,5),(3,0,4,7)]
    m  = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bvs = [bm.verts.new(v) for v in verts]
    for f in faces:
        try:
            bm.faces.new([bvs[i] for i in f])
        except Exception:
            pass
    bm.to_mesh(m)
    bm.free()
    m.update()
    return m


def _build_case_viz(col, canon_idx, offset_b, slot_mats, wire_mat):
    """在 col 中生成 canonical case 的面槽可视化（面 quad + 线框 cube）。"""
    ox, oy, oz = offset_b
    canon = me_canonicals()
    if canon_idx >= len(canon):
        return
    mask = canon[canon_idx]

    for slot_i, corners in enumerate(SLOTS_B):
        if not (mask >> slot_i) & 1:
            continue
        shifted = tuple((v[0] + ox, v[1] + oy, v[2] + oz) for v in corners)
        mesh = _slot_quad_mesh(f"_ms_{canon_idx}_{slot_i}", shifted)
        mesh.materials.append(slot_mats[slot_i])
        obj = bpy.data.objects.new(f"_me_sq_{canon_idx}_{slot_i}", mesh)
        obj.hide_select = True
        _add_to_col(col, obj)

    # 线框 cube — origin 是 Unity 顶点坐标
    ux, uy, uz = b2u(ox, oy, oz)
    wm = _wire_cube_mesh(f"_mw_{canon_idx}", *u2b(ux - 0.5, uy - 0.5, uz - 0.5))
    wm.materials.append(wire_mat)
    wo = bpy.data.objects.new(f"_me_wire_{canon_idx}", wm)
    wo.display_type = 'WIRE'
    wo.hide_select  = True
    _add_to_col(col, wo)


# ─────────────────────────────────────────────────────────────────────────────
# Operator: 构建参考网格（全部 1044 case）
# ─────────────────────────────────────────────────────────────────────────────

class ME_OT_BuildGrid(bpy.types.Operator):
    bl_idname      = "me.build_grid"
    bl_label       = "构建参考网格 (1044 cases)"
    bl_description = "生成全部 1044 canonical ME case 的面槽可视化（%d 列网格）" % GRID_COLS

    def execute(self, context):
        canon = me_canonicals()
        col   = _ensure_col(ME_REF_COL)
        _clear_col(col)

        slot_mats = [_mat(f"me_slot_{i}", SLOT_RGBA[i]) for i in range(12)]
        wire_mat  = _mat("me_wire", (0.35, 0.35, 0.35, 1.0))

        for idx in range(len(canon)):
            c_n = idx % GRID_COLS
            r_n = idx // GRID_COLS
            ox  = c_n * GRID_STEP
            oz  = r_n * GRID_STEP
            _build_case_viz(col, idx, (ox, 0.0, oz), slot_mats, wire_mat)

            if idx % 100 == 0:
                bpy.context.window_manager.progress_update(idx / len(canon))

        self.report({'INFO'},
            f"ME 参考网格已生成：{len(canon)} cases，{GRID_COLS} 列  → Collection: {ME_REF_COL}")
        return {'FINISHED'}


# ─────────────────────────────────────────────────────────────────────────────
# Operator: 单 case 预览
# ─────────────────────────────────────────────────────────────────────────────

class ME_OT_ShowCase(bpy.types.Operator):
    bl_idname      = "me.show_case"
    bl_label       = "显示单个 Case"
    bl_description = "在 Ref 场景中单独显示指定 canonical case 的面槽（原点处）"

    def execute(self, context):
        props = context.scene.me_props
        cidx  = props.case_index
        canon = me_canonicals()

        if not (0 <= cidx < len(canon)):
            self.report({'ERROR'}, f"case_index 需在 0-{len(canon)-1}")
            return {'CANCELLED'}

        col = _ensure_col(ME_REF_COL)
        _clear_col(col)

        slot_mats = [_mat(f"me_slot_{i}", SLOT_RGBA[i]) for i in range(12)]
        wire_mat  = _mat("me_wire", (0.35, 0.35, 0.35, 1.0))
        _build_case_viz(col, cidx, (0.0, 0.0, 0.0), slot_mats, wire_mat)

        mask = canon[cidx]
        bits = f"X:{mask & 0xF:04b}  Y:{(mask>>4) & 0xF:04b}  Z:{(mask>>8) & 0xF:04b}"
        active = bin(mask).count('1')
        self.report({'INFO'}, f"Case {cidx}  mask=0x{mask:03X}  active_slots={active}  {bits}")
        return {'FINISHED'}


# ─────────────────────────────────────────────────────────────────────────────
# Operator: 导出 FBX
# ─────────────────────────────────────────────────────────────────────────────

class ME_OT_ExportFBX(bpy.types.Operator):
    bl_idname      = "me.export_fbx"
    bl_label       = "导出 FBX"
    bl_description = (
        f"将 {ME_MESH_COL} 中命名为 me_case_N 的对象各自导出为 me_case_N.fbx\n"
        "命名规则：me_case_0 … me_case_1043"
    )

    def execute(self, context):
        props      = context.scene.me_props
        output_dir = bpy.path.abspath(props.output_dir)
        os.makedirs(output_dir, exist_ok=True)

        col = bpy.data.collections.get(ME_MESH_COL)
        if col is None:
            self.report({'ERROR'},
                f"找不到 Collection '{ME_MESH_COL}'，"
                "请先创建并将美术网格放入其中，命名为 me_case_N。")
            return {'CANCELLED'}

        exported, skipped = [], []
        pattern = re.compile(r'^me_case_(\d+)$')

        for obj in col.objects:
            m = pattern.match(obj.name)
            if m is None:
                skipped.append(obj.name)
                continue
            idx = int(m.group(1))
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj

            path = os.path.join(output_dir, f"me_case_{idx}.fbx")
            bpy.ops.export_scene.fbx(
                filepath              = path,
                use_selection         = True,
                axis_forward          = 'Y',
                axis_up               = 'Z',
                apply_scale_options   = 'FBX_SCALE_ALL',
                use_mesh_modifiers    = True,
                mesh_smooth_type      = 'FACE',
                add_leaf_bones        = False,
            )
            exported.append(idx)

        msg = f"已导出 {len(exported)} 个 → {output_dir}"
        if skipped:
            msg += f"  |  命名不符（跳过）: {skipped}"
        self.report({'INFO'}, msg)
        return {'FINISHED'}


# ─────────────────────────────────────────────────────────────────────────────
# Scene Properties
# ─────────────────────────────────────────────────────────────────────────────

class MEProps(bpy.types.PropertyGroup):
    output_dir: bpy.props.StringProperty(
        name        = "FBX 输出目录",
        default     = "//me_cases/",
        subtype     = 'DIR_PATH',
        description = "me_case_N.fbx 的输出文件夹（// = .blend 所在目录）",
    )
    case_index: bpy.props.IntProperty(
        name        = "Case 索引",
        default     = 0,
        min         = 0,
        max         = 1043,
        description = "0-1043，对应 canonical case 序号",
    )


# ─────────────────────────────────────────────────────────────────────────────
# Panel
# ─────────────────────────────────────────────────────────────────────────────

class ME_PT_Panel(bpy.types.Panel):
    bl_idname      = "ME_PT_Panel"
    bl_label       = "ME 面建造"
    bl_space_type  = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category    = '建造美术网格'

    def draw(self, context):
        layout = self.layout
        props  = context.scene.me_props

        # 1. 参考网格
        box = layout.box()
        box.label(text="1. 参考网格", icon='MESH_GRID')
        box.operator("me.build_grid", icon='SCENE_DATA')
        row = box.row()
        row.label(text="红 = X面", icon='TRIA_RIGHT')
        row.label(text="绿 = Y面", icon='TRIA_UP')
        row.label(text="蓝 = Z面", icon='DOT')
        box.label(text=f"→ {ME_REF_COL}  ({GRID_COLS} 列)", icon='INFO')

        layout.separator()

        # 2. 单 case 预览
        box = layout.box()
        box.label(text="2. 单 Case 预览", icon='RESTRICT_SELECT_OFF')
        box.prop(props, "case_index")
        box.operator("me.show_case", icon='HIDE_OFF')
        box.label(text="在 Ref 场景原点处显示单个 case", icon='INFO')

        layout.separator()

        # 3. 导出 FBX
        box = layout.box()
        box.label(text="3. 导出 FBX", icon='EXPORT')
        box.label(text=f"Collection: {ME_MESH_COL}", icon='COLLECTION_COLOR_04')
        box.label(text="命名：me_case_0 … me_case_1043", icon='INFO')
        box.prop(props, "output_dir")
        box.operator("me.export_fbx", icon='EXPORT')


# ─────────────────────────────────────────────────────────────────────────────
# Register
# ─────────────────────────────────────────────────────────────────────────────

_ME_CLASSES = [
    MEProps,
    ME_OT_BuildGrid,
    ME_OT_ShowCase,
    ME_OT_ExportFBX,
    ME_PT_Panel,
]
