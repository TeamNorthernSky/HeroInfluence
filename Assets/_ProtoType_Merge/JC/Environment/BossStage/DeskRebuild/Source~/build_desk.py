# JC | 2026-11-16 | 보스 책상 세트를 독립된 닫힌 메시로 재제작합니다.
# 기존 아틀라스의 얼룩·음영을 재사용하지 않고 재질과 실제 베벨로 표면을 구성합니다.
import bpy, bmesh, math, json, os
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__))
OUT='C:/Dev/_scratchpad/boss-desk-rebuild-57abe8cff6'
bpy.ops.wm.read_factory_settings(use_empty=True)
col=bpy.data.collections.new('JC_BossDesk_Rebuilt'); bpy.context.scene.collection.children.link(col)
mats={}; parts={}
def material(name, rgb, rough=.45, metal=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*rgb,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*rgb,1)
    p.inputs['Roughness'].default_value=rough; p.inputs['Metallic'].default_value=metal
    mats[name]=m; return m
# JC | 2026-11-16 | 원본의 청회색 본체·적색 직물·황동 장식을 깨끗한 단색 재질로 분리합니다.
material('Lacquer',(.105,.125,.185),.32)
material('Edge',(.19,.22,.29),.34)
material('Brass',(.52,.395,.18),.36,.62)
material('Fabric',(.31,.026,.044),.88)
material('FabricEdge',(.19,.017,.029),.9)
material('Ivory',(.65,.66,.64),.48)
material('Screen',(.014,.032,.045),.24)
material('Rubber',(.026,.033,.046),.65)
material('Display',(.075,.26,.32),.38)
def adopt(o,name,group,mat):
    o.name=name
    for c in list(o.users_collection): c.objects.unlink(o)
    col.objects.link(o); o.data.materials.append(mats[mat]); parts.setdefault(group,[]).append(o)
    return o
