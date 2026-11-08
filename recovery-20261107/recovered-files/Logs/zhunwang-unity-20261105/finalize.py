from pathlib import Path
import hashlib,json,datetime,re
P=Path('D:/UnityProjects/NarutoVR');L=P/'Logs/zhunwang-unity-20261105';A=P/'Assets/SealCombat/Art/Props/Zhunwang';S=Path('C:/Users/鲁迅/Downloads/Zhunwang_Optimized_20261105')
report=json.loads((L/'import-result.json').read_text(encoding='utf-8-sig'))
assert report['passed'] and report['sceneStatePreserved'] and report['rootScale']=={'x':1.,'y':1.,'z':1.}
files=[]
for p in sorted(A.rglob('*')):
    if p.is_file():files.append({'path':str(p.relative_to(P)).replace('\\','/'),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
for p in [A/'Zhunwang_Low.fbx',*sorted((A/'Textures').glob('*.png'))]:
    q=S/p.relative_to(A);assert hashlib.sha256(p.read_bytes()).digest()==hashlib.sha256(q.read_bytes()).digest()
meta_guids={re.search(r'^guid: (\w+)',p.read_text(),re.M).group(1) for p in A.rglob('*.meta')}
refs=re.findall(r'guid: (\w+)',(A/'Zhunwang.prefab').read_text());assert all(g in meta_guids for g in refs)
assert not (P/'Assets/SealCombat/Editor/ZhunwangImportOnce.cs').exists()
assert not (P/'Assets/SealCombat/Editor/ZhunwangImportOnce.cs.meta').exists()
(L/'asset-manifest.json').write_text(json.dumps({'files':files,'source_hashes_match':True,'prefab_references_resolve':True,'temporary_helper_archived':True},indent=2,ensure_ascii=False),encoding='utf-8')
stamp=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat();h=P/'docs/模型交火/需求变更交接.md';before=h.read_bytes();tail=before.decode('utf-8-sig')[-600:];print('Tail read before append:',tail[-150:])
entry=f'''

## ZHUNWANG-UNITY-20261105-REQ｜准望匣低模导入 Unity 工程

- 任务ID：ZHUNWANG-UNITY-20261105；模型GPT-6（细分型号未提供）；入口Codex；时间{stamp}。
- 来源：用户明确要求“放进unity工程”，承接ZHUNWANG-LOW-20261105-REQ。状态：已实际导入主工程、URP材质及Prefab已创建，Unity导入检查与预览通过；头显佩戴/性能未验收。

### 变化与文件清单

资产根目录 `Assets/SealCombat/Art/Props/Zhunwang/`。所有新资产与.meta逐文件路径、大小和SHA256见 `Logs/zhunwang-unity-20261105/asset-manifest.json`。

| 路径 | 具体变化 |
|---|---|
| Assets/SealCombat/Art/Props.meta、Props/Zhunwang.meta | 新道具分类及准望匣目录元数据 |
| Zhunwang根目录/Zhunwang_Low.fbx及.meta | 导入上一轮23,998三角低模；保留Normals，CalculateMikk切线，无动画/碰撞体/二次UV，原尺寸，材质重映射至独立URP材质 |
| Zhunwang根目录/Textures.meta；Textures/Zhunwang_BaseColor.png、Zhunwang_Normal.png、Zhunwang_Roughness.png、Zhunwang_Metallic.png、Zhunwang_AO.png、Zhunwang_MetallicSmoothness.png及各.meta | 六张2K图；BaseColor为sRGB，Normal为NormalMap/Y+，其余线性；mipmap、Trilinear、Clamp、aniso4、关闭Readable、高质量压缩；当前Android目标实际ASTC4×4 |
| Zhunwang根目录/Materials.meta；Materials/Zhunwang_URP.mat及.meta | URP/Lit Opaque，绑定颜色、法线、金属/光滑度、AO；Smoothness Metallic Alpha/乘数1，开启相关shader关键字 |
| Zhunwang根目录/Zhunwang.prefab及.meta | 根节点单位缩放，Visual子节点保留FBX单位转换；1网格1Renderer1材质，直接拖用；没有接入腕饰追踪或增加碰撞组件 |
| Zhunwang根目录/使用说明.txt及.meta | 使用路径、贴图配置、原尺寸/Visual缩放注意事项及功能/头显边界 |
| Logs/zhunwang-unity-20261105/source-bake-report.json | 保留上一轮制作报告历史快照，原报告“未导入Unity”由本次新验证记录补充 |
| Logs/zhunwang-unity-20261105/import-result.json、import-before-root-wrapper.json、Unity_Preview.png、asset-manifest.json、finalize.py | 实际主工程导入结果、根节点整理前记录、URP渲染预览、逐文件hash与收尾脚本 |
| Logs/zhunwang-unity-20261105/ZhunwangImportOnce.cs及.cs.meta | 一次性工具源码归档；原临时Assets/SealCombat/Editor/ZhunwangImportOnce.cs及.meta已移出Assets，未保留后台轮询代码 |
| docs/模型交火/需求变更交接.md | 仅末尾追加本记录，历史字节保留 |

### 验证、冲突与边界

- 在正在运行的Unity6000.3.10f1主工程通过AssetDatabase实际执行，当前UniversalRenderPipelineAsset；最终23,998三角、22,134个Unity渲染顶点（UV/法线接缝拆点）、1网格/Renderer/材质；shader受支持且无shader错误。Normals/Mikktspace/原尺寸/材质绑定检查通过。
- Prefab根节点scale=(1,1,1)，Visual子节点保留FBX厘米换算，整体包围盒约0.557578×0.894950×0.931198。未把尺寸擅自改成佩戴尺寸；没有将Visual scale直接归一导致100倍缩小。
- 六图2048×2048实际导入成功，设置及ASTC4×4格式见import-result.json；当前Prefab依赖四张运行时贴图，独立Metallic/Roughness留作编辑。
- Unity PreviewRenderUtility在独立预览场景渲染，父代理检查图中铜色、法线、槽口、带子正常，没有粉色缺shader；主KaogongP1Trial场景前后均未修改、未保存标记False，未增加场景实例，未进入Play。
- 原FBX及六PNG与下载目录成品逐字节hash一致，Prefab内GUID均可解析；原Assets/Enemies、已有场景/运行时代码/.meta/包配置均未改。项目已有UPM“path argument undefined”与XR包元数据错误仍可能出现，未修复或清空，不声称整个Console零错误。
- 继续用户指定主代理直接检查的安排，未调用外部子代理。沿用unity-model-pipeline；Computer Use仅用于查看/唤起Unity处理状态，激活曾超时且窗口最小化，实际成功证据来自Unity脚本回执和渲染文件。
- 未验证头显性能、佩戴比例、追踪、铜片插槽碰撞空间；美术资产导入不改变玩法/世界观，不同步专业Word或设计正文。原下载ZIP保持上一轮交付内容。
'''
with h.open('ab') as f:f.write(entry.encode('utf-8'))
assert h.read_bytes().startswith(before)
(L/'handoff-check.json').write_text(json.dumps({'history_preserved':True,'old_bytes':len(before),'old_sha256':hashlib.sha256(before).hexdigest(),'time':stamp},indent=2))
print('DONE',len(files),'asset files')
