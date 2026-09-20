using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VJPractice.Stage {
[Serializable] public class LyricWord { public string text; public float startTime, endTime; }
[Serializable] public class LyricCue {
    public string text="", translation="", romanization="";
    public float startTime=-1, endTime=-1;
    public LyricWord[] words=Array.Empty<LyricWord>();
}
[Serializable] public class LyricDocument {
    public int schemaVersion=1;
    public string title="Untitled", artist="", source="", sourceUri="", sourceHash="", importedUtc="", timing="untimed";
    public float offsetSeconds;
    public List<LyricCue> lines=new List<LyricCue>();
    public int ActiveAt(float seconds) {
        float t=seconds-offsetSeconds;
        for(int i=lines.Count-1;i>=0;i--) if(lines[i].startTime>=0 && t>=lines[i].startTime && t<lines[i].endTime) return i;
        return -1;
    }
    public List<string> Audit(float duration=0) {
        var warnings=new List<string>();
        int untimed=lines.Count(x=>x.startTime<0); if(untimed>0) warnings.Add($"{untimed} 行尚未打點");
        if(lines.Count==0) warnings.Add("沒有可用歌詞");
        float previous=-1;
        foreach(var c in lines) {
            if(c.startTime<0) continue;
            if(!Finite(c.startTime)||!Finite(c.endTime)||c.endTime<=c.startTime) warnings.Add("有無效時間範圍");
            if(c.startTime<previous) warnings.Add("有重疊行：預覽優先顯示較晚開始者");
            previous=c.endTime;
            if(duration>0 && c.startTime+offsetSeconds>duration) warnings.Add("部分歌詞超過音軌長度，請核對版本");
        }
        return warnings.Distinct().ToList();
    }
    public static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
    public void Stamp(int index,float time) {
        if(index<0||index>=lines.Count) return;
        float t=Mathf.Max(0,time-offsetSeconds);
        if(index>0 && lines[index-1].startTime>=t) throw new InvalidOperationException("時間必須晚於上一行；先跳到正確歌曲位置。");
        if(index+1<lines.Count && lines[index+1].startTime>=0 && lines[index+1].startTime<=t) throw new InvalidOperationException("時間必須早於下一行；可先清除後續時間再重新打點。");
        var c=lines[index]; float shift=c.startTime>=0?t-c.startTime:0;
        c.startTime=t; c.endTime=index+1<lines.Count&&lines[index+1].startTime>t?lines[index+1].startTime:t+5;
        if(c.words!=null) foreach(var w in c.words){w.startTime+=shift;w.endTime+=shift;}
        if(index>0 && lines[index-1].startTime>=0) lines[index-1].endTime=t;
        timing="manually aligned";
    }
    public string ToLrc() {
        var b=new StringBuilder(); b.AppendLine("[ti:"+title+"]"); b.AppendLine("[ar:"+artist+"]");
        foreach(var c in lines.Where(x=>x.startTime>=0)) {
            b.AppendLine("["+Format(Mathf.Max(0,c.startTime+offsetSeconds))+"]"+c.text.Replace("\n"," "));
            // Explicit blank cue preserves an instrumental gap / the end of the final line.
            int i=lines.IndexOf(c);
            if(i==lines.Count-1 || lines[i+1].startTime>c.endTime+.01f) b.AppendLine("["+Format(Mathf.Max(0,c.endTime+offsetSeconds))+"]");
        }
        return b.ToString();
    }
    public static string Format(float t) { int ticks=Mathf.RoundToInt(t*100); return $"{ticks/6000:00}:{ticks/100%60:00}.{ticks%100:00}"; }
}
public static class LyricParser {
    static readonly Regex LrcTime=new Regex(@"\[(\d+:\d+(?:\.\d+)?)\]",RegexOptions.Compiled);
    static readonly Regex WordTime=new Regex(@"<(\d+:\d+(?:\.\d+)?)>",RegexOptions.Compiled);
    [Serializable] class FoliaData { public float offset; public string title,artist; public bool wordByWord; public List<LyricCue> lines; }
    public static float Timecode(string value) {
        var parts=value.Trim().Replace(',','.').Split(':'); double total=0;
        foreach(var p in parts) total=total*60+double.Parse(p,CultureInfo.InvariantCulture);
        if(double.IsNaN(total)||double.IsInfinity(total)||total<0||total>86400) throw new FormatException("無效時間戳");
        return (float)total;
    }
    public static LyricDocument Parse(string raw,string name,string source="Local file") {
        if(string.IsNullOrWhiteSpace(raw)) throw new FormatException("歌詞檔案是空的。");
        if(raw.Length>2_000_000) throw new FormatException("歌詞檔案過大。");
        var d=new LyricDocument{source=source,sourceUri=name,importedUtc=DateTime.UtcNow.ToString("o")};
        raw=raw.TrimStart('\uFEFF').Replace("\r","");
        using(var sha=System.Security.Cryptography.SHA256.Create()) d.sourceHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-","").ToLowerInvariant();
        if(raw.TrimStart().StartsWith("{")) {
            if(raw.Contains("\"schemaVersion\"")) { d=JsonUtility.FromJson<LyricDocument>(raw); if(d.schemaVersion!=1) throw new FormatException("不支援的 session 版本"); }
            else { var f=JsonUtility.FromJson<FoliaData>(raw); d.title=f.title??"Folia import";d.artist=f.artist??"";d.lines=f.lines;d.offsetSeconds=f.offset/1000;d.timing=f.wordByWord?"native word timing":"line timing / estimated word animation"; }
        } else if(raw.Contains("-->")) {
            d.timing="subtitle line timing";
            foreach(var block in Regex.Split(raw,@"\n\s*\n")) {
                var rows=block.Split('\n'); int ti=Array.FindIndex(rows,x=>x.Contains("-->")); if(ti<0) continue;
                var times=rows[ti].Split(new[]{"-->"},StringSplitOptions.None);
                string end=times[1].Trim().Split(' ')[0];
                d.lines.Add(new LyricCue{startTime=Timecode(times[0]),endTime=Timecode(end),text=Regex.Replace(string.Join("\n",rows.Skip(ti+1)),"<[^>]+>","")});
            }
        } else if(LrcTime.IsMatch(raw)) {
            d.timing="LRC line timing / estimated word animation";
            foreach(var row in raw.Split('\n')) {
                if(row.StartsWith("[ti:")) d.title=Meta(row);
                if(row.StartsWith("[ar:")) d.artist=Meta(row);
                if(row.StartsWith("[offset:")) d.offsetSeconds=-float.Parse(Meta(row),CultureInfo.InvariantCulture)/1000;
                var stamps=LrcTime.Matches(row); if(stamps.Count==0) continue;
                string body=LrcTime.Replace(row,"").Trim();
                foreach(Match m in stamps) {
                    var c=new LyricCue{startTime=Timecode(m.Groups[1].Value),text=WordTime.Replace(body,"")};
                    var words=WordTime.Matches(body);var list=new List<LyricWord>();
                    for(int i=0;i<words.Count;i++) {
                        int begin=words[i].Index+words[i].Length, end=i+1<words.Count?words[i+1].Index:body.Length;
                        string text=body.Substring(begin,end-begin);if(text.Length==0)continue;
                        float start=Timecode(words[i].Groups[1].Value);
                        list.Add(new LyricWord{text=text,startTime=start,endTime=i+1<words.Count?Timecode(words[i+1].Groups[1].Value):start+.5f});
                    }
                    c.words=list.ToArray();d.lines.Add(c);
                }
            }
            d.lines=d.lines.OrderBy(x=>x.startTime).ToList();
            for(int i=0;i<d.lines.Count;i++) { var c=d.lines[i];c.endTime=i+1<d.lines.Count?d.lines[i+1].startTime:c.startTime+5; if(c.words.Length>0){c.words[c.words.Length-1].endTime=c.endTime;d.timing="enhanced LRC word timing";} }
        } else {
            foreach(var row in raw.Split('\n').Where(x=>!string.IsNullOrWhiteSpace(x))) d.lines.Add(new LyricCue{text=row.Trim()});
        }
        if(d.lines==null || d.lines.Count>10000) throw new FormatException("無效的歌詞資料結構。");
        if(!LyricDocument.Finite(d.offsetSeconds)||Mathf.Abs(d.offsetSeconds)>86400) throw new FormatException("無效偏移。");
        foreach(var c in d.lines) {
            if(c==null||!LyricDocument.Finite(c.startTime)||!LyricDocument.Finite(c.endTime)) throw new FormatException("歌詞含無效時間。");
            c.text=c.text??"";c.translation=c.translation??"";c.romanization=c.romanization??"";c.words=c.words??Array.Empty<LyricWord>();
            foreach(var w in c.words) if(w==null||!LyricDocument.Finite(w.startTime)||!LyricDocument.Finite(w.endTime)||w.endTime<w.startTime) throw new FormatException("無效逐字時間。");
        }
        return d;
    }
    static string Meta(string row) {int c=row.IndexOf(':');return row.Substring(c+1).TrimEnd(']');}
}
}