def finish(o,bevel):
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Small manufactured bevel','BEVEL');mod.width=min(bevel,min(o.dimensions)*.2);mod.segments=2
        mod.affect='EDGES'; bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in o.data.polygons:p.use_smooth=True
    mod=o.modifiers.new('Planar weighted normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return o
def box(name,loc,size,group,mat='Lacquer',bevel=.015,rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc,rotation=rot)
    o=adopt(bpy.context.object,name,group,mat);o.dimensions=size
    return finish(o,bevel)
def cylinder(name,loc,r,depth,group,mat='Rubber',rot=(0,0,0),vertices=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc,rotation=rot)
    return finish(adopt(bpy.context.object,name,group,mat),.008)
def mesh_obj(name,verts,faces,group,material_names,indices=None):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);col.objects.link(o);parts.setdefault(group,[]).append(o)
    for m in material_names:me.materials.append(mats[m])
    if indices:
        for p,i in zip(me.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    return o
# JC | 2026-11-16 | 카펫은 한 장의 닫힌 체적이며 같은 평면에 연속적인 테두리를 분할해 틈과 Z fighting을 방지합니다.
w,d=8.543,3.058
rects=[(w/2,d/2),(w/2-.045,d/2-.045),(w/2-.075,d/2-.075),(w/2-.105,d/2-.105),(w/2-.15,d/2-.15),(w/2-.175,d/2-.175)]
v=[]
for x,y in rects:v += [(-x,-y,.018),(x,-y,.018),(x,y,.018),(-x,y,.018)]
f=[];ix=[]
for k in range(len(rects)-1):
    for j in range(4):f.append((k*4+j,k*4+(j+1)%4,(k+1)*4+(j+1)%4,(k+1)*4+j));ix.append([1,2,1,0,2][k])
f.append(tuple(range(20,24)));ix.append(0)
v += [(-w/2,-d/2,0),(w/2,-d/2,0),(w/2,d/2,0),(-w/2,d/2,0)]
for j in range(4):f.append((j,24+j,24+(j+1)%4,(j+1)%4));ix.append(1)
f.append((27,26,25,24));ix.append(1)
mesh_obj('Continuous bordered carpet',v,f,'01_Carpet',['Fabric','FabricEdge','Brass'],ix)
# JC | 2026-11-16 | 책상만 전용 무광 재질로 분리하여 휘장/소품의 기존 공유 재질을 보존합니다.
material('BodyLacquer',(.105,.125,.185),.82)
material('BodyEdge',(.19,.22,.29),.78)
# JC | 2026-11-16 | 전면 금장만 콘솔 테두리와 같은 광택 황동으로 맞추며 휘장/본체 재질은 보존합니다.
material('BodyBrass',(.52,.395,.18),.36,.62)
material('BodyBase',(.026,.033,.046),.88)
# JC | 2026-11-16 | 얇게 포개진 상판과 전면 판을 단일 닫힌 판으로 합쳐 미세 경계와 하이라이트를 줄입니다.
box('Solid desk carcass',(0,-.20,.65),(5.38,1.16,1.24),'02_Desk','BodyLacquer',.025)
box('Plinth',(0,-.20,.08),(5.49,1.24,.12),'02_Desk','BodyBase',.025)
top=box('Continuous countertop',(0,-.21,1.302),(5.66,1.37,.184),'02_Desk','BodyEdge',.035)
top.data.materials.append(mats['BodyLacquer'])
for face in top.data.polygons:
    if face.normal.z>.999:face.material_index=1
for x in [-2.43,2.43]:
    box('Pedestal foot',(x,-.20,.09),(.72,1.30,.14),'02_Desk','BodyEdge',.022)
    box('Pedestal',(x,-.20,.68),(.62,1.20,1.10),'02_Desk','BodyEdge',.025)
box('Continuous front panel',(0,-.799,.69),(3.65,.090,.96),'02_Desk','BodyLacquer',.018)
# JC | 2026-11-16 | 앞서 승인된 금장의 윗선/두 배 높이를 유지하고 책상 전용 황동을 사용합니다.
box('Front brass inlay',(0,-.851,.8425),(1.5,.014,.110),'02_Desk','BodyBrass',.009)
# JC | 2026-11-16 | 휘장은 상판에서 전면으로 부드럽게 꺾이는 독립 직물. 양끝/뒷면도 닫습니다.
def banner(cx,group):
    # JC | 2026-11-16 | 상판 모서리를 관통하던 직선 대신 외측 원호로 감싸 상면과 앞면 직물이 끊기지 않게 합니다.
    # 두께도 원호의 안쪽 법선으로 계산하여 꺾이는 구간에서 뒷면이 상판에 묻히지 않도록 합니다.
    path=[(.34,1.420),(-.60,1.420)]
    offsets=[(0,-.012),(0,-.012)]
    for step in range(13):
        angle=math.pi*.5*step/12
        path.append((-.870-.065*math.sin(angle),1.355+.065*math.cos(angle)))
        offsets.append((.012*math.sin(angle),-.012*math.cos(angle)))
    tail=[(-.935,1.15),(-.943,.92),(-.938,.65),(-.925,.36),(-.937,.278),(-.94,.25)]
    path+=tail;offsets +=[(.012,0)]*len(tail)
    xs=[-.31,-.28,-.253,.253,.28,.31];vv=[]
    for back in [0,1]:
        for j,(y,z) in enumerate(path):
            for x in xs:vv.append((cx+x,y+back*offsets[j][0],z+back*offsets[j][1]))
    n=len(path)*6;ff=[];mi=[]
    for side in [0,1]:
        for j in range(len(path)-1):
            for k in range(5):
                a=side*n+j*6+k;ff.append((a,a+1,a+7,a+6));mi.append(1 if k in [1,3] or j==len(path)-2 else 0)
    for j in range(len(path)-1):
        for k in [0,5]:a=j*6+k;ff.append((a,a+6,a+6+n,a+n));mi.append(0)
    for j in [0,len(path)-1]:
        for k in range(5):a=j*6+k;ff.append((a,a+n,a+n+1,a+1));mi.append(0)
    o=mesh_obj('Draped fabric',vv,ff,group,['Fabric','Brass'],mi)
    for p in o.data.polygons:p.use_smooth=True
    # 원본의 대칭 날개형 문양을 단순한 금색 자수 실루엣으로 다시 만듭니다.
    wings=[[(-.24,.20),(-.13,.14),(-.055,.035),(-.018,-.16),(-.15,-.01),(-.19,.08)],[(.24,.20),(.13,.14),(.055,.035),(.018,-.16),(.15,-.01),(.19,.08)],[(0,.18),(-.045,.10),(-.028,.015),(0,-.10),(.028,.015),(.045,.10)]]
    for poly in wings:
        verts=[(cx+x,-.956,.70+z) for x,z in poly]+[(cx+x,-.950,.70+z) for x,z in poly]
        faces=[tuple(range(6)),tuple(range(11,5,-1))]+[(j,(j+1)%6,(j+1)%6+6,j+6) for j in range(6)]
        mesh_obj('Gold wing embroidery',verts,faces,group,['Brass'])
banner(-1.78,'03_Banner_Left');banner(1.78,'04_Banner_Right')
# JC | 2026-11-16 | 모니터/랩탑은 의자 쪽을 향한 화면과 전투 시점에서 보이는 후면 모두 정돈합니다.
g='05_Monitor';x=1.25
box('Monitor foot',(x,-.20,1.42),(.57,.39,.055),g,'Rubber')
box('Monitor stand',(x,-.14,1.57),(.13,.13,.28),g,'Edge')
box('Monitor housing',(x,-.12,1.88),(1.65,.13,.66),g,'Lacquer',.025)
box('Monitor screen',(x,-.046,1.89),(1.51,.014,.53),g,'Screen',.01)
box('Rear inset',(x,-.192,1.88),(1.43,.012,.46),g,'Edge',.01)
box('Rear badge',(x,-.202,1.85),(.16,.008,.035),g,'Brass',.004)
g='06_Laptop';x=-1.10
box('Laptop base',(x,-.12,1.422),(.86,.57,.055),g,'Edge',.015)
box('Keyboard recess',(x,.01,1.454),(.71,.22,.016),g,'Rubber',.007)
for r in range(3):
    for k in range(9):box('Key',(x-.30+k*.075,-.06+r*.067,1.466),(.053,.044,.012),g,'Lacquer',.003)
box('Trackpad',(x,.20,1.456),(.23,.10,.008),g,'Lacquer',.006)
box('Laptop lid',(x,-.34,1.72),(.86,.055,.57),g,'Lacquer',.018,(-.13,0,0))
box('Laptop screen',(x,-.308,1.72),(.74,.008,.45),g,'Screen',.008,(-.13,0,0))
box('Laptop rear emblem',(x,-.374,1.72),(.11,.007,.025),g,'Brass',.004)
g='07_Console'
box('Inset console frame',(0,-.18,1.414),(.62,.49,.045),g,'Brass',.015)
box('Console glass',(0,-.18,1.439),(.54,.41,.014),g,'Screen',.008)
for i in range(3):box('Console interface',(0,-.28+i*.10,1.449),(.37,.012,.004),g,'Display',.002)
# 양쪽 흰 물체는 낮은 통신 단말기로 형태를 명확하게 합니다.
for x,g in [(-2.38,'08_Intercom_Left'),(2.38,'09_Intercom_Right')]:
    box('Terminal base',(x,-.14,1.43),(.52,.47,.065),g,'Rubber')
    box('Terminal body',(x,-.14,1.515),(.48,.43,.13),g,'Ivory',.025)
    box('Receiver',(x,-.25,1.60),(.40,.10,.09),g,'Ivory',.028)
    for k in range(4):box('Speaker slot',(x-.12+k*.08,-.08,1.584),(.045,.13,.007),g,'Rubber',.002)
    box('Terminal indicator',(x,.025,1.582),(.18,.025,.008),g,'Display',.003)
# JC | 2026-11-16 | 뒤쪽 의자는 등받이 프레임·붉은 쿠션·헤드레스트를 분리하고 후면도 닫힌 형상으로 만듭니다.
g='10_Chair'
cylinder('Chair base',(0,.92,.10),.49,.10,g,'Rubber',vertices=20)
cylinder('Chair pedestal',(0,.92,.37),.095,.49,g,'Edge')
box('Seat shell',(0,.86,.64),(1.08,.83,.18),g,'Lacquer',.055)
box('Seat cushion',(0,.82,.76),(.93,.70,.15),g,'Fabric',.065)
box('Back shell',(0,1.15,1.28),(1.02,.19,1.22),g,'Lacquer',.065,(-.08,0,0))
box('Back cushion',(0,1.025,1.30),(.86,.16,1.04),g,'Fabric',.06,(-.08,0,0))
box('Headrest shell',(0,1.20,1.94),(.64,.21,.30),g,'Lacquer',.055)
box('Head cushion',(0,1.075,1.95),(.52,.10,.21),g,'Fabric',.045)
for x in [-.56,.56]:
    box('Arm support',(x,.83,.88),(.09,.10,.40),g,'Edge',.015)
    box('Arm pad',(x,.78,1.075),(.16,.58,.09),g,'Rubber',.035)
# JC | 2026-11-16 | 게임에는 의미 단위 10개 렌더러만 내보내며 재질별 서브메시를 보존합니다.
groups=[]
for group,objects in parts.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=group
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);groups.append(o)
    o['JC_note']='JC | 2026-11-16 | 독립 재제작 메시, 기존 배치 기준'
