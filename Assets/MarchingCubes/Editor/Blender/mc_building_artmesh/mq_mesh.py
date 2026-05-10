import bpy
import bmesh
import math
from mathutils import Vector

# ── Case data ─────────────────────────────────────────────────────────────────
#
# 角点编号（每格 quad 四顶点，bit 0~3）：
#   V3(TL) ─── V2(TR)       bit=1 = 高位（base+1）
#     │               │       bit=0 = 低位（base）
#   V0(BL) ─── V1(BR)
#
# 6 canonical cases: 0, 1, 3, 5, 7, 15

CORNER_XZ = [
    (0.0, 0.0),  # V0 BL
    (1.0, 0.0),  # V1 BR
    (1.0, 1.0),  # V2 TR
    (0.0, 1.0),  # V3 TL
]

# quad 四边：V0-V1-V2-V3-V0
QUAD_EDGES = [(0, 1), (1, 2), (2, 3), (3, 0)]

# 每格 mesh 以四角最低点为基准，绝对高度由 terrain 数据决定
# case 15（全高）几何同 case 0（全平），GetMeshCase() 永远不返回 15，但仍生成保持数组完整
# case 0-14：标准 case（四角高差 ≤ 1）
# case 15-18：对角高差 == 2 的特殊 case（4 方向约束允许，需独立 mesh）
ALL_CASES = list(range(19))

# 对角高差=2 case 的高度数组（V0,V1,V2,V3 相对 base 的高度）
DIAGONAL2_HEIGHTS = {
    15: [2.0, 1.0, 0.0, 1.0],  # V0=+2, V2=base
    16: [1.0, 2.0, 1.0, 0.0],  # V1=+2, V3=base
    17: [0.0, 1.0, 2.0, 1.0],  # V2=+2, V0=base
    18: [1.0, 0.0, 1.0, 2.0],  # V3=+2, V1=base
}

CASE_NAMES = {
    0:  "0000 – 全平（base，case 15 复用）",
    1:  "0001 – V0(BL) 高",
    2:  "0010 – V1(BR) 高",
    3:  "0011 – V0+V1（底边）高",
    4:  "0100 – V2(TR) 高",
    5:  "0101 – V0+V2（对角 /）高",
    6:  "0110 – V1+V2（右边）高",
    7:  "0111 – V0+V1+V2 高",
    8:  "1000 – V3(TL) 高",
    9:  "1001 – V0+V3（左边）高",
    10: "1010 – V1+V3（对角 \\）高",
    11: "1011 – V0+V1+V3 高",
    12: "1100 – V2+V3（顶边）高",
    13: "1101 – V0+V2+V3 高",
    14: "1110 – V1+V2+V3 高",
    # ── 对角高差=2 特殊 case ───────────────────────────────────────────────────
    15: "V0=+2/V2=base – 对角双高（V0 最高）",
    16: "V1=+2/V3=base – 对角双高（V1 最高）",
    17: "V2=+2/V0=base – 对角双高（V2 最高）",
    18: "V3=+2/V1=base – 对角双高（V3 最高）",
}

REF_COL_NAME     = "MQ_ArtMesh_Ref"
CTRL_COL_NAME    = "MQ_Ctrl"
REF_MESH_COL     = "MQ_Meshes"      # 参考 mesh（ref 根节点下）
TERRAIN_COL_NAME = "MQ_ArtMesh_Terrain"
GRID_COLS        = 4   # 4×4 grid（16 case）

# ── Arc helper ────────────────────────────────────────────────────────────────

def apply_arc(t, arc_s, flat):
    """t ∈ [0,1] → [0,1]。底部和顶部各保留 flat 平台，中间做 smooth-step。
    arc_s=0 → 线性过渡（参考 mesh 用，保留硬边缘）
    arc_s=1 → 完整 smooth-step（测试 mesh / 导出用）"""
    if t <= flat:       return 0.0
    if t >= 1.0 - flat: return 1.0
    t2 = (t - flat) / (1.0 - 2.0 * flat)
    t_smooth = t2 * t2 * (3.0 - 2.0 * t2)
    return t2 * (1.0 - arc_s) + t_smooth * arc_s


def bilinear_arc(u, v, h, arc_s, flat):
    """先对 UV 坐标做 arc，再做双线性插值。
    使每个高角点产生 flat×flat 正方形平台，而非双曲线三角形。"""
    ua = apply_arc(u, arc_s, flat)
    va = apply_arc(v, arc_s, flat)
    return (1-ua)*(1-va)*h[0] + ua*(1-va)*h[1] + ua*va*h[2] + (1-ua)*va*h[3]


# ── Helpers ───────────────────────────────────────────────────────────────────

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
    return mat


