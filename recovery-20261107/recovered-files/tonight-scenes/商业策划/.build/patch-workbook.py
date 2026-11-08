from pathlib import Path
p=Path(__file__).with_name('build-workbook.mjs')
s=p.read_text('utf-8').replace("set(actual,'A5','核销归因贡献')","set(actual,'A5','订单归因贡献')")
s=s.replace('IF(G10<>1,"",IF(COUNT(H10:I10)=2,H10-I10,"待补数据"))','IF(AND(G10<>1,COUNT(H10:I10)=0),"",IF(COUNT(H10:I10)=2,H10-I10,"待补数据"))')
s=s.replace('IF(G10<>1,"",IF(COUNT(H10:L10)=5,H10-I10-J10-K10-L10,"待补数据"))','IF(AND(G10<>1,COUNT(H10:L10)=0),"",IF(COUNT(H10:L10)=5,H10-I10-J10-K10-L10,"待补数据"))')
s=s.replace("set(actual,'D112','归因贡献不等于净新增贡献；经营增量需另作基线比较。');","set(actual,'D112','取消组如有退款或已发生成本，也录入金额；归因贡献与净新增贡献分开。');")
old="actual.getRange('A10:L10').clear({applyTo:'contents'});\nwb.recalculate();"
new="actual.getRange('A10:L10').values=[['QA-CANCEL',new Date('2026-10-20T00:00:00Z'),'DY01','朋友庆生',1,1,0,0,0,0,10,0]];wb.recalculate();if(actual.getRange('N10').values[0][0]!==-10)throw Error('取消组耗材未计入');actual.getRange('A10:L10').clear({applyTo:'contents'});\nwb.recalculate();"
assert old in s
s=s.replace(old,new)
p.write_text(s,encoding='utf-8')