# 직교/사선 검수용 스튜디오는 내보내기에서 제외합니다.
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=24
sc.world=bpy.data.worlds.new('Studio World');sc.world.color=(.25,.25,.25)
for name,loc,power,size in [('Key',(1,-4,7),1100,5),('Fill',(-5,-1,4),700,4),('Rim',(1,4,6),1000,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(7,-10,6.8));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.85))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=10.1;sc.camera=cam
sc.render.resolution_x=1440;sc.render.resolution_y=960;sc.render.resolution_percentage=100
sc.view_settings.view_transform='AgX';sc.render.image_settings.file_format='PNG';sc.render.film_transparent=False
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion();area.spaces.active.region_3d.view_distance=10;area.spaces.active.region_3d.view_location=Vector((0,0,.8));area.spaces.active.shading.type='MATERIAL'
# JC | 2026-11-16 | Unity 기존 피봇으로 전달할 좌표와 법선, UV를 기록하고 열린 경계가 없는지 검사합니다.
payload={'materials':[],'objects':[]}; audit=[]
for name,m in mats.items():
    p=m.node_tree.nodes.get('Principled BSDF');payload['materials'].append({'name':name,'color':list(m.diffuse_color),'roughness':p.inputs['Roughness'].default_value,'metallic':p.inputs['Metallic'].default_value})
