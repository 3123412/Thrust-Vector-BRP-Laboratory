import bpy
import json
import math
from pathlib import Path

ROOT=Path(globals().get('BRP_PROJECT_ROOT','C:/Developing/My project'))
OUT=ROOT/'Artifacts'
OUT.mkdir(parents=True,exist_ok=True)
OWNER='build_planet_hd'

if bpy.context.mode!='OBJECT':
    raise RuntimeError('Switch Blender to Object Mode before generating the planet sphere')

selection=list(bpy.context.selected_objects)
active=bpy.context.view_layer.objects.active
collection=bpy.data.collections.get('BRP_Planet_HD')
if collection is None:
    collection=bpy.data.collections.new('BRP_Planet_HD')
    bpy.context.scene.collection.children.link(collection)
for obj in list(collection.objects):
    if obj.get('brp_generator')!=OWNER:continue
    mesh=obj.data
    bpy.data.objects.remove(obj,do_unlink=True)
    if mesh.users==0:bpy.data.meshes.remove(mesh)
existing=bpy.data.objects.get('EarthSphereHD')
if existing is not None:
    raise RuntimeError('An unrelated object named EarthSphereHD already exists')

try:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=256,ring_count=128,radius=1,location=(0,0,0),rotation=(0,0,0))
    sphere=bpy.context.object;sphere.name='EarthSphereHD';sphere['brp_generator']=OWNER
    for parent in list(sphere.users_collection):parent.objects.unlink(sphere)
    collection.objects.link(sphere)
    mesh=sphere.data;mesh.name='EarthSphereHDMesh'
    for vertex in mesh.vertices:vertex.co.normalize()
    for polygon in mesh.polygons:polygon.use_smooth=True
    mesh.update()

    incidence=[0]*len(mesh.edges)
    inward=0
    for polygon in mesh.polygons:
        for loop in polygon.loop_indices:incidence[mesh.loops[loop].edge_index]+=1
        center=sum((mesh.vertices[i].co for i in polygon.vertices),start=mesh.vertices[polygon.vertices[0]].co*0)/len(polygon.vertices)
        if polygon.normal.dot(center)<=0:inward+=1
    boundary=sum(count==1 for count in incidence)
    nonmanifold=sum(count!=2 for count in incidence)
    radii=[vertex.co.length for vertex in mesh.vertices]
    uv=mesh.uv_layers.active
    if boundary or nonmanifold or inward or uv is None or max(abs(radius-1) for radius in radii)>1e-6:
        raise RuntimeError('Generated planet sphere failed geometry validation')

    bpy.ops.object.select_all(action='DESELECT');sphere.select_set(True)
    bpy.context.view_layer.objects.active=sphere
    path=OUT/'earth_sphere_hd.fbx'
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,use_triangles=True,mesh_smooth_type='FACE')
    report={
        'file':str(path),'segments':256,'rings':128,'vertices':len(mesh.vertices),
        'polygons':len(mesh.polygons),'triangles':sum(len(p.vertices)-2 for p in mesh.polygons),
        'radiusMin':min(radii),'radiusMax':max(radii),'boundaryEdges':boundary,
        'nonmanifoldEdges':nonmanifold,'inwardFaces':inward,
        'allFacesSmooth':all(p.use_smooth for p in mesh.polygons),'uvLayer':uv.name,
        'uvMin':[min(loop.uv[i] for loop in uv.data) for i in range(2)],
        'uvMax':[max(loop.uv[i] for loop in uv.data) for i in range(2)],
        'blenderScale':list(sphere.scale),'blenderRotation':list(sphere.rotation_euler),
        'sceneUnitScale':bpy.context.scene.unit_settings.scale_length,
        'bytes':path.stat().st_size
    }
    (OUT/'earth-sphere-hd-verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    sphere.hide_set(True)
    print(json.dumps(report))
finally:
    bpy.ops.object.select_all(action='DESELECT')
    for obj in selection:
        if obj.name in bpy.context.view_layer.objects and not obj.hide_get():obj.select_set(True)
    if active is not None and active.name in bpy.context.view_layer.objects:bpy.context.view_layer.objects.active=active
