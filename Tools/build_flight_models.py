import bpy
import math
from pathlib import Path
from mathutils import Vector

root=Path('C:/Developing/My project')
out=root/'Assets/BRP/Art/Models'
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.mesh.primitive_uv_sphere_add(segments=64,ring_count=32,radius=1)
planet=bpy.context.object
planet.name='Planet'
for f in planet.data.polygons: f.use_smooth=True
bpy.ops.export_scene.fbx(filepath=str(out/'planet.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
planet.hide_set(True)
bpy.ops.object.select_all(action='DESELECT')
vertices=[(0,0,2.4)]
for row in range(1,9):
    theta=row/8*math.pi*.48
    for i in range(32):
        a=i*math.tau/32
        vertices.append((math.cos(a)*3.2*math.sin(theta),math.sin(a)*3.2*math.sin(theta),2.4*math.cos(theta)))
faces=[]
for i in range(32): faces.append((0,1+i,1+(i+1)%32))
for row in range(7):
    for i in range(32):
        a=1+row*32+i;b=1+row*32+(i+1)%32;c=b+32;d=a+32
        faces.append((a,b,c,d))
mesh=bpy.data.meshes.new('Canopy');mesh.from_pydata(vertices,[],faces);mesh.update()
canopy=bpy.data.objects.new('Canopy',mesh);bpy.context.scene.collection.objects.link(canopy)
white=bpy.data.materials.get('BRP_Ceramic');orange=bpy.data.materials.get('BRP_Orange')
canopy.data.materials.append(white);canopy.data.materials.append(orange)
for p in canopy.data.polygons: p.material_index=(p.index%32)//4%2;p.use_smooth=True
objects=[canopy]
for i in range(16):
    a=i*math.tau/16
    start=Vector((math.cos(a)*3.2,math.sin(a)*3.2,.15));end=Vector((0,0,-3))
    bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.012,depth=(end-start).length,location=(start+end)/2)
    cord=bpy.context.object;cord.rotation_euler=(end-start).to_track_quat('Z','Y').to_euler();cord.data.materials.append(white);objects.append(cord)
bpy.ops.object.select_all(action='DESELECT')
for o in objects: o.select_set(True)
bpy.context.view_layer.objects.active=canopy
bpy.ops.export_scene.fbx(filepath=str(out/'canopy.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'SourceArt/BRP_Library.blend'))
print('planet and canopy exported')