def _make_sphere(name, cx, cy, cz, r):
    m  = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=r)
    for v in bm.verts:
        v.co.x += cx; v.co.y += cy; v.co.z += cz
    bm.to_mesh(m); bm.free(); m.update()
    return m


def _ensure_col(name, parent=None):
    col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if parent is not None:
        if name not in [c.name for c in parent.children]:
            parent.children.link(col)
            if col.name in bpy.context.scene.collection.children:
                bpy.context.scene.collection.children.unlink(col)
    else:
        if name not in [c.name for c in bpy.context.scene.collection.children]:
            bpy.context.scene.collection.children.link(col)
    return col


def _remove_col(name):
    if name in bpy.data.collections:
        bpy.data.collections.remove(bpy.data.collections[name], do_unlink=True)


def _make_cube_wire(name, ox, oy):
    """生成 1×1×1 cube 线框（底面 Z=0，顶面 Z=1，XY 平面水平）"""
    # 8 顶点：底面4 + 顶面4
    pts = [
        (ox+0, oy+0, 0), (ox+1, oy+0, 0), (ox+1, oy+1, 0), (ox+0, oy+1, 0),
        (ox+0, oy+0, 1), (ox+1, oy+0, 1), (ox+1, oy+1, 1), (ox+0, oy+1, 1),
    ]
    edges = [
        (0,1),(1,2),(2,3),(3,0),  # 底面
        (4,5),(5,6),(6,7),(7,4),  # 顶面
        (0,4),(1,5),(2,6),(3,7),  # 竖边
    ]
    m  = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bvs = [bm.verts.new(Vector(p)) for p in pts]
    for a, b in edges:
        bm.edges.new([bvs[a], bvs[b]])
    bm.to_mesh(m); bm.free()
    return m


def _add_locked(col, name, mesh, mat):
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    if obj.name in bpy.context.scene.collection.objects:
        bpy.context.scene.collection.objects.unlink(obj)
    obj.hide_select   = True
    obj.lock_location = obj.lock_rotation = obj.lock_scale = (True, True, True)
    return obj


# ── Operators ─────────────────────────────────────────────────────────────────

