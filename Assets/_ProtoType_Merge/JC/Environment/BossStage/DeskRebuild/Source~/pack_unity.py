# JC | 2026-11-16 | Blender의 미터 좌표를 기존 Desk 피봇 좌표로 변환합니다.
# X/Z 배치는 기존 카펫 중심, 높이는 기존 바닥 접점에 맞춥니다. 원본 배치 Transform은 바꾸지 않습니다.
import json, struct, pathlib
root=pathlib.Path('C:/Dev/_scratchpad/boss-desk-rebuild-57abe8cff6')
data=json.loads((root/'model.json').read_text())
with (root/'model.bin').open('wb') as f:
    def integer(v):f.write(struct.pack('<i',v))
    def floats(v):f.write(struct.pack('<'+'f'*len(v),*v))
    def string(s):
        b=s.encode('utf8');integer(len(b));f.write(b)
    integer(len(data['materials']))
    for m in data['materials']:string(m['name']);floats(m['color']+[m['roughness'],m['metallic']])
    integer(len(data['objects']))
    for o in data['objects']:
        string(o['name']);integer(len(o['vertices']))
        for x,y,z in o['vertices']:floats([(x/1.8-.090331)/1000,(z/1.8+.572122)/1000,(-y/1.8-3.4912105)/1000])
        for x,y,z in o['normals']:floats([x,z,-y])
        for uv in o['uv']:floats(uv)
        integer(len(o['slots']))
        for slot in o['slots']:
            string(slot['material']);integer(len(slot['triangles']))
            for i in range(0,len(slot['triangles']),3):
                # JC | 2026-11-16 | 회전 좌표 변환은 면의 방향을 보존하며 Unity 외측 노멀과 대조합니다.
                for index in slot['triangles'][i:i+3]:integer(index)
print('Packed',len(data['objects']),'objects')
