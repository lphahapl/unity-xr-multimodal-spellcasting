from pathlib import Path
import shutil,json,hashlib
root=Path(__file__).resolve().parents[1];workspace=root.parent
desktop=Path(r'C:\Users\鲁迅\Desktop\参赛材料')
assert desktop.resolve()==Path(r'C:\Users\鲁迅\Desktop\参赛材料').resolve()
manifest=json.loads((root/'.build/manifest.json').read_text('utf-8'))
for target in [workspace/'参赛材料汇总/齿轮工坊',desktop/'齿轮工坊']:
    target.mkdir(parents=True,exist_ok=True)
    for entry in manifest:
        source=root/entry['path'];out=target/entry['path'];out.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,out)
        assert hashlib.sha256(out.read_bytes()).hexdigest()==entry['sha256']
promise=workspace/'参赛材料汇总/待盖章承诺书/齿轮工坊-380095承诺书.jpg'
(desktop/'待盖章承诺书').mkdir(exist_ok=True)
shutil.copy2(promise,desktop/'待盖章承诺书'/promise.name)
assert hashlib.sha256(promise.read_bytes()).digest()==hashlib.sha256((desktop/'待盖章承诺书'/promise.name).read_bytes()).digest()
catalog=desktop/'材料目录.txt'
text=catalog.read_text('utf-8-sig')
if '齿轮工坊（380095）' not in text:
    text=text.replace('四个项目按名称分别归档','五个项目按名称分别归档').replace('待盖章承诺书：四份文件','待盖章承诺书：五份文件')
    text=text.replace('待盖章承诺书：','齿轮工坊（380095）：可离线运行的完整网页、源码与算法测试、8张运行及导出图、12页正式PPT、Word格式AI说明、申报信息、上传程序包与完整材料包。\n\n待盖章承诺书：')
    catalog.write_text(text,encoding='utf-8-sig')
(workspace/'参赛材料汇总/材料目录.txt').write_text(text,encoding='utf-8-sig')
print(json.dumps({'desktopFolder':str(desktop/'齿轮工坊'),'verifiedFiles':len(manifest),'promiseBytes':promise.stat().st_size},ensure_ascii=False))
