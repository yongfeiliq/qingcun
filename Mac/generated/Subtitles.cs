using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows.Forms;
using System.Web.Script.Serialization;

partial class Host {
    static Dictionary<string,object> SubtitleOptions(Dictionary<string,object> q) {
        var raw=D(q,"subtitles");string mode=S(raw,"mode"),language=S(raw,"language");if(mode=="")mode="none";if(language=="")language="auto";
        if(!new[]{"none","files","burn","only"}.Contains(mode))throw new Exception("无效的字幕操作。");
        if(!new[]{"auto","zh","zh-Hans","zh-Hant","en","ja","ko","fr","de","es"}.Contains(language))throw new Exception("无效的字幕语言。");
        int size=(int)N(raw,"size");if(size==0)size=24;if(!new[]{20,24,30}.Contains(size))throw new Exception("无效的字幕字号。");
        return Obj("mode",mode,"language",language,"automatic",B(raw,"automatic",true),"size",size);
    }
    static string SubtitleLanguage(string code) {
        string c=Regex.Replace(code??"",@"^(?:ai|a)-","",RegexOptions.IgnoreCase).ToLowerInvariant();
        if(c.StartsWith("zh-hans")||c.StartsWith("zh-cn")||c.StartsWith("zh-sg"))return "zh-Hans";
        if(c.StartsWith("zh-hant")||c.StartsWith("zh-tw")||c.StartsWith("zh-hk"))return "zh-Hant";
        return c.Split('-')[0];
    }
    static string SubtitleLabel(string code) {
        switch(SubtitleLanguage(code)){case "zh-Hans":return "简体中文";case "zh-Hant":return "繁体中文";case "zh":return "中文";case "en":return "英语";case "ja":return "日语";case "ko":return "韩语";case "fr":return "法语";case "de":return "德语";case "es":return "西班牙语";case "local":return "本地字幕";default:return code;}
    }
    static Dictionary<string,object> SelectSubtitle(Dictionary<string,object> meta,Dictionary<string,object> options) {
        var tracks=new List<Dictionary<string,object>>();
        foreach(string key in new[]{"subtitles","automatic_captions"}){
            bool auto=key=="automatic_captions";if(auto&&!B(options,"automatic"))continue;
            foreach(var item in D(meta,key)){
                string code=item.Key;if(code.StartsWith("ai-")&&!B(options,"automatic"))continue;
                if(!Regex.IsMatch(code,@"^[A-Za-z0-9][A-Za-z0-9_-]{0,59}$")||code=="live_chat"||code=="danmaku")continue;
                var temp=Obj("items",item.Value);if(!Items(temp,"items").Any(t=>S(t,"url")!=""||S(t,"data")!=""))continue;
                string lang=SubtitleLanguage(code),wanted=S(options,"language");
                if(wanted!="auto"&&!(wanted=="zh"?lang.StartsWith("zh"):lang==wanted))continue;
                // Language preference first, then human-authored tracks over automatic captions.
                int rank=wanted=="auto"?(lang=="zh-Hans"?0:lang=="zh"?1:lang=="zh-Hant"?2:lang=="en"?3:code==S(meta,"language")?4:5):0;
                tracks.Add(Obj("language",code,"label",SubtitleLabel(code),"automatic",auto||code.StartsWith("ai-"),"rank",rank));
            }
        }
        return tracks.OrderBy(t=>N(t,"rank")).ThenBy(t=>B(t,"automatic")?1:0).ThenBy(t=>S(t,"language").Length).ThenBy(t=>S(t,"language")).FirstOrDefault();
    }
    static void SubtitleProgress(Dictionary<string,object> job,string detail,double percent) {
        lock(Gate){job["detail"]=detail;job["percent"]=Math.Max(0,Math.Min(99,percent));Write(JobPath(S(job,"id")),job);}
    }
    static string SubtitleTool(string exe,IEnumerable<string> args,string work,int timeout,Dictionary<string,object> job,double duration=0) {
        var output=new StringBuilder();string lastError="";
        var start=new ProcessStartInfo(exe,String.Join(" ",args.Select(Quote))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=work};
        using(var p=new Process{StartInfo=start}){
            DataReceivedEventHandler receive=(s,e)=>{if(e.Data==null)return;lock(output){
                if(output.Length<24000)output.AppendLine(e.Data);
                if(e.Data.StartsWith("ERROR:")||e.Data.Contains("Invalid data")||e.Data.Contains("Error"))lastError=e.Data;
                if(duration>0&&e.Data.StartsWith("out_time_us=")){double us;if(Double.TryParse(e.Data.Substring(12),NumberStyles.Any,CultureInfo.InvariantCulture,out us))SubtitleProgress(job,"正在烧录字幕 "+Math.Min(99,Math.Round(us/1000000/duration*100))+"%…",40+Math.Min(58,us/1000000/duration*58));}
            }};
            p.OutputDataReceived+=receive;p.ErrorDataReceived+=receive;p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();
            if(!p.WaitForExit(timeout)){try{p.Kill(true);}catch{}throw new Exception(duration>0?"字幕烧录超时，原视频和已下载字幕已保留。":"获取字幕超时，请稍后重试。");}
            p.WaitForExit();if(p.ExitCode!=0)throw new Exception(lastError!=""?Cut(lastError,700):Cut(output.ToString(),700));
        }return output.ToString();
    }
    static List<string> SubtitleEngineArgs(string work) {
        var args=new List<string>{"--ignore-config","--no-playlist","--playlist-end","1","--ignore-no-formats-error","--skip-download","--no-simulate","--windows-filenames","--encoding","utf-8","--socket-timeout","20","--retries","1","--fragment-retries","1","--no-progress","--ffmpeg-location",Path.Combine(Root,"tools"),"-P",work,"-o","captions.%(ext)s"};
        string deno=Path.Combine(Root,"tools",ExecutableName("deno"));if(File.Exists(deno)){args.Add("--js-runtimes");args.Add("deno:"+deno);}return args;
    }
    static Dictionary<string,object> FetchSubtitle(string url,Dictionary<string,object> options,string work,Dictionary<string,object> job) {
        SubtitleProgress(job,"正在检查平台提供的字幕语言…",5);
        var args=SubtitleEngineArgs(work);using(var auth=new VimeoLease(url,S(job,"vimeoSession"),args)){args.Add("--write-info-json");args.Add("--");args.Add(EngineUrl(url,S(job,"vimeoSession")));
        try{SubtitleTool(Path.Combine(Root,"tools",ExecutableName("yt-dlp")),args,work,180000,job);}catch(Exception e){throw new Exception("平台字幕读取失败："+DownloadError(url,e.Message));}
        }
        string metaPath=Path.Combine(work,"captions.info.json");if(!File.Exists(metaPath))throw new Exception("平台没有返回字幕信息。请打开单个视频，或导入自己的 SRT / VTT 字幕。");
        var meta=new JavaScriptSerializer{MaxJsonLength=64*1024*1024}.Deserialize<Dictionary<string,object>>(File.ReadAllText(metaPath,Encoding.UTF8));
        var selected=SelectSubtitle(meta,options);
        if(selected==null)throw new Exception("没有找到"+(S(options,"language")=="auto"?"可下载的":SubtitleLabel(S(options,"language")))+"字幕。"+(!B(options,"automatic")?"可以允许平台自动字幕后再试；":"")+"也可导入 SRT / VTT。画面里已有的文字不能直接导出为字幕。");
        SubtitleProgress(job,"正在下载"+S(selected,"label")+(B(selected,"automatic")?"自动字幕":"字幕")+"…",15);
        args=SubtitleEngineArgs(work);args.AddRange(new[]{"--load-info-json",metaPath,"--write-subs",B(options,"automatic")?"--write-auto-subs":"--no-write-auto-subs","--sub-langs","^"+Regex.Escape(S(selected,"language"))+"$","--sub-format","srt/vtt/ass/ttml/best","--convert-subs","srt"});
        using(var auth=new VimeoLease(url,S(job,"vimeoSession"),args)){try{SubtitleTool(Path.Combine(Root,"tools",ExecutableName("yt-dlp")),args,work,180000,job);}catch(Exception e){throw new Exception("字幕下载未完成："+DownloadError(url,e.Message));}}
        string srt=Path.Combine(work,"captions."+S(selected,"language")+".srt");
        ValidateSrt(srt);selected["path"]=srt;selected["title"]=Cut(S(meta,"title"),120);return selected;
    }
    static void ValidateSrt(string file) {
        if(!File.Exists(file)||new FileInfo(file).Length==0)throw new Exception("未生成有效的 SRT 字幕，平台可能没有字幕或暂不允许读取。");
        if(new FileInfo(file).Length>10*1024*1024)throw new Exception("字幕文件过大，支持 10 MB 以内的字幕。");
        if(!Regex.IsMatch(File.ReadAllText(file,Encoding.UTF8),@"(?m)^\s*\d{2,}:\d{2}:\d{2}[,.]\d{3}\s*-->\s*\d{2,}:\d{2}:\d{2}[,.]\d{3}"))throw new Exception("字幕缺少有效的时间轴，请使用 UTF-8 编码的 SRT / VTT 文件。");
    }
    static string SubtitleOutput(string folder,string name,string suffix,string ext) {
        Directory.CreateDirectory(folder);
        string clean=Regex.Replace(name??"字幕",@"[<>:""/\\|?*\x00-\x1f]","_").Trim().TrimEnd('.');
        int max=Math.Min(110,235-Path.GetFullPath(folder).Length-suffix.Length-ext.Length-16);if(max<8)throw new Exception("保存目录过长，请换一个较短的路径。");
        clean=Cut(clean,max);if(clean=="")clean="字幕";
        string file=Path.Combine(folder,clean+suffix+ext);
        if(File.Exists(file))file=Path.Combine(folder,clean+suffix+" ["+Guid.NewGuid().ToString("N").Substring(0,8)+"]"+ext);
        return file;
    }
    static Dictionary<string,object> SubtitleTarget(string assetId,string filePath,string key) {
        var a=GetAsset(assetId);var f=Items(a,"files").FirstOrDefault(x=>S(x,"path")==filePath);
        if(f==null||!File.Exists(filePath)||MediaKey(f)!=key)throw new Exception("视频文件已变化，请重新选择。原文件未改动。");return f;
    }
    static void AddSubtitleRecord(string assetId,string video,string key,Dictionary<string,object> record) {
        if(assetId=="")return;
        LibraryLock(()=>{SubtitleTarget(assetId,video,key);var a=GetAsset(assetId);var list=Items(a,"subtitleFiles");list.Insert(0,record);a["subtitleFiles"]=list.Take(200).ToList();SaveAsset(a);return true;});
    }
    static List<string> ProcessSubtitles(string url,string assetId,string video,string key,Dictionary<string,object> options,string local,string work,Dictionary<string,object> job) {
        Directory.CreateDirectory(work);Dictionary<string,object> selected;
        if(video!="")SubtitleTarget(assetId,video,key);
        if(local!=""){
            SubtitleProgress(job,"正在读取本地字幕…",10);
            string normalized=Path.Combine(work,"local.srt");
            SubtitleTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),new[]{"-nostdin","-hide_banner","-loglevel","error","-protocol_whitelist","file,pipe","-i",local,"-map","0:s:0","-c:s","srt","-y",normalized},work,30000,job);
            ValidateSrt(normalized);selected=Obj("path",normalized,"language","local","label","本地字幕","automatic",false);
        }else selected=FetchSubtitle(url,options,work,job);
        bool eagleOwned=video!=""&&B(SubtitleTarget(assetId,video,key),"eagleOwned");
        string folder=eagleOwned?Path.Combine(Data,"exports",assetId):video!=""?Path.GetDirectoryName(video):Path.Combine(S(job,"folder"),"字幕");
        string name=video!=""?Path.GetFileNameWithoutExtension(video):S(selected,"title")+" ["+VideoId(url).Substring(0,11)+"]";
        string dest=SubtitleOutput(folder,name,"."+S(selected,"language"),".srt");File.Copy(S(selected,"path"),dest,false);
        var files=new List<string>{dest};
        var record=Obj("path",dest,"language",S(selected,"language"),"label",S(selected,"label"),"automatic",B(selected,"automatic"),"videoPath",video,"created",DateTime.UtcNow.ToString("o"));
        // Persist the subtitle immediately so a later encoding failure still exposes the usable file.
        AddSubtitleRecord(assetId,video,key,record);
        lock(Gate){job["subtitleFiles"]=Strings(job,"subtitleFiles").Concat(files).ToArray();Write(JobPath(S(job,"id")),job);}
        if(S(options,"mode")=="burn"){
            if(video=="")throw new Exception("请先下载视频，再从素材库的“字幕”页面烧录。字幕文件已保存。");
            var media=SubtitleTarget(assetId,video,key);SubtitleProgress(job,"字幕已保存，正在生成带字幕的视频副本…",40);
            File.Copy(dest,Path.Combine(work,"burn.srt"),false);string encoded=Path.Combine(work,"burn.mp4");
            string filter="subtitles=filename=burn.srt:force_style='Fontname=Microsoft YaHei,Fontsize="+S(options,"size")+",PrimaryColour=&H00FFFFFF,OutlineColour=&H00000000,BorderStyle=1,Outline=1.5,Shadow=0,Alignment=2,MarginV=18',scale=ceil(iw/2)*2:ceil(ih/2)*2";
            var args=new[]{"-nostdin","-hide_banner","-loglevel","error","-i",video,"-map","0:v:0","-map","0:a?","-vf",filter,"-c:v","libx264","-preset","fast","-crf","18","-pix_fmt","yuv420p","-c:a","aac","-b:a","192k","-map_metadata","0","-movflags","+faststart","-progress","pipe:1","-nostats","-y",encoded};
            try{SubtitleTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),args,work,6*60*60*1000,job,N(media,"duration"));}catch{throw new Exception("字幕烧录未完成；原视频和字幕已保存。请检查磁盘空间，或缩短视频后重试。");}
            var probe=Probe(encoded,Obj());if(N(media,"duration")>0&&Math.Abs(N(probe,"duration")-N(media,"duration"))>Math.Max(1,N(media,"duration")*.01))throw new Exception("烧录输出时长校验未通过，原视频和字幕已保留。");
            string burned=SubtitleOutput(eagleOwned?ArchiveFolder(Folder(),"","",false):folder,name," [字幕-"+S(selected,"label")+"]",".mp4");
            LibraryLock(()=>{SubtitleTarget(assetId,video,key);File.Move(encoded,burned);var a=GetAsset(assetId);probe["path"]=burned;probe["root"]=eagleOwned?Folder():S(media,"root");probe["managed"]=true;probe["subtitleBurned"]=true;probe["subtitleLanguage"]=S(selected,"label");probe["derivedFrom"]=video;var variants=Items(a,"files");variants.Add(probe);a["files"]=variants;SaveAsset(a);return true;});
            files.Add(burned);
        }
        return files;
    }
    static Dictionary<string,object> QueueSubtitles(Dictionary<string,object> q) {
        var options=SubtitleOptions(q);if(!new[]{"only","files","burn"}.Contains(S(options,"mode")))throw new Exception("请选择下载字幕或烧录字幕。");
        string assetId=S(q,"id"),video="",key="",url="",local="";
        if(assetId!=""){var a=GetAsset(assetId);if(EagleAsset(a))throw new Exception("Eagle 已有视频在这里仅编辑标签。需要处理字幕时，请先将视频导出到普通文件夹。");var f=MediaFile(a,(int)N(q,"index"));video=S(f,"path");key=MediaKey(f);url=S(a,"url");if(B(f,"subtitleBurned")&&S(options,"mode")=="burn")throw new Exception("这个文件已经烧录过字幕，请先选择原视频，避免叠加两层字幕。");}
        if(B(q,"importLocal")){
            if(video==""||S(options,"mode")!="burn")throw new Exception("请先从素材库选择要烧录的视频。");
            using(var dialog=new OpenFileDialog()){dialog.Title="选择 UTF-8 编码的 SRT / VTT 字幕";dialog.Filter="字幕文件 (*.srt;*.vtt)|*.srt;*.vtt";dialog.Multiselect=false;if(dialog.ShowDialog()!=DialogResult.OK)return Obj("ok",true,"count",0,"cancelled",true);local=dialog.FileName;}
            if(!new[]{".srt",".vtt"}.Contains(Path.GetExtension(local).ToLowerInvariant())||!File.Exists(local)||new FileInfo(local).Length>10*1024*1024)throw new Exception("请选择 10 MB 以内的 SRT / VTT 文件。");
        }
        string[] urls=assetId!=""?new[]{local!=""?"":Normalize(url)}:DownloadUrls(q);
        if(video==""&&S(options,"mode")=="burn")throw new Exception("请先下载视频，再从素材库烧录字幕。");
        return LibraryLock(()=>{
            var jobs=Recent(Int32.MaxValue);if(jobs.Count(j=>new[]{"queued","running"}.Contains(S(j,"status")))>=3)throw new Exception("已有 3 个任务正在处理，请稍后重试。");
            if(video!=""&&jobs.Any(j=>S(j,"kind")=="subtitles"&&S(j,"videoPath")==video&&new[]{"queued","running"}.Contains(S(j,"status"))))throw new Exception("当前视频已有字幕任务，请等待完成。");
            string id=Guid.NewGuid().ToString("N"),folder=Folder();Directory.CreateDirectory(folder);
            string localCopy="";if(local!=""){string work=Path.Combine(folder,"_字幕处理中",id);Directory.CreateDirectory(work);localCopy=Path.Combine(work,"input"+Path.GetExtension(local).ToLowerInvariant());File.Copy(local,localCopy,false);}
            var job=Obj("id",id,"vimeoSession",S(q,"vimeoSession"),"kind","subtitles","subtitles",options,"urls",urls,"folder",folder,"assetId",assetId,"videoPath",video,"videoKey",key,"localSubtitle",localCopy,"status","queued","total",urls.Length,"completed",0,"failed",0,"percent",0,"title",S(options,"mode")=="burn"?"烧录字幕":"下载字幕","detail","准备处理字幕…","created",DateTime.UtcNow.ToString("o"));
            Write(JobPath(id),job);
            try{using(var worker=Process.Start(new ProcessStartInfo(Path.Combine(Root,ExecutableName("QingcunHost")),"--subtitles "+id){UseShellExecute=false,WorkingDirectory=Root})){}}catch{job["status"]="failed";job["detail"]="无法启动字幕任务。";Write(JobPath(id),job);throw;}
            return Obj("ok",true,"id",id,"count",urls.Length);
        });
    }
    static void RunSubtitles(string id) {
        var job=Read(JobPath(id));var p=Process.GetCurrentProcess();job["pid"]=p.Id;job["pidStart"]=p.StartTime.ToUniversalTime().Ticks.ToString();job["status"]="running";Write(JobPath(id),job);
        var files=new List<string>();var errors=new List<string>();int done=0,failed=0;string taskWork=Path.Combine(S(job,"folder"),"_字幕处理中",id);
        foreach(string url in Strings(job,"urls")){
            string work=Path.Combine(taskWork,Guid.NewGuid().ToString("N").Substring(0,8));
            try{
                string assetId=S(job,"assetId"),video=S(job,"videoPath"),key=S(job,"videoKey");
                if(assetId==""&&url!=""){var a=LibraryLock(()=>AllAssets().FirstOrDefault(x=>S(x,"requestKey")==VideoId(url)||S(x,"id")==VideoId(url)));if(a!=null){var f=Items(a,"files").FirstOrDefault(x=>!B(x,"subtitleBurned")&&File.Exists(S(x,"path")));if(f!=null){assetId=S(a,"id");video=S(f,"path");key=MediaKey(f);}}}
                files.AddRange(ProcessSubtitles(url,assetId,video,key,D(job,"subtitles"),S(job,"localSubtitle"),work,job));done++;
            }catch(Exception e){failed++;errors.Add(e.Message);}
            finally{CleanupSubtitleWork(work);}
            job["completed"]=done;job["failed"]=failed;Write(JobPath(id),job);
        }
        files=files.Concat(Strings(job,"subtitleFiles")).Distinct().ToList();job["files"]=files;job["errors"]=errors;job["percent"]=100;job["status"]=failed==0?"completed":files.Count>0?"partial":"failed";job["detail"]=failed==0?(S(D(job,"subtitles"),"mode")=="burn"?"字幕已烧录为新的视频副本；原视频和 SRT 均已保留。":"已单独保存 "+files.Count+" 个 SRT 字幕文件。")+" 保存位置："+(files.Count>0?Path.GetDirectoryName(files[0]):S(job,"folder")):String.Join(" | ",errors);Write(JobPath(id),job);
        CleanupSubtitleWork(taskWork);
    }
    static void CleanupSubtitleWork(string work) {
        // Only short-lived, tool-created files are removed, never originals or published subtitle/video outputs.
        try{if(Directory.Exists(work)){foreach(var f in Directory.GetFiles(work))File.Delete(f);if(!Directory.EnumerateFileSystemEntries(work).Any())Directory.Delete(work);}}catch{}
    }
    static Dictionary<string,object> RevealSubtitle(Dictionary<string,object> q) {
        var a=GetAsset(S(q,"id"));var file=Items(a,"subtitleFiles").FirstOrDefault(s=>S(s,"path")==S(q,"path"));if(file==null||!File.Exists(S(file,"path")))throw new Exception("字幕文件已移动或删除。");
        MacOpen(S(file,"path"),true);return Obj("ok",true);
    }
}
