from pathlib import Path
import base64
root=Path(__file__).resolve().parents[1]
image=root/'网页/assets/campaign-poster.png'
(root/'网页/assets/poster-data.js').write_text('/* 原始海报的本地下载副本，兼容 file://，不依赖网络或跨域读取。 */\nwindow.CampaignPosterBase64="'+base64.b64encode(image.read_bytes()).decode('ascii')+'";\n',encoding='utf-8')
html=root/'网页/index.html'
text=html.read_text('utf-8').replace('<script src="app.js"></script>','<script src="assets/poster-data.js"></script><script src="app.js"></script>')
html.write_text(text,encoding='utf-8')
