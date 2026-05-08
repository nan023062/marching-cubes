import bpy
import bmesh
from mathutils import Vector

# ── Case data ─────────────────────────────────────────────────────────────────
#
# 16 cases for 1-level height difference.
# Bit mask: bit0=V0(BL), bit1=V1(BR), bit2=V2(TR), bit3=V3(TL)
# 0 = base height (0), 1 = elevated height (1 unit)
#
# Corner world positions (unit quad, Y-up):
#   V3(0,_,1) ─── V2(1,_,1)
#       │               │
#   V0(0,_,0) ─── V1(1,_,0)
#
# 6 canonical cases: 0, 1, 3, 5, 7, 15

CORNER_XZ = [
    (0.0, 0.0),  # V0 BL
    (1.0, 0.0),  # V1 BR
    (1.0, 1.0),  # V2 TR
    (0.0, 1.0),  # V3 TL
]

CANONICAL_CASES = [0, 1, 3, 5, 7, 15]

CASE_NAMES = {
    0:  "0000 – Flat (all base)",
    1:  "0001 – V0(BL) high",
    3:  "0011 – V0+V1 (bottom edge) high",
    5:  "0101 – V0+V2 (diagonal) high",
    7:  "0111 – V0+V1+V2 high",
    15: "1111 – Flat (all elevated)",
}

# ── Operators ─────────────────────────────────────────────────────────────────

class MQ_OT_GenerateReference(bpy.types.Operator):
    """Generate a reference mesh scaffold for the selected canonical case."""
    bl_idname  = "mq.generate_reference"
    bl_label   = "Generate Reference Scaffold"
    bl_options = {'REGISTER', 'UNDO'}

    case_index: bpy.props.IntProperty(name="Case Index", default=0)

    def execute(self, context):
        ci = self.case_index
        name = f"mq_case_{ci}_ref"

        # Remove existing reference with same name
        if name in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)

        mesh = bpy.data.meshes.new(name)
        obj  = bpy.data.objects.new(name, mesh)
        bm   = bmesh.new()

        base_h = 0.0
        high_h = 1.0

        # Create corner vertices at correct heights
        verts = []
        for i, (x, z) in enumerate(CORNER_XZ):
            h = high_h if (ci & (1 << i)) else base_h
            verts.append(bm.verts.new(Vector((x, h, z))))

        # Create quad face (reference only – artist will model over this)
        bm.faces.new(verts)
        bm.to_mesh(mesh)
        bm.free()

        context.collection.objects.link(obj)

        # Wireframe display so artist can see through it
        obj.display_type = 'WIRE'

        # Select the new object
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        context.view_layer.objects.active = obj

        self.report({'INFO'}, f"Reference scaffold created: {name}")
        return {'FINISHED'}


class MQ_OT_ShowCornerLabels(bpy.types.Operator):
    """Overlay corner position markers for the active case."""
    bl_idname  = "mq.show_corner_labels"
    bl_label   = "Show Corner Markers"
    bl_options = {'REGISTER', 'UNDO'}

    case_index: bpy.props.IntProperty(name="Case Index", default=0)

    def execute(self, context):
        ci = self.case_index
        col = bpy.data.collections.get("MQ_CornerMarkers")
        if col is None:
            col = bpy.data.collections.new("MQ_CornerMarkers")
            context.scene.collection.children.link(col)

        # Remove old markers
        for obj in list(col.objects):
            bpy.data.objects.remove(obj, do_unlink=True)

        corner_names = ["V0_BL", "V1_BR", "V2_TR", "V3_TL"]
        for i, (x, z) in enumerate(CORNER_XZ):
            h    = 1.0 if (ci & (1 << i)) else 0.0
            high = (ci & (1 << i)) != 0

            # Empty as marker
            empty = bpy.data.objects.new(corner_names[i], None)
            empty.empty_display_type  = 'SPHERE'
            empty.empty_display_size  = 0.06
            empty.location            = Vector((x, h, z))

            # Color: orange=high, grey=low
            if bpy.context.preferences.themes:
                pass  # colour is set via object color below
            col.objects.link(empty)
            empty.color = (1.0, 0.4, 0.1, 1.0) if high else (0.5, 0.5, 0.5, 1.0)

        self.report({'INFO'}, f"Corner markers set for case {ci}")
        return {'FINISHED'}


class MQ_OT_ValidateMesh(bpy.types.Operator):
    """Check that the active mesh fits within the unit quad [0,1]×[0,1] and reports corner alignment."""
    bl_idname = "mq.validate_mesh"
    bl_label  = "Validate Mesh"

    def execute(self, context):
        obj = context.active_object
        if obj is None or obj.type != 'MESH':
            self.report({'ERROR'}, "Select a mesh object first.")
            return {'CANCELLED'}

        mesh = obj.data
        issues = []
        tol = 0.001

        for v in mesh.vertices:
            p = obj.matrix_world @ v.co
            if not (-tol <= p.x <= 1 + tol):
                issues.append(f"Vertex X={p.x:.3f} out of [0,1]")
            if not (-tol <= p.z <= 1 + tol):
                issues.append(f"Vertex Z={p.z:.3f} out of [0,1]")

        if issues:
            self.report({'WARNING'}, "Issues: " + " | ".join(issues[:5]))
        else:
            self.report({'INFO'}, f"OK – {len(mesh.vertices)} vertices, all within [0,1]×[0,1] XZ.")

        return {'FINISHED'}


