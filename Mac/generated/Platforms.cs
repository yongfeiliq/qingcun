using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
partial class Host {
    static string QueryValue(Uri u,string key){foreach(string part in u.Query.TrimStart('?').Split('&')){var pair=part.Split(new[]{'='},2);if(pair.Length==2&&pair[0]==key)return Uri.UnescapeDataString(pair[1]);}return "";}
    static string PageMedia(string raw,string page){
        Uri u;if(!Uri.TryCreate(raw,UriKind.Absolute,out u)||u.Scheme!="https"||u.Port!=443||u.UserInfo!=""||raw.Length>8000)throw new Exception("页面视频地址无效，请重新读取正在播放的视频。");
        string h=u.DnsSafeHost.ToLowerInvariant(),source=SourcePlatform(page);bool resource=Regex.IsMatch(u.AbsolutePath,@"/(?:video|aweme)/|\.(mp4|m3u8)$",RegexOptions.IgnoreCase);
        if(source=="新片场视频"){string media=XpcResource(raw);if(media!="")return media;throw new Exception("新片场视频地址不属于当前支持的播放域名。");}
        bool allowed=source=="抖音视频"?new[]{"douyinvod.com","douyin.com","douyinpic.com","bytecdn.cn"}.Any(d=>Domain(h,d)):source=="TikTok视频"?new[]{"tiktok.com","tiktokcdn.com","tiktokcdn-us.com","byteoversea.com"}.Any(d=>Domain(h,d)):false;
        if(!resource||!allowed)throw new Exception("这不是当前平台支持的视频地址，请打开视频详情后重新读取。");return u.AbsoluteUri;
    }
    static string EngineUrl(string url,string session=""){
        if(SourcePlatform(url)=="Vimeo视频"&&session!=""){ReadVimeoSession(session);return Normalize(url);}
        var u=new Uri(url);Match m;if(u.Host=="vimeo.com"&&(m=Regex.Match(u.AbsolutePath,@"^/(\d+)(?:/([a-f0-9]+))?/?$")).Success)return "https://player.vimeo.com/video/"+m.Groups[1].Value+(m.Groups[2].Success?"?h="+m.Groups[2].Value:"");return url;
    }
    static bool Domain(string host,string name){return host==name||host.EndsWith("."+name,StringComparison.OrdinalIgnoreCase);}
    static string Normalize(string raw){
        Uri u;if(!Uri.TryCreate((raw??"").Trim(),UriKind.Absolute,out u)||u.Scheme!="https"||u.Port!=443||u.UserInfo!=""||raw.Length>4000)throw new Exception("请使用完整的 HTTPS 视频或作品链接。");
        string h=u.DnsSafeHost.ToLowerInvariant(),p=u.AbsolutePath;Match m;
        if(h=="youtu.be"||new[]{"youtube.com","www.youtube.com","m.youtube.com","music.youtube.com","www.youtube-nocookie.com"}.Contains(h)){
            string id="";if(h=="youtu.be")id=p.Trim('/');
            else if(p=="/watch"){foreach(string part in u.Query.TrimStart('?').Split('&')){var v=part.Split(new[]{'='},2);if(v.Length==2&&v[0]=="v")id=Uri.UnescapeDataString(v[1]);}}
            else{m=Regex.Match(p,"^/(shorts|live|embed)/([A-Za-z0-9_-]{11})/?$");if(m.Success)id=m.Groups[2].Value;}
            if(Regex.IsMatch(id,"^[A-Za-z0-9_-]{11}$"))return "https://www.youtube.com/watch?v="+id;
        }
        if(Domain(h,"instagram.com")&&(m=Regex.Match(p,"^/(?:reel|reels|p|tv)/([A-Za-z0-9_-]+)/?$")).Success)return "https://www.instagram.com/p/"+m.Groups[1].Value+"/";
        if(Regex.IsMatch(h,@"^(?:[a-z]{2,3}\.)?pinterest\.(?:com|ca|co\.uk|com\.au|fr|de|jp|es|it|ch|co\.kr|com\.mx|tw)$")&&(m=Regex.Match(p,@"^/pin/(?:[\w-]+--)?(\d+)/?$")).Success)return "https://www.pinterest.com/pin/"+m.Groups[1].Value+"/";
        if(new[]{"www.xinpianchang.com","xinpianchang.com"}.Contains(h)&&(m=Regex.Match(p,@"^/a([1-9]\d{3,15})/?$")).Success)return "https://www.xinpianchang.com/a"+m.Groups[1].Value;
        if(Domain(h,"behance.net")&&(m=Regex.Match(p,@"^/gallery/(\d+)(?:/[^/]+)?/?$")).Success)return "https://www.behance.net/gallery/"+m.Groups[1].Value;
        if(new[]{"bilibili.com","www.bilibili.com","m.bilibili.com"}.Contains(h)&&(m=Regex.Match(p,@"^/video/((?:[bB][vV])[A-Za-z0-9]{10}|(?:[aA][vV])[1-9]\d*)/?$")).Success){
            string part=QueryValue(u,"p");if(part=="")part="1";if(!Regex.IsMatch(part,@"^[1-9]\d{0,3}$"))throw new Exception("B站分P参数无效。");
            string code=m.Groups[1].Value;code=code.Substring(0,2).ToLowerInvariant()=="bv"?"BV"+code.Substring(2):"av"+code.Substring(2);
            return "https://www.bilibili.com/video/"+code+"/"+(part!="1"?"?p="+part:"");
        }
        if(new[]{"douyin.com","www.douyin.com","m.douyin.com","www.iesdouyin.com","iesdouyin.com"}.Contains(h)){
            m=Regex.Match(p,@"^/(?:video|share/video)/([1-9]\d{5,23})/?$");string code=m.Success?m.Groups[1].Value:QueryValue(u,"modal_id");
            if(Regex.IsMatch(code,@"^[1-9]\d{5,23}$"))return "https://www.douyin.com/video/"+code;
        }
        if(new[]{"tiktok.com","www.tiktok.com","m.tiktok.com"}.Contains(h)){
            m=Regex.Match(p,@"^/@([A-Za-z0-9_.-]{1,100})/video/([1-9]\d{5,23})/?$");if(m.Success)return "https://www.tiktok.com/@"+m.Groups[1].Value+"/video/"+m.Groups[2].Value;
            m=Regex.Match(p,@"^/(?:share/video|embed(?:/v2)?)/([1-9]\d{5,23})/?$");if(m.Success)return "https://www.tiktok.com/share/video/"+m.Groups[1].Value;
        }
        if((h=="vimeo.com"||h=="www.vimeo.com")&&(m=Regex.Match(p,@"^/(?:channels/[\w-]+/|groups/[\w-]+/videos/|(?:album|showcase)/\d+/video/)(\d+)/?$")).Success)return "https://vimeo.com/"+m.Groups[1].Value;
        if((h=="vimeo.com"||h=="www.vimeo.com"||h=="player.vimeo.com")&&(m=Regex.Match(p,@"^/(?:video/)?(\d+)(?:/([a-f0-9]+))?/?$")).Success){
            string hash=m.Groups[2].Value;foreach(string part in u.Query.TrimStart('?').Split('&')){var v=part.Split(new[]{'='},2);if(v.Length==2&&v[0]=="h"&&hash==""&&Regex.IsMatch(v[1],"^[a-f0-9]+$"))hash=v[1];}
            return "https://vimeo.com/"+m.Groups[1].Value+(hash!=""?"/"+hash:"");
        }
        if((Domain(h,"behance.net")||Domain(h,"pinimg.com")||Domain(h,"cdninstagram.com")||Domain(h,"fbcdn.net"))&&Regex.IsMatch(p,@"\.(mp4|m3u8)$",RegexOptions.IgnoreCase))return u.GetLeftPart(UriPartial.Path)+u.Query;
        throw new Exception("支持 YouTube、Instagram、Pinterest、Behance、Vimeo、Bilibili（B站）、抖音、TikTok、新片场的单条视频。首页、个人页和短链接请先打开到视频详情。");
    }
    static bool SameVideo(string a,string b){try{return VideoId(a)==VideoId(b);}catch{return a==b;}}
    static string VideoId(string url){string n=Normalize(url);return n.StartsWith("https://www.youtube.com/watch?v=")?n.Substring("https://www.youtube.com/watch?v=".Length):"src_"+HashText(SourcePlatform(n)=="哔哩哔哩视频"?n:SourcePlatform(n)=="TikTok视频"?"tiktok:"+new Uri(n).AbsolutePath.TrimEnd('/').Split('/').Last():new Uri(n).GetLeftPart(UriPartial.Path)).Substring(0,24);}
    static Dictionary<string,object> DownloadContext(Dictionary<string,object> q,string url){
        var context=Items(q,"contexts").FirstOrDefault(x=>S(x,"url")==url);if(context==null)return Obj();
        string page=Normalize(S(context,"pageUrl"));string platform=SourcePlatform(page);
        if(platform!="设计作品平台"&&platform!="照片墙视频"&&platform!="灵感收藏平台"&&platform!="抖音视频"&&platform!="TikTok视频"&&!SameVideo(page,url))throw new Exception("页面视频来源不受支持。");
        string media=S(context,"mediaUrl");if(media!=""){if(VideoId(page)!=VideoId(url))throw new Exception("页面视频与下载链接不一致，请重新读取。");media=PageMedia(media,page);}
        string profile="";try{profile=SafeWeb(S(context,"profileUrl"));if(profile!=""&&new Uri(profile).Host!=new Uri(page).Host)profile="";}catch{}
        if(platform=="新片场视频"){var captured=XpcContext(context,page);captured["url"]=url;SaveXpcPage(captured);return captured;}
        var description=CaptureDescriptionContext(context);
        return Obj("tags",SourceLabels(Strings(context,"tags")),"description",S(description,"description"),"descriptionExcerpt",B(description,"descriptionExcerpt"),"descriptionTruncated",B(description,"descriptionTruncated"),"pageUrl",page,"mediaUrl",media,"title",Cut(S(context,"title"),250),"name",Cut(S(context,"name"),120),"account",Cut(S(context,"account"),100),"profileUrl",profile);
    }
    static void PrepareDownloadedSource(string file,Dictionary<string,object> decision,bool multiple,string vimeoSession=""){
        string metaPath=Path.ChangeExtension(file,"info.json");
        var meta=File.Exists(metaPath)?BigJson().Deserialize<Dictionary<string,object>>(File.ReadAllText(metaPath)):Obj();
        string url=S(decision,"url"),requestKey=VideoId(url),id=requestKey;var context=D(decision,"context");
        meta["descriptionUrl"]=SourceWeb(S(meta,"webpage_url"))!=""?SourceWeb(S(meta,"webpage_url")):url;meta["descriptionOrigin"]="platform";
        if(S(meta,"description").Trim()==""&&S(context,"description")!=""){meta["description"]=S(context,"description");meta["descriptionOrigin"]="page";meta["descriptionUrl"]=S(context,"pageUrl");meta["descriptionExcerpt"]=B(context,"descriptionExcerpt");meta["descriptionTruncated"]=B(context,"descriptionTruncated");}
        if(!url.StartsWith("https://www.youtube.com/watch?v=")){
            string item=S(meta,"id");bool direct=Regex.IsMatch(new Uri(url).AbsolutePath,@"\.(mp4|m3u8)$",RegexOptions.IgnoreCase);bool single=new[]{"哔哩哔哩视频","抖音视频","TikTok视频","新片场视频"}.Contains(SourcePlatform(url))&&!multiple;id=direct||single?requestKey:"src_"+HashText(requestKey+"|"+item).Substring(0,24);
            meta["qingcun_id"]=id;meta["requestKey"]=requestKey;
            meta["media_source_url"]=SourceWeb(S(meta,"webpage_url"));
            meta["webpage_url"]=S(context,"pageUrl")!=""?S(context,"pageUrl"):Normalize(url);
            if(context.Count>0){
                if(S(context,"name")!=""||S(context,"account")!=""||S(context,"profileUrl")!=""){
                if(SourcePlatform(url)=="新片场视频"){
                    if(S(context,"name")!=""){meta["uploader"]=S(context,"name");meta["channel"]=S(context,"name");}
                    if(S(context,"account")!="")meta["uploader_id"]=S(context,"account");
                    if(S(context,"profileUrl")!="")meta["uploader_url"]=S(context,"profileUrl");
                }else{
                foreach(string k in new[]{"uploader","uploader_id","uploader_url","channel","channel_id","channel_url"})meta[k]="";
                meta["channel"]=S(context,"name");meta["uploader"]=new[]{"抖音视频","TikTok视频"}.Contains(SourcePlatform(url))?S(context,"account"):S(context,"name");meta["uploader_id"]=S(context,"account");meta["uploader_url"]=S(context,"profileUrl");
                }}
                if(S(context,"title")!=""&&(S(meta,"title")==""||S(context,"mediaUrl")!=""||!SameVideo(S(context,"pageUrl"),url)))meta["title"]=S(context,"title")+(multiple?" · "+item:"");
            }
        }
        if(S(decision,"mode")=="replace"&&S(decision,"videoId")!=id)throw new Exception("当前视频与选中的旧文件不一致，未替换。新文件保留在暂存目录。");
        if(Strings(context,"categories").Length>0)meta["categories"]=SourceLabels(Strings(meta,"categories").Concat(Strings(context,"categories")));
        if(SourcePlatform(url)=="新片场视频"&&S(meta,"uploader_url")=="")meta["uploader_url"]=ProfileFor(S(meta,"uploader_id"),url);
        if(Strings(context,"tags").Length>0)meta["tags"]=SourceLabels(Strings(meta,"tags").Concat(Strings(context,"tags")));
        if(context.Count>0){var savedContext=new Dictionary<string,object>(context);savedContext.Remove("mediaUrl");meta["sourcePages"]=new[]{savedContext};}
        if(SourcePlatform(url)=="Vimeo视频"){
            var extra=FetchVimeoMetadata(url,vimeoSession);foreach(var pair in extra){if(pair.Key=="id"||pair.Key=="title"||pair.Key=="webpage_url")continue;if(pair.Key=="tags"){meta["tags"]=SourceLabels(Strings(meta,"tags").Concat(Strings(extra,"tags")));continue;}if(pair.Value!=null&&(Convert.ToString(pair.Value)!=""||pair.Value is bool))meta[pair.Key]=pair.Value;}
            var status=D(extra,"vimeoMetadata");if(S(status,"description")!="read"||S(status,"credits")!="read")decision["metadataWarning"]=S(status,"message");
        }
        decision["videoId"]=id;
        var clean=Obj();foreach(string key in new[]{"id","qingcun_id","requestKey","xpcMetadata","vimeoCredits","vimeoMetadata","sourcePages","media_source_url","title","description","descriptionOrigin","descriptionUrl","descriptionExcerpt","descriptionTruncated","webpage_url","uploader","uploader_id","uploader_url","channel","channel_id","channel_url","duration","width","height","fps","upload_date","tags","categories","brand","client"})if(meta.ContainsKey(key))clean[key]=meta[key];clean["description"]=DescriptionText(S(meta,"description"));clean["descriptionTruncated"]=B(meta,"descriptionTruncated")||S(meta,"description").Length>DescriptionLimit;Write(metaPath,clean);
    }
    static string DownloadError(string url,string error){
        string hint="";string platform=SourcePlatform(url);
        if(platform=="设计作品平台")hint="请在浏览器打开 Behance 作品、播放一次视频，再点“读取此页视频”选择播放器。";
        else if(platform=="抖音视频"||platform=="TikTok视频")hint="请打开这条视频并播放，再点击“读取此页视频”尝试保存当前画面；链接过期时重新读取。";
        else if(platform=="哔哩哔哩视频")hint="请确认 BV / AV 视频与分P可播放。部分清晰度、会员或付费视频需要平台授权，当前按可访问的画质保存。";
        else if(platform=="新片场视频")hint="请在能播放这条作品的浏览器打开轻存，点击“下载当前视频”，视频、简介、标签和分类会一起读取。";
        else if(platform=="Vimeo视频")hint="请使用单条视频地址；登录、密码或嵌入权限限制的视频暂无法直接保存。";
        else if(platform=="照片墙视频"||platform=="灵感收藏平台")hint="请打开原页面确认视频可播放，再试“读取此页视频”；平台要求登录或限制访问时，部分视频仍无法保存。";
        if(Regex.IsMatch(error??"","DRM|encrypted|password.protected",RegexOptions.IgnoreCase))return "下载未完成：平台对这个视频启用了加密或密码保护，轻存无法直接保存。";
        if(platform=="Vimeo视频"&&Regex.IsMatch(error??"","cookies|logged.in|login|401|Unauthorized",RegexOptions.IgnoreCase))return "Vimeo 未接受当前浏览器的访问状态。请在这个浏览器打开原视频确认能播放，再点击下载；首次使用如出现浏览器的 Vimeo 权限提示，请确认。";
        if(platform=="Vimeo视频"&&Regex.IsMatch(error??"","403|Forbidden",RegexOptions.IgnoreCase))return "Vimeo 拒绝了这次请求（403）。请在当前浏览器打开原视频确认能播放，然后重试下载。";
        if(Regex.IsMatch(error??"","cookies|logged.in|login|403|401",RegexOptions.IgnoreCase))return "下载未完成。"+hint+" 平台要求登录或限制了本次访问。";
        return "下载未完成。"+hint+" "+Cut(error!=""?error:"页面没有可下载的视频，或网络暂时不可用。",240);
    }
}
