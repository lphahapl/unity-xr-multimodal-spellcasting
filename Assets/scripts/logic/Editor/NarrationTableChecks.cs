using System;
using System.IO;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class NarrationTableChecks
    {
        /// <summary>可从 batchmode 的 -executeMethod 调用。只写系统临时 CSV。</summary>
        public static void Run()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
            var line = new NarrationLine
            {
                audioFileName = "a.wav", speaker = "师傅", text = "原正文",
                advanceMode = NarrationAdvanceMode.AudioAndOperation, delayAfter = 1
            };
            var duplicate = new NarrationLine { audioFileName = "a.wav", speaker = "师傅", text = "第二句" };
            bool invoked = false;
            line.onAdvanced.AddListener(() => invoked = true);
            var old = new[] { line, duplicate };
            void Check(bool result, string detail)
            {
                if (!result) throw new InvalidDataException(detail);
            }
            try
            {
                File.WriteAllText(path, "a.wav,师傅\na.wav,师傅");
                var read = NarrationTable.Read(path, old);
                Check(read[0] == line && read[1] == duplicate && line.text == "原正文" && line.delayAfter == 1,
                    "无表头两列重导未保留重复音频的两个独立配置。");
                File.WriteAllText(path, "a.wav,师傅,0.5");
                read = NarrationTable.Read(path, old);
                Check(read[0].delayAfter == .5f && read[0].text == "原正文", "无表头三列兼容失败。");
                File.WriteAllText(path, "audioFileName,speaker,delayAfter\na.wav,师傅,2");
                read = NarrationTable.Read(path, old);
                read[0].onAdvanced.Invoke();
                Check(invoked && read[0].text == "原正文" && read[0].delayAfter == 2
                    && read[0].advanceMode == NarrationAdvanceMode.AudioAndOperation, "旧表重导丢失正文、模式或事件。");
                File.WriteAllText(path, "speaker,text,audioFileName\n师傅,\"读,\"\"铜\"\"\n叶\",a.wav");
                read = NarrationTable.Read(path, old);
                Check(read[0].text == "读,\"铜\"\n叶" && read[0].delayAfter == 2, "新表列序或正文转义错误。");
                File.WriteAllText(path, "audioFileName,speaker,delayAfter,text\na.wav,师傅,0,更换\nb.wav,师傅,NaN,错误");
                bool rejected = false;
                try { NarrationTable.Read(path, old); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected && line.text == "读,\"铜\"\n叶" && line.delayAfter == 2, "坏行导致部分写入。");
                File.WriteAllText(path, "speaker,text,audioFileName\n师傅,,a.wav");
                read = NarrationTable.Read(path, old);
                Check(read[0].text == "" && read[0].delayAfter == 2, "空正文处理错误。");
                Debug.Log("旁白表格回归通过。");
            }
            finally { File.Delete(path); }
        }
    }
}