class MQ_OT_ExportCase(bpy.types.Operator):
    """Export active object as mq_case_N.fbx to the chosen directory."""
    bl_idname = "mq.export_case"
    bl_label  = "Export as FBX"

    def execute(self, context):
        props    = context.scene.mq_props
        ci       = props.case_index
        out_dir  = bpy.path.abspath(props.export_dir)

        if not out_dir:
            self.report({'ERROR'}, "Set Export Directory first.")
            return {'CANCELLED'}

        import os
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
        self.report({'INFO'}, f"Exported → {filepath}")
        return {'FINISHED'}


# ── Properties ────────────────────────────────────────────────────────────────

class MQProperties(bpy.types.PropertyGroup):
    case_index: bpy.props.EnumProperty(
        name  = "Canonical Case",
        items = [(str(c), f"Case {c}: {CASE_NAMES.get(c, '')}", "") for c in CANONICAL_CASES],
        default = '0',
    )
    export_dir: bpy.props.StringProperty(
        name    = "Export Directory",
        subtype = 'DIR_PATH',
        default = "//mq_cases/",
    )


# ── Panel ─────────────────────────────────────────────────────────────────────

class MQ_PT_Panel(bpy.types.Panel):
    bl_label       = "MQ ArtMesh"
    bl_idname      = "MQ_PT_panel"
    bl_space_type  = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category    = "MQ ArtMesh"

    def draw(self, context):
        layout = self.layout
        props  = context.scene.mq_props
        ci     = int(props.case_index)

        # ── Case selector ─────────────────────────────────────────────────────
        box = layout.box()
        box.label(text="Canonical Case", icon='GRID')
        box.prop(props, "case_index", text="")

        # Corner state display
        col = box.column(align=True)
        col.label(text="Corner heights  (L=base  H=+1):")
        row = col.row(align=True)
        corners = ["V3(TL)", "V2(TR)"]
        for i, name in zip([3, 2], corners):
            h = (ci & (1 << i)) != 0
            row.label(text=f"{name}={'H' if h else 'L'}",
                      icon='LAYER_ACTIVE' if h else 'LAYER_USED')
        row = col.row(align=True)
        for i, name in zip([0, 1], ["V0(BL)", "V1(BR)"]):
            h = (ci & (1 << i)) != 0
            row.label(text=f"{name}={'H' if h else 'L'}",
                      icon='LAYER_ACTIVE' if h else 'LAYER_USED')

        # ── Tools ─────────────────────────────────────────────────────────────
        box2 = layout.box()
        box2.label(text="Tools", icon='TOOL_SETTINGS')

        op = box2.operator("mq.generate_reference", text="Generate Reference Scaffold")
        op.case_index = ci

        op2 = box2.operator("mq.show_corner_labels", text="Show Corner Markers")
        op2.case_index = ci

        box2.operator("mq.validate_mesh", text="Validate Mesh")

        # ── Guide ─────────────────────────────────────────────────────────────
        box3 = layout.box()
        box3.label(text="Mesh Guide", icon='INFO')
        box3.label(text="• Unit quad: X=[0,1], Z=[0,1]")
        box3.label(text="• Low corners: Y = 0")
        box3.label(text="• High corners: Y = 1")
        box3.label(text="• Origin at (0,0,0) = V0/BL")
        box3.label(text="• Seam verts must be exact")

        # ── Export ────────────────────────────────────────────────────────────
        box4 = layout.box()
        box4.label(text="Export", icon='EXPORT')
        box4.prop(props, "export_dir", text="Dir")
        box4.operator("mq.export_case", text=f"Export  mq_case_{ci}.fbx")

        # ── All canonical cases overview ───────────────────────────────────────
        box5 = layout.box()
        box5.label(text="All 6 Canonical Cases", icon='LINENUMBERS_ON')
        for c in CANONICAL_CASES:
            row = box5.row()
            name = CASE_NAMES.get(c, f"Case {c}")
            corners_str = "".join(
                ('H' if (c & (1 << i)) else 'L') for i in range(4))
            done = "✓" if any(
                obj.name == f"mq_case_{c}" for obj in bpy.data.objects) else "·"
            row.label(text=f"{done} [{corners_str}]  Case {c}: {name.split('–')[-1].strip()}")


# ── Registration ──────────────────────────────────────────────────────────────

_MQ_CLASSES = [
    MQProperties,
    MQ_OT_GenerateReference,
    MQ_OT_ShowCornerLabels,
    MQ_OT_ValidateMesh,
    MQ_OT_ExportCase,
    MQ_PT_Panel,
]
