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

CANONICAL_CASES = [0, 1, 3, 5, 7, 15]

CASE_NAMES = {
    0:  "0000 – 全平（基准高度）",
    1:  "0001 – V0(左下) 高",
    3:  "0011 – V0+V1（底边）高",
    5:  "0101 – V0+V2（对角）高",
    7:  "0111 – V0+V1+V2 高",
    15: "1111 – 全平（高位）",
}

REF_COL_NAME   = "MQ_ArtMesh_Ref"
CTRL_COL_NAME  = "MQ_Ctrl"
MESH_COL_NAME  = "MQ_ArtMesh_Meshes"
GRID_COLS      = 3

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
    """生成所有 6 个 canonical case 的参考线框、角点球和顶点编号标签"""
    bl_idname = "mq.setup_all_cases"
    bl_label  = "初始化全部 Case 参考场景"

    def execute(self, context):
        _remove_col(REF_COL_NAME)

        MAT_HIGH = _ensure_mat("mq_high", (1.00, 0.40, 0.10))  # 橙：高位
        MAT_LOW  = _ensure_mat("mq_low",  (0.35, 0.35, 0.35))  # 灰：低位
        MAT_WIRE = _ensure_mat("mq_wire", (0.10, 0.65, 0.15))  # 绿：线框

        ref_root  = _ensure_col(REF_COL_NAME)
        ctrl_root = _ensure_col(CTRL_COL_NAME, parent=ref_root)

        for n, ci in enumerate(CANONICAL_CASES):
            col_n = n % GRID_COLS
            row_n = n // GRID_COLS
            # Blender Z-up：水平面 = XY，高度 = Z
            # ox/oy 控制网格排列偏移，高度 h 映射到 Blender Z
            ox = col_n * 2.0   # Blender X 偏移（列）
            oy = row_n * 2.0   # Blender Y 偏移（行）

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

        # 切换到材质预览
        for area in context.screen.areas:
            if area.type == 'VIEW_3D':
                for sp in area.spaces:
                    if sp.type == 'VIEW_3D':
                        sp.shading.type = 'MATERIAL'
                        break

        self.report({'INFO'}, f"MQ 参考场景已初始化：{len(CANONICAL_CASES)} 个 canonical case")
        return {'FINISHED'}


class MQ_OT_GenerateMeshes(bpy.types.Operator):
    """为所有 6 个 canonical case 生成双线性插值的高度场参考 mesh（可编辑）"""
    bl_idname = "mq.generate_meshes"
    bl_label  = "生成 Case 参考 Mesh"

    subdivisions: bpy.props.IntProperty(
        name="细分数", default=8, min=1, max=32,
        description="每个方向的细分段数，越高越平滑"
    )

    def execute(self, context):
        _remove_col(MESH_COL_NAME)
        mesh_root = _ensure_col(MESH_COL_NAME)

        MAT_MESH = _ensure_mat("mq_mesh_surface", (0.05, 0.35, 1.00), strength=0.8, alpha=0.7)

        for n, ci in enumerate(CANONICAL_CASES):
            col_n = n % GRID_COLS
            row_n = n // GRID_COLS
            ox = col_n * 2.0
            oy = row_n * 2.0

            # 四角高度（Blender Z）
            h = [1.0 if (ci & (1 << i)) else 0.0 for i in range(4)]
            # h[0]=V0(BL), h[1]=V1(BR), h[2]=V2(TR), h[3]=V3(TL)
            # 双线性插值：corner_xz = [(0,0),(1,0),(1,1),(0,1)]
            # 在 u∈[0,1], v∈[0,1] 上：H(u,v) = (1-u)(1-v)*h0 + u(1-v)*h1 + u*v*h2 + (1-u)*v*h3

            sub = self.subdivisions
            bm  = bmesh.new()
            grid_verts = []
            for row in range(sub + 1):
                v_row = []
                for col in range(sub + 1):
                    u = col / sub
                    v = row / sub
                    hz = ((1-u)*(1-v)*h[0] + u*(1-v)*h[1] +
                          u*v*h[2] + (1-u)*v*h[3])
                    v_row.append(bm.verts.new(Vector((ox + u, oy + v, hz))))
                grid_verts.append(v_row)

            for row in range(sub):
                for col in range(sub):
                    v00 = grid_verts[row][col]
                    v10 = grid_verts[row][col + 1]
                    v01 = grid_verts[row + 1][col]
                    v11 = grid_verts[row + 1][col + 1]
                    bm.faces.new([v00, v10, v11, v01])

            bm.normal_update()
            mesh = bpy.data.meshes.new(f"mq_mesh_{ci}")
            bm.to_mesh(mesh); bm.free()
            mesh.materials.append(MAT_MESH)

            case_col = bpy.data.collections.new(f"case_{ci}")
            mesh_root.children.link(case_col)
            if case_col.name in context.scene.collection.children:
                context.scene.collection.children.unlink(case_col)

            obj = bpy.data.objects.new(f"mq_mesh_{ci}", mesh)
            case_col.objects.link(obj)
            if obj.name in context.scene.collection.objects:
                context.scene.collection.objects.unlink(obj)

        self.report({'INFO'}, f"已生成 {len(CANONICAL_CASES)} 个 case 参考 mesh → {MESH_COL_NAME}")
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


