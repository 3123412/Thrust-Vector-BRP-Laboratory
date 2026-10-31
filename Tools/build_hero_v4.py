import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(globals().get('BRP_PROJECT_ROOT','C:/Developing/My project'))
OUT=ROOT/'Artifacts/HeroV4'
OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'Assets/BRP/Art/Models'
IDS=['booster_engine','vacuum_engine','first_stage','second_stage','fuel_tank','oxidizer_tank','capsule','heat_shield','landing_leg','adapter','fairing','docking_port','stack_decoupler','instrument_bay','gimbal','rcs']
PARTS={p['id']:p for p in json.loads((ROOT/'Assets/BRP/Resources/BRP/parts.json').read_text(encoding='utf-8'))['parts']}
OWNER='build_hero_v4'
if bpy.context.mode!='OBJECT':raise RuntimeError('Switch Blender to Object Mode before generating hero modules')
selection=list(bpy.context.selected_objects);active=bpy.context.view_layer.objects.active
old=bpy.data.collections.get('BRP_Hero_V4')
if old is not None:
    for obj in list(old.objects):
        if obj.get('brp_generator')==OWNER:bpy.data.objects.remove(obj,do_unlink=True)
    if not old.objects:bpy.data.collections.remove(old)
library=bpy.data.collections.new('BRP_Hero_V4');bpy.context.scene.collection.children.link(library)
palette={
    'BRP_Silver':((.64,.71,.76),.72,.32),
    'BRP_Blue':((.025,.1,.17),.42,.42),
    'BRP_Graphite':((.025,.037,.046),.22,.58),
    'BRP_Copper':((.48,.22,.09),.7,.36),
    'BRP_Ceramic':((.79,.81,.77),.08,.65),
    'BRP_Glass':((.025,.25,.36),.45,.22)
}
materials={}
for name,(color,metal,rough) in palette.items():
    mat=bpy.data.materials.get(name)
    if mat is None:
        mat=bpy.data.materials.new(name);mat.use_nodes=True;mat.diffuse_color=(*color,1)
        node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        node.inputs['Base Color'].default_value=(*color,1);node.inputs['Metallic'].default_value=metal;node.inputs['Roughness'].default_value=rough
    materials[name]=mat
SILVER='BRP_Silver';BLUE='BRP_Blue';DARK='BRP_Graphite';COPPER='BRP_Copper';CERAMIC='BRP_Ceramic';GLASS='BRP_Glass'
groups={}
baseline={}
report=[]

def bounds(objects):
    points=[obj.matrix_world@vertex.co for obj in objects if obj.type=='MESH' for vertex in obj.data.vertices]
    return [[min(point[i] for point in points) for i in range(3)],[max(point[i] for point in points) for i in range(3)]]

for part_id in IDS:
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(SOURCE/(part_id+'.fbx')),use_image_search=False)
    imported=list(set(bpy.data.objects)-before);baseline[part_id]=bounds(imported)
    for obj in imported:bpy.data.objects.remove(obj,do_unlink=True)

def add(vertices,faces,material,smooth=False):
    if material not in groups:groups[material]=[[],[],[]]
    target=groups[material];offset=len(target[0]);target[0].extend(tuple(v) for v in vertices)
    target[1].extend(tuple(offset+index for index in face) for face in faces)
    target[2].extend([smooth]*len(faces) if isinstance(smooth,bool) else smooth)

def transform(vertices,origin,rotation=None,scale=(1,1,1)):
    result=[]
    for vertex in vertices:
        v=Vector((vertex[0]*scale[0],vertex[1]*scale[1],vertex[2]*scale[2]))
        if rotation is not None:v=rotation@v
        result.append(tuple(Vector(origin)+v))
    return result