class MQ_OT_SetupAllCases(bpy.types.Operator):
    """生成全部 16 个 case 的参考线框、角点球和顶点编号标签"""
    bl_idname = "mq.setup_all_cases"
    bl_label  = "初始化全部 Case 参考场景"

    def execute(self, context):
        _remove_col(REF_COL_NAME)

        MAT_HIGH = _ensure_mat("mq_high", (1.00, 0.40, 0.10))  # 橙：高位
        MAT_LOW  = _ensure_mat("mq_low",  (0.35, 0.35, 0.35))  # 灰：低位
        MAT_WIRE = _ensure_mat("mq_wire", (0.10, 0.65, 0.15))  # 绿：线框

        ref_root  = _ensure_col(REF_COL_NAME)
        ctrl_root = _ensure_col(CTRL_COL_NAME, parent=ref_root)

        for n, ci in enumerate(ALL_CASES):
            col_n = n % GRID_COLS
            row_n = n // GRID_COLS
            ox = col_n * 2.0
            oy = row_n * 2.0
            h = DIAGONAL2_HEIGHTS.get(ci, [1.0 if (ci & (1 << i)) else 0.0 for i in range(4)])

            case_col = bpy.data.collections.new(f"case_{ci}")
            ctrl_root.children.link(case_col)
            if case_col.name in context.scene.collection.children:
                context.scene.collection.children.unlink(case_col)

            # ── Case 标签（index + bit pattern）──────────────────────────────
            lbl = bpy.data.curves.new(f"_lbl_mq_{n}", type='FONT')
            lbl.body     = f"ci={ci}  {bin(ci)[2:].zfill(4)}"
            lbl.size     = 0.18
            lbl.align_x  = 'LEFT'
            lbl_obj = bpy.data.objects.new(f"_lbl_mq_{n}", lbl)
            lbl_obj.location    = Vector((ox - 0.1, oy - 0.35, 1.4))  # quad 上方（Z）
            lbl_obj.hide_select = True
            lbl_obj.lock_location = lbl_obj.lock_rotation = lbl_obj.lock_scale = (True, True, True)
            case_col.objects.link(lbl_obj)
            if lbl_obj.name in context.scene.collection.objects:
                context.scene.collection.objects.unlink(lbl_obj)

            # ── 线框 quad（水平面 XY，高度用 Blender Z）─────────────────────
            wire_mesh = bpy.data.meshes.new(f"_wire_mq_{n}")
            bm = bmesh.new()
            bvs = []
            for i, (cx, cz) in enumerate(CORNER_XZ):
                h = 1.0 if (ci & (1 << i)) else 0.0
                bvs.append(bm.verts.new(Vector((ox + cx, oy + cz, h))))
            for ea, eb in QUAD_EDGES:
                bm.edges.new([bvs[ea], bvs[eb]])
            bm.to_mesh(wire_mesh); bm.free()
            _add_locked(case_col, f"_wire_mq_{n}", wire_mesh, MAT_WIRE)

            # ── 1×1×1 Cube 线框（MQ mesh 所在空间边界）──────────────────────
            MAT_CUBE = _ensure_mat("mq_cube_wire", (0.10, 0.55, 0.85), strength=0.9)
            cube_mesh = _make_cube_wire(f"_cube_mq_{n}", ox, oy)
            _add_locked(case_col, f"_cube_mq_{n}", cube_mesh, MAT_CUBE)

            # ── 角点球 + 顶点编号标签 ─────────────────────────────────────────
            center_x, center_y = 0.5, 0.5
            for i, (cx, cz) in enumerate(CORNER_XZ):
                h    = 1.0 if (ci & (1 << i)) else 0.0
                high = (ci & (1 << i)) != 0

                # 球（Blender XYZ = cx, cz, h）
                r = 0.07 if high else 0.04
                _add_locked(case_col, f"_sph_mq_{n}_{i}",
                            _make_sphere(f"_sph_mq_{n}_{i}", ox+cx, oy+cz, h, r),
                            MAT_HIGH if high else MAT_LOW)

                # 顶点编号标签（悬浮在球上方）
                vl = bpy.data.curves.new(f"_vl_mq_{n}_{i}", type='FONT')
                vl.body    = f"V{i}"
                vl.size    = 0.10
                vl.align_x = 'CENTER'
                vl_obj = bpy.data.objects.new(f"_vl_mq_{n}_{i}", vl)
                off_x = (cx - center_x) * 0.28
                off_y = (cz - center_y) * 0.28
                vl_obj.location    = Vector((ox+cx+off_x, oy+cz+off_y, h + 0.12))
                vl_obj.hide_select = True
                vl_obj.lock_location = vl_obj.lock_rotation = vl_obj.lock_scale = (True, True, True)
                case_col.objects.link(vl_obj)
                if vl_obj.name in context.scene.collection.objects:
                    context.scene.collection.objects.unlink(vl_obj)

        # ── 参考 Mesh（双线性插值高度场，挂在 Ref 根节点下）────────────────
        mesh_root = _ensure_col(REF_MESH_COL, parent=ref_root)
        MAT_REF   = _ensure_mat("mq_ref_mesh", (0.05, 0.35, 1.00), strength=0.7, alpha=0.7)
        sub       = 8
        flat      = 0.25  # 固定平台边距

        for n, ci in enumerate(ALL_CASES):
            col_n = n % GRID_COLS
            row_n = n // GRID_COLS
            ox    = col_n * 2.0
            oy    = row_n * 2.0
            h = DIAGONAL2_HEIGHTS.get(ci, [1.0 if (ci & (1 << i)) else 0.0 for i in range(4)])

            bm2 = bmesh.new()
            gv  = []
            for row in range(sub + 1):
                r = []
                for col in range(sub + 1):
                    u  = col / sub
                    v  = row / sub
                    # 参考 mesh：arc_s=0（硬边缘），先 arc UV 再插值 → 正方形平台
                    hz = bilinear_arc(u, v, h, 0.0, flat)
                    r.append(bm2.verts.new(Vector((ox+u, oy+v, hz))))
                gv.append(r)
            for row in range(sub):
                for col in range(sub):
                    bm2.faces.new([gv[row][col], gv[row][col+1],
                                   gv[row+1][col+1], gv[row+1][col]])
            bm2.normal_update()
            ref_mesh = bpy.data.meshes.new(f"mq_ref_{ci}")
            bm2.to_mesh(ref_mesh); bm2.free()
            ref_mesh.materials.append(MAT_REF)

            rm_col = bpy.data.collections.new(f"case_{ci}")
            mesh_root.children.link(rm_col)
            if rm_col.name in context.scene.collection.children:
                context.scene.collection.children.unlink(rm_col)
            rm_obj = bpy.data.objects.new(f"mq_ref_{ci}", ref_mesh)
            rm_col.objects.link(rm_obj)
            if rm_obj.name in context.scene.collection.objects:
                context.scene.collection.objects.unlink(rm_obj)

        # 切换到材质预览
        for area in context.screen.areas:
            if area.type == 'VIEW_3D':
                for sp in area.spaces:
                    if sp.type == 'VIEW_3D':
                        sp.shading.type = 'MATERIAL'
                        break

        self.report({'INFO'}, f"MQ 参考场景已初始化：{len(ALL_CASES)} 个 case")
        return {'FINISHED'}


