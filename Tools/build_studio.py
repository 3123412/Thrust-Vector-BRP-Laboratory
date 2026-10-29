import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/Developing/My project')
OUT=ROOT/'Assets/BRP/Art/Models'
collection=bpy.data.collections.new('BRP_Studio')
bpy.context.scene.collection.children.link(collection)
objects=[]

def mat(name,color,metal=.5,rough=.4):
    result=bpy.data.materials.get(name)
    if result is None:
        result=bpy.data.materials.new(name);result.diffuse_color=(*color,1);result.use_nodes=True
        node=next(n for n in result.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        node.inputs['Base Color'].default_value=(*color,1);node.inputs['Metallic'].default_value=metal;node.inputs['Roughness'].default_value=rough
    return result

steel=mat('BRP_Steel',(.24,.29,.31),.8,.38)
ivory=mat('BRP_Ivory',(.64,.65,.6),.25,.5)
navy=mat('BRP_Navy',(.045,.075,.09),.55,.45)
black=mat('BRP_Rubber',(.016,.022,.025),.05,.8)
floor=mat('BRP_Floor',(.085,.11,.12),.35,.55)
orange=mat('BRP_Orange',(.85,.25,.055))
cyan=mat('BRP_CyanLight',(.08,.65,.8),.15,.2)
warm=mat('BRP_WarmLight',(1,.63,.27),.05,.2)
screen=mat('BRP_Screen',(.035,.14,.16),.35,.27)

def finish(o,name,material):
    o.name=name
    for c in list(o.users_collection):c.objects.unlink(o)
    collection.objects.link(o);o.data.materials.append(material);objects.append(o)
    return o

def box(name,p,s,m=steel,b=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p)
    o=finish(bpy.context.object,name,m);o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if b:
        modifier=o.modifiers.new('EdgeBevel','BEVEL');modifier.width=b;modifier.segments=3
        o.modifiers.new('WeightedNormals','WEIGHTED_NORMAL')
    return o

def cylinder(name,p,r,h,m=steel,vertices=32,r2=None):
    if r2 is None:bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=h,location=p)
    else:bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r,radius2=r2,depth=h,location=p)
    o=finish(bpy.context.object,name,m)
    modifier=o.modifiers.new('EdgeBevel','BEVEL');modifier.width=.012;modifier.segments=2
    for f in o.data.polygons:f.use_smooth=True
    return o

def tube(name,a,b,r=.025,m=steel):
    a,b=Vector(a),Vector(b)
    o=cylinder(name,(a+b)/2,r,(b-a).length,m,12)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

def torus(name,p,r,thick=.015,m=steel):
    bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=8,major_radius=r,minor_radius=thick,location=p)
    return finish(bpy.context.object,name,m)

