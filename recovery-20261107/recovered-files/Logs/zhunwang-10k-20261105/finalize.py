from pathlib import Path
from datetime import datetime, timezone, timedelta
import hashlib, json, shutil, zipfile

P = Path('D:/UnityProjects/NarutoVR')
L = P/'Logs/zhunwang-10k-20261105'
R = Path('C:/Users/鲁迅/Downloads/Zhunwang_10k_20261105')
A = P/'Assets/SealCombat/Art/Props/Zhunwang'
B = L/'before'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,obj): p.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
source = Path('C:/Users/鲁迅/Downloads/afc732f53efb08af2705e86f28f16e8d.fbx')
assert sha(source)=='6adcad1eebf7e732d22653e70932a41e81a0b12d0d30261ed6ba1511cb47cc59'
unity=read(L/'unity-result.json')
assert unity['passed'] and unity['triangles']==10000 and unity['scenePreserved']
assert unity['prefabGuid']=='3192f5b6fb34ea343be780793e1234c8'
assert unity['fbxGuid']=='4f646d0a1e365804892cdf9bac78f5cd'
assert unity['materialGuid']=='cabcacbedda957b46a68d3ab6cc165ef'
manifest=[]
for f in sorted(A.rglob('*')):
    if not f.is_file(): continue
    rel=f.relative_to(A)
    old=B/rel
    row={'file':str(f.relative_to(P)).replace('\\','/'),'bytes':f.stat().st_size,'sha256':sha(f),'before_sha256':sha(old) if old.exists() else None}
    row['changed']=row['sha256']!=row['before_sha256']
    if f.suffix=='.meta' or f.suffix in ('.mat','.prefab'): assert not row['changed'],str(rel)
    if f.suffix in ('.fbx','.png'): assert sha(R/rel)==row['sha256']
    manifest.append(row)
