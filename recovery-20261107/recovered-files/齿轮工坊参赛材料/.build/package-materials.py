from pathlib import Path
import json,shutil,zipfile,hashlib
root=Path(__file__).resolve().parents[1]; workspace=root.parent
src=workspace/'gear-workshop';program=root/'项目程序'
program.mkdir(exist_ok=True)
for folder in ['dist','tests']:
    shutil.copytree(src/folder,program/folder,dirs_exist_ok=True)
shutil.copy2(src/'README.md',program/'README.md')
data=json.loads((root/'申报信息.json').read_text('utf-8'))
text='齿轮工坊——看得见的机械传动\n作品编号：'+str(data['projectId'])+'\n分类：智慧教育与科普创作\n参赛学生：'+ '、'.join(data['students'])+'\n指导教师：'+'、'.join(data['teachers'])+'\n\n运行：解压后双击 项目程序/dist/index.html，推荐 Chrome 或 Edge，可离线使用。\n\n材料：项目程序、8张实机/导出图、12页作品汇报PPT、Word格式AI说明和申报信息。\n承诺书已下载，另置桌面“参赛材料/待盖章承诺书”，盖章后再上传。\n\n'
for key,title in [('worksintroduction','作品简介'),('chuangyi','创意来源'),('huanjing','运行环境')]:text+=title+'\n'+data[key]+'\n\n'
(root/'申报与运行说明.txt').write_text(text,encoding='utf-8-sig')
upload=root/'上传程序包';upload.mkdir(exist_ok=True)
zip_path=upload/'齿轮工坊.zip'
with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED)as z:
    for file in sorted(program.rglob('*')):
        if file.is_file():z.write(file,file.relative_to(root))
    for file in (root/'Word').glob('*.docx'):z.write(file,file.relative_to(root))
    z.write(root/'申报与运行说明.txt','申报与运行说明.txt')
with zipfile.ZipFile(zip_path)as z:assert z.testzip()is None
full=root/'齿轮工坊参赛材料.zip'
with zipfile.ZipFile(full,'w',zipfile.ZIP_DEFLATED)as z:
    for file in sorted(root.rglob('*')):
        if file.is_file() and '.build' not in file.relative_to(root).parts and file!=full:z.write(file,Path('齿轮工坊')/file.relative_to(root))
with zipfile.ZipFile(full)as z:assert z.testzip()is None
manifest=[]
for file in root.rglob('*'):
    if file.is_file()and'.build'not in file.relative_to(root).parts:manifest.append({'path':file.relative_to(root).as_posix(),'bytes':file.stat().st_size,'sha256':hashlib.sha256(file.read_bytes()).hexdigest()})
(root/'.build/manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'files':len(manifest),'uploadBytes':zip_path.stat().st_size,'fullBytes':full.stat().st_size},ensure_ascii=False))
