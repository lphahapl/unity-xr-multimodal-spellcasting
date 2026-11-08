from pathlib import Path
from datetime import datetime,timezone,timedelta
import json,hashlib
P=Path('D:/UnityProjects/NarutoVR');L=P/'Logs/zhunwang-inscription-20261105';now=datetime.now(timezone(timedelta(hours=8))).isoformat();task='ZHUNWANG-INSCRIPTION-20261105';result=[]
req=f'''

## {task}-REQ｜准望匣圆盘五字小篆风格铭文

- 任务ID：{task}；模型GPT-6（细分型号未提供）；入口Codex；时间：{now}。
- 用户要求修改圆盘贴图，沿圆心添加铜叶关键字，随后明确指定“规矩律准绳”；第三字采用“律”。状态：铭文字稿、局部烘焙、Unity原位更新及实际URP预览已完成；主观样式与头显阅读效果未获用户后续评价。
- 以五个小篆风格字形“規、矩、律、準、繩”环排，中心留给旋钮。字稿用内置image_gen生成，浅刻颜色/法线/粗糙度/AO烘焙到既有UV。铜叶五种全名不变，不新增玩法。承接ZHUNWANG-10K-20261105-REQ。

### 文件清单

以下资产简称根目录为 `Assets/SealCombat/Art/Props/Zhunwang/`；完整前后hash见 `Logs/zhunwang-inscription-20261105/asset-manifest.json`。

| 路径 | 具体变化 |
|---|---|
| Zhunwang根目录/Textures/Zhunwang_BaseColor.png | 圆盘175个既有面的UV内添加深铜色五字铭文；2K |
| Zhunwang根目录/Textures/Zhunwang_Normal.png | 将投射铭文浅凹刻Bump与原法线一起局部烘焙；不新增面 |
| Zhunwang根目录/Textures/Zhunwang_Roughness.png、Zhunwang_AO.png | 铭文槽内粗糙度和遮蔽局部调整；2K |
| Zhunwang根目录/Textures/Zhunwang_MetallicSmoothness.png | 保留金属RGB，用新粗糙度反转更新Alpha |
| Zhunwang根目录/使用说明.txt | 新增五字、最新源目录/验证记录、无铭文10k备份路径；明确FBX/材质/meta保持 |
| Logs/zhunwang-inscription-20261105/before/ | 无铭文10k版本完整资产备份，含meta |
| Logs/zhunwang-inscription-20261105/unity-result.json、Unity_Inscribed.png、tool-ready.txt | 实际主工程导入、计数/材质/引用/场景状态、URP图与编译回执 |
| Logs/zhunwang-inscription-20261105/ZhunwangInscriptionUpdateOnce.cs及.cs.meta | 临时Editor检查器由Assets/SealCombat/Editor同名路径移出归档；不保留轮询代码 |
| Logs/zhunwang-inscription-20261105/final-report.json、asset-manifest.json、package-check.json、completion-check.json、finalize.py、record_handoff.py | 制作/验收/打包、变更hash、追加历史检查及生成脚本 |
| C:/Users/鲁迅/Downloads/Zhunwang_Inscribed_20261105/ | 新独立成品目录：原FBX副本、新blend/六贴图、Seal_Ring_Source.png、generation_prompt.txt、README_使用说明.txt、report.json、manifest.json及Previews；Work保留投射/烘焙/检查脚本和报告 |
| C:/Users/鲁迅/Downloads/Zhunwang_Inscribed_20261105.zip | 上述交付文件ZIP，不含Work中间缓存；CRC通过 |
| docs/模型交火/需求变更交接.md、世界观变更交接.md | 同任务ID仅末尾追加，既有字节校验保留 |

### 验证与范围

- Blender5.2.2 LTS隔离后台；新旧blend的顶点/面索引/UV/自定义法线/变换合并SHA256均为497dca9ef168b95459cf55ecd9fba4713c19f56e293b2af4ca4eefbcb60e219a。FBX逐字节相同；仍10000三角/4930几何顶点/1材质。未改原高模和旧10k成品。
- 使用image_gen内置模式生成透明五字字稿，完整提示词已存。检查字稿、整体/俯视/背面/圆盘近景；字形为生成的小篆风格，不宣称古文字考据认证。最后一次采用零扩边、旧图为底的局部烘焙；首轮2px扩边导致约千个相邻有效UV像素变化，已通过收紧范围解决，不把首轮报告冒充通过。
- 最终2048图可解码，改动仅在圆盘UV区域；其他有效UV像素变化数逐图均0。Metallic原图字节不变，打包RGB/Alpha逐字节正确；原28个极少量短法线向量像素数量未增加。证据在新成品Work/texture_validation.json与blend_validation.json。
- Unity6000.3.10f1实际导入通过，10000三角/10382渲染顶点/1Renderer；六图实际ASTC4×4、颜色/法线类型正确，四运行时贴图绑定正常。Prefab、FBX、材质及全部meta字节与备份相同，GUID/fileID不变。
- PreviewRenderUtility独立预览可见圆盘五字，未出现缺材质粉色；KaogongP1Trial前后dirty=True，保留已有未保存状态，没有保存场景/新增实例/进入Play。临时脚本归档后refresh完成，Editor程序集18:42:05更新；现有UPM path argument undefined等环境错误未修改或清空。
- 新blend低模使用相对外置贴图，保留已打包的Inscription_Editable_Source源材质；没有给Unity增加叠层材质/透明draw call或几何。头显佩戴阅读、帧时、碰撞空间未实测。
- 使用imagegen、unity-model-pipeline，读取multi-agent-router；遵循用户之前“由你直接完成检查”，本次父代理直接执行/验收，无子代理。Computer Use仅尝试激活Unity处理暂停刷新；激活工具超时，但后续本地编译/请求回执证明实际完成，未用UI保存场景。
- 不更改铜叶名称/战斗效果/世界规则/场景/其他任务代码。现有小篆与金石工艺方向保持，未改世界设定正文、专业Word、README总索引；本次仅资产局部铭文与交接记录。世界观交接同任务记录注明装饰范围。
'''
world=f'''

## {task}-WORLD｜准望匣圆盘五字铭文

- 任务ID：{task}；模型GPT-6（细分型号未提供）；入口Codex；时间：{now}。
- 用户明确指定圆盘沿圆心加入“规矩律准绳”，本次实现为五字环排的小篆风格铜面浅刻；第三字“律”。已烘焙并在Unity实际预览，主观样式待后续使用反馈。
- 本次属于器物表面装饰，延续既有金石/篆体方向；不改变方圆规印、勾股矩印、负衡律印、平准水度、直绳雷度五种铜叶的全名，不新增或确认战斗效果，不将圆盘字序解释为新的机关交互规则。
- 字稿由image_gen生成并人工目视检查，未作为古文字考据定本。面数仍10000，其他有效UV区域保持原样。世界设定正文/专业Word未改；资产说明已更新。完整文件、像素/几何/Unity验证与备份见[需求变更交接](需求变更交接.md) {task}-REQ。
'''
for filename,text in [('需求变更交接.md',req),('世界观变更交接.md',world)]:
    p=P/'docs/模型交火'/filename;before=p.read_bytes();assert (task+('-REQ' if filename.startswith('需求') else '-WORLD')).encode() not in before
    with p.open('ab') as f:f.write(text.encode('utf8'))
    assert p.read_bytes()[:len(before)]==before
    result.append({'file':str(p),'old_bytes':len(before),'old_sha256':hashlib.sha256(before).hexdigest(),'prefix_preserved':True})
checks={'time':now,'handoffs':result,'temporary_script_removed':not(P/'Assets/SealCombat/Editor/ZhunwangInscriptionUpdateOnce.cs').exists(),'temporary_meta_removed':not(P/'Assets/SealCombat/Editor/ZhunwangInscriptionUpdateOnce.cs.meta').exists(),'refresh_request_consumed':not(P/'Logs/linear-rpg.request').exists(),'editor_assembly_time':datetime.fromtimestamp((P/'Library/ScriptAssemblies/Assembly-CSharp-Editor.dll').stat().st_mtime,timezone(timedelta(hours=8))).isoformat()}
assert checks['temporary_script_removed'] and checks['temporary_meta_removed'] and checks['refresh_request_consumed']
(L/'completion-check.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(checks,ensure_ascii=False,indent=2))