write(L/'asset-manifest.json',manifest)
evidence={name:read(R/'Work'/name) for name in ['retopo_report.json','uv_report.json','uv_sliver_repair.json','uv_check.json','uv_exact_check.json','bake_report.json','readback_report.json','texture_check.json','visual_metrics.json','path_check.json']}
evidence['bake_report.json']['pilot_uv_pixels']=evidence['bake_report.json'].pop('final_uv_pixels')
evidence['bake_report.json']['final_uv_pixels']=evidence['uv_check.json']['occupied_pixels']
evidence['bake_report.json']['unity_import_verified']=True
evidence['readback_report.json']['unity_import_verified']=True
now=datetime.now(timezone(timedelta(hours=8))).isoformat()
report={'generated_at':now,'status':'Completed: rebaked 10k model imported and previewed in main Unity project','model':'GPT-6; finer model identifier unavailable','source':{'path':str(source),'sha256':sha(source),'unchanged':True},'previous_triangles':23998,'triangles':10000,'triangle_reduction_percent':(1-10000/23998)*100,'previous_unity_vertices':22134,'unity_vertices':unity['vertices'],'unity_vertex_reduction_percent':(1-unity['vertices']/22134)*100,'blender_version':'5.2.2 LTS d13f752e3b9c','method':'Static edge-collapse from repaired 160k working mesh, weighted normals fixed before bake, new xatlas UV; not manual quad retopology','textures':'Six 2048 PNGs rebaked from original high mesh and original material; URP uses BaseColor, Normal, MetallicSmoothness, AO','evidence':evidence,'unity':unity,'asset_manifest':manifest,'backup':str(B),'limitations':['Very small bevels and slot-end curves are visibly more polygonal at close inspection.','Normal map has 28 pixels shorter than 0.5 among 1770683 UV pixels; recorded, not asserted perfectly artifact-free.','Four-view silhouette IoU measures outline similarity only, not full surface equivalence.','No Quest hardware frame-time or headset wear-scale test. No new gameplay or collision components.','Scene had existing dirty=True before update and remains dirty=True; not saved or altered by this task.'],'review':'Parent checked numeric QA, four Blender material views, wireframe, comparison, exported FBX, actual Unity preview. User requested direct parent checks.'}
write(R/'report.json',report);write(L/'final-report.json',report)
text='''准望匣 · 一万三角面版本

已从上一版 23,998 三角面降到 10,000，减少约 58.3%。
几何顶点 4,930；Unity 渲染顶点 10,382（UV/法线接缝拆点）。
1 网格、1 Renderer、1 材质。没有动画和碰撞体。

Unity 工程已更新：
D:/UnityProjects/NarutoVR/Assets/SealCombat/Art/Props/Zhunwang/Zhunwang.prefab
原位替换 FBX 与六张贴图；Prefab、材质和全部 .meta 字节保持一致。
原 24k Unity 资产备份：D:/UnityProjects/NarutoVR/Logs/zhunwang-10k-20261105/before/

文件：
- Zhunwang_Low.fbx：低模，-Z Forward / Y Up。
- Zhunwang_pipeline.blend：低模、新 UV、烘焙材质，以及隐藏的源高模；高模原图已打包。
- Textures：BaseColor、Normal、Roughness、Metallic、AO、MetallicSmoothness，均 2048×2048。
- Previews：高低模同机位对比、24k/10k 对比、线框、UV、实际 Unity 预览。
- report.json：制作与实际验收数据。

采用静态边折叠减面，从修复后的约 160k 工作网格重做；非手工四边形重拓扑。
重新展开 UV、修复微小重叠，从原始 1499540 三角高模重新烘焙。
UV 检查无重叠、越界、零面积；网格无非流形边和退化面。
零扩边 512 投射检查未命中约 0.161%；最终 2K 图扩边 16 像素。
法线中有 28 个极少量短向量像素，详见报告。四视角高低模轮廓 IoU 99.50%–99.81%。
轮廓、槽口、带扣及主要纹饰保留；细小倒角/圆弧近看会更简化。

Unity 6000.3.10f1 实际导入与 URP/Lit 预览通过。
BaseColor=sRGB；其余为线性；Normal 使用 Y+、导入法线与 Mikk 切线。
MetallicSmoothness：RGB=Metallic，Alpha=1-Roughness；材质 Smoothness 乘数 1。
实际 Android 纹理 ASTC4×4，保留 mipmap 与原有贴图设置。
贴图保持 2K，因此不据此宣称纹理内存降低或头显帧率提升。

根节点 scale=1，Visual 保留 FBX 单位换算 scale=100。
整体尺寸约 0.558×0.895×0.931 Unity 单位；佩戴尺寸需在根节点统一缩放。
未实测 Quest GPU 帧时、佩戴比例、追踪和铜片插入空间/碰撞。
主场景原有未保存状态保持不变；本任务未保存场景。
'''
(R/'README_使用说明.txt').write_text(text,encoding='utf8')
deliver=[R/'Zhunwang_Low.fbx',R/'Zhunwang_pipeline.blend',R/'README_使用说明.txt',R/'report.json']+sorted((R/'Textures').glob('*.png'))+sorted((R/'Previews').glob('*.png'))
write(R/'manifest.json',[{'file':str(p.relative_to(R)).replace('\\','/'),'bytes':p.stat().st_size,'sha256':sha(p)} for p in deliver])
deliver.append(R/'manifest.json')
archive=R.with_suffix('.zip')
assert not archive.exists(),'Do not overwrite an existing package'
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for f in deliver: z.write(f,f.relative_to(R))
with zipfile.ZipFile(archive) as z: assert z.testzip() is None
write(L/'package-check.json',{'archive':str(archive),'sha256':sha(archive),'bytes':archive.stat().st_size,'crc_check':True,'files':len(deliver),'utc_offset_timestamp':now})
print(json.dumps({'triangles':report['triangles'],'unity_vertices':report['unity_vertices'],'all_meta_prefab_material_bytes_unchanged':True,'changed_files':[x['file'] for x in manifest if x['changed']],'package':str(archive)},ensure_ascii=False,indent=2))
