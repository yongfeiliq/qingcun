using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
partial class Host {
    static readonly string XpcBridgeDir=Path.Combine(Data,"xpc-browser");
    static bool XpcPlayable(Dictionary<string,object> c){return XpcResource(S(c,"mediaUrl"))!=""||(S(D(c,"xpc"),"vid")!=""&&S(D(c,"xpc"),"appKey")!="");}
    static string XpcBridgePath(string id){if(!Regex.IsMatch(id??"",@"^[a-f0-9]{32}$"))throw new Exception("网页读取任务无效。");return Path.Combine(XpcBridgeDir,id+".json");}
    static bool XpcBridgeAlive(Dictionary<string,object> r){DateTime t;return DateTime.TryParse(S(r,"expires"),out t)&&t.ToUniversalTime()>DateTime.UtcNow&&!new[]{"ready","failed","expired"}.Contains(S(r,"state"));}
    static Dictionary<string,object> CreateXpcBrowserRequest(string url,string jobId){
        return LibraryLock(()=>{Directory.CreateDirectory(XpcBridgeDir);var r=Obj("id",Guid.NewGuid().ToString("N"),"url",Normalize(url),"jobId",jobId,"state","pending","created",DateTime.UtcNow.ToString("o"),"expires",DateTime.UtcNow.AddMinutes(4).ToString("o"));Write(XpcBridgePath(S(r,"id")),r);return r;});
    }
    static string XpcClient(Dictionary<string,object> q){string c=S(q,"client");if(!Regex.IsMatch(c,@"^[a-f0-9-]{36}$"))throw new Exception("浏览器连接标识无效。");return c;}
    static Dictionary<string,object> PollXpcBrowser(Dictionary<string,object> q){
        string client=XpcClient(q);return LibraryLock(()=>{
            var requests=new List<Dictionary<string,object>>();if(!Directory.Exists(XpcBridgeDir))return Obj("ok",true,"requests",requests);
            foreach(string p in Directory.GetFiles(XpcBridgeDir,"*.json").OrderBy(x=>File.GetCreationTimeUtc(x))){
                var r=Read(p);if(!XpcBridgeAlive(r))continue;
                DateTime lease;if(S(r,"client")!=""&&S(r,"client")!=client&&DateTime.TryParse(S(r,"lease"),out lease)&&lease.ToUniversalTime()>DateTime.UtcNow)continue;
                r["client"]=client;r["lease"]=DateTime.UtcNow.AddSeconds(75).ToString("o");if(S(r,"state")=="pending")r["state"]="reading";Write(p,r);
                requests.Add(Obj("id",S(r,"id"),"url",S(r,"url"),"state",S(r,"state"),"expires",S(r,"expires")));break;
            }
            return Obj("ok",true,"requests",requests);
        });
    }
    static Dictionary<string,object> CompleteXpcBrowser(Dictionary<string,object> q){
        string client=XpcClient(q);return LibraryLock(()=>{
            string p=XpcBridgePath(S(q,"id"));var r=Read(p);if(!XpcBridgeAlive(r)||S(r,"client")!=client)throw new Exception("网页读取任务已结束或由其他浏览器接管。");
            string state=S(q,"state");
            if(state=="ready"){
                var page=D(q,"page");if(XpcId(S(page,"pageUrl"))!=XpcId(S(r,"url")))throw new Exception("读取页面与下载作品不一致。");
                var context=XpcContext(page,S(r,"url"));if(!XpcPlayable(context))throw new Exception("页面没有返回有效的播放器资料。");
                SaveXpcPage(context);r["context"]=context;
            }else if(!new[]{"reading","verification","failed"}.Contains(state))throw new Exception("网页读取状态无效。");
            r["state"]=state;r["message"]=Cut(S(q,"message"),200);Write(p,r);return Obj("ok",true);
        });
    }
    static Dictionary<string,object> WaitXpcBrowser(string url,Dictionary<string,object> job){
        var request=CreateXpcBrowserRequest(url,S(job,"id"));string p=XpcBridgePath(S(request,"id")),last="";
        try{
            while(true){
                request=Read(p);string state=S(request,"state");if(state=="ready")return D(request,"context");
                if(state=="failed")throw new Exception(S(request,"message")!=""?S(request,"message"):"浏览器未能读取此作品，请重新添加链接。");
                if(!XpcBridgeAlive(request))throw new Exception(state=="verification"?"新片场仍要求网页验证。请完成浏览器中该作品页的验证后重新下载。":"未收到浏览器的可播放资料。请保持 Chrome／Edge 运行，并在扩展管理页重新加载轻存、允许访问新片场后重试。");
                string detail=state=="verification"?"新片场要求验证：请在浏览器中打开的作品页完成，完成后本次下载会自动继续。":state=="reading"?"正在通过浏览器读取新片场作品，读取后自动继续下载…":"正在连接浏览器读取新片场作品，请保持 Chrome／Edge 运行…";
                if(detail!=last){job["detail"]=detail;Write(JobPath(S(job,"id")),job);last=detail;}
                Thread.Sleep(1000);
            }
        }finally{LibraryLock(()=>{var latest=Read(p);if(!new[]{"ready","failed"}.Contains(S(latest,"state"))){latest["state"]="expired";Write(p,latest);}return true;});}
    }
    static Dictionary<string,object> XpcDownloadForJob(string url,Dictionary<string,object> supplied,Dictionary<string,object> job){
        try{return XpcDownloadInfo(url,supplied);}catch{
            var context=WaitXpcBrowser(url,job);foreach(var pair in context)supplied[pair.Key]=pair.Value;
            job["detail"]="网页资料已读取，正在继续下载新片场视频…";Write(JobPath(S(job,"id")),job);
            return XpcDownloadInfo(url,context);
        }
    }
}
