using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class Host {
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string Data = Path.Combine(Root, "data");
    static readonly string Jobs = Path.Combine(Data, "jobs");
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1048576 };
    static readonly object Gate = new object();
    static Dictionary<string,object> Obj(params object[] pairs) { var d = new Dictionary<string,object>(); for(int i=0;i<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1]; return d; }
    static string S(Dictionary<string,object> d,string k) { return d.ContainsKey(k) && d[k]!=null ? Convert.ToString(d[k]) : ""; }
    static Dictionary<string,object> Read(string p) { for(int attempt=0;;attempt++){try{using(var stream=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream,Encoding.UTF8))return Json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());}catch(IOException){if(attempt>=9)throw;Thread.Sleep(50);}} }
    static void Write(string p,object o) { string t=Path.Combine(Path.GetDirectoryName(p),".qingcun-"+Guid.NewGuid().ToString("N")+".tmp"); try{File.WriteAllText(t,Json.Serialize(o),new UTF8Encoding(false)); for(int attempt=0;;attempt++){try{if(File.Exists(p))File.Replace(t,p,null);else File.Move(t,p);break;}catch(IOException){if(attempt>=9)throw;Thread.Sleep(50);}}}finally{if(File.Exists(t))File.Delete(t);} }
    static string JobPath(string id) { if(!Regex.IsMatch(id??"","^[a-f0-9]{32}$")) throw new Exception("Invalid task ID."); return Path.Combine(Jobs,id+".json"); }
    static string Folder() { string p=Path.Combine(Data,"settings.json"); string f=File.Exists(p)?S(Read(p),"folder"):""; if(String.IsNullOrWhiteSpace(f)) f=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads","轻存视频"); return f; }
    static List<Dictionary<string,object>> Recent(int limit=30) {
        var result=new List<Dictionary<string,object>>();
        foreach(var f in new DirectoryInfo(Jobs).GetFiles("*.json").OrderByDescending(f=>f.CreationTimeUtc).Take(limit)) {
            try { var d=Read(f.FullName); string st=S(d,"status");
                if(st=="running" || st=="queued") {
                    bool alive=true;
                    if(d.ContainsKey("pid")) { try { var p=Process.GetProcessById(Convert.ToInt32(d["pid"])); alive=p.StartTime.ToUniversalTime().Ticks.ToString()==S(d,"pidStart"); } catch {alive=false;} }
                    else alive=(DateTime.UtcNow-f.CreationTimeUtc).TotalMinutes<2;
                    if(!alive) { d["status"]="failed"; d["detail"]="任务已中断，请重新添加视频链接。"; Write(f.FullName,d); }
                }
                result.Add(d);
            } catch { }
        } return result;
    }
    static Dictionary<string,object> Handle(Dictionary<string,object> q) {
        switch(S(q,"action")) {
            case "openPlayback":return OpenPlayback(q);
            case "keepPlayback":return KeepPlayback(q);
            case "closePlayback":return ClosePlayback(q);
            case "openOriginal":return OpenOriginal(q);
            case "saveVimeoSession":return SaveVimeoSession(q);
            case "vimeoStatus":return VimeoStatus(q);
            case "clearVimeoSession":return ClearVimeoSession(q);
            case "adoptEagleCopy":return AdoptEagleCopy(q);
            case "cleanupEagleCopy":return CleanupEagleCopy(q);
            case "configureEagleStorage":return ConfigureEagleStorage(q);
            case "ping": return Obj("ok",true,"version","2.3.3","macInstallRevision",3,"xpcBrowserBridge",true,"modelToggle",true,"eagleStorage",true,"eagleSidecarCleanup",true,"eagleEmptyFolderCleanup",true,"categorySchema",1,"cleanupBackups",false,"filmTypeAnalysis",true,"eagleRpc",true,"folder",Folder());
            case "list": return Obj("ok",true,"jobs",Recent(),"folder",Folder(),"storageMode",S(Settings(),"storageMode"));
            case "openFolder": { string f=Folder();Directory.CreateDirectory(f);MacOpen(f);return Obj("ok",true); }
            case "chooseFolder": {
                using(var dialog=new FolderBrowserDialog()) { dialog.Description="选择轻存视频的保存文件夹";dialog.SelectedPath=Folder();
                    if(dialog.ShowDialog()==DialogResult.OK) LibraryLock(()=>{var settings=Settings();settings["folder"]=dialog.SelectedPath;Write(Path.Combine(Data,"settings.json"),settings);return true;});
                } return Obj("ok",true,"folder",Folder());
            }
            case "xpcBrowserPoll":return PollXpcBrowser(q);
            case "xpcBrowserResult":return CompleteXpcBrowser(q);
            case "saveXpcPage":return SaveXpcPage(q);
            case "savePageDescription": return SavePageDescription(q);
            case "exportDescription": return ExportDescriptionAction(q);
            case "refreshDescription": q["refreshDescription"]=true; return RefreshSource(q);
            case "subtitle": return QueueSubtitles(q);
            case "revealSubtitle": return RevealSubtitle(q);
            case "checkDownloads": return QueueDownload(q,true);
            case "download": return QueueDownload(q,false);
            default: return LibraryAction(q);
        }
    }
    static string Quote(string s) {
        if(s.IndexOf('\0')>=0) throw new Exception("Invalid argument.");
        return "\""+Regex.Replace(s,@"(\\*)\""",m=>m.Groups[1].Value+m.Groups[1].Value+"\\\"")+Regex.Match(s,@"\\*$").Value+"\"";
    }
    static void Worker(string id) {
        string path=JobPath(id);var d=Read(path);var process=Process.GetCurrentProcess();d["pid"]=process.Id;d["pidStart"]=process.StartTime.ToUniversalTime().Ticks.ToString();d["status"]="running";Write(path,d);
        string staging=Path.Combine(S(d,"folder"),"_下载中",id);Directory.CreateDirectory(staging);
        var urls=((IEnumerable)d["urls"]).Cast<object>().Select(Convert.ToString).ToArray();int done=0,failed=0;var files=new List<string>();var errors=new List<string>();
        try {
            for(int index=0;index<urls.Length;index++) {
                string lastError="";var savedPaths=new List<string>();int n=index;d["detail"]="正在解析第 "+(index+1)+" / "+urls.Length+" 个视频…";d["percent"]=index*100.0/urls.Length;Write(path,d);
                string quality=S(d,"quality");string cap=quality=="best"?"":"[height<=?"+quality+"]";
                var args=new List<string>{"--ignore-config","--no-playlist","--playlist-end","20","--write-info-json","--windows-filenames","--no-overwrites","--socket-timeout","25","--retries","3","--fragment-retries","3","--ffmpeg-location",Path.Combine(Root,"tools"),"-f","bv*[ext=mp4]"+cap+"+ba[ext=m4a]/b[ext=mp4]"+cap+"/bv*"+cap+"+ba/b"+cap,"--merge-output-format","mp4","--remux-video","mp4","--match-filter","!is_live","--newline","--progress","--progress-delta","1","--encoding","utf-8","--print","before_dl:TITLE:%(title)s","--print","after_move:SAVED:%(filepath)s","-P",staging,"-o","%(title).140B [%(extractor_key)s-%(id)s] [%(height)sp].%(ext)s"};
                if(SourcePlatform(urls[index])=="Vimeo视频")args.Add("--check-formats");
                string deno=Path.Combine(Root,"tools",ExecutableName("deno"));if(File.Exists(deno)) {args.Add("--js-runtimes");args.Add("deno:"+deno);}
                var itemContext=D(Items(d,"decisions").First(x=>S(x,"url")==urls[index]),"context");if(S(itemContext,"pageUrl")!=""){args.Add("--referer");args.Add(S(itemContext,"pageUrl"));}
                using(var auth=new VimeoLease(urls[index],S(d,"vimeoSession"),args)){
                if(SourcePlatform(urls[index])=="新片场视频"){
                    d["detail"]="正在读取新片场播放器、标签和简介…";Write(path,d);
                    var info=XpcDownloadForJob(urls[index],itemContext,d);string input=Path.Combine(staging,"xpc-"+index+".json");Write(input,info);args.Add("--load-info-json");args.Add(input);
                }else{args.Add("--");args.Add(S(itemContext,"mediaUrl")!=""?PageMedia(S(itemContext,"mediaUrl"),urls[index]):EngineUrl(urls[index],S(d,"vimeoSession")));}
                var pi=new ProcessStartInfo(Path.Combine(Root,"tools",ExecutableName("yt-dlp")),String.Join(" ",args.Select(Quote))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Root};
                using(var p=new Process{StartInfo=pi}) {
                    DataReceivedEventHandler receive=(sender,e)=>{if(e.Data==null)return;lock(Gate) {
                        string line=e.Data;File.AppendAllText(Path.Combine(Jobs,id+".log"),line+Environment.NewLine,Encoding.UTF8);
                        if(line.StartsWith("TITLE:"))d["title"]=line.Substring(6);
                        else if(line.StartsWith("SAVED:"))savedPaths.Add(line.Substring(6));
                        else if(line.StartsWith("ERROR:")) {lastError=line;d["detail"]=line;}
                        else if(line.Contains("[download]")) {Match m=Regex.Match(line,@"(\d+(?:\.\d+)?)%");if(m.Success) {double progress=Double.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);d["percent"]=Math.Min(99,(n+progress/100)*100/urls.Length);d["detail"]="第 "+(n+1)+" / "+urls.Length+" 个 · "+line.Replace("[download]","").Trim();}}
                        else if(line.Contains("[Merger]") || line.Contains("[VideoRemuxer]"))d["detail"]="正在合并画面与声音…";
                        Write(path,d);
                    }};
                    p.OutputDataReceived+=receive;p.ErrorDataReceived+=receive;p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();p.WaitForExit();
                    foreach(string downloadedPath in savedPaths.Distinct().Where(File.Exists)){
                        string saved=downloadedPath;
                        try{
                            if(!Path.GetFullPath(saved).StartsWith(Path.GetFullPath(staging)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("下载文件不在当前任务目录。");
                            var originalDecision=Items(d,"decisions").First(x=>S(x,"url")==urls[index]);var decision=new Dictionary<string,object>(originalDecision);
                            if(savedPaths.Count>1&&S(decision,"mode")=="replace")throw new Exception("页面包含多个视频，本次未替换。请另存一份后分别整理。");
                            if(SourcePlatform(urls[index])=="Vimeo视频"){d["detail"]="视频已下载，正在读取 Vimeo 简介与制作人员…";Write(path,d);}
                            PrepareDownloadedSource(saved,decision,savedPaths.Count>1,S(d,"vimeoSession"));
                            d["detail"]="视频已下载，正在归类…";Write(path,d);
                            var indexed=IndexVideo(saved,S(d,"folder"),B(Settings(),"autoClassify",true),true,decision);
                            saved=S(indexed,"path");files.Add(saved);files.AddRange(Strings(indexed,"descriptionFiles"));errors.AddRange(Strings(indexed,"descriptionErrors"));if(S(decision,"metadataWarning")!="")errors.Add(S(decision,"metadataWarning"));done++;RecordDownload(urls[index],saved);
                            var subtitleOptions=D(d,"subtitles");
                            if(new[]{"files","burn"}.Contains(S(subtitleOptions,"mode"))){
                                string subtitleWork=Path.Combine(staging,"sub-"+Guid.NewGuid().ToString("N").Substring(0,8));
                                try{var a=GetAsset(S(indexed,"id"));var f=Items(a,"files").First(x=>S(x,"path")==saved);files.AddRange(ProcessSubtitles(urls[index],S(indexed,"id"),saved,MediaKey(f),subtitleOptions,"",subtitleWork,d));}
                                catch(Exception e){failed++;errors.Add("视频已保存，字幕未完成："+e.Message);}
                                finally{CleanupSubtitleWork(subtitleWork);}
                            }
                            if(S(decision,"backup")!="")errors.Add("旧版本已备份："+S(decision,"backup"));
                            if(B(Settings(),"autoAnalyze"))try{StartAnalysis(Obj("id",S(indexed,"id"),"index",0));}catch(Exception ex){errors.Add("视频已保存，自动分析未启动："+ex.Message);}
                        }catch(Exception ex){failed++;files.Add(saved);errors.Add("视频保留在暂存目录："+ex.Message);}
                    }
                    if(p.ExitCode!=0||savedPaths.Count==0){failed++;errors.Add(DownloadError(urls[index],lastError));}
                }
                }
                d["completed"]=done;d["failed"]=failed;d["files"]=files;d["percent"]=(index+1)*100.0/urls.Length;Write(path,d);
            }
            files=files.Concat(Strings(d,"subtitleFiles")).Distinct().ToList();d["files"]=files;
            d["status"]=failed==0?"completed":(done>0?"partial":"failed");d["detail"]=failed==0?"已保存 "+done+" 个视频 · "+S(d,"folder"):"已保存 "+done+" 个，失败 "+failed+" 个。"+String.Join(" | ",errors);if(failed==0&&new[]{"files","burn"}.Contains(S(D(d,"subtitles"),"mode")))d["detail"]="已保存 "+done+" 个视频及 SRT 字幕"+(S(D(d,"subtitles"),"mode")=="burn"?"，带字幕的视频副本已另存":"")+" · "+S(d,"folder");d["errors"]=errors;if(failed==0&&errors.Count>0)d["detail"]="视频已保存；"+String.Join(" | ",errors);Write(path,d);if(Directory.Exists(staging)&&!Directory.EnumerateFileSystemEntries(staging).Any())Directory.Delete(staging);
        } catch(Exception e) {d["status"]="failed";d["detail"]=e.Message;d["completed"]=done;Write(path,d);}
        finally{for(int i=0;i<urls.Length;i++){string input=Path.Combine(staging,"xpc-"+i+".json");try{if(File.Exists(input))File.Delete(input);}catch{}}try{var settings=Settings();if(S(settings,"storageMode")=="eagle")CleanupEmptyDownloadFolders(staging,S(d,"folder"),S(settings,"storageLibraryPath"));else if(Directory.Exists(staging)&&!Directory.EnumerateFileSystemEntries(staging).Any())Directory.Delete(staging);}catch{}}
    }
    static byte[] ReadBytes(Stream input,int size) {var b=new byte[size];int pos=0;while(pos<size){int n=input.Read(b,pos,size-pos);if(n==0)throw new EndOfStreamException();pos+=n;}return b;}
    [STAThread] internal static int Main(string[] args) {
        try {
            if(args.Length==1&&args[0]=="--mac-self-test")return MacSelfTest();
            if(args.Length==4&&args[0]=="--mac-install")return MacInstall(args[1],args[2],args[3]=="browser");
            Directory.CreateDirectory(Jobs);if(OperatingSystem.IsMacOS())File.SetUnixFileMode(Data,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            if(args.Length==2 && args[0]=="--playback") {RunPlayback(args[1]);return 0;}
            if(args.Length==2 && args[0]=="--worker") {Worker(args[1]);return 0;}
            if(args.Length==2 && args[0]=="--subtitles") {RunSubtitles(args[1]);return 0;}
            if(args.Length==2 && args[0]=="--analysis") {RunAnalysis(args[1]);return 0;}
            if(!(args.Length==1 && args[0]=="--eagle-rpc")) {
                string origin="chrome-extension://"+File.ReadAllText(Path.Combine(Root,"extension-id.txt")).Trim()+"/";
                if(args.Length<1 || args[0]!=origin) return 2;
            }
            Stream input=Console.OpenStandardInput(),output=Console.OpenStandardOutput();uint size=BitConverter.ToUInt32(ReadBytes(input,4),0);if(size==0 || size>1048576)return 3;
            Dictionary<string,object> response;
            try {var q=Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(ReadBytes(input,(int)size)));response=Handle(q);}
            catch(Exception e) {response=Obj("ok",false,"error",e.Message);}
            byte[] b=Encoding.UTF8.GetBytes(Json.Serialize(response));byte[] len=BitConverter.GetBytes(b.Length);output.Write(len,0,4);output.Write(b,0,b.Length);output.Flush();return 0;
        } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
