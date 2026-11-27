"""Inspect the actual Hunyuan PineInk WrappedCore return in Blender."""
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

sys.dont_write_bytecode = True
import bpy
import numpy as np
from mathutils import Vector

WORK = Path(__file__).resolve().parent
SOURCE = WORK.parent / "Source/WrappedCore.glb"
qa_root = Path("C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts")
sys.path.insert(0, str(qa_root))
import qa_check
import review_shots


def digest(path):
    with path.open("rb") as data:
        return hashlib.file_digest(data, "sha256").hexdigest()


def gltf_document(path):
    with path.open("rb") as source:
        magic, version, size, json_length, marker = struct.unpack("<4sIIII", source.read(20))
        assert magic == b"glTF" and version == 2 and marker == 0x4E4F534A and size == path.stat().st_size
        return json.loads(source.read(json_length))


def load_source(path=SOURCE, reset=True):
    if reset:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    previous = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(path), merge_vertices=True, import_pack_images=True)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj not in previous]
    if len(meshes) != 1:
        raise ValueError("Expected one independently generated cloud component")
    mesh = meshes[0]
    mesh.name = path.stem + "_Source"
    bpy.context.view_layer.update()
    return mesh


def data_of(obj):
    obj.data.calc_loop_triangles()
    points = np.array([tuple(obj.matrix_world @ vert.co) for vert in obj.data.vertices])
    faces = np.array([tuple(face.vertices) for face in obj.data.loop_triangles])
    return points, faces


def bbox(points):
    box_min, box_max = points.min(axis=0), points.max(axis=0)
    return dict(min=box_min.tolist(),max=box_max.tolist(),extent=(box_max-box_min).tolist())


def snapshot(obj, path=SOURCE):
    points, faces = data_of(obj)
    centered = points - points.mean(axis=0)
    vectors, eigenvalues, _ = np.linalg.svd(np.cov(centered.T))
    return {
        "blender": bpy.app.version_string,
        "asset": path.name,
        "sourceSHA256": digest(path),
        "sourceMetresAreNotAuthoringScale": True,
        "counts": {"triangles": len(faces), "verticesAfterWeldingGltfSplits": len(points)},
        "bboxBlenderOriginal": bbox(points),
        "pcaBasis": vectors.tolist(),
        "pcaEigenvalues": eigenvalues.tolist(),
        "matrixBlenderImported": [list(row) for row in obj.matrix_world],
        "gltfOriginalMaterials": gltf_document(path)["materials"],
        "embeddedPng": [
            {"name": img.name,"size":list(img.size),"space":img.colorspace_settings.name,"packed":bool(img.packed_file)}
            for img in bpy.data.images
        ],
        "qa": qa_check.run(objects=[obj.name], profile="game", eps=.00015, tol=.003)
    }


def studio_asset(obj, options):
    scene = bpy.context.scene
    scale = max(obj.dimensions)
    center = Vector(sum([obj.matrix_world @ Vector(point) for point in obj.bound_box],Vector()) / 8)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("Neutral graphite studio")
    scene.world.use_nodes = True
    background = next(node for node in scene.world.node_tree.nodes if node.type=="BACKGROUND")
    background.inputs["Color"].default_value = (.12,.14,.17,1)
    background.inputs["Strength"].default_value = .30

    def lamp(name,offset,intensity,color,radius):
        light = bpy.data.lights.new(name,"AREA")
        light.energy = intensity*scale*scale
        light.color = color
        light.shape = "DISK"
        light.size = radius*scale
        entity = bpy.data.objects.new(name,light)
        scene.collection.objects.link(entity)
        entity.location = center+Vector(offset)*scale
        entity.rotation_euler=(center-entity.location).to_track_quat("-Z","Y").to_euler()
        return entity

    lamps = [
        lamp("Warm rope sidelight",(-1.6,-2.1,2.2),155,(1,.90,.75),1.8),
        lamp("Cool soft fill",(2.1,-1.6,.4),90,(.72,.84,1),2.3),
        lamp("Edge and end exposure",(.5,2.1,1.5),130,(1,.97,.84),1.4),
        lamp("Seam fill",(.3,-1.2,-2.6),50,(.80,.87,1),1.8)
    ]
    camera_data = bpy.data.cameras.new("Pine ink macro")
    camera = bpy.data.objects.new("Pine ink macro",camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = scale*1.49
    camera_data.clip_start = scale*.001
    camera_data.clip_end = scale*20
    scene.camera=camera

    prefix,directions=options
    for label,offset in directions.items():
        camera.location=center+Vector(offset).normalized()*scale*4.3
        camera.rotation_euler=(center-camera.location).to_track_quat("-Z","Y").to_euler()
        scene.render.filepath=str(WORK/f"{prefix}-{label}.png")
        bpy.ops.render.render(write_still=True)

    # Scene tools are temporary and never enter the derived mesh export.
    for thing in lamps+[camera]:
        bpy.data.objects.remove(thing,do_unlink=True)


def main():
    args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    asset = SOURCE if not args else SOURCE.with_name(args[0]+".glb")
    high = load_source(asset)
    report = snapshot(high,asset)
    (WORK/(asset.stem+"-SourceQA.json")).write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k!="qa"},ensure_ascii=False),flush=True)
    if "data-only" in args:
        return
    studio_asset(high,("Original" if asset==SOURCE else asset.stem+"-Original",{
        "px":(1,0,0),"nx":(-1,0,0),"py":(0,1,0),"ny":(0,-1,0),
        "pz":(0,-.0001,1),"nz":(0,-.0001,-1),"angle":(1,-2.1,1.4)
    }))


if __name__ == "__main__":
    main()

