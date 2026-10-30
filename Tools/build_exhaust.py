import bpy
import math
from pathlib import Path

root=Path('C:/Developing/My project')
vertices=[]
faces=[]
rings=25
sides=24
for j in range(rings):
    t=j/(rings-1)
    radius=(.09+.14*math.sin(t*math.pi)**.6)*(1-t)**.5+.002
    for i in range(sides):
        angle=i*math.tau/sides
        vertices.append((math.cos(angle)*radius,math.sin(angle)*radius,-t*3.5))
for j in range(rings-1):
    for i in range(sides):
        faces.append((j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i))
mesh=bpy.data.meshes.new('ExhaustCoreMesh')
mesh.from_pydata(vertices,[],faces)
mesh.update()
uv=mesh.uv_layers.new(name='PlumeUV')
for polygon in mesh.polygons:
    polygon.use_smooth=True
    for loop_index in polygon.loop_indices:
        index=mesh.loops[loop_index].vertex_index
        uv.data[loop_index].uv=((index%sides)/sides,(index//sides)/(rings-1))
obj=bpy.data.objects.new('ExhaustCore',mesh)
bpy.context.scene.collection.objects.link(obj)
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active=obj
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/BRP/Art/Models/exhaust_core.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
obj.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'SourceArt/BRP_Studio.blend'))
print('continuous exhaust mesh exported')
