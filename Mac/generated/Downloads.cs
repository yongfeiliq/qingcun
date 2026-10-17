using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

partial class Host {
    static string[] DownloadUrls(Dictionary<string,object> q) {
        var raw=Strings(q,"urls");if(raw.Length<1||raw.Length>20)throw new Exception("一次支持 1 至 20 个视频。");
        return raw.Select(Normalize).GroupBy(VideoId).Select(g=>g.First()).ToArray();
    }
    static Dictionary<string,object> DownloadReview(string[] urls,List<Dictionary<string,object>> jobs) {
        var items=new List<Dictionary<string,object>>();
        foreach(string url in urls){
            string id=VideoId(url),p=AssetPath(id);var related=AllAssets().Where(x=>S(x,"requestKey")==id||S(x,"id")==id).ToList();var a=related.FirstOrDefault()??Obj();if(related.Count==1)id=S(a,"id");
            var files=new List<Dictionary<string,object>>();
            var analysis=ReadAnalysisJob(S(a,"analysisJobId"));bool analyzing=related.Any(x=>new[]{"queued","running"}.Contains(S(ReadAnalysisJob(S(x,"analysisJobId")),"state")));
            foreach(var asset in related)ResolveEagleStorage(asset);
            foreach(var f in related.SelectMany(x=>Items(x,"files"))){
                string file=S(f,"path");bool exists=File.Exists(file);
                files.Add(Obj("path",file,"exists",exists,"key",exists?MediaKey(f):"","height",N(f,"height"),"bytes",exists?new FileInfo(file).Length:0,
                    "canReplace",related.Count==1&&exists&&(B(f,"managed")||EagleStorageFile(f))&&Path.GetExtension(file).Equals(".mp4",StringComparison.OrdinalIgnoreCase)&&!analyzing));
            }
            var active=jobs.Where(j=>new[]{"queued","running"}.Contains(S(j,"status"))&&Strings(j,"urls").Any(u=>SameVideo(u,url))).Select(j=>S(j,"id")).ToArray();
            bool history=File.Exists(Path.Combine(Data,"download-history",VideoId(url)+".json"))||jobs.Any(j=>
                Strings(j,"urls").Any(u=>SameVideo(u,url))&&N(j,"completed")>0&&
                (Strings(j,"urls").Length==1||Strings(j,"files").Any(f=>Path.GetFileName(f).Contains("["+id+"]"))));
            bool known=a.Count>0||history;
            items.Add(Obj("url",url,"videoId",id,"title",S(a,"title")!=""?S(a,"title"):id,"known",known,"files",files,
                "collection",related.Count>1,"active",active.Length>0,"activeJobs",active,"analyzing",analyzing,"missing",known&&!files.Any(f=>B(f,"exists"))));
        }
        return Obj("items",items,"snapshot",HashText(Json.Serialize(items)));
    }
    static Dictionary<string,object> QueueDownload(Dictionary<string,object> q,bool previewOnly){
        var subtitles=SubtitleOptions(q);if(S(subtitles,"mode")=="only"){if(previewOnly){DownloadUrls(q);return Obj("ok",true,"requiresConfirmation",false);}return QueueSubtitles(q);}
        string[] urls=DownloadUrls(q);string quality=S(q,"quality");
        if(!new[]{"best","1080","720"}.Contains(quality))throw new Exception("无效的画质选项。");
        using(var mutex=new Mutex(false,"QingcunJobs_"+UserKey())){
            bool held=false;try{
                try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}
                if(!held)throw new Exception("组件忙碌，请稍后再试。");
                return LibraryLock(()=>{
                    var jobs=Recent(Int32.MaxValue);var review=DownloadReview(urls,jobs.Where(j=>S(j,"kind")!="subtitles").ToList());var rows=Items(review,"items");
                    bool needs=rows.Any(r=>B(r,"known")||B(r,"active"));
                    if(previewOnly||(needs&&S(q,"snapshot")!=S(review,"snapshot"))||(S(q,"snapshot")!=""&&S(q,"snapshot")!=S(review,"snapshot")))
                        return Obj("ok",true,"requiresConfirmation",needs,"review",review,"changed",S(q,"snapshot")!="");
                    var choices=Items(q,"choices");var accepted=new List<string>();var decisions=new List<Dictionary<string,object>>();
                    foreach(var row in rows){
                        string url=S(row,"url");var choice=choices.FirstOrDefault(c=>S(c,"url")==url);
                        string mode=choice==null?"":S(choice,"decision");
                        if(B(row,"active")){if(mode=="skip")continue;throw new Exception("这个视频已有下载任务，请等待完成后再下载。");}
                        if(B(row,"known")){
                            if(mode=="skip")continue;
                            if(mode!="copy"&&mode!="replace")throw new Exception("请为已下载的视频选择跳过、替换或另存一份。");
                        }else {if(mode=="skip")continue;mode="new";}
                        var decision=Obj("url",url,"videoId",S(row,"videoId"),"mode",mode,"brandOverride",BrandName(S(q,"brandOverride")),"context",DownloadContext(q,url));
                        if(mode=="replace"){
                            var target=Items(row,"files").FirstOrDefault(f=>S(f,"path")==S(choice,"target")&&B(f,"canReplace"));
                            if(target==null)throw new Exception("待替换文件已变化或正在分析，请重新检查下载记录。");
                            decision["target"]=S(target,"path");decision["targetKey"]=S(target,"key");
                        }
                        accepted.Add(url);decisions.Add(decision);
                    }
                    if(accepted.Count==0)return Obj("ok",true,"count",0,"skipped",urls.Length);
                    if(jobs.Count(x=>new[]{"running","queued"}.Contains(S(x,"status")))>=3)throw new Exception("已有 3 个批次正在下载，请等待一个批次完成。");
                    foreach(string exe in new[]{ExecutableName("yt-dlp"),ExecutableName("ffmpeg")})if(!File.Exists(Path.Combine(Root,"tools",exe)))throw new Exception("下载组件缺失，请重新安装。");
                    string folder=Folder();Directory.CreateDirectory(folder);string id=Guid.NewGuid().ToString("N");
                    var d=Obj("id",id,"vimeoSession",S(q,"vimeoSession"),"urls",accepted,"decisions",decisions,"subtitles",subtitles,"quality",quality,"folder",folder,"status","queued","total",accepted.Count,
                        "completed",0,"failed",0,"percent",0,"title",accepted.Count+" 个视频","detail","准备开始…","created",DateTime.UtcNow.ToString("o"));
                    Write(JobPath(id),d);
                    try{using(var worker=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Root,ExecutableName("QingcunHost")),"--worker "+id){UseShellExecute=false,WorkingDirectory=Root})){}}
                    catch{d["status"]="failed";d["detail"]="无法启动下载任务。";Write(JobPath(id),d);throw;}
                    return Obj("ok",true,"id",id,"count",accepted.Count,"skipped",urls.Length-accepted.Count);
                });
            }finally{if(held)mutex.ReleaseMutex();}
        }
    }
    static string InstallDownload(string source,string folder,Dictionary<string,object> asset,Dictionary<string,object> decision){
        string mode=S(decision,"mode");
        if(mode!="replace"){
            Directory.CreateDirectory(folder);
            string name=Path.GetFileNameWithoutExtension(source),extension=Path.GetExtension(source);
            string dest=ArchiveFile(folder,name,extension);
            if(mode=="copy"||File.Exists(dest)||File.Exists(Path.ChangeExtension(dest,"info.json"))||File.Exists(Path.ChangeExtension(dest,"qingcun.json")))dest=ArchiveFile(folder,name,extension," [副本 "+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+"]");
            File.Move(source,dest);return dest;
        }
        string target=S(decision,"target");var f=Items(asset,"files").FirstOrDefault(x=>String.Equals(S(x,"path"),target,StringComparison.OrdinalIgnoreCase));
        if(f==null||(!B(f,"managed")&&!EagleStorageFile(f))||!File.Exists(target)||MediaKey(f)!=S(decision,"targetKey")||!Path.GetExtension(target).Equals(".mp4",StringComparison.OrdinalIgnoreCase))
            throw new Exception("旧文件已变化，本次未替换。新视频保留在下载暂存目录，请重新确认。");
        var job=ReadAnalysisJob(S(asset,"analysisJobId"));if(new[]{"queued","running"}.Contains(S(job,"state")))throw new Exception("旧文件正在分析，本次未替换；新视频保留在下载暂存目录。");
        if(EagleStorageFile(f)){
            var binding=D(f,"eagleStorage");string current=StorageItemPath(S(binding,"libraryPath"),S(binding,"itemId"));if(!SamePath(target,current)||FileHash(current)!=S(binding,"sha256"))throw new Exception("Eagle 原视频已变化，请重新确认替换。");
            decision["eagleReplacement"]=Obj("libraryPath",S(binding,"libraryPath"),"itemId",S(binding,"itemId"),"oldSha",S(binding,"sha256"),"syncKey",S(f,"syncKey"));
            Directory.CreateDirectory(folder);string pending=ArchiveFile(folder,Path.GetFileNameWithoutExtension(source),Path.GetExtension(source)," [更新-"+Guid.NewGuid().ToString("N").Substring(0,8)+"]");File.Move(source,pending);return pending;
        }
        string backup=Path.Combine(Path.GetDirectoryName(target),"轻存替换备份",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
        Directory.CreateDirectory(backup);
        foreach(string ext in new[]{"info.json","qingcun.json"}){string sidecar=Path.ChangeExtension(target,ext);if(File.Exists(sidecar))File.Copy(sidecar,ArchiveFile(backup,Path.GetFileNameWithoutExtension(sidecar),Path.GetExtension(sidecar)),false);}
        string temp=Path.Combine(Path.GetDirectoryName(target),".qc-"+Guid.NewGuid().ToString("N")+".replacement");
        try{File.Copy(source,temp,false);File.Replace(temp,target,ArchiveFile(backup,Path.GetFileNameWithoutExtension(target),Path.GetExtension(target)));}
        finally{if(File.Exists(temp))File.Delete(temp);}
        decision["backup"]=backup;
        return target;
    }
    static void RecordDownload(string url,string file){
        string dir=Path.Combine(Data,"download-history");Directory.CreateDirectory(dir);
        Write(Path.Combine(dir,VideoId(url)+".json"),Obj("url",url,"path",file,"downloaded",DateTime.UtcNow.ToString("o")));
    }
}
