import bpy
import math
import json
from mathutils import Vector
from pathlib import Path

ROOT = Path('C:/Developing/My project')
OUT = ROOT / 'Assets/BRP/Art/Models'
OUT.mkdir(parents=True, exist_ok=True)
(ROOT / 'SourceArt').mkdir(exist_ok=True)
parts = json.loads((ROOT/'Assets/BRP/Resources/BRP/parts.json').read_text(encoding='utf-8'))['parts']
library = bpy.data.collections.new('BRP_Library')
bpy.context.scene.collection.children.link(library)

def material(name, color, metallic=0, roughness=.4):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color,1)
    m.use_nodes = True
    node = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = (*color,1)
    node.inputs['Metallic'].default_value = metallic
    node.inputs['Roughness'].default_value = roughness
    return m

silver=material('BRP_Silver',(.64,.71,.76),.8,.27)
blue=material('BRP_Blue',(.025,.10,.17),.65,.32)
dark=material('BRP_Graphite',(.025,.037,.046),.5,.4)
orange=material('BRP_Orange',(.85,.25,.055),.3,.38)
copper=material('BRP_Copper',(.48,.22,.09),.8,.3)
white=material('BRP_Ceramic',(.79,.81,.77),.1,.6)
glass=material('BRP_Glass',(.025,.25,.36),.65,.15)
solar=material('BRP_Solar',(.025,.065,.22),.75,.25)
concrete=material('BRP_Concrete',(.11,.14,.16),.1,.8)
all_mats=[silver,blue,dark,orange,copper,white,glass,solar,concrete]
current=[]

def finish(obj,name,mat):
    obj.name=name
    for c in list(obj.users_collection): c.objects.unlink(obj)
    library.objects.link(obj)
    obj.data.materials.append(mat)
    current.append(obj)
    return obj

def box(name,pos,size,mat=silver,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    o=finish(bpy.context.object,name,mat)
    o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        b=o.modifiers.new('Edge','BEVEL'); b.width=bevel; b.segments=2
        o.modifiers.new('Normals','WEIGHTED_NORMAL')
    return o

def cylinder(name,pos,radius,depth,mat=silver,r2=None,vertices=32):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=pos)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=radius,radius2=r2,depth=depth,location=pos)
    o=finish(bpy.context.object,name,mat)
    b=o.modifiers.new('Edge','BEVEL'); b.width=min(.012,depth*.15); b.segments=2
    o.modifiers.new('Normals','WEIGHTED_NORMAL')
    for face in o.data.polygons: face.use_smooth=True
    return o

def sphere(name,pos,size,mat=silver):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,radius=1,location=pos)
    o=finish(bpy.context.object,name,mat); o.scale=size
    for face in o.data.polygons: face.use_smooth=True
    return o

def tube(name,start,end,radius,mat=silver):
    a,b=Vector(start),Vector(end)
    o=cylinder(name,(a+b)/2,radius,(b-a).length,mat,vertices=12)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return o

def ring(name,z,r,mat=silver):
    bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=8,major_radius=r,minor_radius=.018,location=(0,0,z))
    return finish(bpy.context.object,name,mat)

def bolts(z,r,count=8):
    for i in range(count):
        a=i*math.tau/count
        cylinder('Bolt',(math.cos(a)*r,math.sin(a)*r,z),.018,.018,dark,vertices=6)

def shell(h,r=.45,mat=silver):
    cylinder('PressureShell',(0,0,h/2),r,h,mat)
    for z in [.035,h-.035]:
        cylinder('Reinforcement',(0,0,z),r+.016,.045,blue)
        bolts(z+.025,r*.85)
    cylinder('IdentificationBand',(0,0,h*.67),r+.004,.09,orange)
    for a in [0,math.pi]:
        box('ServicePanel',(math.sin(a)*r,math.cos(a)*r,h*.4),(.16,.018,h*.25),blue,.01)

def panel(h,width=.48,mat=blue):
    box('Housing',(0,0,h/2),(width,.16,h),mat)
    for x in [-width*.4,width*.4]:
        for z in [.06,h-.06]:
            o=cylinder('Fastener',(x,-.09,z),.014,.016,silver,vertices=6); o.rotation_euler.x=math.pi/2

