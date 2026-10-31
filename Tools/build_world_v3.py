import bpy
import math
import random
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path('C:/Developing/My project')
OUT=ROOT/'Assets/BRP/Art/Models'
TEX=ROOT/'Assets/BRP/Art/Textures'
if not any(obj.name.startswith('studio_hangar_') for obj in bpy.data.objects) or not any(obj.name.startswith('spaceport_') for obj in bpy.data.objects):
    raise RuntimeError('Open SourceArt/BRP_World_V2.blend or BRP_World_V3.blend before running this generator')
previous=bpy.data.collections.get('BRP_World_V3')
if previous is not None:
    for obj in list(previous.objects):bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.collections.remove(previous)
library=bpy.data.collections.new('BRP_World_V3')
bpy.context.scene.collection.children.link(library)
groups={}
created=[]

def material(name,color,metal=0,rough=.7):
    mat=bpy.data.materials.get(name)
    if mat is None:mat=bpy.data.materials.new(name)
    mat.diffuse_color=(*color,1);mat.use_nodes=True
    node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value=(*color,1)
    node.inputs['Metallic'].default_value=metal;node.inputs['Roughness'].default_value=rough
    return mat

shell=material('BRP_Shell_V3',(.58,.62,.63),.08,.65)
paint=material('BRP_Paint_V3',(.22,.29,.33),.3,.5)
steel=material('BRP_Brushed_V3',(.38,.43,.46),.75,.38)
concrete=material('BRP_Concrete_V3',(.42,.43,.4),0,.92)
ceramic=material('BRP_Refractory_V3',(.25,.23,.2),0,.95)
orange=material('BRP_Orange_V3',(.88,.34,.07),.1,.55)
rubber=material('BRP_Rubber_V3',(.025,.028,.03),0,.9)
glass=material('BRP_Window_V3',(.08,.19,.25),.25,.3)
white=material('BRP_White_V3',(.77,.76,.68),.1,.65)
moonmat=material('BRP_Moon_V3',(.7,.7,.69),0,.95)
soil=material('BRP_LunarSoil_V3',(.36,.35,.33),0,1)
rock=material('BRP_LunarRock_V3',(.27,.26,.24),0,1)

def add(vertices,faces,mat,center=(0,0,0),smooth=False):
    key=(mat.name,math.floor(center[0]/24),math.floor(center[1]/24))
    if key not in groups:groups[key]=[[],[],[],mat]
    target=groups[key];start=len(target[0]);target[0].extend(vertices)
    target[1].extend(tuple(start+i for i in face) for face in faces)
    target[2].extend([smooth]*len(faces))

def box(name,p,s,mat=steel,rotation=None):
    vertices=[]
    for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]:
        v=Vector((x*s[0]/2,y*s[1]/2,z*s[2]/2))
        if rotation is not None:v=rotation@v
        vertices.append(tuple(Vector(p)+v))
    add(vertices,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,p)

def tube(name,a,b,r,mat=steel,segments=16):
    a,b=Vector(a),Vector(b);axis=b-a
    rotation=axis.to_track_quat('Z','Y').to_matrix()
    vertices=[]
    for end in [a,b]:
        for i in range(segments):
            angle=i*math.tau/segments
            vertices.append(tuple(end+rotation@Vector((math.cos(angle)*r,math.sin(angle)*r,0))))
    faces=[tuple(reversed(range(segments))),tuple(range(segments,segments*2))]
    faces.extend((i,(i+1)%segments,(i+1)%segments+segments,i+segments) for i in range(segments))
    add(vertices,faces,mat,(a+b)/2)