class MQ_OT_GenerateTerrain(bpy.types.Operator):
    """生成测试地形 MQ 网格 — 全部 canonical case 排列展示，可调细分和侧壁厚度"""
    bl_idname      = "mq.generate_terrain"
    bl_label       = "生成测试地形"
    bl_description = "生成双线性高度场地面 + 侧壁填充，对标 MC 生成圆角方块"

    def execute(self, context):
        props = context.scene.mq_props
        _remove_col(TERRAIN_COL_NAME)
        terrain_root = _ensure_col(TERRAIN_COL_NAME)

        sub        = props.subdivisions
        arc_s      = props.arc_strength
        flat       = 0.25   # 固定平台边距
        wall_depth = 0.02   # 固定厚度

        MAT_TOP  = _ensure_mat("mq_terrain_top",  (0.55, 0.35, 0.10), strength=0.85)
        MAT_WALL = _ensure_mat("mq_terrain_wall",  (0.22, 0.18, 0.08), strength=0.6)

        for n, ci in enumerate(ALL_CASES):
            col_n = n % GRID_COLS
            row_n = n // GRID_COLS
            ox = col_n * 2.0
            oy = row_n * 2.0
            h = DIAGONAL2_HEIGHTS.get(ci, [1.0 if (ci & (1 << i)) else 0.0 for i in range(4)])

            bm = bmesh.new()

            # ── 地面：先 arc UV 再插值 → 正方形平台 ──────────────────────────
            gv = []
            for row in range(sub + 1):
                r = []
                for col in range(sub + 1):
                    u  = col / sub
                    v  = row / sub
                    hz = bilinear_arc(u, v, h, arc_s, flat)
                    r.append(bm.verts.new(Vector((ox+u, oy+v, hz))))
                gv.append(r)
            for row in range(sub):
                for col in range(sub):
                    f = bm.faces.new([gv[row][col], gv[row][col+1],
                                      gv[row+1][col+1], gv[row+1][col]])
                    f.material_index = 0

            # ── 侧壁（沿 quad 四边向下延伸）─────────────────────────────────
            if wall_depth > 0:
                edge_segs = [
                    ([(col, 0)        for col in range(sub + 1)], False),  # 前边 v=0
                    ([(col, sub)      for col in range(sub + 1)], False),  # 后边 v=sub
                    ([(0,   row)      for row in range(sub + 1)], True),   # 左边 u=0
                    ([(sub, row)      for row in range(sub + 1)], True),   # 右边 u=sub
                ]
                for seg_indices, is_col_edge in edge_segs:
                    for k in range(len(seg_indices) - 1):
                        c0, r0 = seg_indices[k]
                        c1, r1 = seg_indices[k + 1]
                        vt0 = gv[r0][c0]
                        vt1 = gv[r1][c1]
                        vb0 = bm.verts.new(Vector((vt0.co.x, vt0.co.y, vt0.co.z - wall_depth)))
                        vb1 = bm.verts.new(Vector((vt1.co.x, vt1.co.y, vt1.co.z - wall_depth)))
                        f = bm.faces.new([vt0, vt1, vb1, vb0])
                        f.material_index = 1

            bm.normal_update()
            mesh = bpy.data.meshes.new(f"mq_terrain_{ci}")
            bm.to_mesh(mesh); bm.free()
            mesh.materials.append(MAT_TOP)
            mesh.materials.append(MAT_WALL)
            for poly in mesh.polygons:
                poly.use_smooth = True
            mesh.update()

            case_col = bpy.data.collections.new(f"case_{ci}")
            terrain_root.children.link(case_col)
            if case_col.name in context.scene.collection.children:
                context.scene.collection.children.unlink(case_col)
            obj = bpy.data.objects.new(f"mq_terrain_{ci}", mesh)
            case_col.objects.link(obj)
            if obj.name in context.scene.collection.objects:
                context.scene.collection.objects.unlink(obj)

        self.report({'INFO'}, f"已生成 {len(ALL_CASES)} 个 case 测试地形 → {TERRAIN_COL_NAME}")
        return {'FINISHED'}


