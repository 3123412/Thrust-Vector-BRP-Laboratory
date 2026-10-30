import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path('C:/Developing/My project')
source = (ROOT / 'Tools/build_studio.py').read_text(encoding='utf-8')
source = source.replace("collection=bpy.data.collections.new('BRP_Studio')", "collection=bpy.data.collections.new('BRP_World_V2')")
source = source.replace('modifier.segments=3', 'modifier.segments=2')
source = source.replace('bake_anim=False)', 'bake_anim=False,use_triangles=True)')
source = source.replace("def export(name):", "def original_export(name):")
prefix, body = source.split("box('StructuralSlab'", 1)
exec(prefix)

def export(name):
    groups = {}
    for obj in list(objects):
        if obj.hide_get():
            continue
        bpy.context.view_layer.objects.active = obj
        for modifier in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        key = (obj.data.materials[0].name, math.floor(obj.location.x / 12), math.floor(obj.location.y / 12))
        groups.setdefault(key, []).append(obj)
    merged = []
    for key, members in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in members:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = members[0]
        bpy.ops.object.join()
        obj = members[0]
        obj.name = f'{name}_{key[0]}_{key[1]}_{key[2]}'
        merged.append(obj)
    objects[:] = merged
    original_export(name)

extra = '''
for side in [-1,1]:
    box('MaintenanceGallery',(side*7.6,0,3.05),(2,16,.14),steel)
    for y in range(-7,9,2):
        tube('GalleryPost',(side*6.65,y,3.1),(side*6.65,y,4.15),.026,orange)
        tube('GalleryBracket',(side*8.7,y,2),(side*6.7,y,3),.055,steel)
    for z in [3.55,4.15]:tube('GalleryRail',(side*6.65,-7,z),(side*6.65,8,z),.028,orange)
    for i in range(14):
        box('AccessStair',(side*7.6,-7.8+i*.22,.11+i*.21),(1.4,.25,.1),steel,.012)
    tube('StairRail',(side*6.9,-7.8,1),(side*6.9,-4.72,3.94),.03,orange)
    for y in [-5,0,5]:
        for k in range(4):box('RoofAcousticPanel',(side*(2+k*1.8),y,8.45-k*.3),(1.65,4.85,.09),ivory,.01)
    for y in [-5,0,5]:
        box('OverheadDiffuser',(side*3,y,7.65),(1.25,3.5,.06),warm,.01)
for i in range(8):
    box('DoorRib',(0,8.31,.5+i*.55),(2.8,.045,.06),steel,.008)
for x in [-5.3,5.3]:
    for y in [-1,0,1]:
        box('TransportCrate',(x,y,.32),(1,.72,.64),ivory,.035)
        for dx in [-.33,.33]:box('CrateBand',(x+dx,y,.32),(.045,.75,.67),navy,.008)
    box('TrolleyDeck',(x,-2,.3),(1.2,1.7,.12),orange)
    for dx in [-.45,.45]:
        for dy in [-.65,.65]:
            wheel=cylinder('TrolleyWheel',(x+dx,-2+dy,.14),.14,.08,black,16)
            wheel.rotation_euler.y=math.pi/2
box('FrontLintel',(0,-9,6),(18,.4,1),ivory)
for x in [-8.8,8.8]:box('FrontPortal',(x,-9,3),(.4,.6,6),steel)
'''
body = "box('StructuralSlab'" + body
body = body.replace("export('studio_hangar')", extra + "\nexport('studio_hangar')")
body = body.split('bpy.ops.wm.save_as_mainfile')[0]
exec(body)

concrete = mat('BRP_Concrete', (.34,.36,.35), .05, .85)
road = mat('BRP_Asphalt', (.065,.075,.08), 0, .92)
grass = mat('BRP_Grass', (.18,.23,.14), 0, 1)
box('Apron',(0,0,-.24),(64,64,.4),concrete,.04)
box('FlameTrench',(0,0,-.05),(7,28,.12),black,.01)
for x in [-4.7,4.7]:box('LaunchAbutment',(x,0,.65),(2.4,10,1.3),concrete)
for y in [-3.8,3.8]:box('ServiceBridge',(0,y,1.25),(12,1.4,.25),steel)
for x in [-2.4,2.4]:
    for y in [-2.4,2.4]:
        tube('HoldDown',(x,y,.2),(x*.55,y*.55,1.6),.2,orange)
        cylinder('ClampHead',(x*.55,y*.55,1.6),.32,.2,steel,16)