def export(name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
    for o in objects:o.hide_set(True)
    objects.clear()

box('StructuralSlab',(0,0,-.18),(20,20,.3),floor)
for x in range(-8,10,2):
    for y in range(-8,10,2):
        box('FloorTile',(x,y,-.018),(1.98,1.98,.035),floor,.005)
for x in [-2.3,2.3]:
    box('WalkwayBorder',(x,-1,.008),(.05,12,.012),ivory,.002)
    for y in range(-6,6):box('SafetyDash',(x+.12,y,.011),(.08,.35,.016),orange,.002)
for y in [-7,7]:
    for x in range(-8,9):
        stripe=box('HazardStripe',(x*.25,y,.01),(.13,.45,.012),orange,.002);stripe.rotation_euler.z=-math.pi/4
for x in [-9,9]:
    box('WallBase',(x,0,.65),(.25,18,1.3),navy)
    for y in [-7,-3,1,5]:
        box('WallPanel',(x,y,3.5),(.22,3.8,4.2),ivory)
        box('WallInset',(x*.98,y,3.4),(.08,3.3,3.65),navy)
        box('VerticalLight',(x*.96,y-1.5,3.2),(.035,.045,3.4),cyan,.007)
    for y in [-9,-5,-1,3,7]:
        box('StructuralColumn',(x*.97,y,3.4),(.4,.3,6.8),steel)
        box('ColumnFoot',(x*.97,y,.3),(.65,.6,.6),black)
for x in [-6,-2,2,6]:
    box('RearWallPanel',(x,8.8,3.5),(3.8,.25,6.8),navy)
    box('WallTrim',(x,8.61,1.0),(3.7,.07,.035),ivory)
    box('UpperServicePanel',(x,8.58,5.8),(3.3,.05,.7),ivory)
for x in [-4,4]:
    box('ObservationWindow',(x,8.5,3.2),(2.8,.08,1.7),screen)
    for dx in [-1.5,1.5]:box('WindowFrame',(x+dx,8.44,3.2),(.08,.08,1.9),steel)
    for z in [2.25,4.15]:box('WindowFrame',(x,8.44,z),(3.05,.08,.08),steel)
box('MainBlastDoor',(0,8.65,2.5),(3.1,.25,5),black)
for x in [-1.55,1.55]:box('DoorFrame',(x,8.4,2.6),(.13,.25,5.2),steel)
box('DoorHeader',(0,8.4,5.15),(3.25,.25,.18),ivory)
for x in [-.8,.8]:
    box('DoorLeaf',(x,8.4,2.45),(1.45,.09,4.8),navy)
    box('DoorWarning',(x,8.34,2.5),(1.4,.03,.12),orange,.003)
for y in [-6,-1,4,8]:
    for side in [-1,1]:
        tube('ArchBeam',(side*8.8,y,6.1),(side*4,y,8),.13,steel)
        tube('ArchBeam',(side*4,y,8),(0,y,8.5),.13,steel)
        tube('DiagonalBrace',(side*8.8,y,5.6),(side*4,y,7.6),.045,ivory)
    box('CeilingLight',(0,y,7.5),(5,.25,.08),warm,.01)
for x in [-6,6]:
    tube('ServicePipe',(x,-9,6.2),(x,9,6.2),.08,steel)
    tube('ServicePipe',(x+.22,-9,6.2),(x+.22,9,6.2),.035,orange)
box('CraneTrack',(0,0,7.8),(.3,17,.3),orange)
box('CraneTrolley',(0,1,7.35),(1.2,1,.45),steel)
tube('CraneCable',(0,1,7.1),(0,1,5.3),.012,black)
torus('CraneHook',(0,1,5.2),.13,.035,orange).rotation_euler.x=math.pi/2

for x in [-5.4,5.4]:
    box('ConsoleBase',(x,2,.55),(2.8,1.1,1.1),navy)
    top=box('ConsoleDesk',(x,1.8,1.1),(2.9,1.3,.15),steel)
    for dx in [-.75,.0,.75]:
        body=box('MonitorHousing',(x+dx,2,1.65),(.65,.22,.55),black)
        box('MonitorGlass',(x+dx,1.87,1.65),(.56,.018,.44),screen,.012)
        for j in range(3):box('ScreenTrace',(x+dx-.2+j*.12,1.85,1.62+j*.05),(.07,.008,.012),cyan,.002)
    for dx in [-1,-.5,0,.5,1]:
        box('SwitchPlate',(x+dx,1.34,1.19),(.22,.16,.035),black,.008)
        cylinder('Switch',(x+dx,1.34,1.23),.02,.055,orange,12)
for x in [-7.6,7.6]:
    box('EquipmentRack',(x,5,1.45),(1.35,1.2,2.9),black)
    for z in range(7):
        box('RackUnit',(x,4.36,.25+z*.38),(1.16,.08,.3),navy,.01)
        for i in range(5):box('RackLED',(x-.45+i*.08,4.3,.25+z*.38),(.028,.018,.028),cyan,.003)
for x in [-4,4]:
    box('ToolCabinet',(x,-3.7,.5),(1.5,.8,1),navy)
    for z in [.25,.5,.75]:
        box('DrawerFace',(x,-4.13,z),(1.3,.05,.19),steel,.012)
        box('DrawerHandle',(x,-4.17,z+.02),(.5,.05,.025),ivory,.005)
for x in [-3.3,3.3]:
    cylinder('GasCylinder',(x,5.2,.8),.22,1.5,ivory)
    torus('CylinderBand',(x,5.2,.4),.225,.02,orange)
    cylinder('CylinderValve',(x,5.2,1.61),.055,.12,steel)
export('studio_hangar')

cylinder('InstrumentPedestal',(0,0,.43),.52,.85,navy)
cylinder('Foot',(0,0,.06),.95,.12,black,64)
cylinder('ServiceDeck',(0,0,.91),1.06,.12,steel,64)
cylinder('Turntable',(0,0,.99),.82,.06,black,64)
torus('TurntableLight',(0,0,1.03),.78,.012,cyan)
for i in range(48):
    a=i*math.tau/48
    tick=box('AngularScale',(math.cos(a)*.93,math.sin(a)*.93,.98),(.04,.012,.008),ivory,.001);tick.rotation_euler.z=a
for x in [-1,1]:
    box('OperatorWing',(x*1.3,0,.94),(.55,.9,.13),navy)
    for y in [-.27,-.1,.07,.24]:cylinder('Key',(x*1.3,y,1.03),.035,.025,orange if y<0 else steel,12)
export('studio_bench')

bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/BRP_Studio.blend'))
print('studio_hangar and studio_bench exported')
