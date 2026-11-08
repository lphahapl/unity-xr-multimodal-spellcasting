from pathlib import Path
import json

root = Path(r'D:\UnityProjects\Campus-Monopoly')
stage = Path(__file__).resolve().parent
backup = stage / '发布前备份'
backup.mkdir(exist_ok=True)
files = ['.gitattributes', 'README.md', 'Docs/Standards/GIT_COMMIT_CONVENTION.md']
for name in files:
    dst = backup / name
    dst.parent.mkdir(parents=True, exist_ok=True)
    assert not dst.exists(), dst
    dst.write_bytes((root / name).read_bytes())

old = (root / '.gitattributes').read_text(encoding='utf-8')
attrs = old.split('# Preserve binary assets')[0]
attrs += '# Binary source assets use Git LFS (case-insensitive extensions)\n'
extensions = 'fbx blend obj max ma mb c4d png tga jpg jpeg psd psb tif tiff exr hdr dds bmp webp kra spp sbsar wav ogg mp3 aif aiff flac m4a mp4 mov avi webm ttf otf woff woff2 dll so dylib'.split()
for ext in extensions:
    pattern = '*.' + ''.join(f'[{c}{c.upper()}]' if c.isalpha() else c for c in ext)
    attrs += f'{pattern} filter=lfs diff=lfs merge=lfs -text\n'
attrs += '\n# Binary Unity lighting data; keep other .asset files out of LFS\nLightingData.asset filter=lfs diff=lfs merge=lfs -text\n*.asset -text\n'
(stage / 'gitattributes.new').write_text(attrs, encoding='utf-8', newline='\n')

guide = (root / 'Docs/Standards/GIT_COMMIT_CONVENTION.md').read_text(encoding='utf-8')
previous = '5. FBX、音频、源美术等二进制资产无法逐行合并。超过远程仓库单文件限制，或团队开始频繁提交大型二进制前，由仓库维护人配置并验证 Git LFS；不能只写 LFS 规则却让其他人无法拉取。'
assert previous in guide
guide = guide.replace(previous, '5. 工程已启用 Git LFS。模型、图片、音频、视频、字体和二进制插件按根目录 `.gitattributes` 规则存储；脚本、场景、Prefab、材质与 `.meta` 不进入 LFS。新增二进制格式前补充对应规则；已经提交的大文件需要迁移历史时，先与维护人确认，不能擅自改写共享历史。')
guide += '''
## Git LFS 使用

首次参与工程，先安装 Git LFS，在本机执行一次 `git lfs install`，再克隆仓库：

```powershell
git lfs install
git clone https://cnb.cool/NeuGameMR/Campus_Monopoly.git
cd Campus_Monopoly
git lfs pull
```

已有工作副本也执行 `git lfs install` 和 `git lfs pull`。常规提交继续使用 `git add`、`git commit`、`git push`，LFS 的提交过滤器和 pre-push hook 会自动处理资源；不要跳过 hook，也不要设置跳过 LFS 下载后把指针文件当作模型交给 Unity。

提交前用 `git lfs ls-files` 查看受管资源，用 `git lfs status` 查看 LFS 变更。推送后确认 Git 和 LFS 均成功；新环境拉取完成后再打开 Unity。`Library/`、`Temp/`、日志和个人设置由 `.gitignore` 排除，不通过 LFS 上传。
'''
(stage / 'git_convention.new').write_text(guide, encoding='utf-8', newline='\n')
readme = (root / 'README.md').read_text(encoding='utf-8')
readme += '''
## 获取工程与大文件

仓库：https://cnb.cool/NeuGameMR/Campus_Monopoly ，主分支为 `main`。

先安装 Git LFS 并执行 `git lfs install`，再克隆工程；已有副本执行 `git lfs pull`，确认模型等资源已完整下载后再用 Unity Hub 打开。二进制资源由 `.gitattributes` 自动交给 LFS 管理。具体命令和团队约定见 [Git 规范](Docs/Standards/GIT_COMMIT_CONVENTION.md#git-lfs-使用)。
'''
(stage / 'readme.new').write_text(readme, encoding='utf-8', newline='\n')
print(json.dumps({'staged_files': files, 'lfs_extensions': len(extensions)}, ensure_ascii=False))
