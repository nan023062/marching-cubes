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

        for n, ci in enumerate(ALL_CASES):
            col_n = n % GRID_COLS
            row_n = n // GRID_COLS
            ox = col_n * 2.0
            oy = row_n * 2.0
            h  = DIAGONAL2_HEIGHTS.get(ci, [1.0 if (ci & (1 << i)) else 0.0 for i in range(4)])

            bm2 = bmesh.new()
            gv  = []
            for row in range(sub + 1):
                r = []
                for col in range(sub + 1):
                    u  = col / sub
                    v  = row / sub
                    hz = (1-u)*(1-v)*h[0] + u*(1-v)*h[1] + u*v*h[2] + (1-u)*v*h[3]
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
        wall_depth = props.wall_depth
        arc_s      = props.arc_strength
        flat       = props.flat_margin

        def arc(t):
            """底部和顶部各保留 flat 平台，中间做 smooth-step 过渡。
            flat=0 → 退化为原始 smooth-step；flat=0.25 → 各端 25% 保持平面。"""
            if t <= flat:      return 0.0
            if t >= 1.0 - flat: return 1.0
            t2 = (t - flat) / (1.0 - 2.0 * flat)   # 重映射到 [0,1]
            t_smooth = t2 * t2 * (3.0 - 2.0 * t2)
            return t2 * (1.0 - arc_s) + t_smooth * arc_s

        MAT_TOP  = _ensure_mat("mq_terrain_top",  (0.55, 0.35, 0.10), strength=0.85)
        MAT_WALL = _ensure_mat("mq_terrain_wall",  (0.22, 0.18, 0.08), strength=0.6)

        for n, ci in enumerate(ALL_CASES):
            col_n = n % GRID_COLS
            row_n = n // GRID_COLS
            ox = col_n * 2.0
            oy = row_n * 2.0
            h  = DIAGONAL2_HEIGHTS.get(ci, [1.0 if (ci & (1 << i)) else 0.0 for i in range(4)])

            bm = bmesh.new()

            # ── 地面（双线性插值）────────────────────────────────────────────
            gv = []
            for row in range(sub + 1):
                r = []
                for col in range(sub + 1):
                    u  = col / sub
                    v  = row / sub
                    hz_lin = (1-u)*(1-v)*h[0] + u*(1-v)*h[1] + u*v*h[2] + (1-u)*v*h[3]
                    hz = arc(hz_lin)
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
        arc_s   = props.arc_strength
        flat    = props.flat_margin

        if not out_dir:
            self.report({'ERROR'}, "请先设置导出目录。")
            return {'CANCELLED'}

        os.makedirs(out_dir, exist_ok=True)

        def arc(t):
            if t <= flat:       return 0.0
            if t >= 1.0 - flat: return 1.0
            t2 = (t - flat) / (1.0 - 2.0 * flat)
            t_smooth = t2 * t2 * (3.0 - 2.0 * t2)
            return t2 * (1.0 - arc_s) + t_smooth * arc_s

        exported = []
        for ci in ALL_CASES:
            h = DIAGONAL2_HEIGHTS.get(ci, [1.0 if (ci & (1 << i)) else 0.0 for i in range(4)])
            # 对角高差=2 case 高度超出 [0,1]，arc 函数无效，改用线性插值
            use_arc = ci not in DIAGONAL2_HEIGHTS

            # 重新生成 mesh，V0(BL) 固定在原点 (0,0,0)
            bm = bmesh.new()
            gv = []
            for row in range(sub + 1):
                r = []
                for col in range(sub + 1):
                    u      = col / sub
                    v      = row / sub
                    hz_lin = (1-u)*(1-v)*h[0] + u*(1-v)*h[1] + u*v*h[2] + (1-u)*v*h[3]
                    hz = arc(hz_lin) if use_arc else hz_lin  # 对角高差=2 case 使用线性插值
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

            context.scene.collection.objects.unlink(exp_obj)
            bpy.data.objects.remove(exp_obj)
            bpy.data.meshes.remove(mesh)
            exported.append(ci)

        bpy.ops.object.select_all(action='DESELECT')
        self.report({'INFO'}, f"已导出 {len(exported)} 个 case → {out_dir}  (mq_case_0..15.fbx)")
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
    wall_depth: bpy.props.FloatProperty(
        name="侧壁深度", default=0.5, min=0.0, max=2.0, step=5,
        description="侧壁向下延伸的深度（0 = 无侧壁）"
    )
    arc_strength: bpy.props.FloatProperty(
        name="圆弧强度", default=0.8, min=0.0, max=1.0, step=5,
        description="坡面圆弧程度：0 = 线性斜面，1 = 完整 smooth-step 圆弧"
    )
    flat_margin: bpy.props.FloatProperty(
        name="平台边距", default=0.25, min=0.0, max=0.49, step=1,
        description="顶部和底部各保留平面的范围（0=无平台，0.25=各端25%保持平面）"
    )
    export_dir: bpy.props.StringProperty(
        name    = "导出目录",
        subtype = 'DIR_PATH',
        default = "//mq_cases/",
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
        ci     = int(props.case_index)

        # 1. 参考场景（线框 + 角点 + 标签 + 参考 Mesh 一键生成）
        box = layout.box()
        box.label(text="1. 参考场景", icon='MESH_GRID')
        box.operator("mq.setup_all_cases", icon='SCENE_DATA')
        box.label(text="MQ_ArtMesh_Ref / MQ_Ctrl + MQ_Meshes", icon='INFO')

        layout.separator()

        # 2. 测试地形（对标 MC 生成圆角 Cube）
        box2 = layout.box()
        box2.label(text="2. 测试地形", icon='MESH_PLANE')
        box2.prop(props, "subdivisions")
        box2.prop(props, "wall_depth")
        box2.prop(props, "arc_strength")
        box2.prop(props, "flat_margin")
        box2.operator("mq.generate_terrain", icon='MESH_UVSPHERE')
        box2.label(text="MQ_ArtMesh_Terrain — 地面 + 侧壁", icon='INFO')

        layout.separator()

        # 3. 验证
        box2 = layout.box()
        box2.label(text="3. 验证网格", icon='VIEWZOOM')
        box2.operator("mq.validate_mesh", icon='CHECKMARK')
        box2.label(text="选中建模完成的 mesh 后验证", icon='INFO')

        layout.separator()

        # 4. 批量导出
        box3 = layout.box()
        box3.label(text="4. 批量导出 FBX", icon='EXPORT')
        box3.prop(props, "export_dir", text="目录")
        box3.prop(props, "arc_strength")
        box3.prop(props, "flat_margin")
        box3.operator("mq.export_all_cases", icon='EXPORT')
        box3.label(text="导出 mq_case_0..15.fbx（含 UVMap）", icon='INFO')

        layout.separator()

        # 4. 建模规范
        box4 = layout.box()
        box4.label(text="建模规范", icon='INFO')
        box4.label(text="• 单位 quad：X=[0,1]  Z=[0,1]")
        box4.label(text="• 低角点 Y = 0  高角点 Y = 1")
        box4.label(text="• 原点 (0,0,0) = V0/BL")
        box4.label(text="• 接缝顶点必须精确对齐")

        layout.separator()

        # 5. Case 一览
        box5 = layout.box()
        box5.label(text="16 个 Case 一览", icon='LINENUMBERS_ON')
        for c in ALL_CASES:
            row = box5.row()
            bits = "".join(('H' if (c & (1 << i)) else 'L') for i in range(4))
            done = "✓" if any(obj.name == f"mq_case_{c}"
                               for obj in bpy.data.objects) else "·"
            desc = CASE_NAMES.get(c, "").split("–")[-1].strip()
            row.label(text=f"{done} [{bits}] Case {c}：{desc}")


# ── Registration ──────────────────────────────────────────────────────────────

_MQ_CLASSES = [
    MQProperties,
    MQ_OT_SetupAllCases,
    MQ_OT_GenerateTerrain,
    MQ_OT_ValidateMesh,
    MQ_OT_ExportAllCases,
    MQ_PT_Panel,
]
