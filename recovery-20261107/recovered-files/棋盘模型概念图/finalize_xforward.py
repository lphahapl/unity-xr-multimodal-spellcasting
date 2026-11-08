from pathlib import Path
import re, json, tarfile, io, zipfile, hashlib, os
import numpy as np
from scipy.spatial.transform import Rotation
import yaml

workspace=Path(__file__).resolve().parent.parent
root=Path(os.environ.get('MODULAR_BOARD_OUT',str(workspace/'棋盘模型-v2-X轴前进')))
version='v3' if root.name.startswith('棋盘模型-v3') else 'v2'
target=Path(r'D:\unity_training\Runnner&&Lucy\Assets\ModularBoard')
prefab=target/'Prefabs/Board_10m_36Tiles.prefab'
source=(root/'Unity/Assets/ModularBoard/Prefabs/Board_10m_36Tiles.prefab')
assert source.read_bytes()==prefab.read_bytes()
text=prefab.read_text(encoding='utf-8-sig')
objects={}
for m in re.finditer(r'^--- !u!\d+ &(-?\d+)\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S):
    objects[int(m.group(1))]=yaml.safe_load(m.group(2))
transforms={k:v['Transform'] for k,v in objects.items() if 'Transform' in v}
names={k:v['GameObject']['m_Name'] for k,v in objects.items() if 'GameObject' in v}
cache={}
def vector(d,keys='xyz'): return np.array([d[k] for k in keys],dtype=float)
def world(key):
    if key==0: return np.eye(4)
    if key in cache: return cache[key]
    t=transforms[key]
    local=np.eye(4)
    local[:3,:3]=Rotation.from_quat(vector(t['m_LocalRotation'],'xyzw')).as_matrix()@np.diag(vector(t['m_LocalScale']))
    local[:3,3]=vector(t['m_LocalPosition'])
    cache[key]=world(t['m_Father']['fileID'])@local
    return cache[key]
tiles=sorted([(names[t['m_GameObject']['fileID']],key) for key,t in transforms.items()
              if re.match(r'^Tile_[0-9]{2}_',names[t['m_GameObject']['fileID']])])
assert len(tiles)==36
layout=[]
for i,(name,key) in enumerate(tiles):
    matrix=world(key); next_matrix=world(tiles[(i+1)%36][1])
    delta=next_matrix[:3,3]-matrix[:3,3]
    right=matrix[:3,0]/np.linalg.norm(matrix[:3,0])
    up=matrix[:3,1]/np.linalg.norm(matrix[:3,1])
    assert np.dot(right,delta/np.linalg.norm(delta))>.99999,(name,right,delta)
    assert np.dot(up,[0,1,0])>.99999,name
    assert abs(np.linalg.norm(delta)-1)<.0001,name
    layout.append({'index':i,'next_index':(i+1)%36,'name':name,'position':np.round(matrix[:3,3],6).tolist(),
                   'right':np.round(right,6).tolist(),'up':np.round(up,6).tolist()})
(root/'unity-route-layout.json').write_text(json.dumps(layout,ensure_ascii=False,indent=2),encoding='utf-8')

# Explicitly distinguish authoring/glTF coordinates from the tested Unity FBX coordinates.
layout_path=root/'board-layout.json'
authoring=json.loads(layout_path.read_text(encoding='utf-8'))
for entry,actual in zip(authoring,layout):
    entry['position_gltf_y_up_m']=entry.pop('position_unity_m')
    entry['forward_gltf_y_up']=entry.pop('forward_unity')
    entry['position_unity_m']=actual['position']
    entry['forward_unity']=actual['right']
layout_path.write_text(json.dumps(authoring,ensure_ascii=False,indent=2),encoding='utf-8')

# Keep the native exported package; create the URP variant with the project's verified material assets.
package=root/f'ModularBoard_{version}-BuiltIn.unitypackage'
urp=root/f'ModularBoard_{version}-URP.unitypackage'
with tarfile.open(package,'r:gz') as src:
    members=src.getmembers()
    data={m.name:src.extractfile(m).read() for m in members if m.isfile()}
paths={name.rsplit('/',1)[0]:content.decode('utf-8').rstrip('\x00\r\n') for name,content in data.items() if name.endswith('/pathname')}
replaced=0
with tarfile.open(urp,'w:gz') as dest:
    for member in members:
        if member.isfile():
            content=data[member.name]
            folder,leaf=member.name.rsplit('/',1)
            pathname=paths.get(folder,'')
            if leaf=='asset' and re.match(r'^Assets/ModularBoard/Materials/MB_.*\.mat$',pathname):
                material=target/Path(pathname).relative_to('Assets/ModularBoard')
                content=material.read_bytes()
                assert b'933532a4fcc9baf4fa0491de14d08ed7' in content
                replaced+=1
            member.size=len(content)
            dest.addfile(member,io.BytesIO(content))
        else: dest.addfile(member)
assert replaced==10,replaced
with tarfile.open(urp,'r:gz') as check:
    assert len([m for m in check.getmembers() if m.name.endswith('/pathname')])>30

readme=root/'使用说明.md'
doc=readme.read_text(encoding='utf-8').replace('# 棋盘模型 v1','# 棋盘模型 v2：本地 +X 前进').replace('ModularBoard_v1.blend','ModularBoard_v2.blend')
doc=doc.replace('`board-layout.json` 保存 36 格的坐标和类型；Unity 坐标字段采用 Blender 到 Y-up 的坐标换算约定，实际业务路径可直接读取场景中各格的 Transform。',
                '`board-layout.json` 保存建模坐标与 Unity 坐标；`unity-route-layout.json` 保存已验证的 Unity 预制体实际位置、right、up 和下一格编号。')
intro='''
## 本次修改：移动方向

- 完整棋盘每格的本地 **+X** 指向下一格中心，本地 **+Y** 朝上。
- 编号按 **00 → 01 → … → 35 → 00** 闭环；转角取离开该格的方向。
- 在 `Geometry` 下使用 `Tile_00_Start` 等格子根节点的 `Transform`。网格零件（如 Top、Strip）的坐标轴用于美术变换。
- `Route_Colliders/Tile_00` 等碰撞节点的方向与对应格子一致。
- 独立单格预制体同样以本地 +X 为移动正向；起点箭头指向 +X。
- Unity 代码中使用 `tileTransform.right` 获取世界空间前进方向。`transform.forward` 对应的是本地 +Z。
- 格子仍为 1m × 1m，可见模型外观与原版一致，坐标轴在 FBX 导入后的 Unity 预制体上逐格验证。

```csharp
Vector3 moveDirection = currentTile.right;
player.position += moveDirection * speed * Time.deltaTime;
```

这是移动方向示例，跨格、转角时由游戏代码更新 `currentTile`。

## 导入包选择

你的 `Runnner&&Lucy` 工程使用 URP，选择 **ModularBoard_v2-URP.unitypackage**；Built-in 工程选择 **ModularBoard_v2-BuiltIn.unitypackage**。`Unity` 文件夹保留 Built-in 验证工程的源资源。目标工程已就地更新 Models、Prefabs、Editor，材质保持工程内原有 URP 版本，场景未替换。

'''
doc=doc.replace('## 尺寸',intro+'## 尺寸',1)
if version=='v2': readme.write_text(doc,encoding='utf-8')
for old_blend in (root/'Blender').glob('*.blend'):
    if old_blend.name!=f'ModularBoard_{version}.blend': old_blend.unlink()

excluded={'Library','Temp','Logs','UserSettings','obj','.vs'}
files=[p for p in root.rglob('*') if p.is_file() and not any(s in excluded for s in p.relative_to(root).parts) and p.suffix not in ('.blend1','.log','.tmp') and p.name!='sha256-manifest.json']
hashes={str(p.relative_to(root)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
(root/'sha256-manifest.json').write_text(json.dumps(hashes,ensure_ascii=False,indent=2),encoding='utf-8')
archive=workspace/(root.name+'-完整交付.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for path in files+[root/'sha256-manifest.json']: z.write(path,str(Path(root.name)/path.relative_to(root)))
with zipfile.ZipFile(archive) as z: assert z.testzip() is None
print('PASS: 36 target-project tile frames, native URP package, delivery zip:',str(archive))
