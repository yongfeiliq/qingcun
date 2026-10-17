using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
partial class Host {
    static string VimeoNumber(string url){try{var u=new Uri(Normalize(url));if(u.Host!="vimeo.com")return "";var m=Regex.Match(u.AbsolutePath,@"^/(\d+)(?:/[a-f0-9]+)?/?$");return m.Success?m.Groups[1].Value:"";}catch{return "";}}
    static string VimeoProfile(string raw){
        try{var u=new Uri(new Uri("https://vimeo.com"),System.Net.WebUtility.HtmlDecode(raw??"").Trim());
            if(u.Scheme!="https"||u.UserInfo!=""||u.Port!=443||!new[]{"vimeo.com","www.vimeo.com"}.Contains(u.Host))return "";
            string slug=u.AbsolutePath.Trim('/');if(!Regex.IsMatch(slug,@"^[A-Za-z0-9_-]{2,100}$")||Regex.IsMatch(slug,@"^\d+$")||new[]{"join","log_in","login","help","watch","settings","features","upgrade","home","search"}.Contains(slug.ToLowerInvariant()))return "";
            return "https://vimeo.com/"+slug;
        }catch{return "";}
    }
    static string VimeoText(string html){
        string s=Regex.Replace(html??"", @"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>","",RegexOptions.IgnoreCase);
        s=Regex.Replace(s,@"<br\s*/?>|</(?:p|div)\s*>","\n",RegexOptions.IgnoreCase);
        s=WebUtility.HtmlDecode(Regex.Replace(s,@"<[^>]+>","")).Replace('\u00a0',' ');
        return Regex.Replace(s,@"[ \t]+\n","\n").Trim();
    }
    static string VimeoAttribute(string tag,string name){var m=Regex.Match(tag,@"\b"+name+@"\s*=\s*[""']([^""']*)[""']",RegexOptions.IgnoreCase);return m.Success?WebUtility.HtmlDecode(m.Groups[1].Value):"";}
    static string VimeoRole(string role){
        string r=Regex.Replace(role??"",@"\s+"," ").Trim().ToLowerInvariant();
        switch(r){
            case "director":case "directed by":case "导演":return "导演";
            case "co-director":case "co director":return "联合导演";
            case "distributor":case "distribution":case "发行商":case "发行":return "发行";
            case "producer":case "produced by":return "制片";
            case "executive producer":return "执行制片";
            case "production company":case "production":return "制作公司";
            case "director of photography":case "dop":case "dp":case "cinematographer":return "摄影指导";
            case "camera":case "camera operator":return "摄影";
            case "writer":case "screenwriter":return "编剧";
            case "animator":case "animation":return "动画";
            case "animation director":return "动画导演";
            case "art director":case "production designer":return "美术指导";
            case "editor":case "editing":return "剪辑";
            case "colorist":case "colourist":return "调色";
            case "composer":case "music":return "配乐";
            case "sound designer":case "sound design":return "声音设计";
            case "vfx":case "visual effects":return "视觉特效";
            case "actor":case "actress":case "cast":return "出演";
            default:return "制作人员";
        }
    }
    static List<Dictionary<string,object>> ParseVimeoCredits(string html,string videoUrl,out bool complete){
        complete=false;var result=new List<Dictionary<string,object>>();string id=VimeoNumber(videoUrl);
        if(id=="")return result;
        var canonical=Regex.Matches(html??"", @"<link\b[^>]*>",RegexOptions.IgnoreCase).Cast<Match>().FirstOrDefault(m=>VimeoAttribute(m.Value,"rel")=="canonical");
        if(canonical==null||VimeoAttribute(canonical.Value,"href").TrimEnd('/')!="https://vimeo.com/"+id+"/credits")return result;
        // Only the dedicated credit list is authoritative, never navigation, likes or recommendations.
        var list=Regex.Match(html??"", @"<ol\b[^>]*class\s*=\s*[""'][^""']*\bbrowse_people\b[^""']*[""'][^>]*>([\s\S]*?)</ol\s*>",RegexOptions.IgnoreCase);
        if(!list.Success){
            if(Regex.IsMatch(html??"", @"<div\b[^>]*id\s*=\s*[""']browse_content[""'][^>]*>\s*<h3\b[^>]*class\s*=\s*[""']empty[""'][^>]*>\s*Sorry,\s*no people found\s*</h3>",RegexOptions.IgnoreCase)
                &&Regex.IsMatch(html??"", @"has\s*<em>\s*0 people\s*</em>",RegexOptions.IgnoreCase))complete=true;
            return result;
        }
        complete=true;
        foreach(Match card in Regex.Matches(list.Groups[1].Value,@"<li\b[^>]*\bid\s*=\s*[""']user_(\d+)[""'][^>]*>([\s\S]*?)</li\s*>",RegexOptions.IgnoreCase)){
            var a=Regex.Match(card.Groups[2].Value,@"<a\b[^>]*>",RegexOptions.IgnoreCase);if(!a.Success)continue;
            string profile=VimeoProfile(VimeoAttribute(a.Value,"href"));if(profile=="")continue;
            var title=Regex.Match(card.Groups[2].Value,@"<p\b[^>]*class\s*=\s*[""'][^""']*\btitle\b[^""']*[""'][^>]*>([\s\S]*?)</p\s*>",RegexOptions.IgnoreCase);
            var role=Regex.Match(card.Groups[2].Value,@"<p\b[^>]*class\s*=\s*[""'][^""']*\bmeta\b[^""']*[""'][^>]*>([\s\S]*?)</p\s*>",RegexOptions.IgnoreCase);
            string name=Cut(title.Success?VimeoText(title.Groups[1].Value):VimeoAttribute(a.Value,"title"),100),original=Cut(role.Success?VimeoText(role.Groups[1].Value):"",100);
            if(name=="")continue;
            result.Add(Obj("id",Guid.NewGuid().ToString("N"),"role",VimeoRole(original),"roleOriginal",original,"name",name,"account",new Uri(profile).AbsolutePath.Trim('/'),"url",profile,"platform","Vimeo","stableId",card.Groups[1].Value,"source","vimeo-credits","status","source","accountStatus","来源明确","evidence","Vimeo 制作人员页："+name+" · "+(original!=""?original:"未注明职务"),"sourceUrl","https://vimeo.com/"+id+"/credits"));
            if(result.Count>=50)break;
        }
        return result.GroupBy(c=>S(c,"stableId")+"|"+S(c,"roleOriginal")).Select(g=>g.First()).ToList();
    }
    static string VimeoGet(string url,string session=""){
        var uri=new Uri(url);if(uri.Scheme!="https"||uri.Host!="vimeo.com"||uri.Port!=443||uri.UserInfo!="")throw new Exception("Vimeo 资料地址无效。");
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create(uri);request.Method="GET";request.Timeout=12000;request.ReadWriteTimeout=12000;request.AllowAutoRedirect=false;
        request.Proxy=WebRequest.GetSystemWebProxy();request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
        request.UserAgent="Qingcun/2.0.1";request.Accept="application/json,text/html";request.Headers[HttpRequestHeader.AcceptLanguage]="en-US,en;q=0.8";
        if(session!=""){
            var stored=ReadVimeoSession(session);var jar=new CookieContainer();
            foreach(var c in Items(stored,"cookies"))try{var cookie=new Cookie(S(c,"name"),S(c,"value"),S(c,"path"),S(c,"domain")){Secure=B(c,"secure"),HttpOnly=B(c,"httpOnly")};jar.Add(cookie);}catch{}
            request.CookieContainer=jar;
        }
        using(var response=(HttpWebResponse)request.GetResponse()){
            if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>2*1024*1024)throw new Exception("Vimeo 没有返回可用资料。");
            using(var input=response.GetResponseStream())using(var memory=new MemoryStream()){
                var buffer=new byte[8192];int size;while((size=input.Read(buffer,0,buffer.Length))>0){if(memory.Length+size>2*1024*1024)throw new Exception("Vimeo 资料过大。");memory.Write(buffer,0,size);}
                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }
    }
    static Dictionary<string,object> FetchVimeoMetadata(string videoUrl,string session=""){
        string url=Normalize(videoUrl),id=VimeoNumber(url);if(id=="")throw new Exception("请输入 Vimeo 单条视频网址。");
        var meta=Obj("id",id,"webpage_url",url);var state=Obj("schema",2,"checkedAt",DateTime.UtcNow.ToString("o"),"description","unavailable","credits","unavailable","creditCount",0,"tags","unavailable","tagCount",0);
        try{
            var o=BigJson().Deserialize<Dictionary<string,object>>(VimeoGet("https://vimeo.com/api/oembed.json?url="+Uri.EscapeDataString(url),session));
            if(S(o,"video_id")!=id)throw new Exception("Vimeo 返回了其他视频。");
            meta["title"]=S(o,"title");meta["description"]=DescriptionText(S(o,"description"));meta["descriptionOrigin"]="vimeo-oembed";meta["descriptionUrl"]=url;meta["descriptionExcerpt"]=false;
            string profile=VimeoProfile(S(o,"author_url"));meta["uploader"]=S(o,"author_name");meta["channel"]=S(o,"author_name");meta["uploader_url"]=profile;meta["uploader_id"]=profile!=""?new Uri(profile).AbsolutePath.Trim('/'):"";
            meta["upload_date"]=S(o,"upload_date");state["description"]="read";
        }catch{}
        try{
            bool parsed;string html=VimeoGet("https://vimeo.com/"+id+"/credits",session);var credits=ParseVimeoCredits(html,url,out parsed);
            if(parsed){
                meta["vimeoCredits"]=credits;state["credits"]="read";state["creditCount"]=credits.Count;
                var owner=credits.FirstOrDefault(c=>S(c,"url")==S(meta,"uploader_url"));if(owner!=null)meta["channel_id"]=S(owner,"stableId");
                // A next-page link means this is a partial list; preserve that distinction.
                if(Regex.IsMatch(html,@"rel\s*=\s*[""']next[""']",RegexOptions.IgnoreCase)||credits.Count>=50)state["credits"]="partial";
            }
        }catch{}
        try{
            var rows=BigJson().Deserialize<List<Dictionary<string,object>>>(VimeoGet("https://vimeo.com/api/v2/video/"+id+".json",session));
            var legacy=rows.FirstOrDefault(x=>S(x,"id")==id);if(legacy==null)throw new Exception("Vimeo 标签来源不匹配。");
            if(legacy.ContainsKey("tags")){meta["tags"]=SourceLabels(S(legacy,"tags").Split(','));state["tags"]="read";state["tagCount"]=Strings(meta,"tags").Length;}
            if(S(legacy,"description")!=""){meta["description"]=DescriptionText(VimeoText(S(legacy,"description")));meta["descriptionOrigin"]="vimeo-public";meta["descriptionUrl"]=url;meta["descriptionExcerpt"]=false;state["description"]="read";}
            if(S(meta,"uploader_url")==""){meta["uploader_url"]=VimeoProfile(S(legacy,"user_url"));meta["uploader_id"]=S(meta,"uploader_url")!=""?new Uri(S(meta,"uploader_url")).AbsolutePath.Trim('/'):"";meta["uploader"]=S(legacy,"user_name");meta["channel"]=S(legacy,"user_name");}
        }catch{}
        bool full=S(state,"description")=="read"&&S(state,"credits")=="read"&&S(state,"tags")=="read";
        state["message"]=full?(N(state,"creditCount")==0?"已读取 Vimeo 简介；平台未列出制作人员账号。":"已读取 Vimeo 简介及 "+N(state,"creditCount")+" 位制作人员的账号。"):"Vimeo "+(S(state,"description")!="read"?"简介":"")+(S(state,"description")!="read"&&S(state,"credits")!="read"?"与":"")+(S(state,"credits")!="read"?"制作人员":"")+"资料未完整返回，已有内容保留；可点击“补全平台资料”重试。";
        state["message"]=S(state,"message")+(S(state,"tags")=="read"?" 已读取 "+N(state,"tagCount")+" 个原始标签。":" 原始标签暂未完整返回，已有标签保留。");meta["vimeoMetadata"]=state;return meta;
    }
    static bool SameCreditPerson(Dictionary<string,object> a,Dictionary<string,object> b){
        if(S(a,"stableId")!=""&&S(b,"stableId")!=""&&S(a,"platform")==S(b,"platform"))return S(a,"stableId")==S(b,"stableId");
        if(S(a,"url")!=""&&S(b,"url")!="")return S(a,"url").TrimEnd('/')==S(b,"url").TrimEnd('/');
        return S(a,"name").Trim().Equals(S(b,"name").Trim(),StringComparison.OrdinalIgnoreCase)&&(S(a,"account")==""||S(b,"account")==""||S(a,"account").Equals(S(b,"account"),StringComparison.OrdinalIgnoreCase));
    }
    static void MergeSourceCredits(Dictionary<string,object> a,List<Dictionary<string,object>> incoming){
        var credits=Items(a,"credits");var dismissed=Strings(a,"dismissedCredits");
        foreach(var c in incoming){
            if(dismissed.Any(k=>{var parts=k.Split('|');return k==CreditKey(c)||(parts.Length==3&&parts[0]==S(c,"role")&&parts[1]==S(c,"name")&&parts[2]=="");}))continue;
            var same=credits.FirstOrDefault(old=>SameCreditPerson(old,c)&&(S(old,"role")==S(c,"role")||S(old,"role")=="制作人员"||S(c,"role")=="制作人员"));
            if(same==null){if(credits.Count<50)credits.Add(c);continue;}
            if(S(same,"source")=="manual"||S(same,"status")=="confirmed")continue;
            if(S(c,"role")=="制作人员"&&S(same,"role")!="制作人员")continue;
            foreach(var pair in c){if(pair.Key=="id")continue;if(pair.Value!=null&&Convert.ToString(pair.Value)!="")same[pair.Key]=pair.Value;}
        }
        a["credits"]=credits.Take(50).ToList();
    }
}
