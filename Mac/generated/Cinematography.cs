using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
partial class Host {
    const string ShotRulesVersion="cinematography-2026-09-v1";
    // Original concise rules informed by the linked research, not model weights or dataset copies.
    static string CinematographyRules(){
        return "镜头语言判定规程："+ 
        "先区分摄影机平移、旋转、焦距变化，再判断是否在跟随主体；必须比较同一镜头的连续画面与背景，不能凭一张图判断运镜。"+
        "推近/拉远：机位沿视轴前后位移，前后景相对大小和遮挡关系通常随视差变化。变焦：机位基本不变、视角收放，透视关系相对稳定；单凭主体变大不能判为推近。"+
        "横移：机位左右位移、近远背景视差不同；水平摇镜：机位大致固定、镜头朝左右转动。升降是机位上下平移；俯仰摇镜是向上/向下转动观察方向。"+
        "环绕：摄影机围绕相对稳定的主体运行，能看到其不同侧面和背景相对运动；只有主体自转、灯光/反射移动不构成环绕。滚转：画面绕视轴旋转，地平线倾斜变化。"+
        "跟拍：摄影机主动跟随移动主体，主体位置相对稳定但环境发生位移；物体自己移动而背景固定不叫跟拍。手持需有不规则机位晃动；平稳运动不能自动标成手持。"+
        "甩镜是快速转向并出现强运动模糊；推拉变焦必须同时有机位前后位移和反向变焦。复合运镜用于两种及以上明确摄影机运动，不是证据不足的替代词。"+
        "固定：连续画面的背景和透视基本稳定；主体动作、变光、反光、拉焦、剪辑缩放都不能单独证明机位移动。证据不足用待确认，不要为了填满标签硬猜。"+
        "景别：远景以环境为主；全景展示主体全貌及少量周边；中景展示主体主要部分和空间关系；近景主体大部分占幅、外围裁切；特写集中于主体重要部位；大特写只剩极小局部。非人物素材按主体全貌与局部覆盖比例判断，不套用人体腰胸标准。"+
        "一个连续镜头发生明显景别变化可用变化景别；黑场、字幕卡、无法确定参照主体的画面可用待确认。"+
        "镜头边界：真正的剪切/转场才分段。不要把每张抽帧、闪光、遮挡、快速平移、物体出入画或分析分批边界当作剪切；候选切点仅供核对。"+
        "冷暖色调按整体可见色温倾向选暖调或冷调；黑白灰为中性；明显冷暖并置才用冷暖混合；不要罗列具体颜色。"+
        "仅输出中文枚举标签与时间，不输出画面内容、剧情、制作方式、理由、导演或人物身份。";
    }
    static string[] DetectCutCandidates(string path,double duration){
        // Rolling-neighbour contrast follows AdaptiveDetector's idea; scores here are FFmpeg scene scores.
        string log=RunTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),new[]{"-hide_banner","-i",path,"-an","-vf","scale=160:-2,select=gte(scene\\,0),metadata=print:key=lavfi.scene_score","-fps_mode","passthrough","-f","null","-"},180000,false);
        var matches=Regex.Matches(log,@"pts_time:([\d.]+)[^\r\n]*[\r\n]+[^\r\n]*lavfi.scene_score=([\d.]+)").Cast<Match>().ToArray();
        if(matches.Length==0)throw new Exception("未能读取镜头变化信息，请检查视频是否能完整播放。");
        var times=matches.Select(m=>Double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)).ToArray();
        var scores=matches.Select(m=>Double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)).ToArray();
        return AdaptiveCutTimes(times,scores,duration).Select(x=>x.ToString("0.###",CultureInfo.InvariantCulture)).ToArray();
    }
    static List<double> AdaptiveCutTimes(double[] times,double[] scores,double duration){
        var indices=new List<int>();for(int i=1;i<scores.Length;i++){
            double sum=0;int count=0;for(int k=Math.Max(0,i-2);k<=Math.Min(scores.Length-1,i+2);k++)if(k!=i){sum+=scores[k];count++;}
            double ratio=scores[i]/Math.Max(.01,count>0?sum/count:0);
            if(times[i]<=.04||times[i]>=duration-.04||scores[i]<.08||(ratio<3&&scores[i]<.5))continue;
            if(indices.Count>0&&times[i]-times[indices.Last()]<.16){if(scores[i]>scores[indices.Last()])indices[indices.Count-1]=i;}
            else indices.Add(i);
        }
        return indices.Take(1200).Select(i=>Math.Round(times[i],3)).ToList();
    }
    static List<Dictionary<string,object>> AnalysisChunks(double duration,string[] cuts){
        var values=cuts.Select(x=>Double.Parse(x,CultureInfo.InvariantCulture)).ToArray();var chunks=new List<Dictionary<string,object>>();double start=0;
        while(start<duration-.001){
            double end=Math.Min(duration,start+24);
            if(duration-end>4){var candidates=values.Where(t=>t>=start+20&&t<=start+28).OrderBy(t=>Math.Abs(t-(start+24))).ToArray();if(candidates.Length>0)end=candidates[0];}
            else end=duration;
            end=Math.Round(end,3);chunks.Add(Obj("start",start,"end",end));start=end;
        }return chunks;
    }
    static void MergeChunkShots(List<Dictionary<string,object>> target,Dictionary<string,object> chunk){
        var shots=Items(chunk,"shots").Select(s=>new Dictionary<string,object>(s)).ToList();double offset=N(chunk,"start");
        foreach(var s in shots){s["start"]=Math.Round(N(s,"start")+offset,3);s["end"]=Math.Round(N(s,"end")+offset,3);}
        if(target.Count>0&&shots.Count>0&&B(chunk,"continuesPrevious")){
            var last=target.Last();last["end"]=shots[0]["end"];
            foreach(string key in new[]{"movement","scale","color"})if(S(last,key)!=S(shots[0],key)){
                string a=S(last,key),b=S(shots[0],key);
                if(a=="待确认"||b=="待确认")last[key]="待确认";
                else if(key=="movement")last[key]=a=="固定"?b:b=="固定"?a:"复合运镜";
                else if(key=="scale")last[key]="变化景别";
                else last[key]=a=="中性"||b=="中性"?"待确认":"冷暖混合";
            }
            shots.RemoveAt(0);
        }
        target.AddRange(shots);
    }
}