def lathe(profile,material=SILVER,segments=64,closed=False,origin=(0,0,0),rotation=None,scale=(1,1,1)):
    vertices=[];rings=[];faces=[];smooth=[]
    for radius,z in profile:
        if abs(radius)<1e-8:
            rings.append([len(vertices)]);vertices.append((0,0,z))
        else:
            ring=[]
            for index in range(segments):
                a=index*math.tau/segments;ring.append(len(vertices));vertices.append((math.cos(a)*radius,math.sin(a)*radius,z))
            rings.append(ring)
    pairs=list(zip(rings,rings[1:]))
    if closed:pairs.append((rings[-1],rings[0]))
    for lower,upper in pairs:
        if len(lower)==1 and len(upper)==1:continue
        for index in range(segments):
            n=(index+1)%segments
            if len(lower)==1:face=(lower[0],upper[n],upper[index])
            elif len(upper)==1:face=(lower[index],lower[n],upper[0])
            else:face=(lower[index],lower[n],upper[n],upper[index])
            faces.append(face);smooth.append(True)
    if not closed:
        if len(rings[0])>1:faces.append(tuple(reversed(rings[0])));smooth.append(False)
        if len(rings[-1])>1:faces.append(tuple(rings[-1]));smooth.append(False)
    add(transform(vertices,origin,rotation,scale),faces,material,smooth)

def ring(radius,z,thickness=.008,material=SILVER,segments=64,origin=(0,0,0),rotation=None,scale=(1,1,1)):
    profile=[(radius+math.cos(i*math.tau/8)*thickness,z+math.sin(i*math.tau/8)*thickness) for i in range(8)]
    lathe(profile,material,segments,True,origin,rotation,scale)

def tube(a,b,radius,material=SILVER,segments=12,radius_end=None):
    a,b=Vector(a),Vector(b);axis=b-a
    lathe([(radius,0),(radius if radius_end is None else radius_end,axis.length)],material,segments,False,a,axis.to_track_quat('Z','Y').to_matrix())

def box(origin,size,material=SILVER,bevel=.006,rotation=None):
    width,depth,height=size;b=min(bevel,width*.2,depth*.2,height*.2)
    vertices=[];rings=[]
    for z,inset in [(-height/2,b),(-height/2+b,0),(height/2-b,0),(height/2,b)]:
        w=width/2-inset;d=depth/2-inset;c=max(.0001,b)
        outline=[(-w+c,-d),(w-c,-d),(w,-d+c),(w,d-c),(w-c,d),(-w+c,d),(-w,d-c),(-w,-d+c)]
        rings.append(list(range(len(vertices),len(vertices)+8)));vertices.extend((x,y,z) for x,y in outline)
    faces=[]
    for lower,upper in zip(rings,rings[1:]):
        for i in range(8):n=(i+1)%8;faces.append((lower[i],lower[n],upper[n],upper[i]))
    faces.extend([tuple(reversed(rings[0])),tuple(rings[-1])])
    add(transform(vertices,origin,rotation),faces,material)

def bolts(z,radius,count=12,size=.008):
    for index in range(count):
        angle=index*math.tau/count
        lathe([(size,0),(size,.008)],DARK,6,origin=(math.cos(angle)*radius,math.sin(angle)*radius,z))

def annulus(inner,outer,z0,z1,material=SILVER,segments=64):
    lathe([(outer,z0),(outer,z1),(inner,z1),(inner,z0)],material,segments,True)

def curved_patch(radius,z0,z1,angle,width,material,depth=.004):
    vertices=[];steps=8
    for r in [radius,radius+depth]:
        for z in [z0,z1]:
            for i in range(steps+1):
                a=angle-width/2+width*i/steps;vertices.append((math.cos(a)*r,math.sin(a)*r,z))
    row=steps+1;faces=[]
    for i in range(steps):
        faces.extend([(i,i+row,i+row+1,i+1),(i+row*2,i+row*2+1,i+row*3+1,i+row*3),(i,i+1,i+row*2+1,i+row*2),(i+row,i+row*3,i+row*3+1,i+row+1)])
    faces.extend([(0,row*2,row*3,row),(steps,steps+row,steps+row*3,steps+row*2)])
    add(vertices,faces,material)

