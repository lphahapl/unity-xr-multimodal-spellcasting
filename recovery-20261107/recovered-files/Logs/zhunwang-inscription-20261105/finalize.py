from pathlib import Path
from datetime import datetime,timezone,timedelta
import json,hashlib,zipfile,shutil
P=Path('D:/UnityProjects/NarutoVR');L=P/'Logs/zhunwang-inscription-20261105';R=Path('C:/Users/鲁迅/Downloads/Zhunwang_Inscribed_20261105');A=P/'Assets/SealCombat/Art/Props/Zhunwang';B=L/'before'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,obj):p.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
u=read(L/'unity-result.json');v=read(R/'Work/texture_validation.json');g=read(R/'Work/blend_validation.json');assert u['passed'] and v['pass'] and g['pass']
assert all(x['changed_other_occupied_uv_pixels']==0 for x in v['changes'])
manifest=[]
for f in sorted(A.rglob('*')):
    if not f.is_file():continue
    relative=f.relative_to(A);previous=B/relative;row={'path':str(f.relative_to(P)).replace('\\','/'),'bytes':f.stat().st_size,'sha256':sha(f),'before_sha256':sha(previous)};row['changed']=row['sha256']!=row['before_sha256']
    if f.suffix in ['.meta','.fbx','.mat','.prefab'] or f.name=='Zhunwang_Metallic.png':assert not row['changed'],str(relative)
    if f.suffix=='.png':assert sha(f)==sha(R/relative)
    manifest.append(row)
write(L/'asset-manifest.json',manifest)
report={'time':datetime.now(timezone(timedelta(hours=8))).isoformat(),'request':'Circular seal-style lettering on Zhunwang dial','confirmed_text':'规矩律准绳','rendered_glyph_forms':'規矩律準繩','scope':'175 existing dial faces; no mesh, UV, transform, material-count or shader change','generated_asset':'Built-in image_gen, transparent black glyph ring; prompt archived as generation_prompt.txt','font_note':'Seal-style generated glyphs; not a scholarly palaeographic transcription','asset_manifest':manifest,'bake':read(R/'Work/inscription_bake_report.json'),'texture_validation':v,'blend_validation':g,'unity':u,'backup':str(B),'review':'Parent reviewed glyph alpha inspection, overall, top, close-up and back Blender renders, plus actual Unity URP preview. No Quest hardware test. Existing scene dirty=True was preserved, not saved.'}
write(R/'report.json',report);write(L/'final-report.json',report)
shutil.copy2(R/'Work/generation_prompt.txt',R/'generation_prompt.txt')
shutil.copy2(R/'Work/Seal_Ring_Generated.png',R/'Seal_Ring_Source.png')
(R/'README_使用说明.txt').write_text('''准望匣 · 圆盘五字铭文

用户确定的文字：规、矩、律、准、绳（第三字为律）。
以小篆风格的規、矩、律、準、繩围绕圆心环排，保留中央旋钮留白。
字稿由内置 image_gen 生成，提示词见 generation_prompt.txt；不是文物字形考据认证。

已更新工程：
D:/UnityProjects/NarutoVR/Assets/SealCombat/Art/Props/Zhunwang/Zhunwang.prefab

圆盘局部更新 BaseColor、Normal、Roughness、AO，再打包 MetallicSmoothness。
六张图仍为2048×2048；独立 Metallic 未改，URP实际仍用四张贴图。
初轮2px局部扩边影响相邻UV，已修正为零扩边覆盖在旧图上。
最终检查：圆盘以外其他有效UV像素改变为0，未改原网格/UV/自定义法线/变换。
FBX、Prefab、材质和全部meta字节保持不变；仍10000三角、Unity10382顶点、1网格1材质。

Zhunwang_pipeline.blend：新贴图绑定成品，源高模隐藏。
Inscription_Editable_Source材质保留投射与凹刻节点；相关源图已打包。
Seal_Ring_Source.png：生成的透明铭文字稿；图像黑色笔画需在浅背景查看。
Previews：Unity实际预览、Blender整体/俯视/圆盘特写/背面。
report.json：实际制作、像素范围、几何一致性及Unity验收记录。

铭文前版本备份：D:/UnityProjects/NarutoVR/Logs/zhunwang-inscription-20261105/before/
不改铜叶的五种全名或战斗效果；铭文是本次圆盘装饰。
原先28个极少量短法线向量像素数量未增加。未进行Quest头显阅读距离或帧时实测。
''',encoding='utf8')
files=[R/'Zhunwang_Low.fbx',R/'Zhunwang_pipeline.blend',R/'Seal_Ring_Source.png',R/'generation_prompt.txt',R/'README_使用说明.txt',R/'report.json']+sorted((R/'Textures').glob('*.png'))+sorted((R/'Previews').glob('*.png'))
write(R/'manifest.json',[{'path':str(p.relative_to(R)).replace('\\','/'),'bytes':p.stat().st_size,'sha256':sha(p)} for p in files]);files.append(R/'manifest.json')
archive=R.with_suffix('.zip');assert not archive.exists()
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for f in files:z.write(f,f.relative_to(R))
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
write(L/'package-check.json',{'archive':str(archive),'sha256':sha(archive),'bytes':archive.stat().st_size,'crc_check':True})
print(json.dumps({'changed_files':[x['path'] for x in manifest if x['changed']],'triangles':u['triangles'],'unity_passed':u['passed'],'package_bytes':archive.stat().st_size},ensure_ascii=False,indent=2))