def build(p):
    h=p['height']; r=p['radius']; k=p['kind']
    if k in ['FirstStage','SecondStage','FuelTank','OxidizerTank','InstrumentBay','ReactionWheel','CargoBay']:
        shell(h,r,blue if k in ['InstrumentBay','ReactionWheel'] else silver)
        if k=='CargoBay':
            box('Hatch',(0,-r-.01,h*.5),(.5,.035,h*.78),blue)
            for z in [h*.25,h*.75]: tube('Hinge',(-.28,-r-.03,z),(-.28,-r-.03,z+.12),.035)
        if k=='ReactionWheel':
            ring('Flywheel',h*.5,r+.035,copper)
    elif k in ['BoosterEngine','VacuumEngine','VernierEngine']:
        cylinder('Nozzle',(0,0,h*.29),r*.84,h*.58,dark,r*.28)
        cylinder('Chamber',(0,0,h*.73),r*.3,h*.3,copper)
        cylinder('Mount',(0,0,h*.94),r*.75,h*.12,blue)
        for i in range(12):
            a=i*math.tau/12
            tube('CoolingPipe',(math.cos(a)*r*.82,math.sin(a)*r*.82,h*.03),(math.cos(a)*r*.3,math.sin(a)*r*.3,h*.58),.012,copper)
        for i in range(4):
            a=i*math.pi/2
            tube('FeedLine',(math.cos(a)*r*.4,math.sin(a)*r*.4,h*.6),(math.cos(a)*r*.55,math.sin(a)*r*.55,h*.92),.04,silver)
        ring('NozzleLip',h*.02,r*.84,silver)
    elif k in ['Fairing','Adapter','Capsule','EscapeTower']:
        if k=='EscapeTower':
            for i in range(3):
                a=i*math.tau/3
                tube('Tower',(math.cos(a)*.22,math.sin(a)*.22,0),(0,0,h*.85),.025)
            cylinder('EscapeMotor',(0,0,h*.65),.1,h*.5,white,.03)
            for x in [-.12,.12]: cylinder('AbortNozzle',(x,0,h*.4),.07,.12,dark,.03)
        else:
            cylinder('Form',(0,0,h*.5),r,h,white if k=='Capsule' else silver,.08 if k=='Fairing' else r*.7)
            ring('Base',.025,r,blue)
            if k=='Capsule':
                o=cylinder('Porthole',(0,-r*.78,h*.5),.1,.04,glass); o.rotation_euler.x=math.pi/2
                for i in range(4): tube('Seam',(-r*.6,0,h*.1),(-r*.35,0,h*.8),.008,dark)
    elif k in ['StackDecoupler','PayloadSeparator','DockingPort','Gimbal','HeatShield']:
        cylinder('Collar',(0,0,h*.5),r,h,dark if k=='HeatShield' else blue)
        for z in [.025,h-.025]: ring('Rim',z,r,silver)
        bolts(h,r*.8,12)
        if k=='HeatShield':
            for x in [-.24,0,.24]:
                for y in [-.24,0,.24]: box('ThermalTile',(x,y,h+.006),(.21,.21,.02),white,.005)
        if k=='DockingPort':
            cylinder('CaptureRing',(0,0,h),r*.65,.06,silver)
            for i in range(3):
                a=i*math.tau/3; box('CapturePetal',(math.cos(a)*r*.75,math.sin(a)*r*.75,h+.05),(.07,.07,.13),orange,.008)
        if k=='Gimbal':
            ring('GimbalBearing',h*.5,r+.04,copper)
    elif k in ['Fin','ControlFin','AirBrake']:
        vertices=[(0,0,0),(.65,0,0),(.12,0,h),(.0,0,h), (0,.045,0),(.65,.045,0),(.12,.045,h),(0,.045,h)]
        faces=[(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(1,5,6,2),(0,3,7,4)]
        mesh=bpy.data.meshes.new(k);mesh.from_pydata(vertices,[],faces);mesh.update()
        o=bpy.data.objects.new(k,mesh);library.objects.link(o);o.data.materials.append(blue);current.append(o)
        tube('Root',(0,.02,.03),(0,.02,h-.03),.035,silver)
        if k!='Fin': cylinder('Actuator',(.13,.06,h*.3),.06,.18,copper)
    elif k in ['SolarPanel','Radiator']:
        box('Frame',(.42,0,h*.5),(.84,.055,h),silver)
        for x in range(4):
            for z in range(6): box('Cell',(.1+x*.2,-.035,.08+z*(h-.16)/5),(.18,.012,(h-.12)/6),solar if k=='SolarPanel' else dark,.003)
        tube('Arm',(-.13,0,h*.5),(.13,0,h*.5),.035,copper)
    elif k in ['Antenna','HorizonSensor','Thermometer','Altimeter','Gyroscope']:
        panel(min(h,.22),.2)
        if k=='Antenna':
            tube('Mast',(0,0,.15),(0,0,h),.012)
            for a in [0,math.pi/2]: tube('Dipole',(-math.cos(a)*.2,-math.sin(a)*.2,h*.8),(math.cos(a)*.2,math.sin(a)*.2,h*.8),.009)
        else:
            o=cylinder('Sensor',(0,-.1,h*.55),.06,.06,glass);o.rotation_euler.x=math.pi/2
    elif k in ['FuelLine','FuelValve','FuelPump','Pressurizer','ServiceTank']:
        if k in ['Pressurizer','ServiceTank']:
            sphere('PressureVessel',(0,0,h*.5),(.16,.16,h*.5),copper if k=='Pressurizer' else white)
            cylinder('Valve',(0,0,h),.045,.08,dark)
            for z in [h*.25,h*.75]: box('Strap',(0,0,z),(.35,.07,.04),blue)
        elif k=='FuelLine':
            tube('Pipe',(0,0,0),(0,0,h*.7),.035,copper)
            tube('Elbow',(0,0,h*.7),(.16,0,h),.035,copper)
        else:
            cylinder('PumpBody',(0,0,h*.45),.14,h*.75,blue)
            tube('Inlet',(-.24,0,h*.45),(.24,0,h*.45),.04,copper)
            if k=='FuelValve':
                o=ring('Handwheel',h,.12,orange)
            else: box('Motor',(0,.14,h*.5),(.16,.23,.22),silver)
    elif k=='SolidBooster':
        shell(h,.18,white)
        cylinder('Nose',(0,0,h+.12),.18,.24,orange,0)
        cylinder('Exhaust',(0,0,-.08),.16,.16,dark,.08)
    elif k=='RcsBlock':
        box('ValveBlock',(0,0,.1),(.18,.18,.2),blue)
        for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]:
            o=cylinder('Thruster',(dx*.14,dy*.14,.1),.05,.12,dark,.025)
            o.rotation_euler=Vector((dx,dy,0)).to_track_quat('Z','Y').to_euler()
    elif k=='LandingLeg':
        tube('Piston',(0,0,h),(.4,0,.12),.04)
        tube('Brace',(0,0,h*.45),(.36,0,.16),.024,copper)
        box('Foot',(.4,0,.04),(.28,.2,.08),dark)
        cylinder('Hinge',(0,0,h),.07,.1,blue)
    elif k=='Satellite':
        box('Bus',(0,0,h*.5),(.5,.5,h),white)
        for x in [-1,1]:
            box('Wing',(x*.6,0,h*.5),(.65,.035,.45),solar)
            tube('Aerial',(x*.16,0,h),(x*.3,0,h+.2),.008)
        box('Instrument',(0,-.28,h*.55),(.22,.1,.26),glass)
    elif k in ['MainParachute','DrogueParachute']:
        cylinder('Canister',(0,0,h*.5),.18,h,white)
        cylinder('Lid',(0,0,h),.19,.035,orange)
        for x in [-.2,.2]: box('Buckle',(x,0,h*.4),(.04,.13,.1),blue)
    else:
        panel(h,.35,orange if k=='FlightRecorder' else blue)
        if k=='Battery':
            for x in [-.1,.1]: cylinder('Terminal',(x,0,h+.025),.025,.05,copper)
        elif k=='Insulation':
            for z in range(5): box('Quilt',(0,-.09,(z+.5)*h/5),(.31,.035,h/5-.01),white,.012)
        elif k=='RadialDecoupler':
            tube('Pusher',(0,-.1,h*.5),(0,-.32,h*.5),.06,copper)
        else:
            for i in range(3): box('Vent',(-.09+i*.09,-.09,h*.6),(.045,.02,h*.3),dark,.005)
            box('StatusLight',(.1,-.1,h*.2),(.03,.02,.03),orange,.003)