def flush(prefix):
    output=[]
    for key,(vertices,faces,smooth,mat) in groups.items():
        mesh=bpy.data.meshes.new(prefix+'_'+key[0]);mesh.from_pydata(vertices,[],faces);mesh.update()
        obj=bpy.data.objects.new(f'{prefix}_{key[0]}_{key[1]}_{key[2]}',mesh);library.objects.link(obj)
        mesh.materials.append(mat)
        for polygon,value in zip(mesh.polygons,smooth):polygon.use_smooth=value
        uv=mesh.uv_layers.new(name='SurfaceUV')
        for polygon in mesh.polygons:
            normal=polygon.normal
            axis=max(range(3),key=lambda i:abs(normal[i]))
            axes=[i for i in range(3) if i!=axis]
            for loop in polygon.loop_indices:
                v=mesh.vertices[mesh.loops[loop].vertex_index].co
                uv.data[loop].uv=(v[axes[0]]*.25,v[axes[1]]*.25)
        output.append(obj);created.append(obj)
    groups.clear();return output

def export(name,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,use_triangles=True)
    for obj in objects:obj.hide_set(True)
    print(name,len(objects),'render groups')

def copy_source(prefix,exclude=()):
    output=[]
    for obj in list(bpy.data.objects):
        if not obj.name.startswith(prefix) or obj.name in exclude:continue
        clone=obj.copy();clone.data=obj.data.copy();clone.name='V3_'+obj.name;library.objects.link(clone)
        clone.hide_set(False);output.append(clone);created.append(clone)
    return output

hangar=copy_source('studio_hangar_')
for side in [-1,1]:
    box('ClosedSideWall',(side*9.15,0,3.25),(.22,18.6,6.5),shell)
    rotation=Matrix.Rotation(side*math.atan2(2.4,9.25),3,'Y')
    box('InsulatedRoof',(side*4.625,0,7.56),(math.sqrt(9.25**2+2.4**2),18.7,.22),shell,rotation)
    for y in ([-9.22,9.22] if side==1 else []):
        vertices=[(-9.25,y-.1,6.35),(9.25,y-.1,6.35),(0,y-.1,8.86),(-9.25,y+.1,6.35),(9.25,y+.1,6.35),(0,y+.1,8.86)]
        add(vertices,[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],shell,(0,y,7.5))
    for y in [-8,-4,0,4,8]:
        box('InnerWallPanel',(side*9.01,y,4.8),(.07,3.92,2.2),paint)
        for z in [1.4,5.9]:tube('WallConduit',(side*8.94,y-1.7,z),(side*8.94,y+1.7,z),.025,steel,8)
    tube('EaveGutter',(side*9.24,-9.3,6.4),(side*9.24,9.3,6.4),.13,paint)
    tube('GutterDrain',(side*9.24,8.7,.1),(side*9.24,8.7,6.4),.09,paint)
for y in [-9.24,9.24]:
    box('ClosedEndWall',(0,y,3.2),(18.6,.24,6.4),shell)
for side in [-1,1]:
    box('FrontBlastDoor',(side*2,-9.05,2.75),(3.98,.15,5.5),paint)
    for z in [.4,1,1.6,2.2,2.8,3.4,4,4.6,5.2]:box('FrontDoorRib',(side*2,-8.94,z),(3.8,.04,.035),steel)
    for x in [side*4.2]:
        tube('DoorHydraulic',(x,-8.8,.4),(x,-8.8,5.7),.1,steel)
    box('SafetyStripe',(side*2,-8.94,1.1),(3.8,.03,.13),orange)
for x in [-4,0,4]:
    box('VentilationDuct',(x,0,7.15),(.45,17.9,.34),steel)
    for y in [-6,-2,2,6]:
        box('VentGrille',(x,y,6.95),(.5,.7,.04),rubber)
        for k in range(5):box('VentSlat',(x,y-.27+k*.13,6.91),(.48,.025,.025),steel)
for x in [-7,7]:
    box('InspectionCabinet',(x,-6,1.1),(1.3,.65,2.2),paint)
    for z in [.4,1.1,1.8]:
        box('CabinetPanel',(x,-6.35,z),(1.12,.04,.62),white)
        tube('CabinetHandle',(x+.35,-6.39,z-.13),(x+.35,-6.39,z+.13),.018,steel,8)
hangar.extend(flush('HangarV3'))
export('studio_hangar_v3',hangar)

