bl_info = {
    "name":        "Building ArtMesh",
    "author":      "MarchingCubes Project",
    "version":     (1, 0, 0),
    "blender":     (3, 6, 0),
    "location":    "3D Viewport › N-Panel › Building ArtMesh",
    "description": "MC ArtMesh (Cube cases) + MQ ArtMesh (Quad terrain tiles) — unified building art tool",
    "category":    "Mesh",
}

from . import mc_mesh
from . import mq_mesh

_ALL_CLASSES = mc_mesh._CLASSES + mq_mesh._MQ_CLASSES


def register():
    import bpy
    for cls in _ALL_CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.mc_props       = bpy.props.PointerProperty(type=mc_mesh.MCProps)
    bpy.types.Scene.mc_cubes_props = bpy.props.PointerProperty(type=mc_mesh.MCCubesProps)
    bpy.types.Scene.mq_props       = bpy.props.PointerProperty(type=mq_mesh.MQProperties)


def unregister():
    import bpy
    for cls in reversed(_ALL_CLASSES):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.mc_props
    del bpy.types.Scene.mc_cubes_props
    del bpy.types.Scene.mq_props


if __name__ == "__main__":
    register()