deps=bpy.context.evaluated_depsgraph_get()
for o in groups:
    me=bpy.data.meshes.new_from_object(o.evaluated_get(deps));me.calc_loop_triangles()
    bm=bmesh.new();bm.from_mesh(me);bad=sum(not e.is_manifold for e in bm.edges);bm.free()
    audit.append({'object':o.name,'nonmanifold_edges':bad,'triangles':len(me.loop_triangles)})
    assert bad==0,(o.name,bad)
    # 분리된 면 코너를 내보내어 베벨의 커스텀 법선과 평면 법선을 그대로 전달합니다.
    vs=[];ns=[];uv=[];slots=[[] for m in me.materials]
    for tri in me.loop_triangles:
        assert tri.area>1e-10,(o.name,'degenerate')
        for li in tri.loops:
            vi=me.loops[li].vertex_index;vs.append(list(me.vertices[vi].co));ns.append(list(me.corner_normals[li].vector));uv.append([me.vertices[vi].co.x,me.vertices[vi].co.y])
        slots[tri.material_index]+=list(range(len(vs)-3,len(vs)))
    payload['objects'].append({'name':o.name,'vertices':vs,'normals':ns,'uv':uv,'slots':[{'material':me.materials[i].name,'triangles':ts} for i,ts in enumerate(slots) if ts]})
    bpy.data.meshes.remove(me)
os.makedirs(OUT,exist_ok=True)
json.dump(payload,open(OUT+'/model.json','w'));json.dump(audit,open(OUT+'/topology.json','w'),indent=2)
bpy.ops.object.select_all(action='DESELECT')
for o in groups:o.select_set(True)
bpy.context.view_layer.objects.active=groups[1]
bpy.ops.wm.save_as_mainfile(filepath=HERE+'/JC_BossDesk.blend')
bpy.ops.export_scene.fbx(filepath=HERE+'/JC_BossDesk.fbx',use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
for i,loc in enumerate([(7,-10,6.8),(0,-12,3),(7,9,6)]):
    cam.location=loc;cam.rotation_euler=(Vector((0,0,.85))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=OUT+'/blender-'+str(i)+'.png';bpy.ops.render.render(write_still=True)
print('JC_MODEL_DONE',json.dumps(audit))