for z in range(0,26,4):
    box('TowerDeck',(-9,2,z+.2),(4.5,4.5,.22),steel)
    for x in [-11,-7]:
        for y in [0,4]:
            box('TowerColumn',(x,y,z+2),(.28,.28,4),ivory)
        tube('TowerBrace',(x,0,z+.4),(x,4,z+3.9),.08,orange)
    for y in [0,4]:tube('TowerBrace',(-11,y,z+.4),(-7,y,z+3.9),.08,orange)
    for y in [.3,3.7]:
        tube('TowerHandrail',(-10.7,y,z+1.25),(-7.3,y,z+1.25),.035,orange)
    for i in range(12):box('TowerLadderRung',(-10.5,1,z+.3+i*.3),(.65,.07,.055),steel,.005)
for z in [8,15,22]:
    box('RetractedServiceArm',(-6,2,z),(3,1.3,.25),orange)
    tube('Umbilical',(-7,2,z+.2),(-4.5,2,z+.2),.09,steel)
for x,y in [(-22,-22),(22,-22),(22,22)]:
    cylinder('LightningMast',(x,y,17),.2,34,ivory,12,r2=.08)
    for dx,dy in [(-4,-3),(4,-3),(0,4)]:tube('MastStay',(x+dx,y+dy,0),(x,y,24),.025,steel)
for x in [-14,14]:
    box('ServiceRoad',(x,-95,-.1),(7,145,.1),road,.005)
    for y in range(-160,-25,8):box('RoadMark',(x,y,-.04),(.13,3,.012),ivory,.001)
for y in [-55,-120]:box('CrossRoad',(0,y,-.1),(170,7,.1),road,.005)
for x,y in [(48,25),(60,25),(48,40)]:
    cylinder('PropellantTank',(x,y,5),4,10,ivory,32)
    cylinder('TankCap',(x,y,10.3),4,.6,steel,32,r2=3.4)
    for z in [1,5,9]:torus('TankHoop',(x,y,z),4.02,.08,steel)
    tube('FeedLine',(x,y,.5),(x,-25,.5),.18,steel)
    tube('FeedLine',(x,-25,.5),(9,-25,.5),.18,steel)
box('TankContainment',(54,32,-.02),(32,35,.15),concrete)
for x,y,w,d,h in [(-65,40,24,40,18),(-65,-35,30,18,7),(55,-75,28,16,9),(-90,-85,18,30,6)]:
    box('Facility',(x,y,h/2),(w,d,h),ivory,.08)
    box('Roof',(x,y,h+.15),(w+.5,d+.5,.3),navy,.03)
    for dx in range(-int(w/2)+2,int(w/2),3):
        box('FacadePanel',(x+dx,y-d/2-.04,h*.5),(2.5,.08,h*.8),steel,.01)
        box('OfficeWindow',(x+dx,y-d/2-.1,h*.65),(2,.03,1.25),screen,.005)
    box('RollerDoor',(x,y-d/2-.12,2.5),(5,.08,5),navy,.01)
for y in range(-25,30,3):
    for x in [-28,28]:tube('FencePost',(x,y,0),(x,y,2.3),.04,steel)
for x in [-28,28]:
    for z in [.4,1.2,2.2]:tube('FenceRail',(x,-25,z),(x,29,z),.018,steel)
for y in [-25,25]:
    for x in range(-24,28,4):box('ApronWarning',(x,y,.01),(1.5,.18,.014),orange,.002)
export('spaceport')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/BRP_World_V2.blend'))
print('BRP World V2 exported: hangar, bench, spaceport; spatial material batches applied')
