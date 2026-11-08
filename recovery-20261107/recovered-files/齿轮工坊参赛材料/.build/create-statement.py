from pathlib import Path
import json
from docx import Document
from docx.shared import Cm,Pt,RGBColor
from docx.oxml.ns import qn
root=Path(__file__).resolve().parents[1]
data=json.loads((root/'申报信息.json').read_text('utf-8'))
doc=Document();sec=doc.sections[0]
sec.page_width=Cm(21);sec.page_height=Cm(29.7)
sec.top_margin=Cm(2.4);sec.bottom_margin=Cm(2.4);sec.left_margin=Cm(2.5);sec.right_margin=Cm(2.5)
for name in ['Normal','Title','Heading 1']:
    style=doc.styles[name];style.font.name='Microsoft YaHei';style.font.color.rgb=RGBColor(0,0,0)
    style._element.get_or_add_rPr().get_or_add_rFonts().set(qn('w:eastAsia'),'Microsoft YaHei')
doc.styles['Normal'].font.size=Pt(11)
doc.styles['Normal'].paragraph_format.line_spacing=1.4
doc.styles['Normal'].paragraph_format.space_after=Pt(8)
doc.styles['Title'].font.size=Pt(18)
doc.styles['Title'].paragraph_format.space_after=Pt(15)
doc.styles['Heading 1'].font.size=Pt(12)
doc.styles['Heading 1'].paragraph_format.space_before=Pt(13)
doc.styles['Heading 1'].paragraph_format.space_after=Pt(7)
parts=[('Title','齿轮工坊人工智能工具使用情况说明'),('Normal','作品编号：'+str(data['projectId'])),
('Heading 1','一、使用工具'),('Normal','本项目使用 OpenAI ChatGPT/Codex 对话式辅助环境，辅助讨论方案与实现细节、编写和调试程序，以及整理项目说明和参赛汇报材料。'),
('Heading 1','二、使用范围'),
('Normal','界面与交互：辅助设计浅色网格实验台、零件选择、传动参数显示、教学实验、挑战任务和手机布局。'),
('Normal','程序实现：辅助编写 HTML、CSS 与 JavaScript，实现参数化齿廓绘制、拖拽吸附、齿轮图遍历、转速与旋向求解、旋转相位处理、冲突检测、设计文件保存与恢复、PNG 图片导出。'),
('Normal','测试与调试：辅助编写和运行传动算法测试，核对大小齿轮传动、惰轮、闭环冲突、设计恢复、键盘操作、挑战判定及手机视口，修正观察到的问题。'),
('Normal','材料整理：辅助撰写作品简介、创意来源、运行环境、操作说明、作品汇报 PPT 及本说明。'),
('Heading 1','三、资料与图像'),
('Normal','齿轮、网格与实验图由本项目使用 SVG 和 Canvas 程序化绘制，未使用生成式图像作为页面素材。参赛截图及 PPT 中的页面图像来自本项目实际运行界面，实验记录图由项目自身导出功能生成。'),
('Normal','齿轮运动学原理参考 MIT OpenCourseWare 课程 2.72 Elements of Mechanical Design 的 Lecture 12: Belt, friction, gear drives（2009）。课程资料用于原理参考，未复制其图像作为项目素材。'),
('Normal','参考资料：https://ocw.mit.edu/courses/2-72-elements-of-mechanical-design-spring-2009/ （Lecture 12）。')]
for style,text in parts:doc.add_paragraph(text,style)
for elem in [doc.element,doc.styles.element]:
    for border in list(elem.xpath('.//w:pBdr')):border.getparent().remove(border)
out=root/'Word/齿轮工坊人工智能工具使用情况说明.docx';out.parent.mkdir(parents=True,exist_ok=True)
doc.save(out);print(out)