class MQ_OT_ValidateMesh(bpy.types.Operator):
    """检查当前选中 mesh 是否在 [0,1]×[0,1] XZ 范围内"""
    bl_idname = "mq.validate_mesh"
    bl_label  = "验证网格"

    def execute(self, context):
        obj = context.active_object
        if obj is None or obj.type != 'MESH':
            self.report({'ERROR'}, "请先选中一个 Mesh 对象。")
            return {'CANCELLED'}

        issues = []
        tol    = 0.001
        for v in obj.data.vertices:
            p = obj.matrix_world @ v.co
            if not (-tol <= p.x <= 1 + tol):
                issues.append(f"顶点 X={p.x:.3f} 超出 [0,1]")
            if not (-tol <= p.z <= 1 + tol):
                issues.append(f"顶点 Z={p.z:.3f} 超出 [0,1]")

        if issues:
            self.report({'WARNING'}, "问题：" + " | ".join(issues[:5]))
        else:
            self.report({'INFO'}, f"OK – {len(obj.data.vertices)} 个顶点，全部在 [0,1]×[0,1] XZ 范围内。")
        return {'FINISHED'}


class MQ_OT_ExportAllCases(bpy.types.Operator):
    """批量导出全部 canonical case，以 V0(BL) 为原点，坐标系与 MC 一致"""
    bl_idname = "mq.export_all_cases"
    bl_label  = "批量导出全部 FBX"

    def execute(self, context):
        import os
        props   = context.scene.mq_props
        out_dir = bpy.path.abspath(props.export_dir)
        sub     = props.subdivisions
        arc_s = props.arc_strength
        flat  = 0.25  # 固定平台边距

        if not out_dir:
            self.report({'ERROR'}, "请先设置导出目录。")
            return {'CANCELLED'}

        os.makedirs(out_dir, exist_ok=True)

        # ── 法线贴图烘焙：所有 case 共享同一 noise_field（保证边界连续）─────────
        noise_field = None
        bake_res    = props.normal_map_resolution
        if props.bake_normal_maps:
            try:
                from . import noise as _noise_mod
            except ImportError:
                self.report({'ERROR'}, "noise.py 缺失，无法烘焙法线贴图。")
                return {'CANCELLED'}
            noise_field = _noise_mod.TileableNoiseField(
                seed      = props.noise_seed,
                octaves   = props.noise_octaves,
                amplitude = props.noise_amplitude,
                frequency = props.noise_frequency,
            )

        exported = []
        baked    = 0
        for ci in ALL_CASES:
            h = DIAGONAL2_HEIGHTS.get(ci, [1.0 if (ci & (1 << i)) else 0.0 for i in range(4)])

            # 重新生成 mesh，V0(BL) 固定在原点 (0,0,0)
            bm = bmesh.new()
            gv = []
            for row in range(sub + 1):
                r = []
                for col in range(sub + 1):
                    u  = col / sub
                    v  = row / sub
                    hz = bilinear_arc(u, v, h, arc_s, flat)
                    r.append(bm.verts.new(Vector((u, v, hz))))
                gv.append(r)
            for row in range(sub):
                for col in range(sub):
                    bm.faces.new([gv[row][col],   gv[row][col+1],
                                  gv[row+1][col+1], gv[row+1][col]])

            # 平面 UV（U=X，V=Y），与顶点坐标 (u,v,height) 对应
            uv_layer = bm.loops.layers.uv.new("UVMap")
            for face in bm.faces:
                for loop in face.loops:
                    loop[uv_layer].uv = (loop.vert.co.x, loop.vert.co.y)
            bm.normal_update()

            mesh = bpy.data.meshes.new(f"_exp_mq_{ci}")
            bm.to_mesh(mesh); bm.free()
            for p in mesh.polygons:
                p.use_smooth = True
            mesh.update()

            exp_obj = bpy.data.objects.new(f"mq_case_{ci}", mesh)
            context.scene.collection.objects.link(exp_obj)

            bpy.ops.object.select_all(action='DESELECT')
            exp_obj.select_set(True)
            context.view_layer.objects.active = exp_obj

            # 与 MC 插件保持相同导出设置
            filepath = os.path.join(out_dir, f"mq_case_{ci}.fbx")
            bpy.ops.export_scene.fbx(
                filepath             = filepath,
                use_selection        = True,
                axis_forward         = 'Y',
                axis_up              = 'Z',
                apply_scale_options  = 'FBX_SCALE_ALL',
                use_mesh_modifiers   = True,
                mesh_smooth_type     = 'FACE',
                add_leaf_bones       = False,
            )

            # ── 烘焙法线贴图（与 .fbx 同目录、同名后缀 _normal.png）──────────
            if noise_field is not None:
                from . import noise as _noise_mod
                pixels = _noise_mod.bake_normal_map(mesh, noise_field, bake_res)
                img_name = f"mq_case_{ci}_normal"
                # 若同名 image 已存在（重导出场景），先清理避免引用泄漏
                if img_name in bpy.data.images:
                    bpy.data.images.remove(bpy.data.images[img_name])
                img = bpy.data.images.new(img_name, bake_res, bake_res, alpha=True)
                # Blender image.pixels: row-major, row 0 = bottom (Y up)，与 numpy bake 输出一致
                # numpy uint8 → float32 ∈ [0, 1]
                img.pixels = (pixels.astype('float32') / 255.0).flatten()
                img.filepath_raw = os.path.join(out_dir, f"{img_name}.png")
                img.file_format  = 'PNG'
                img.save()
                bpy.data.images.remove(img)
                baked += 1

            context.scene.collection.objects.unlink(exp_obj)
            bpy.data.objects.remove(exp_obj)
            bpy.data.meshes.remove(mesh)
            exported.append(ci)

        bpy.ops.object.select_all(action='DESELECT')
        bake_msg = f" + {baked} normal maps" if noise_field is not None else ""
        self.report({'INFO'}, f"已导出 {len(exported)} 个 case{bake_msg} → {out_dir}")
        return {'FINISHED'}