def engine(part):
    h=part['height'];vacuum=part['id']=='vacuum_engine';bell_height=h*(.65 if vacuum else .55)
    points=[]
    for i in range(13):
        t=i/12;r=.12+(.385-.12)*(1-t)**(1.35 if vacuum else 1.8)
        points.append((r,.022+t*bell_height))
    inner=[(max(.095,r-.009),z) for r,z in reversed(points)]
    lathe(points+inner,DARK,64,True)
    ring(.386,.022,.01,SILVER)
    throat=.022+bell_height
    chamber_top=h*.89
    lathe([(.128,throat-.008),(.143,throat+.02),(.145,chamber_top-.04),(.13,chamber_top),(.11,chamber_top),(.112,throat-.008)],COPPER,48,True)
    ring(.148,throat+.025,.011,COPPER,48)
    for z in [h*.88,h*.96]:annulus(.115,.338,z-.015,min(h,z+.015),BLUE)
    for index in range(4):
        a=index*math.pi/2+math.pi/4
        tube((math.cos(a)*.17,math.sin(a)*.17,throat+.015),(math.cos(a)*.3,math.sin(a)*.3,h*.95),.022,SILVER)
        box((math.cos(a)*.29,math.sin(a)*.29,h*.93),(.08,.08,h*.1),SILVER)
    for index in range(6):
        a=index*math.tau/6
        for (r0,z0),(r1,z1) in zip(points[::2],points[2::2]):
            tube((math.cos(a)*(r0+.007),math.sin(a)*(r0+.007),z0),(math.cos(a)*(r1+.007),math.sin(a)*(r1+.007),z1),.0045,COPPER,6)
    for side in [-1,1]:
        tube((side*.16,0,h*.7),(side*.22,.07,h*.94),.028,SILVER)
        lathe([(.056,0),(.071,.02),(.071,.12),(.056,.14)],SILVER,24,origin=(side*.21,.065,h*.73))
        ring(.074,.07,.006,DARK,24,origin=(side*.21,.065,h*.73))
    annulus(.115,.337,h-.025,h,BLUE)
    bolts(h-.009,.29,12)

def tank(part):
    h=part['height'];r=part['radius'];bay=part['id']=='instrument_bay'
    profile=[(r-.018,0),(r+.008,.008),(r+.016,.022),(r+.016,.04),(r-.004,.06),(r-.006,h-.06),(r+.016,h-.04),(r+.016,h-.022),(r+.008,h-.008),(r-.018,h)]
    lathe(profile,SILVER,64)
    for z in [.047,h-.047]:ring(r+.002,z,.006,BLUE)
    for z in [.075,h-.075]:ring(r-.003,z,.003,SILVER)
    for z in [.024,h-.032]:bolts(z,r*.88,12,.007)
    if bay:
        for index in range(8):
            angle=index*math.tau/8
            curved_patch(r-.004,.08,h-.08,angle,.46,DARK,.004)
            curved_patch(r,.095,h-.095,angle,.36,SILVER,.005)
            for z in [.11,h-.11]:
                center=(math.cos(angle)*(r+.008),math.sin(angle)*(r+.008),z)
                box(center,(.025,.025,.018),BLUE,.003)
    else:
        for a in [-math.pi/2,math.pi/2]:
            width=min(.48,.19/r)
            curved_patch(r-.003,h*.29,h*.58,a,width,SILVER,.007)
            curved_patch(r+.004,h*.31,h*.56,a,width*.83,DARK,.002)
            curved_patch(r+.006,h*.325,h*.545,a,width*.72,SILVER,.002)
        curved_patch(r-.003,h*.68,h*.715,0,math.tau-.001,BLUE,.004)
        for angle in [0,math.pi]:
            tube((math.cos(angle)*(r-.003),math.sin(angle)*(r-.003),h*.12),(math.cos(angle)*(r-.003),math.sin(angle)*(r-.003),h*.88),.003,SILVER,8)
        if part['id'] in ['first_stage','second_stage']:
            for index in range(16):
                angle=index*math.tau/16
                curved_patch(r-.004,.085,min(h*.2,.23),angle,.025,SILVER,.006)

