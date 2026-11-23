"""Run in Blender background mode after generate_terrain.py."""
import json
from pathlib import Path
import sys

import bpy
import numpy as np


def main(data, assets):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    samples = np.load(data / "MeshSampling.npz")
    axis, elevation = samples["axis"], samples["height"]
    size = len(axis)
    extent = float(axis[-1])

    vertices = [(float(x), float(z), float(elevation[j, i]))
                for j, z in enumerate(axis) for i, x in enumerate(axis)]
    faces = []
    for j in range(size - 1):
        for i in range(size - 1):
            a = j * size + i
            b = a + size
            faces.extend(((a, a+1, b+1), (a, b+1, b)))

    mesh = bpy.data.meshes.new("GorgeRelief")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    assert not mesh.validate()
    model = bpy.data.objects.new("GorgeRelief", mesh)
    bpy.context.collection.objects.link(model)
    uvs = mesh.uv_layers.new(name="GorgeRelief")
    for poly in mesh.polygons:
        poly.use_smooth = True
    for loop in mesh.loops:
        x, z, _ = vertices[loop.vertex_index]
        uvs.data[loop.index].uv = (.5 + x/(2*extent), .5 + z/(2*extent))

    material = bpy.data.materials.new("GorgeRelief")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    image = nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(assets / "GorgeRelief_BaseColor.png"))
    image.image.pack()
    material.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = .92
    mesh.materials.append(material)

    bpy.context.scene.unit_settings.system = "METRIC"
    model.select_set(True)
    bpy.context.view_layer.objects.active = model
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(data / "GorgeRelief.blend"))
    bpy.ops.export_scene.fbx(
        filepath=str(assets / "GorgeRelief.fbx"), use_selection=True,
        axis_forward="-Z", axis_up="Y", object_types={"MESH"},
        apply_unit_scale=True, bake_space_transform=True,
        use_mesh_modifiers=False, mesh_smooth_type="FACE", add_leaf_bones=False,
    )
    # Four distinct corners make axis and scale verification possible after import.
    print(json.dumps({"blender_corners_SW_SE_NW_NE": [
        float(elevation[row,col]) for row,col in ((0,0),(0,-1),(-1,0),(-1,-1))],
        "triangles": len(faces), "vertices": len(vertices)}))


if __name__ == "__main__":
    first, second = sys.argv[sys.argv.index("--") + 1:]
    main(Path(first).resolve(), Path(second).resolve())