# ── Properties ────────────────────────────────────────────────────────────────

class MQProperties(bpy.types.PropertyGroup):
    case_index: bpy.props.EnumProperty(
        name    = "Case",
        items   = [(str(c), f"Case {c}：{CASE_NAMES.get(c, '')}", "") for c in ALL_CASES],
        default = '0',
    )
    subdivisions: bpy.props.IntProperty(
        name="细分数", default=8, min=1, max=32,
        description="地形 mesh 每方向细分段数，越高越平滑"
    )
    arc_strength: bpy.props.FloatProperty(
        name="圆弧强度", default=0.8, min=0.0, max=1.0, step=5,
        description="坡面圆弧程度：0 = 线性斜面，1 = 完整 smooth-step 圆弧"
    )
    export_dir: bpy.props.StringProperty(
        name    = "导出目录",
        subtype = 'DIR_PATH',
        default = "//mq_export/",
        description="地形和悬崖 FBX 统一输出目录"
    )
    # ── 法线贴图烘焙参数（仅地形 19 case 使用，与 .fbx 同 operator 同目录导出）─
    bake_normal_maps: bpy.props.BoolProperty(
        name="烘焙法线贴图",
        default=True,
        description="导出地形 .fbx 同时烘焙 mq_case_N_normal.png（tileable 3D fbm noise）",
    )
    noise_seed: bpy.props.IntProperty(
        name="噪声种子", default=42,
        description="伪随机种子，固定后结果可复现",
    )
    noise_octaves: bpy.props.IntProperty(
        name="噪声 Octaves", default=3, min=1, max=8,
        description="fbm 叠加层数，越多细节越丰富但越慢",
    )
    noise_amplitude: bpy.props.FloatProperty(
        name="噪声振幅衰减", default=0.5, min=0.0, max=1.0, step=5,
        description="高频 octave 的振幅衰减系数（典型 0.5）",
    )
    noise_frequency: bpy.props.IntProperty(
        name="噪声基频", default=4, min=1, max=16,
        description="基础周期分割数（必须整数，每 octave 周期 = freq×2^k 保证 tileable）",
    )
    normal_map_resolution: bpy.props.IntProperty(
        name="法线贴图分辨率", default=128, min=64, max=512,
        description="烘焙输出 PNG 像素尺寸（正方形）",
    )


# ── Panel ─────────────────────────────────────────────────────────────────────

class MQ_PT_Panel(bpy.types.Panel):
    bl_label       = "MQ 地形格"
    bl_idname      = "MQ_PT_panel"
    bl_space_type  = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category    = "建造美术网格"

    def draw(self, context):
        layout = self.layout
        props  = context.scene.mq_props

        # ── 导出目录 + 圆弧强度（全局参数）─────────────────────────────────
        layout.prop(props, "export_dir",  text="导出目录")
        layout.prop(props, "arc_strength")
        layout.separator()

        # 1. 初始化场景（地形参考 + 悬崖参考）
        box1 = layout.box()
        box1.label(text="1. 初始化参考场景", icon='SCENE_DATA')
        row1 = box1.row(align=True)
        row1.operator("mq.setup_all_cases",   text="地形",   icon='MESH_GRID')
        row1.operator("mq.setup_cliff_cases",  text="悬崖",  icon='MESH_CUBE')

        layout.separator()

        # 2. 测试地形（圆滑预览）
        box2 = layout.box()
        box2.label(text="2. 生成测试地形", icon='MESH_PLANE')
        box2.prop(props, "subdivisions")
        box2.operator("mq.generate_terrain", icon='MESH_UVSPHERE')

        layout.separator()

        # 3. 验证
        box3 = layout.box()
        box3.label(text="3. 验证网格", icon='VIEWZOOM')
        box3.operator("mq.validate_mesh", icon='CHECKMARK')

        layout.separator()

        # 4. 一键导出全部（地形 19 + 悬崖 5）
        box4 = layout.box()
        box4.label(text="4. 导出全部 FBX", icon='EXPORT')
        box4.operator("mq.export_all", icon='EXPORT')
        box4.label(text="mq_case_0..18.fbx + mq_cliff_1/3/5/7/15.fbx", icon='INFO')

        # 5. 法线贴图烘焙参数（与地形 .fbx 同 operator 同目录导出）
        box5 = layout.box()
        box5.label(text="5. 法线贴图烘焙（仅地形 19 case）", icon='TEXTURE')
        box5.prop(props, "bake_normal_maps")
        sub = box5.column()
        sub.enabled = props.bake_normal_maps
        sub.prop(props, "noise_seed")
        sub.prop(props, "noise_octaves")
        sub.prop(props, "noise_amplitude")
        sub.prop(props, "noise_frequency")
        sub.prop(props, "normal_map_resolution")
        box5.label(text="mq_case_N_normal.png 与 .fbx 同目录", icon='INFO')


