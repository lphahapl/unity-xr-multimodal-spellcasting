$ErrorActionPreference='Stop'
$source='C:\Users\鲁迅\Desktop\讲好中国故事\tonight-scenes\商业策划\今晚唱哪一幕执行与预算.xlsx'
$copy='C:\Users\鲁迅\Desktop\讲好中国故事\tonight-scenes\商业策划\.build\native-check.xlsx'
Copy-Item -LiteralPath $source -Destination $copy -Force
$app=$null;$book=$null
try {
  $app=New-Object -ComObject Excel.Application
  $app.Visible=$false;$app.DisplayAlerts=$false
  $book=$app.Workbooks.Open($copy)
  $sheet=$book.Worksheets.Item('预算与测算')
  $app.CalculateFull()
  $sample=[double]$sheet.Range('B12').Value2
  if([Math]::Abs($sample-221.568)-gt 0.000001){throw 'Native sample mismatch'}
  $sheet.Range('B3').Value2=1
  $app.CalculateFull()
  if($sheet.Range('B12').Value2-ne '待确认'){throw 'Missing inputs became numeric'}
  $vals=@(199.0,0.08,60.0,10.0,16.0,0.6,20.0)
  for($i=0;$i-lt $vals.Count;$i++){$sheet.Cells.Item(20+$i,2).Value2=$vals[$i]}
  $app.CalculateFull()
  if([Math]::Abs([double]$sheet.Range('B12').Value2-221.568)-gt 0.000001){throw 'Confirmed-input recalculation mismatch'}
  $sheet.Range('B25').Value2=0.4
  $app.CalculateFull()
  if([double]$sheet.Range('B12').Value2-ge 0){throw 'Low incrementality did not become negative'}
  $sheet.Range('B20:B26').ClearContents()
  $sheet.Range('B3').Value2=2
  $actual=$book.Worksheets.Item('核销登记')
  $actual.Range('A10').Value2='QA-CANCEL'
  $actual.Range('G10').Value2=0
  foreach($cell in @('H10','I10','J10','L10')){$actual.Range($cell).Value2=0}
  $actual.Range('K10').Value2=10
  $app.CalculateFull()
  if([double]$actual.Range('N10').Value2-ne -10){throw 'Cancelled booking cost omitted'}
  $actual.Range('A10:L10').ClearContents()
  $app.CalculateFull()
  $book.Save()
  Write-Output ('Native Excel pass. Sample='+$sample+'; breakeven='+$sheet.Range('B14').Value2)
  $book.Close($false);$book=$null
} finally {
  if($book){try{$book.Close($false)}catch{}}
  if($app){try{$app.Quit()}catch{};[void][Runtime.InteropServices.Marshal]::ReleaseComObject($app)}
}