skip={'spaceport_BRP_Concrete_0_0','spaceport_BRP_Rubber_0_0'}
pad=copy_source('spaceport_',skip)
for x in [-18.25,18.25]:box('ApronSide',(x,0,-.25),(29.5,64,.5),concrete)
for y in [-23,23]:box('ApronEnd',(0,y,-.25),(7,18,.5),concrete)
for x in [-4.7,4.7]:box('ReinforcedAbutment',(x,0,.65),(2.4,10,1.3),concrete)
box('TrenchFloor',(0,0,-1.02),(7,76,.3),ceramic)
for x in [-3.8,3.8]:box('TrenchWall',(x,0,-.5),(.6,76,1),concrete)
for y in [-15.3,15.3]:
    box('OutletBlastWall',(0,y*2.6,1),(12,.55,2),concrete)
for side in [-1,1]:
    for k in range(5):
        y=side*(.8+k*1.15);z=-.3-k*.12
        box('FlameDeflector',(0,y,z),(6.6,1.3,.25),ceramic,Matrix.Rotation(side*-.36,3,'X'))
    tube('DelugeFeed',(side*8,-18,.5),(side*8,8,.5),.18,steel)
    tube('DelugeRiser',(side*8,0,.5),(side*8,0,2.2),.18,steel)
    tube('DelugeHeader',(side*8,0,2.2),(side*3.8,0,2.2),.14,steel)
for i in range(24):
    a=i*math.tau/24;b=(i+1)*math.tau/24
    tube('DelugeRing',(math.cos(a)*3.7,math.sin(a)*3.7,1.75),(math.cos(b)*3.7,math.sin(b)*3.7,1.75),.1,steel)
    if i%3==0:tube('SprayNozzle',(math.cos(a)*3.7,math.sin(a)*3.7,1.75),(math.cos(a)*3.35,math.sin(a)*3.35,1.95),.055,steel,8)
for x in [-6.2,6.2]:
    for y in [-6.5,6.5]:
        box('ServiceLanding',(x,y,1.5),(3.4,2.2,.18),steel)
        for k in range(7):
            box('PlatformStep',(x,y+(-1 if y<0 else 1)*(1.3+k*.3),1.4-k*.2),(2,.32,.13),steel)
        for dx in [-1.55,1.55]:
            tube('LandingPost',(x+dx,y-1,1.5),(x+dx,y-1,2.55),.035,orange,8)
            tube('LandingPost',(x+dx,y+1,1.5),(x+dx,y+1,2.55),.035,orange,8)
            tube('LandingRail',(x+dx,y-1,2.55),(x+dx,y+1,2.55),.035,orange,8)
box('ElevatorShaft',(-10.3,3,13),(.8,1.2,26),paint)
for z in range(1,27,4):
    box('ElevatorDoor',(-9.85,3,z),(.04,.85,2),steel)
    box('LandingNumberPlate',(-9.8,2.45,z+.75),(.03,.22,.25),orange)
for z in [8,15,22]:
    for y in [1.3,2.7]:
        tube('AccessArmHandrail',(-7,y,z+1.1),(-4.7,y,z+1.1),.035,orange,8)
        for x in [-7,-5.9,-4.7]:tube('ArmStanchion',(x,y,z+.1),(x,y,z+1.1),.035,orange,8)
    for k in range(3):tube('UmbilicalLine',(-10,4.4,z),(-7,4.4,z+k*.12),.045,steel,12)
for x,y,w,d,h in [(-65,40,24,40,18),(-65,-35,30,18,7),(55,-75,28,16,9),(-90,-85,18,30,6)]:
    box('FacilityFoundation',(x,y,-.65),(w+.4,d+.4,1.3),concrete)
    for side in [-1,1]:
        for dx in range(-int(w/2)+2,int(w/2),3):
            box('RearFacadeRib',(x+dx,y+side*(d/2+.08),h*.5),(.12,.12,h*.9),steel)
            box('RearOfficeWindow',(x+dx,y+side*(d/2+.12),h*.66),(2.2,.05,1.5),glass)
        for dy in range(-int(d/2)+2,int(d/2),4):
            box('SideFacadeRib',(x+side*(w/2+.08),y+dy,h*.5),(.12,.14,h*.9),steel)
            box('SideOfficeWindow',(x+side*(w/2+.12),y+dy,h*.66),(.05,2.8,1.5),glass)
    for dx in [-w*.25,w*.25]:
        box('RooftopVent',(x+dx,y,h+.8),(2.8,4,1.3),steel)
        for k in range(6):box('RooftopGrille',(x+dx-1.15+k*.45,y-2.03,h+.8),(.2,.04,1.05),rubber)
