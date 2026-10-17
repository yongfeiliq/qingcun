using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Web.Script.Serialization;

partial class Host {
    static readonly string AnalysisDir=Path.Combine(Data,"analysis-jobs");
    static readonly string PreviewDir=Path.Combine(Data,"previews");
    static readonly string SecretPath=Path.Combine(Data,"model-secret.bin");
    static readonly string DefaultModel="gemini-3.8-flash";
    static JavaScriptSerializer BigJson(){return new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=100};}
    static string ModelName(Dictionary<string,object> s){return S(s,"model")!=""?S(s,"model"):DefaultModel;}
    static string ReadModelKey(){
        if(!File.Exists(SecretPath))return "";
        try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(SecretPath),null,DataProtectionScope.CurrentUser));}
        catch{throw new Exception("模型密钥无法解密，请在设置中重新填写。");}
    }
    static Dictionary<string,object> ModelSettings(){var s=Settings();bool enabled=B(s,"analysisEnabled");var codex=enabled&&Provider(s)=="codex"?CodexStatus():Obj("checked",false,"message","Codex 分析未启用；无需登录或配置。");bool configured=Provider(s)=="codex"?B(codex,"loggedIn"):File.Exists(SecretPath);return Obj("ok",true,"provider",Provider(s),"model",ModelName(s),"hasKey",File.Exists(SecretPath),"configured",configured,"codex",codex,"enabled",B(s,"analysisEnabled"),"autoAnalyze",B(s,"autoAnalyze"),"sampleFps",N(s,"sampleFps")>0?N(s,"sampleFps"):4,"maxDuration",300);}
    static Dictionary<string,object> SetAnalysisEnabled(Dictionary<string,object> q){
        bool enabled=B(q,"enabled");var current=Settings();string provider=S(q,"provider")==""?Provider(current):S(q,"provider");
        if(enabled){if(provider!="codex"&&provider!="gemini")throw new Exception("请选择有效的模型服务。");if(provider=="codex"&&!B(CodexStatus(),"loggedIn"))throw new Exception("尚未连接 Codex，开关保持关闭。需要智能分析时，登录本机 Codex 后再开启；下载无需配置。");if(provider=="gemini"&&!File.Exists(SecretPath))throw new Exception("尚未保存 Gemini 密钥，开关保持关闭。可在高级设置中填写，或使用本机 Codex。");}
        return LibraryLock(()=>{var s=Settings();if(enabled)s["provider"]=provider;s["analysisEnabled"]=enabled;if(!enabled){s["autoAnalyze"]=false;s["analysisRevision"]=N(s,"analysisRevision")+1;}Write(Path.Combine(Data,"settings.json"),s);return ModelSettings();});
    }
    static void CheckAnalysisEnabled(Dictionary<string,object> job){var settings=Settings();if(!B(settings,"analysisEnabled")||Provider(settings)!=S(job,"provider")||N(settings,"analysisRevision")!=N(job,"analysisRevision"))throw new Exception("智能分析已关闭或设置已变化，已有标签保留。");}
    static Dictionary<string,object> SaveModelSettings(Dictionary<string,object> q){
        string provider=S(q,"provider");if(provider=="")provider=Provider(Settings());if(provider!="codex"&&provider!="gemini")throw new Exception("请选择有效的模型服务。");
        bool enabled=B(q,"enabled");if(provider=="codex"&&enabled&&!B(CodexStatus(),"loggedIn"))throw new Exception("请先在本机 Codex 中使用 ChatGPT 登录，再启用画面分析。");
        return LibraryLock(()=>{var s=Settings();string model=S(q,"model").Trim(),key=S(q,"apiKey").Trim();
            if(provider=="gemini"&&!Regex.IsMatch(model,@"^gemini-[a-zA-Z0-9._-]{1,90}$"))throw new Exception("模型名称应为有效的 Gemini 模型 ID。");
            double fps=N(q,"sampleFps");if(!new[]{2.0,4.0,8.0}.Contains(fps))throw new Exception("请选择有效的采样密度。");
            if(B(q,"removeKey")&&provider=="gemini"&&enabled)throw new Exception("移除密钥时需要同时停用 Gemini 分析。");
            if(provider=="gemini"&&enabled&&key==""&&!File.Exists(SecretPath))throw new Exception("先填写密钥，才能启用 Gemini 分析。");
            if(B(q,"removeKey")){if(File.Exists(SecretPath))File.Delete(SecretPath);}
            else if(provider=="gemini"&&key!=""){if(key.Length<12||key.Length>300||Regex.IsMatch(key,@"\s"))throw new Exception("密钥格式不正确。");File.WriteAllBytes(SecretPath,ProtectedData.Protect(Encoding.UTF8.GetBytes(key),null,DataProtectionScope.CurrentUser));}
            s["provider"]=provider;if(provider=="gemini")s["model"]=model;s["sampleFps"]=fps;s["analysisEnabled"]=enabled;if(!enabled)s["analysisRevision"]=N(s,"analysisRevision")+1;s["autoAnalyze"]=enabled&&B(q,"autoAnalyze");Write(Path.Combine(Data,"settings.json"),s);return ModelSettings();
        });
    }
    static string ApiBase(){
#if QINGCUN_TESTS
        string p=Path.Combine(Root,"test-api.txt");if(File.Exists(p)){string url=File.ReadAllText(p).Trim();Uri u=new Uri(url);if(u.Scheme=="http"&&u.Host=="127.0.0.1")return url.TrimEnd('/');throw new Exception("Invalid test endpoint");}
#endif
        return "https://generativelanguage.googleapis.com/v1beta";
    }
    static Dictionary<string,object> ModelRequest(string suffix,object body,string key,int timeout){
        if(body!=null&&!B(Settings(),"analysisEnabled"))throw new Exception("智能分析已关闭，未发送新的分析请求。");
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create(ApiBase()+suffix);request.Method=body==null?"GET":"POST";request.Timeout=timeout;request.ReadWriteTimeout=timeout;request.AllowAutoRedirect=false;
        request.Headers["x-goog-api-key"]=key;request.ContentType="application/json; charset=utf-8";
        try{
            if(body!=null){byte[] bytes=Encoding.UTF8.GetBytes(BigJson().Serialize(body));if(bytes.Length>19*1024*1024)throw new Exception("分析副本超过当前请求大小限制，请选择更短的视频。");request.ContentLength=bytes.Length;using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);}
            using(var response=(HttpWebResponse)request.GetResponse()){
                if((int)response.StatusCode<200||(int)response.StatusCode>=300)throw new Exception("模型服务返回重定向或异常状态，请检查服务连接。");
                using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)){
                    char[] buffer=new char[8192];var text=new StringBuilder();int n;while((n=reader.Read(buffer,0,buffer.Length))>0){text.Append(buffer,0,n);if(text.Length>2*1024*1024)throw new Exception("模型返回内容过大。");}
                    return BigJson().Deserialize<Dictionary<string,object>>(text.ToString());
                }
            }
        }catch(WebException e){
            int status=e.Response is HttpWebResponse?(int)((HttpWebResponse)e.Response).StatusCode:0;if(e.Response!=null)e.Response.Close();
            if(status==401||status==403)throw new Exception("密钥无效、项目权限不足或服务在当前地区不可用，请检查模型账户。");
            if(status==429)throw new Exception("模型额度不足或请求过于频繁。请检查账单与配额后重试。");
            if(status==404)throw new Exception("这个模型不可用，请核对模型名称或更换模型。");
            if(status==400)throw new Exception("模型不接受本次视频或参数，请确认模型支持视频与结构化输出。");
            throw new Exception(status>0?"模型服务暂时出错（"+status+"），原视频和已有分析已保留。":"无法连接模型服务或请求超时，请检查网络后重试。");
        }
    }
    static Dictionary<string,object> TestModel(){
        if(!B(Settings(),"analysisEnabled"))throw new Exception("智能分析已关闭；需要使用时再打开开关，无需配置即可下载。");
        if(Provider(Settings())=="codex"){var status=CodexStatus();if(!B(status,"loggedIn"))throw new Exception(S(status,"message"));return Obj("ok",true,"message","已检测到本机 ChatGPT 登录。此检查不上传图片；实际模型可用性与分析效果以首次分析为准。");}
        string key=ReadModelKey();if(key=="")throw new Exception("请先保存模型密钥。");
        var r=ModelRequest("/models/"+ModelName(Settings()),null,key,20000);
        if(!Strings(r,"supportedGenerationMethods").Contains("generateContent"))throw new Exception("当前模型不支持所需的内容分析接口。");
        return Obj("ok",true,"message","密钥与模型权限检查通过。尚未上传视频；视频分析效果需要首次分析后确认。");
    }
    static Dictionary<string,object> MediaFile(Dictionary<string,object> a,int index){
        var files=Items(a,"files");if(index<0||index>=files.Count)throw new Exception("视频文件不存在。");
        var f=files[index];if(!File.Exists(S(f,"path")))throw new Exception("视频已移动，请在新位置重新导入。");
        return f;
    }
    static string MediaKey(Dictionary<string,object> f){var info=new FileInfo(S(f,"path"));return HashText(info.FullName.ToLowerInvariant()+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks).Substring(0,32);}
    static string PreviewPath(string key){if(!Regex.IsMatch(key,@"^[a-f0-9]{32}$"))throw new Exception("无效的预览标识。");return Path.Combine(PreviewDir,key+".mp4");}
    static string EnsurePreview(Dictionary<string,object> f){
        double duration=N(f,"duration");if(duration<=0||duration>300)throw new Exception("本版画面分析与内嵌预览支持 5 分钟以内的视频；较长视频可先分段导入。");
        Directory.CreateDirectory(PreviewDir);string key=MediaKey(f),target=PreviewPath(key);
        using(var mutex=new Mutex(false,"QingcunPreview_"+key)){bool held=false;try{try{held=mutex.WaitOne(180000);}catch(AbandonedMutexException){held=true;}if(!held)throw new Exception("预览正在生成，请稍后重试。");
            if(File.Exists(target)&&new FileInfo(target).Length>0)return target;
            string temp=Path.Combine(PreviewDir,key+"."+Guid.NewGuid().ToString("N")+".mp4");
            try{
                RunTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),new[]{"-y","-hide_banner","-loglevel","error","-i",S(f,"path"),"-an","-vf","fps=12,scale=640:640:force_original_aspect_ratio=decrease:force_divisible_by=2","-c:v","libx264","-preset","veryfast","-crf","27","-maxrate","300k","-bufsize","600k","-pix_fmt","yuv420p","-movflags","+faststart",temp},180000);
                if(new FileInfo(temp).Length>13*1024*1024)throw new Exception("视频副本过大，请使用更短的片段。");
                File.Move(temp,target);return target;
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }finally{if(held)mutex.ReleaseMutex();}}
    }
    static Dictionary<string,object> PreviewInfo(Dictionary<string,object> q){var f=MediaFile(GetAsset(S(q,"id")),(int)N(q,"index"));string p=EnsurePreview(f);return Obj("ok",true,"key",MediaKey(f),"bytes",new FileInfo(p).Length,"chunkSize",262144,"mimeType","video/mp4");}
    static Dictionary<string,object> PreviewChunk(Dictionary<string,object> q){
        var a=GetAsset(S(q,"id"));var f=MediaFile(a,(int)N(q,"index"));string key=MediaKey(f);if(key!=S(q,"key"))throw new Exception("视频文件已变化，请重新加载预览。");
        string p=PreviewPath(key);if(!File.Exists(p))throw new Exception("请先生成视频预览。");long offset=(long)N(q,"offset");using(var stream=File.OpenRead(p)){if(offset<0||offset>=stream.Length)throw new Exception("无效的预览位置。");stream.Position=offset;byte[] b=new byte[(int)Math.Min(262144,stream.Length-offset)];int n=stream.Read(b,0,b.Length);return Obj("ok",true,"data",Convert.ToBase64String(b,0,n),"nextOffset",offset+n);}
    }
    static double ExtractFramePng(string source,double requested,double duration,string target,int width,int height){
        if(!File.Exists(source))throw new Exception("视频文件已移动或删除，请重新导入原文件。");
        double start=Math.Max(0,Math.Min(requested,duration));
        // MP4 duration may include an audio tail; seek backwards only when no image was produced.
        double[] candidates=new[]{start,Math.Max(0,start-.12),Math.Max(0,start-.5),Math.Max(0,start-1)}.Distinct().ToArray();
        string temp=target+"."+Guid.NewGuid().ToString("N")+".png";
        try{
            foreach(double time in candidates){
                if(File.Exists(temp))File.Delete(temp);
                RunTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),new[]{"-y","-hide_banner","-loglevel","error","-ss",time.ToString("0.###",CultureInfo.InvariantCulture),"-i",source,
                    "-map","0:v:0","-frames:v","1","-vf","scale="+width+":"+height+":force_original_aspect_ratio=decrease",
                    "-pix_fmt","rgb24","-threads","1","-c:v","png","-f","image2",temp},30000,false);
                if(!File.Exists(temp)||new FileInfo(temp).Length==0)continue;
                try{if(!PngValid(temp))continue;}
                catch{continue;}
                if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
                return time;
            }
            throw new Exception("无法提取第 "+requested.ToString("0.###",CultureInfo.InvariantCulture)+" 秒附近的画面，已保留原视频与分析结果。");
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
    static Dictionary<string,object> ShotThumbnail(Dictionary<string,object> q){
        var a=GetAsset(S(q,"id"));var f=MediaFile(a,(int)N(q,"index"));double time=N(q,"time");
        if(Double.IsNaN(time)||Double.IsInfinity(time)||time<0||time>N(f,"duration"))throw new Exception("无效的镜头时间。");
        string dir=Path.Combine(PreviewDir,"frames");Directory.CreateDirectory(dir);string p=Path.Combine(dir,MediaKey(f)+"_"+Math.Round(time*1000).ToString(CultureInfo.InvariantCulture)+"_safe.png");
        if(!File.Exists(p))ExtractFramePng(S(f,"path"),time,N(f,"duration"),p,240,426);
        return Obj("ok",true,"image",new FileInfo(p).Length<450000?"data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(p)):"");
    }

    static string AnalysisJobPath(string id){if(!Regex.IsMatch(id??"",@"^[a-f0-9]{32}$"))throw new Exception("无效的分析任务。");return Path.Combine(AnalysisDir,id+".json");}
    static Dictionary<string,object> ReadAnalysisJob(string id){
        if(id=="")return Obj();string p=AnalysisJobPath(id);Dictionary<string,object> j;try{j=Read(p);}catch(FileNotFoundException){return Obj();}catch(DirectoryNotFoundException){return Obj();}
        if(new[]{"queued","running"}.Contains(S(j,"state"))&&j.ContainsKey("pid")){
            bool alive;try{using(var process=Process.GetProcessById((int)N(j,"pid")))alive=process.StartTime.ToUniversalTime().Ticks.ToString()==S(j,"pidStart");}catch{alive=false;}
            if(!alive){var latest=Read(p);if(!new[]{"queued","running"}.Contains(S(latest,"state")))return latest;j["state"]="failed";j["message"]="分析已中断，请重新开始；已有结果保留。";Write(p,j);}
        }else if(S(j,"state")=="queued"&&!j.ContainsKey("pid")){DateTime created;if(DateTime.TryParse(S(j,"created"),out created)&&(DateTime.UtcNow-created.ToUniversalTime()).TotalMinutes>2){j["state"]="failed";j["message"]="分析任务未能启动，请重试。";Write(p,j);}}
        return j;
    }
    static Dictionary<string,object> StartAnalysis(Dictionary<string,object> q){
        var settings=Settings();if(!B(settings,"analysisEnabled"))throw new Exception("智能分析已关闭。需要时可在设置中打开开关，下载与手动标签可照常使用。");if(!ModelReady(settings))throw new Exception("模型暂未连接，请在设置中检查当前服务。");
        return LibraryLock(()=>{
            var a=Read(AssetPath(S(q,"id")));var old=ReadAnalysisJob(S(a,"analysisJobId"));
            if(new[]{"queued","running"}.Contains(S(old,"state")))return Obj("ok",true,"job",old);
            var f=MediaFile(a,(int)N(q,"index"));if(ImageAsset(a)&&S(f,"key")!=MediaKey(f))throw new Exception("图片文件已变化，请重新读取。");if(!ImageAsset(a)&&(N(f,"duration")<=0||N(f,"duration")>300))throw new Exception("本版支持 5 分钟以内的视频分析，请先将长视频分段。");
            Directory.CreateDirectory(AnalysisDir);string id=Guid.NewGuid().ToString("N");var j=Obj("id",id,"assetId",S(a,"id"),"index",(int)N(q,"index"),"filePath",S(f,"path"),"fileKey",MediaKey(f),"state","queued","message","等待本地预处理","percent",0,"created",DateTime.UtcNow.ToString("o"),"provider",Provider(settings),"analysisRevision",N(settings,"analysisRevision"),"model",AnalysisModelName(settings),"sampleFps",N(settings,"sampleFps")>0?N(settings,"sampleFps"):4);
            j["kind"]=S(q,"kind");Write(AnalysisJobPath(id),j);a["analysisJobId"]=id;Write(AssetPath(S(a,"id")),a);
            try{using(var process=Process.Start(new ProcessStartInfo(Path.Combine(Root,ExecutableName("QingcunHost")),"--analysis "+id){UseShellExecute=false,WorkingDirectory=Root})){}}
            catch{j["state"]="failed";j["message"]="无法启动分析，请重新安装本地组件。";Write(AnalysisJobPath(id),j);throw;}
            return Obj("ok",true,"job",j);
        });
    }
    static readonly string[] ShotScales={"远景","全景","中景","近景","特写","大特写","变化景别","待确认"};
    static readonly string[] ShotMovements={"固定","推近","拉远","横移","跟拍","环绕","水平摇镜","俯仰摇镜","升降","手持","变焦","滚转","甩镜","推拉变焦","复合运镜","待确认"};
    static readonly string[] ShotTones={"暖调","冷调","中性","冷暖混合","待确认"};
    static void CheckShotLabel(string key,string value){if(!(key=="scale"?ShotScales:key=="movement"?ShotMovements:ShotTones).Contains(value))throw new Exception("镜头标签不在可选范围，请使用景别、运镜和冷暖色调选项。");}
    static object AnalysisSchema(){
        var shotProps=Obj("start",Obj("type","number"),"end",Obj("type","number"));
        foreach(string k in new[]{"scale","movement","color"})shotProps[k]=Obj("type","string","enum",k=="scale"?ShotScales:k=="movement"?ShotMovements:ShotTones);
        return Obj("type","object","properties",Obj("shots",Obj("type","array","items",Obj("type","object","properties",shotProps,"required",shotProps.Keys.ToArray()),"minItems",1,"maxItems",600)),"required",new[]{"shots"});
    }
    static double Finite(Dictionary<string,object> d,string key){double n;if(!d.ContainsKey(key)||!Double.TryParse(S(d,key),NumberStyles.Any,CultureInfo.InvariantCulture,out n)||Double.IsNaN(n)||Double.IsInfinity(n))throw new Exception("镜头时间格式无效。");return n;}
    static Dictionary<string,object> ValidateAnalysis(Dictionary<string,object> raw,double duration){
        var shots=Items(raw,"shots");if(shots.Count==0||shots.Count>600)throw new Exception("模型没有返回有效的镜头表，原有分析已保留。");
        double previous=0;var clean=new List<Dictionary<string,object>>();
        foreach(var shot in shots){
            double start=Finite(shot,"start"),end=Finite(shot,"end");
            if(start<0||end>duration+.4||end<=start||Math.Abs(start-previous)>.4)throw new Exception("模型镜头时间不连续或超出视频范围，请重新分析或降低片段长度。");
            start=previous;end=Math.Min(duration,end);if(end<=start)throw new Exception("镜头时长无效。");
            var s=Obj("id",Guid.NewGuid().ToString("N"),"start",Math.Round(start,3),"end",Math.Round(end,3),"status","suggested");
            foreach(string key in new[]{"scale","movement","color"}){
                string value=Cut(S(shot,key),key=="description"?280:80);if(value==""||!Regex.IsMatch(value,@"[\u3400-\u9fff]"))throw new Exception("模型没有返回完整的中文镜头标签，请重试。");CheckShotLabel(key,value);s[key]=value;
            }
            previous=end;clean.Add(s);
        }
        if(Math.Abs(previous-duration)>.4)throw new Exception("模型没有覆盖完整视频，已有结果保留。");
        clean[clean.Count-1]["end"]=Math.Round(duration,3);
        return Obj("schemaVersion",2,"shots",clean,"status","suggested","source","model","updated",DateTime.UtcNow.ToString("o"));
    }
    static void RunAnalysis(string id){
        string p=AnalysisJobPath(id);var j=Read(p);using(var process=Process.GetCurrentProcess()){j["pid"]=process.Id;j["pidStart"]=process.StartTime.ToUniversalTime().Ticks.ToString();}Write(p,j);
        using(var mutex=new Mutex(false,"QingcunAnalysis_"+UserKey())){
            bool held=false;try{
                try{held=mutex.WaitOne(600000);}catch(AbandonedMutexException){held=true;}if(!held)throw new Exception("分析队列等待超时，请稍后重试。");
                CheckAnalysisEnabled(j);j["state"]="running";j["stage"]="preparing";j["message"]=S(j,"provider")=="codex"?"正在准备本地抽帧":"正在生成本地静音分析副本";j["percent"]=10;Write(p,j);
                var a=GetAsset(S(j,"assetId"));var f=Items(a,"files").FirstOrDefault(x=>S(x,"path")==S(j,"filePath"));if(f==null||MediaKey(f)!=S(j,"fileKey"))throw new Exception("视频文件已变化，请重新发起分析。");
                if(ImageAsset(a)){RunImageAnalysis(j,f);return;}
                if(S(j,"kind")=="film_type"){RunFilmTypeAnalysis(j,f);return;}
                string preview=S(j,"provider")=="codex"?"":EnsurePreview(f);
                j["message"]="正在检测镜头切换候选";j["percent"]=25;Write(p,j);
                var times=DetectCutCandidates(S(f,"path"),N(f,"duration"));
                string prompt=CinematographyRules();
                if(!B(Settings(),"analysisEnabled"))throw new Exception("画面分析已在设置中停用。");
                string result;
                if(S(j,"provider")=="codex"){result=CodexFrameAnalysis(j,f,times,prompt);}
                else {
                if(Provider(Settings())!="gemini")throw new Exception("分析服务已切换，请重新开始。");
                string key=ReadModelKey();if(key=="")throw new Exception("模型密钥已移除，请重新配置。");
                j["message"]="正在上传静音副本并分析画面";j["percent"]=45;Write(p,j);
                var body=Obj("contents",new[]{Obj("role","user","parts",new[]{Obj("inlineData",Obj("mimeType","video/mp4","data",Convert.ToBase64String(File.ReadAllBytes(preview))),"videoMetadata",Obj("fps",N(j,"sampleFps"))),Obj("text",prompt+" 完整视频时长："+N(f,"duration").ToString(CultureInfo.InvariantCulture)+" 秒，从 0 开始覆盖全片，首尾连续；切点候选："+String.Join(", ",times))})},"generationConfig",Obj("responseMimeType","application/json","responseJsonSchema",AnalysisSchema(),"maxOutputTokens",32768));
                var response=ModelRequest("/models/"+S(j,"model")+":generateContent",body,key,300000);key="";
                var candidate=Items(response,"candidates").FirstOrDefault();if(candidate==null)throw new Exception("模型没有返回分析结果，可能触发服务限制。");
                if(S(candidate,"finishReason")!="STOP")throw new Exception("模型未完整返回结果（输出截断或受限），请改用更短的片段。");
                result=String.Join("",Items(D(candidate,"content"),"parts").Where(x=>!B(x,"thought")).Select(x=>S(x,"text")));}

                j["stage"]="validating";Write(p,j);
                Dictionary<string,object> raw;try{raw=BigJson().Deserialize<Dictionary<string,object>>(result);}catch{throw new Exception("模型返回格式不正确，原有结果已保留。");}
                var analysis=ValidateAnalysis(raw,N(f,"duration"));analysis["fileKey"]=S(j,"fileKey");analysis["filePath"]=S(f,"path");analysis["duration"]=N(f,"duration");analysis["model"]=S(j,"model");analysis["sampleFps"]=N(j,"sampleFps");analysis["provider"]=S(j,"provider");analysis["method"]=S(j,"provider")=="codex"?"sampled_frames":"video";analysis["frameCount"]=N(j,"frameCount");analysis["samplingNote"]=S(j,"samplingNote");analysis["rulesVersion"]=ShotRulesVersion;analysis["chunkCount"]=N(j,"chunksTotal");
                LibraryLock(()=>{CheckAnalysisEnabled(j);var latest=Read(AssetPath(S(j,"assetId")));if(S(latest,"analysisJobId")!=id)throw new Exception("已存在更新的分析任务，本次结果未覆盖。");if(MediaKey(f)!=S(j,"fileKey"))throw new Exception("分析期间视频文件发生变化，请重新分析。");latest["analysisDraft"]=analysis;latest["analysisState"]="review";SaveAsset(latest);return true;});
                j["state"]="completed";j["message"]="分析完成，请复核运镜、景别与冷暖色调";j["percent"]=100;Write(p,j);
            }catch(Exception e){j["state"]="failed";j["message"]=Cut((S(j,"stage")=="frames"?"本地抽帧未完成，尚未提交模型。":"")+e.Message,260);j["percent"]=0;Write(p,j);}
            finally{if(held)mutex.ReleaseMutex();}
        }
    }
    static Dictionary<string,object> EditAnalysis(Dictionary<string,object> q){
        return LibraryLock(()=>{
            var a=Read(AssetPath(S(q,"id")));bool draft=D(a,"analysisDraft").Count>0;var v=VisibleAnalysis(a);if(v.Count==0)throw new Exception("请先完成一次画面分析。");
            var shots=Items(v,"shots");string op=S(q,"op");
            if(op=="accept"){v["status"]="confirmed";foreach(var s in shots)s["status"]="confirmed";a["analysis"]=v;a.Remove("analysisDraft");a["analysisState"]="confirmed";}
            else if(op=="discard"){if(!draft)throw new Exception("当前没有待审阅的新结果。");a.Remove("analysisDraft");a["analysisState"]=D(a,"analysis").Count>0?"confirmed":"not_analyzed";}
            else if(op=="overview"){string production=S(q,"production");if(!ProductionTypes.Contains(production))throw new Exception("无效的制作方式。");v["production"]=production;v["colors"]=Strings(q,"colors").Where(x=>Regex.IsMatch(x,@"[\u3400-\u9fff]")).Select(x=>Cut(x,40)).Take(6).ToArray();v["summary"]=Cut(S(q,"summary"),800);v["overviewStatus"]="confirmed";}
            else{
                int index=shots.FindIndex(s=>S(s,"id")==S(q,"shotId"));if(index<0)throw new Exception("镜头已变化，请刷新后重试。");var shot=shots[index];
                if(op=="split"){double mid=Math.Round((N(shot,"start")+N(shot,"end"))/2,3);if(mid-N(shot,"start")<.05)throw new Exception("镜头过短，无法继续拆分。");var next=new Dictionary<string,object>(shot);next["id"]=Guid.NewGuid().ToString("N");next["start"]=mid;next["status"]="suggested";shot["end"]=mid;shot["status"]="suggested";shots.Insert(index+1,next);}
                else if(op=="merge"){if(index==shots.Count-1)throw new Exception("最后一个镜头没有后续镜头可合并。");shot["end"]=shots[index+1]["end"];foreach(string key in new[]{"scale","movement","color"})if(S(shot,key)!=S(shots[index+1],key))shot[key]="待确认";shot["status"]="suggested";shots.RemoveAt(index+1);}
                else if(op=="shot"){
                    double start=Finite(q,"start"),end=Finite(q,"end"),duration=N(v,"duration");
                    if(start<0||end>duration||end<=start||index==0&&Math.Abs(start)>.001||index==shots.Count-1&&Math.Abs(end-duration)>.001||index>0&&start<=N(shots[index-1],"start")||index+1<shots.Count&&end>=N(shots[index+1],"end"))throw new Exception("镜头起止时间无效：首尾必须覆盖全片，不能跨过相邻镜头。");
                    shot["start"]=Math.Round(start,3);shot["end"]=Math.Round(end,3);
                    if(index>0){shots[index-1]["end"]=start;shots[index-1]["status"]="suggested";}if(index+1<shots.Count){shots[index+1]["start"]=end;shots[index+1]["status"]="suggested";}
                    foreach(string k in new[]{"scale","movement","color"}){string value=Cut(S(q,k),k=="description"?280:80);if(!Regex.IsMatch(value,@"[\u3400-\u9fff]"))throw new Exception("镜头描述和标签请使用中文，不确定可填写“待确认”。");CheckShotLabel(k,value);shot[k]=value;}shot["status"]="confirmed";
                }else throw new Exception("无效的镜头操作。");
                if(shots.Count>600)throw new Exception("本版每次分析最多保存 600 个镜头。");v["shots"]=shots;v["status"]=shots.All(x=>S(x,"status")=="confirmed")?"confirmed":"suggested";a["analysisState"]=S(v,"status")=="confirmed"?"confirmed":"review";
            }
            v["updated"]=DateTime.UtcNow.ToString("o");SaveAsset(a);return Obj("ok",true,"asset",a);
        });
    }
    static Dictionary<string,object> AnalysisDispatch(Dictionary<string,object> q){
        switch(S(q,"action")){
            case "modelSettings":return ModelSettings();
            case "setAnalysisEnabled":return SetAnalysisEnabled(q);
            case "saveModelSettings":return SaveModelSettings(q);
            case "testModel":return TestModel();
            case "startFilmTypeAnalysis":q["kind"]="film_type";return StartAnalysis(q);
            case "filmTypeInfo":return FilmTypeInfo(q);
            case "editFilmTypeAnalysis":return EditFilmTypeAnalysis(q);
            case "startAnalysis":if(EagleAsset(GetAsset(S(q,"id"))))q["kind"]="film_type";return StartAnalysis(q);
            case "analysisJob":{var a=GetAsset(S(q,"id"));return Obj("ok",true,"job",ReadAnalysisJob(S(a,"analysisJobId")));}
            case "imageInfo":return ImageInfo(q);
            case "editImageAnalysis":return EditImageAnalysis(q);
            case "editAnalysis":return EditAnalysis(q);
            case "editCredit":return EditCredit(q);
            case "refreshSource":return RefreshSource(q);
            case "editSource":return EditSource(q);
            case "previewInfo":return PreviewInfo(q);
            case "previewChunk":return PreviewChunk(q);
            case "shotThumbnail":return ShotThumbnail(q);
            default:throw new Exception("未知的组件操作，请确认扩展和本地组件版本一致。");
        }
    }
}