# ── Cliff operators ───────────────────────────────────────────────────────────
#
# 悬崖 case bit mask（与地形 tile 边约定一致）：
#   bit 0 = E0 南边（Blender Y=-0.5，Unity -Z）
#   bit 1 = E1 东边（Blender X=+0.5，Unity +X）
#   bit 2 = E2 北边（Blender Y=+0.5，Unity +Z）
#   bit 3 = E3 西边（Blender X=-0.5，Unity -X）
#
# Mesh 以格子中心为原点（XY∈[-0.5,0.5]，Z∈[0,1]），
# 导出后 Unity 旋转 Euler(0, 90*rotCount, 0) 派生所有 16 个 case。
#
# 规范 case（只需导出这 5 个 FBX）：1 3 5 7 15

CLIFF_CANONICAL = [1, 3, 5, 7, 15]

CLIFF_CASE_NAMES = {
    0: "无悬崖",
    1: "南墙",      2: "东墙",      4: "北墙",      8: "西墙",
    3: "南+东角",   6: "东+北角",  12: "北+西角",   9: "西+南角",
    5: "南北对穿", 10: "东西对穿",
    7: "南+东+北", 11: "南+东+西", 13: "南+北+西", 14: "东+北+西",
    15: "四面（孤岛）",
}

# 各边的两端顶点在以格子中心为原点的坐标系中（Blender XY，Z=高度）
# 顶点顺序保证法线向外（面对低侧）
_CLIFF_WALLS = [
    ((-0.5, -0.5), (0.5, -0.5)),   # E0 南，法线 -Y
    ((0.5,  -0.5), (0.5,  0.5)),   # E1 东，法线 +X
    ((0.5,   0.5), (-0.5, 0.5)),   # E2 北，法线 +Y
    ((-0.5,  0.5), (-0.5,-0.5)),   # E3 西，法线 -X
]

CLIFF_COL_NAME = "MQ_ArtMesh_Cliff"


def _build_cliff_bm(ci):
    """生成 cliff case 的 bmesh（垂直墙面，Z 高度 0-1，XY 中心在原点）"""
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for bit, ((x0, y0), (x1, y1)) in enumerate(_CLIFF_WALLS):
        if not (ci & (1 << bit)):
            continue
        v00 = bm.verts.new((x0, y0, 0.0))
        v10 = bm.verts.new((x1, y1, 0.0))
        v11 = bm.verts.new((x1, y1, 1.0))
        v01 = bm.verts.new((x0, y0, 1.0))
        face = bm.faces.new([v00, v10, v11, v01])
        for loop, uv in zip(face.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]):
            loop[uv_layer].uv = uv
    bm.normal_update()
    return bm


class MQ_OT_ExportAll(bpy.types.Operator):
    """一键导出所有地形 case（19 个）+ 悬崖规范 case（5 个）"""
    bl_idname = "mq.export_all"
    bl_label  = "导出全部（地形 19 + 悬崖 5）"

    def execute(self, context):
        r1 = bpy.ops.mq.export_all_cases()
        r2 = bpy.ops.mq.export_cliff_cases()
        if 'FINISHED' in r1 and 'FINISHED' in r2:
            self.report({'INFO'}, "全部导出完成：地形 19 个 + 悬崖 5 个")
        return {'FINISHED'}