for x,y in [(48,25),(60,25),(48,40)]:
    for k in range(12):
        z=.3+k*.8;tube('TankLadderRung',(x+3.65,y-.35,z),(x+3.65,y+.35,z),.025,steel,8)
    for dy in [-.4,.4]:tube('TankLadderRail',(x+3.65,y+dy,.2),(x+3.65,y+dy,10.5),.035,steel,8)
    for dx in [-1,1]:
        tube('ValveBranch',(x+dx*.3,y-4.4,.5),(x+dx*.3,y-4.4,1.1),.045,steel,8)
        box('ValveWheel',(x+dx*.3,y-4.4,1.13),(.25,.25,.04),orange)
pad.extend(flush('SpaceportV3'))
export('spaceport_v3',pad)

def move_to_library(obj):
    for parent in list(obj.users_collection):parent.objects.unlink(obj)
    library.objects.link(obj);created.append(obj)

moon_random=random.Random(1969)
moon_craters=[]
for index in range(90):
    longitude=moon_random.uniform(-math.pi,math.pi);latitude=math.asin(moon_random.uniform(-1,1))
    moon_craters.append((Vector((math.cos(latitude)*math.cos(longitude),math.cos(latitude)*math.sin(longitude),math.sin(latitude))),moon_random.uniform(.025,.15)))

def moon_shape(n):
    relief=math.sin(n.x*33+n.y*17)*math.cos(n.z*31)*.00015
    shade=0
    for center,radius in moon_craters:
        dot=max(-1,min(1,n.dot(center)))
        if dot<math.cos(radius*1.45):continue
        d=math.acos(dot)/radius
        rim=math.exp(-((d-1)/.13)**2)
        bowl=max(0,1-d*d)
        relief+=radius*(rim*.014-bowl*.023)
        shade+=rim*.08-bowl*.07
    return relief,shade

height_path=TEX/'Moon_NASA_LOLA_Height.jpg'
height_image=bpy.data.images.load(str(height_path),check_existing=True) if height_path.exists() and height_path.stat().st_size>10000 else None
if height_image is not None:height_pixels=list(height_image.pixels);hw,hh=height_image.size
bpy.ops.mesh.primitive_uv_sphere_add(segments=192,ring_count=96,radius=1)
moon=bpy.context.object;moon.name='LRO Moon with relief';move_to_library(moon)
for vertex in moon.data.vertices:
    n=vertex.co.normalized();u=(math.atan2(n.y,n.x)/math.tau+.5)%1;v=math.asin(n.z)/math.pi+.5
    if height_image is not None:
        sample=height_pixels[(min(hh-1,int(v*hh))*hw+min(hw-1,int(u*hw)))*4]
        vertex.co*=1+(sample-.5)*.009
    else:vertex.co*=1+moon_shape(n)[0]
moon.data.materials.append(moonmat)
for polygon in moon.data.polygons:polygon.use_smooth=True
for polygon in moon.data.polygons:
    coords=[]
    for loop in polygon.loop_indices:
        n=moon.data.vertices[moon.data.loops[loop].vertex_index].co.normalized()
        coords.append([math.atan2(n.y,n.x)/math.tau+.5,math.asin(n.z)/math.pi+.5])
    if max(c[0] for c in coords)-min(c[0] for c in coords)>.5:
        for c in coords:
            if c[0]<.5:c[0]+=1
    for loop,coord in zip(polygon.loop_indices,coords):moon.data.uv_layers.active.data[loop].uv=coord
export('moon_v3',[moon])
bpy.ops.mesh.primitive_uv_sphere_add(segments=96,ring_count=48,radius=1)
sphere=bpy.context.object;sphere.name='Planet sphere';move_to_library(sphere);sphere.data.materials.append(white)
for polygon in sphere.data.polygons:polygon.use_smooth=True
export('planet_sphere_v3',[sphere])