def capsule(part):
    h=part['height'];r=part['radius']
    lathe([(r-.018,0),(r,.025),(r,.08),(r-.012,.16),(r*.94,.28),(r*.84,.48),(r*.73,.72),(r*.70,h-.04),(r*.69,h)],SILVER,64)
    ring(r,.025,.018,DARK);ring(r-.006,.095,.008,BLUE)
    annulus(.17,r*.72,h-.045,h,BLUE,48);bolts(h-.012,r*.65,8)
    for index in range(3):
        angle=-math.pi/2+index*math.tau/3;z=h*.56;radial=r*.81
        normal=Vector((math.cos(angle),math.sin(angle),.26)).normalized();rotation=normal.to_track_quat('Z','Y').to_matrix()
        location=Vector((math.cos(angle)*radial,math.sin(angle)*radial,z))
        lathe([(.105,-.006),(.109,.001),(.105,.014),(.087,.018),(.083,.014),(.083,-.006)],DARK,32,True,location,rotation,scale=(1,.74,1))
        lathe([(.101,.014),(.103,.018),(.099,.024),(.088,.024),(.085,.018)],SILVER,32,True,location,rotation,scale=(1,.74,1))
        lathe([(.084,.009),(.084,.012)],GLASS,32,origin=location,rotation=rotation,scale=(1,.74,1))
        for t in [-1,1]:
            pos=location+rotation@Vector((t*.102,0,.023));box(pos,(.012,.012,.008),DARK,.002,rotation)
    curved_patch(r*.87,h*.33,h*.62,math.pi/2,.52,DARK,.004)
    curved_patch(r*.87+.005,h*.35,h*.6,math.pi/2,.43,SILVER,.003)
    for angle in [0,math.pi]:
        tube((math.cos(angle)*r*.97,math.sin(angle)*r*.97,.13),(math.cos(angle)*r*.76,math.sin(angle)*r*.76,h*.78),.004,DARK,8)

def heat_shield(part):
    h=part['height'];r=part['radius']
    lathe([(0,0),(.12,.004),(.26,.018),(r*.88,.04),(r,.065),(r,h-.045),(r-.015,h-.025),(0,h-.025)],DARK,64)
    ring(r,.025,.018,SILVER);ring(r,h-.025,.018,SILVER)
    annulus(r*.65,r-.015,h-.035,h-.008,BLUE)
    bolts(h+.008,r*.8,12,.007)
    pitch=.085
    for row in range(-5,6):
        for column in range(-5,6):
            x=(column+.5*(row%2))*pitch;y=row*pitch*.866
            radius=math.hypot(x,y)
            if radius>r-.065:continue
            z=.004+.05*(radius/r)**2
            lathe([(.044,z),(.044,z+.009)],DARK,6,origin=(x,y,0))

def landing_leg(part):
    h=part['height']
    tube((0,0,h),(.4,0,.14),.033,SILVER,16)
    a=Vector((0,0,h));b=Vector((.4,0,.14));delta=b-a
    tube(a+delta*.2,a+delta*.64,.048,DARK,16)
    tube(a+delta*.22,a+delta*.29,.051,SILVER,16)
    tube(a+delta*.58,a+delta*.65,.051,SILVER,16)
    for side in [-1,1]:tube((0,side*.028,h*.45),(.36,side*.028,.16),.017,COPPER,12)
    lathe([(.07,h-.05),(.07,h+.05)],BLUE,32)
    rotation=Vector((0,1,0)).to_track_quat('Z','Y').to_matrix()
    lathe([(.047,0),(.047,.1)],SILVER,16,origin=(0,-.05,h),rotation=rotation)
    lathe([(.14,0),(.14,.018),(.125,.035),(.09,.055),(.065,.08)],DARK,32,origin=(.4,0,0),scale=(1,.1/.14,1))
    tube((.4,0,.07),(.4,0,.18),.035,SILVER,16)
    for x in [.31,.49]:
        for y in [-.055,.055]:lathe([(.009,.024),(.009,.032)],SILVER,6,origin=(x,y,0))

