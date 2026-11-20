import bpy, sys, math
from mathutils import Vector
p, out = sys.argv[sys.argv.index('--')+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=p)
ms=[o for o in bpy.data.objects if o.type=='MESH']
pts=[o.matrix_world@Vector(c) for o in ms for c in o.bound_box]
mn=Vector((min(q.x for q in pts),min(q.y for q in pts),min(q.z for q in pts))); mx=Vector((max(q.x for q in pts),max(q.y for q in pts),max(q.z for q in pts)))
c=(mn+mx)/2; size=max(mx-mn)

for m in bpy.data.materials:
    if not m.use_nodes: continue
    b=m.node_tree.nodes.get('Principled BSDF')
    ec=b.inputs['Emission Color']
    if ec.is_linked:
        src=ec.links[0].from_socket; m.node_tree.links.new(src,b.inputs['Base Color']); m.node_tree.links.remove(ec.links[0]); b.inputs['Emission Strength'].default_value=0; b.inputs['Roughness'].default_value=0.6; b.inputs['Specular IOR Level'].default_value=0.15
import os
for im in bpy.data.images:
    im.colorspace_settings.name='sRGB'
    if os.environ.get('TEX'): im.filepath=os.environ['TEX']; im.source='FILE'; im.reload()
print('BBOX',[round(v,3) for v in mn],[round(v,3) for v in mx], 'tris', sum(len(o.data.polygons) for o in ms))
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=24; sc.render.film_transparent=False; sc.render.resolution_x=sc.render.resolution_y=700
sc.view_settings.view_transform='Standard'
w=bpy.data.worlds.new('w'); w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(0.5,0.5,0.52,1); w.node_tree.nodes['Background'].inputs[1].default_value=0.5; sc.world=w
L=bpy.data.objects.new('L',bpy.data.lights.new('L','AREA')); L.data.energy=30*size*size/0.25; L.data.size=size*2; sc.collection.objects.link(L)
L.location=c+Vector((-size*0.6,-size*1.5,size*0.6)); L.rotation_euler=(c-L.location).to_track_quat('-Z','Y').to_euler()
for name,ang in (('thang',0),('34',35)):
    cd=bpy.data.cameras.new(name); cd.type='ORTHO'; cd.ortho_scale=size*1.05; cam=bpy.data.objects.new(name,cd); sc.collection.objects.link(cam)
    a=math.radians(ang); cam.location=c+Vector((size*2*math.sin(a),-size*2*math.cos(a),0))
    cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler(); sc.camera=cam
    sc.render.filepath=f'{out}_{name}.png'; bpy.ops.render.render(write_still=True)