randomizer=random.Random(1961)
craters=[]
for i in range(62):
    x=randomizer.uniform(-1050,1050);y=randomizer.uniform(-1050,1050)
    if math.hypot(x,y)<140:continue
    craters.append((x,y,randomizer.uniform(18,105)))

def lunar_height(x,y):
    distance=math.hypot(x,y);blend=min(1,max(0,(distance-65)/90))
    height=(math.sin(x*.009)*math.cos(y*.012)*8+math.sin(x*.043+y*.031)*2)*blend
    for cx,cy,r in craters:
        d=math.hypot(x-cx,y-cy)/r
        if d<1.6:height+=(-r*.18*max(0,1-d*d)+r*.065*math.exp(-((d-1)/.14)**2))*blend
    return height

size=160;vertices=[];faces=[]
for j in range(size+1):
    for i in range(size+1):
        x=(i/size-.5)*2400;y=(j/size-.5)*2400;vertices.append((x,y,lunar_height(x,y)))
for j in range(size):
    for i in range(size):
        a=j*(size+1)+i;faces.append((a,a+1,a+size+2,a+size+1))
add(vertices,faces,soil,smooth=True)
for i in range(170):
    x=randomizer.uniform(-550,550);y=randomizer.uniform(-550,550)
    if math.hypot(x,y)<45:continue
    radius=randomizer.uniform(.3,2.4);z=lunar_height(x,y)
    points=[]
    for k in range(7):
        a=k*math.tau/7;r=radius*randomizer.uniform(.7,1.1)
        points.append((x+math.cos(a)*r,y+math.sin(a)*r,z-radius*.1))
    points.append((x+radius*.15,y-radius*.2,z+radius*.7))
    add(points,[tuple(reversed(range(7)))]+[(k,(k+1)%7,7) for k in range(7)],rock,(x/8,y/8,z))
lunar=flush('LunarSurfaceV3');export('lunar_surface_v3',lunar)
for inner,outer in [(1.3,1.53),(1.6,1.89),(1.98,2.24)]:
    verts=[];faces=[];steps=128
    for z in [-.003,.003]:
        for r in [inner,outer]:
            for i in range(steps):
                a=i*math.tau/steps;verts.append((math.cos(a)*r,math.sin(a)*r,z))
    for i in range(steps):
        n=(i+1)%steps
        faces.extend([(i,n,n+steps,i+steps),(i+steps*2,i+steps*3,n+steps*3,n+steps*2),(i,i+steps*2,n+steps*2,n),(i+steps,n+steps,n+steps*3,i+steps*3)])
    add(verts,faces,white)
rings=flush('SaturnRingsV3');export('ring_saturn_v3',rings)

def texture(name,width,height,color_function):
    image=bpy.data.images.new(name,width=width,height=height,alpha=False)
    pixels=[]
    for j in range(height):
        for i in range(width):
            color=color_function(i/width,j/height)
            pixels.extend((*color,1))
    image.pixels.foreach_set(pixels);image.filepath_raw=str(TEX/(name+'.png'));image.file_format='PNG';image.save()
    return image

def grain(u,v):return math.sin(u*math.tau*83+math.sin(v*math.tau*37))*math.sin(v*math.tau*71+math.cos(u*math.tau*29))
def value_noise(u,v,size):
    x=u*size;y=v*size;ix=math.floor(x);iy=math.floor(y);fx=x-ix;fy=y-iy
    fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
    def cell(a,b):
        n=((a%size)*374761393+(b%size)*668265263)&0xffffffff;n=((n^(n>>13))*1274126177)&0xffffffff
        return ((n^(n>>16))&65535)/65535*2-1
    return ((1-fx)*cell(ix,iy)+fx*cell(ix+1,iy))*(1-fy)+((1-fx)*cell(ix,iy+1)+fx*cell(ix+1,iy+1))*fy
