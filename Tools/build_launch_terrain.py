import bpy
import math
from pathlib import Path

root=Path('C:/Developing/My project')
vertices=[]
faces=[]
size=128
for j in range(size+1):
    for i in range(size+1):
        x=(i/size-.5)*2000;y=(j/size-.5)*2000
        distance=math.sqrt(x*x+y*y)
        height=(math.sin(x*.012)*math.cos(y*.009)*12+math.sin(x*.032+y*.015)*4)*min(1,max(0,(distance-80)/180))
        vertices.append((x,y,height-.32))
for j in range(size):
    for i in range(size):
        a=j*(size+1)+i;faces.append((a,a+1,a+size+2,a+size+1))
mesh=bpy.data.meshes.new('LaunchTerrain');mesh.from_pydata(vertices,[],faces);mesh.update()
terrain=bpy.data.objects.new('LaunchTerrain',mesh);bpy.context.scene.collection.objects.link(terrain)
material=bpy.data.materials.get('BRP_Concrete')
terrain.data.materials.append(material)
for p in terrain.data.polygons:p.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');terrain.select_set(True);bpy.context.view_layer.objects.active=terrain
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/BRP/Art/Models/launch_terrain.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'SourceArt/BRP_Studio.blend'))
print('launch terrain exported')