class MQ_OT_ExportCase(bpy.types.Operator):
    """将当前选中对象导出为 mq_case_N.fbx"""
    bl_idname = "mq.export_case"
    bl_label  = "导出 FBX"

    def execute(self, context):
        import os
        props   = context.scene.mq_props
        ci      = int(props.case_index)
        out_dir = bpy.path.abspath(props.export_dir)

        if not out_dir:
            self.report({'ERROR'}, "请先设置导出目录。")
            return {'CANCELLED'}

        os.makedirs(out_dir, exist_ok=True)
        filepath = os.path.join(out_dir, f"mq_case_{ci}.fbx")

        bpy.ops.export_scene.fbx(
            filepath         = filepath,
            use_selection    = True,
            axis_forward     = 'Z',
            axis_up          = 'Y',
            apply_unit_scale = True,
            global_scale     = 1.0,
            mesh_smooth_type = 'OFF',
        )
        self.report({'INFO'}, f"已导出 → {filepath}")
        return {'FINISHED'}


# ── Properties ────────────────────────────────────────────────────────────────

class MQProperties(bpy.types.PropertyGroup):
    case_index: bpy.props.EnumProperty(
        name    = "标准 Case",
        items   = [(str(c), f"Case {c}：{CASE_NAMES.get(c, '')}", "") for c in CANONICAL_CASES],
        default = '0',
    )
    subdivisions: bpy.props.IntProperty(
        name="细分数", default=8, min=1, max=32,
        description="生成参考 Mesh 时每方向的细分段数"
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

        # 1. 参考场景
        box = layout.box()
        box.label(text="1. 参考场景", icon='MESH_GRID')
        box.operator("mq.setup_all_cases", icon='SCENE_DATA')
        box.label(text="MQ_ArtMesh_Ref / MQ_Ctrl — 线框 + 角点 + 标签", icon='INFO')

        layout.separator()

        # 2. Case Mesh
        box2m = layout.box()
        box2m.label(text="2. Case 参考 Mesh", icon='MESH_PLANE')
        box2m.prop(context.scene.mq_props, "subdivisions")
        box2m.operator("mq.generate_meshes", icon='MESH_UVSPHERE')
        box2m.label(text="MQ_ArtMesh_Meshes — 双线性高度场 mesh", icon='INFO')

        layout.separator()

        # 3. 验证
        box2 = layout.box()
        box2.label(text="3. 验证网格", icon='VIEWZOOM')
        box2.operator("mq.validate_mesh", icon='CHECKMARK')
        box2.label(text="选中建模完成的 mesh 后验证", icon='INFO')

        layout.separator()

        # 4. 导出
        box3 = layout.box()
        box3.label(text="4. 导出 FBX", icon='EXPORT')
        box3.prop(props, "case_index")
        box3.prop(props, "export_dir", text="目录")
        box3.operator("mq.export_case",
                      text=f"导出  mq_case_{ci}.fbx", icon='EXPORT')

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
        box5.label(text="6 个标准 Case 一览", icon='LINENUMBERS_ON')
        for c in CANONICAL_CASES:
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
    MQ_OT_GenerateMeshes,
    MQ_OT_ValidateMesh,
    MQ_OT_ExportCase,
    MQ_PT_Panel,
]