class MQ_OT_SetupCliffCases(bpy.types.Operator):
    """生成 5 个规范悬崖 case 的参考 mesh（垂直墙面，中心为原点）"""
    bl_idname = "mq.setup_cliff_cases"
    bl_label  = "初始化悬崖参考场景"

    def execute(self, context):
        if CLIFF_COL_NAME in bpy.data.collections:
            bpy.data.collections.remove(bpy.data.collections[CLIFF_COL_NAME], do_unlink=True)

        root_col = _ensure_col(CLIFF_COL_NAME)
        MAT_CLIFF = _ensure_mat("mq_cliff_wall", (0.55, 0.25, 0.05), strength=0.9)

        for n, ci in enumerate(CLIFF_CANONICAL):
            ox = (n % 4) * 2.0
            oy = (n // 4) * 2.0

            case_col = bpy.data.collections.new(f"cliff_{ci}")
            root_col.children.link(case_col)
            if case_col.name in context.scene.collection.children:
                context.scene.collection.children.unlink(case_col)

            # 标签
            lbl = bpy.data.curves.new(f"_lbl_cliff_{ci}", type='FONT')
            lbl.body = f"cliff ci={ci}  {CLIFF_CASE_NAMES.get(ci,'')}"
            lbl.size = 0.18
            lbl_obj = bpy.data.objects.new(f"_lbl_cliff_{ci}", lbl)
            lbl_obj.location = Vector((ox - 0.5, oy - 0.6, 1.2))
            lbl_obj.hide_select = True
            lbl_obj.lock_location = lbl_obj.lock_rotation = lbl_obj.lock_scale = (True, True, True)
            case_col.objects.link(lbl_obj)
            if lbl_obj.name in context.scene.collection.objects:
                context.scene.collection.objects.unlink(lbl_obj)

            # Cliff mesh（以原点为中心，偏移到网格位置）
            bm = _build_cliff_bm(ci)
            for v in bm.verts:
                v.co.x += ox + 0.5
                v.co.y += oy + 0.5
            mesh = bpy.data.meshes.new(f"cliff_ref_{ci}")
            bm.to_mesh(mesh); bm.free()
            mesh.materials.append(MAT_CLIFF)
            obj = bpy.data.objects.new(f"cliff_ref_{ci}", mesh)
            case_col.objects.link(obj)
            if obj.name in context.scene.collection.objects:
                context.scene.collection.objects.unlink(obj)

        self.report({'INFO'}, f"悬崖参考场景已生成：{len(CLIFF_CANONICAL)} 个规范 case")
        return {'FINISHED'}


class MQ_OT_ExportCliffCases(bpy.types.Operator):
    """导出 5 个规范悬崖 case FBX（mq_cliff_1/3/5/7/15.fbx，Mesh 中心为格子原点）"""
    bl_idname = "mq.export_cliff_cases"
    bl_label  = "批量导出悬崖 FBX"

    def execute(self, context):
        import os
        props   = context.scene.mq_props
        out_dir = bpy.path.abspath(props.export_dir)
        if not out_dir:
            self.report({'ERROR'}, "请先设置悬崖导出目录。")
            return {'CANCELLED'}
        os.makedirs(out_dir, exist_ok=True)

        exported = []
        for ci in CLIFF_CANONICAL:
            bm = _build_cliff_bm(ci)
            mesh = bpy.data.meshes.new(f"_exp_cliff_{ci}")
            bm.to_mesh(mesh); bm.free()
            mesh.update()

            exp_obj = bpy.data.objects.new(f"mq_cliff_{ci}", mesh)
            context.scene.collection.objects.link(exp_obj)
            bpy.ops.object.select_all(action='DESELECT')
            exp_obj.select_set(True)
            context.view_layer.objects.active = exp_obj

            filepath = os.path.join(out_dir, f"mq_cliff_{ci}.fbx")
            bpy.ops.export_scene.fbx(
                filepath            = filepath,
                use_selection       = True,
                axis_forward        = 'Y',
                axis_up             = 'Z',
                apply_scale_options = 'FBX_SCALE_ALL',
                use_mesh_modifiers  = True,
                mesh_smooth_type    = 'FACE',
                add_leaf_bones      = False,
            )
            context.scene.collection.objects.unlink(exp_obj)
            bpy.data.objects.remove(exp_obj)
            bpy.data.meshes.remove(mesh)
            exported.append(ci)

        bpy.ops.object.select_all(action='DESELECT')
        self.report({'INFO'}, f"已导出 {len(exported)} 个规范悬崖 case → {out_dir}")
        return {'FINISHED'}


# ── Registration ──────────────────────────────────────────────────────────────

_MQ_CLASSES = [
    MQProperties,
    MQ_OT_ExportAll,
    MQ_OT_SetupAllCases,
    MQ_OT_GenerateTerrain,
    MQ_OT_ValidateMesh,
    MQ_OT_ExportAllCases,
    MQ_OT_SetupCliffCases,
    MQ_OT_ExportCliffCases,
    MQ_PT_Panel,
]
