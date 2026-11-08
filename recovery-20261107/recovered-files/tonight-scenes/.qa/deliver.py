from pathlib import Path
import hashlib,shutil,zipfile,json
root=Path(__file__).resolve().parents[1]
target=Path(r'C:\Users\鲁迅\Desktop\今晚唱哪一幕')
assert target.resolve()==Path(r'C:\Users\鲁迅\Desktop\今晚唱哪一幕').resolve()
files=[p for p in root.rglob('*') if p.is_file() and not {'.qa','.build'}.intersection(p.relative_to(root).parts) and p.suffix.lower()!='.zip' and not p.name.endswith('.inspect.ndjson')]
archive=root/'今晚唱哪一幕.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED)as z:
    for file in sorted(files):z.write(file,Path('今晚唱哪一幕')/file.relative_to(root))
with zipfile.ZipFile(archive)as z:assert z.testzip()is None
files.append(archive)
manifest=[]
for source in files:
    out=target/source.relative_to(root);out.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,out)
    digest=hashlib.sha256(source.read_bytes()).hexdigest()
    assert hashlib.sha256(out.read_bytes()).hexdigest()==digest
    manifest.append({'path':str(source.relative_to(root)),'bytes':out.stat().st_size,'sha256':digest})
(root/'.qa/delivery-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'desktop':str(target),'files':len(files),'archiveBytes':archive.stat().st_size},ensure_ascii=False))