def adapter(part):
    h=part['height'];r=part['radius']
    lathe([(r,0),(r,.035),(r-.018,.07),(r*.73,h-.065),(r*.7,h-.035),(r*.7,h)],SILVER,64)
    ring(r,.025,.018,BLUE);ring(r*.7,h-.025,.012,DARK)
    for index in range(12):
        a=index*math.tau/12
        tube((math.cos(a)*(r-.004),math.sin(a)*(r-.004),.065),(math.cos(a)*r*.73,math.sin(a)*r*.73,h-.065),.004,SILVER,6)
    bolts(.03,r*.85,12)

def fairing(part):
    h=part['height'];r=part['radius']
    profile=[]
    for i in range(17):
        t=i/16;profile.append((max(.008,r*math.cos(t*math.pi/2)**.72),h*t))
    lathe(profile,SILVER,64);ring(r,.025,.018,BLUE)
    for index in range(4):
        a=index*math.pi/2
        for (r0,z0),(r1,z1) in zip(profile[1::2],profile[3::2]):
            tube((math.cos(a)*(r0+.002),math.sin(a)*(r0+.002),z0),(math.cos(a)*(r1+.002),math.sin(a)*(r1+.002),z1),.0025,DARK,6)

def docking_port(part):
    h=part['height'];r=part['radius']
    annulus(r*.55,r-.008,0,h,BLUE)
    for z in [.025,h-.025]:ring(r,z,.018,SILVER)
    annulus(r*.54,r*.7,h-.01,h+.03,SILVER)
    bolts(h,r*.8,12)
    for index in range(3):
        a=index*math.tau/3;rotation=Matrix.Rotation(a,3,'Z')
        box((math.cos(a)*r*.75,math.sin(a)*r*.75,h+.05),(.07,.07,.13),BLUE,.007,rotation)
        tube((math.cos(a)*r*.75,math.sin(a)*r*.75,h+.086),(math.cos(a)*r*.58,math.sin(a)*r*.58,h+.108),.013,SILVER,8)

def decoupler(part):
    h=part['height'];r=part['radius']
    annulus(r*.76,r-.01,0,h,BLUE)
    for z in [.025,h-.025]:ring(r,z,.018,SILVER)
    ring(r-.003,h*.5,.009,DARK)
    for index in range(12):
        a=index*math.tau/12
        box((math.cos(a)*(r-.016),math.sin(a)*(r-.016),h*.5),(.034,.034,h*.55),SILVER,.004,Matrix.Rotation(a,3,'Z'))
    bolts(h,r*.8,12)

def gimbal(part):
    h=part['height'];r=part['radius']
    annulus(r*.55,r-.015,0,h,BLUE)
    for z in [.025,h-.025]:ring(r,z,.018,SILVER)
    ring(r+.04,h*.5,.018,COPPER)
    for index in range(4):
        a=index*math.pi/2
        normal=Vector((math.cos(a),math.sin(a),0));rotation=normal.to_track_quat('Z','Y').to_matrix()
        lathe([(.05,0),(.055,.012),(.055,.06)],SILVER,20,origin=(normal.x*r*.93,normal.y*r*.93,h*.5),rotation=rotation)
    bolts(h,r*.8,12)

def rcs(part):
    box((0,0,.1),(.18,.18,.2),SILVER,.012)
    for index in range(4):
        a=index*math.pi/2;axis=Vector((math.cos(a),math.sin(a),0));rotation=axis.to_track_quat('Z','Y').to_matrix()
        location=(axis.x*.08,axis.y*.08,.1)
        lathe([(.025,0),(.03,.03),(.05,.12),(.043,.12),(.019,0)],DARK,24,True,location,rotation)
        ring(.033,.025,.005,COPPER,24,location,rotation)
    box((0,0,.195),(.12,.12,.01),BLUE,.003)

def make(part):
    kind=part['id']
    if kind in ['booster_engine','vacuum_engine']:engine(part)
    elif kind in ['first_stage','second_stage','fuel_tank','oxidizer_tank','instrument_bay']:tank(part)
    else:globals()[{'stack_decoupler':'decoupler'}.get(kind,kind)](part)