def export(name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in current: o.select_set(True)
    bpy.context.view_layer.objects.active=current[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
    for o in current: o.hide_set(True)
    current.clear()

for p in parts:
    build(p)
    export(p['id'])

box('HangarFloor',(0,0,-.16),(18,20,.3),concrete,.03)
for x in [-8.8,8.8]:
    box('SideWall',(x,0,3.5),(.3,20,7),blue,.04)
    for y in [-8,-4,0,4,8]:
        box('Column',(x*.98,y,3),(.45,.5,6),silver,.025)
        box('ServiceStrip',(x*.95,y,2.4),(.07,.2,4.1),orange,.01)
box('BackWall',(0,9.8,3.5),(18,.3,7),blue,.03)
for x in [-7,-3.5,0,3.5,7]:
    box('DoorPanel',(x,9.5,3.2),(3.3,.15,6.2),dark,.04)
    box('DoorStripe',(x,9.39,1.15),(3.2,.02,.1),orange,.005)
for y in [-8,-4,0,4,8]:
    box('RoofBeam',(0,y,6.6),(18,.28,.35),silver,.03)
    box('RoofLight',(0,y,6.38),(8,.18,.06),white,.01)
for x in [-6,-3,0,3,6]: box('FloorSeam',(x,0,.003),(.025,19,.006),silver,0)
for y in [-8,-4,0,4,8]: box('FloorSeam',(0,y,.003),(17,.025,.006),silver,0)
for x in [-2.4,2.4]: box('SafetyLane',(x,0,.008),(.065,9,.012),orange,.002)
export('hangar')

box('WorkbenchTop',(0,0,1), (3.8,1.8,.14),blue,.06)
box('WorkbenchInset',(0,0,1.078),(3.55,1.55,.025),dark,.02)
for x in [-1.6,1.6]:
    for y in [-.6,.6]: box('WorkbenchLeg',(x,y,.5),(.16,.16,1),silver,.025)
for x in [-1.3,0,1.3]:
    box('Drawer',(x,.0,.67),(1.1,1.4,.36),blue,.035)
    box('Handle',(x,-.76,.69),(.38,.045,.035),silver,.01)
cylinder('Turntable',(0,0,1.1),.65,.055,silver)
cylinder('TurntableInset',(0,0,1.14),.58,.028,dark)
export('workbench')

cylinder('PadBase',(0,0,.14),5,.28,concrete,vertices=64)
cylinder('PadDeck',(0,0,.32),2.8,.12,dark,vertices=48)
for i in range(4):
    a=i*math.pi/2
    x,y=math.cos(a)*2,math.sin(a)*2
    box('ClampBase',(x,y,.55),(.4,.4,.5),orange)
    tube('ClampArm',(x,y,.8),(x*.5,y*.5,1.1),.1,silver)
for z in [1,3,5,7,9]:
    for x in [-1,1]: tube('TowerUpright',(3+x*.5,0,z-1),(3+x*.5,0,z+1),.08,silver)
    tube('TowerBrace',(2.5,0,z-1),(3.5,0,z+1),.04,orange)
box('TowerPlatform',(3,0,10),(1.5,1.5,.2),blue)
export('launchpad')

box('StandFloor',(0,0,.12),(3,3,.24),concrete)
for x in [-.8,.8]:
    for y in [-.8,.8]: tube('StandLeg',(x,y,.2),(x*.5,y*.5,1.6),.07,silver)
cylinder('StandCollar',(0,0,1.6),.65,.18,blue)
for y in [-.9,.9]: tube('Pipe',(0,y,.1),(0,y,1.45),.1,copper)
export('teststand')

for o in library.objects: o.hide_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/BRP_Library.blend'))
print(json.dumps({'models':len(parts)+4,'output':str(OUT)}))