texture('Concrete_Detail_V3',512,512,lambda u,v:tuple(.76+value_noise(u,v,8)*.025+value_noise(u,v,32)*.018+value_noise(u,v,128)*.008 for _ in range(3)))
texture('Brushed_Detail_V3',512,512,lambda u,v:tuple(.8+math.sin(v*math.tau*192)*.035+grain(u,v)*.016 for _ in range(3)))
texture('Lunar_Regolith_V3',512,512,lambda u,v:tuple(.58+value_noise(u,v,16)*.06+value_noise(u,v,128)*.04 for _ in range(3)))

def moon_color(u,v):
    longitude=(u-.5)*math.tau;latitude=(v-.5)*math.pi
    n=Vector((math.cos(latitude)*math.cos(longitude),math.cos(latitude)*math.sin(longitude),math.sin(latitude)))
    shade=moon_shape(n)[1]
    basin=max(0,math.sin(longitude*2.3+.5)*math.cos(latitude*3.8))
    value=.56-basin**3*.18+shade+grain(u,v)*.022
    return (value,value*.99,value*.97)

texture('Moon_Albedo_V3',1024,512,moon_color)

def planet_color(name,u,v):
    longitude=u*math.tau;latitude=(v-.5)*math.pi
    band=math.sin(latitude*28+math.sin(longitude*3)*.28)+.45*math.sin(latitude*63+math.cos(longitude*5)*.16)
    dust=grain(u,v)*.018
    if name=='Mars':
        continent=math.sin(longitude*3+math.sin(latitude*7))*math.cos(latitude*5)
        ice=max(0,(abs(latitude)-1.28)*3)
        return tuple(min(1,c+ice*.4+dust) for c in (.45+continent*.10,.22+continent*.055,.12+continent*.03))
    if name=='Venus':return (.68+band*.035+dust,.52+band*.04+dust,.29+band*.035+dust)
    if name=='Jupiter':
        storm=math.exp(-(((u-.68)/.055)**2+((v-.42)/.035)**2))
        return (.62+band*.065+storm*.13+dust,.5+band*.055-storm*.12+dust,.37+band*.045-storm*.12+dust)
    if name=='Saturn':return (.68+band*.025+dust,.59+band*.024+dust,.41+band*.02+dust)
    if name=='Uranus':return (.29+band*.006,.64+band*.008,.68+band*.008)
    return (.09+band*.015,.23+band*.025,.64+band*.04)

for name in ['Mars','Venus','Jupiter','Saturn','Uranus','Neptune']:
    texture(name+'_Albedo_V3',1024,512,lambda u,v,n=name:planet_color(n,u,v))

surface_maps={'BRP_Shell_V3':'Brushed_Detail_V3','BRP_Paint_V3':'Brushed_Detail_V3','BRP_Brushed_V3':'Brushed_Detail_V3','BRP_Concrete_V3':'Concrete_Detail_V3','BRP_Refractory_V3':'Concrete_Detail_V3','BRP_Moon_V3':'Moon_Albedo_V3','BRP_LunarSoil_V3':'Lunar_Regolith_V3','BRP_LunarRock_V3':'Lunar_Regolith_V3'}
for name,texture_name in surface_maps.items():
    mat=bpy.data.materials[name];nodes=mat.node_tree.nodes;node=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    image=bpy.data.images.load(str(TEX/(texture_name+'.png')),check_existing=True);image.reload()
    tex=next((n for n in nodes if n.type=='TEX_IMAGE'),None)
    if tex is None:tex=nodes.new('ShaderNodeTexImage')
    tex.image=image
    mix=next((n for n in nodes if n.type=='MIX_RGB'),None)
    if mix is None:mix=nodes.new('ShaderNodeMixRGB')
    mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=mat.diffuse_color
    mat.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]);mat.node_tree.links.new(mix.outputs[0],node.inputs['Base Color'])
    image.pack()

bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/BRP_World_V3.blend'))
(ROOT/'Artifacts/world-v3-export.txt').write_text('Closed roof and four walls; open flame trench; lunar relief and 2.4 km landing patch; six planet textures.\n'+str(len(created))+' generated groups\n',encoding='utf-8')
print('BRP World V3 complete')