for part_id in IDS:
    part=PARTS[part_id];make(part)
    objects=[]
    for name,(vertices,faces,smooth) in groups.items():
        mesh=bpy.data.meshes.new(part_id+'_'+name);mesh.from_pydata(vertices,[],faces);mesh.update()
        obj=bpy.data.objects.new(part_id+'_'+name,mesh);obj['brp_generator']=OWNER;obj['part_id']=part_id;library.objects.link(obj);mesh.materials.append(materials[name])
        for polygon,value in zip(mesh.polygons,smooth):polygon.use_smooth=value
        uv=mesh.uv_layers.new(name='SurfaceUV')
        for polygon in mesh.polygons:
            values=[]
            for loop in polygon.loop_indices:
                v=mesh.vertices[mesh.loops[loop].vertex_index].co;values.append((math.atan2(v.y,v.x)/math.tau+.5,v.z/max(.01,part['height'])))
            if max(v[0] for v in values)-min(v[0] for v in values)>.5:values=[(u+1 if u<.5 else u,v) for u,v in values]
            for loop,value in zip(polygon.loop_indices,values):uv.data[loop].uv=value
        objects.append(obj)
    groups.clear()
    own_bounds=bounds(objects);target=baseline[part_id]
    factors=[(target[1][i]-target[0][i])/(own_bounds[1][i]-own_bounds[0][i]) for i in range(3)]
    if any(abs(factor-1)>.25 for factor in factors):raise RuntimeError('Hero envelope requires excessive correction: '+part_id+' '+str(factors))
    for obj in objects:
        for vertex in obj.data.vertices:
            for i in range(3):vertex.co[i]=target[0][i]+(vertex.co[i]-own_bounds[0][i])*factors[i]
        obj.data.update()
    corrected=bounds(objects)
    triangles=sum(len(p.vertices)-2 for obj in objects for p in obj.data.polygons)
    entry={'id':part_id,'catalogHeight':part['height'],'catalogRadius':part['radius'],'sourceBounds':target,'heroBounds':corrected,'fitScale':factors,'materialGroups':len(objects),'triangles':triangles,'materials':[obj.data.materials[0].name for obj in objects]}
    if len(objects)>4:raise RuntimeError('Too many material groups: '+part_id)
    error=max(abs(corrected[bound][axis]-target[bound][axis]) for bound in range(2) for axis in range(3))
    if error>1e-5:raise RuntimeError('Envelope mismatch: '+part_id)
    entry['envelopeError']=error
    geometry={'boundaryEdges':0,'nonmanifoldEdges':0,'signedVolumes':[],'axialRayHits':[]}
    for obj in objects:
        bm=bmesh.new();bm.from_mesh(obj.data)
        geometry['boundaryEdges']+=sum(edge.is_boundary for edge in bm.edges)
        geometry['nonmanifoldEdges']+=sum(not edge.is_manifold for edge in bm.edges)
        geometry['signedVolumes'].append(bm.calc_volume(signed=True))
        if part_id in ['booster_engine','vacuum_engine']:
            hit=BVHTree.FromBMesh(bm).ray_cast(Vector((0,0,-.1)),Vector((0,0,1)),2)
            if hit[0] is not None:geometry['axialRayHits'].append(obj.name)
        bm.free()
    if geometry['boundaryEdges'] or geometry['nonmanifoldEdges'] or any(volume<=0 for volume in geometry['signedVolumes']) or geometry['axialRayHits']:
        raise RuntimeError('Hero geometry validation failed: '+part_id+' '+str(geometry))
    entry['geometry']=geometry
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(part_id+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,use_triangles=True,mesh_smooth_type='FACE')
    for obj in objects:obj.hide_set(True)
    report.append(entry);print(part_id,triangles,'triangles',len(objects),'material groups')

(OUT/'manifest.json').write_text(json.dumps({'generator':'Tools/build_hero_v4.py','partIds':IDS,'modules':report},indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/BRP_Hero_V4.blend'))
bpy.ops.object.select_all(action='DESELECT')
for obj in selection:
    if obj.name in bpy.context.view_layer.objects and not obj.hide_get():obj.select_set(True)
if active is not None and active.name in bpy.context.view_layer.objects:bpy.context.view_layer.objects.active=active
print('Hero V4 exported to Artifacts only')
