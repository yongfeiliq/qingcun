using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

partial class Host {
    static string XpcId(string url){try{string n=Normalize(url);if(SourcePlatform(n)!="新片场视频")return "";return new Uri(n).AbsolutePath.Trim('/').Substring(1);}catch{return "";}}
    static string XpcResource(string raw){
        Uri u;if(!Uri.TryCreate(raw,UriKind.Absolute,out u)||u.Scheme!="https"||u.Port!=443||u.UserInfo!=""||raw.Length>8000)return "";
        return (Domain(u.Host,"xpccdn.com")||Domain(u.Host,"xinpianchang.com"))&&Regex.IsMatch(u.AbsolutePath,@"\.(mp4|m3u8)$",RegexOptions.IgnoreCase)?u.AbsoluteUri:"";
    }
    static string XpcGet(string url){
        Uri u=new Uri(url);if(u.Scheme!="https"||u.UserInfo!=""||u.Port!=443||!new[]{"www.xinpianchang.com","mod-api.xinpianchang.com"}.Contains(u.Host))throw new Exception("新片场资料地址无效。");
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create(u);request.Timeout=20000;request.ReadWriteTimeout=20000;request.AllowAutoRedirect=false;request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
        request.UserAgent="Mozilla/5.0";request.Referer=u.Host=="www.xinpianchang.com"?url:"https://www.xinpianchang.com/";request.Proxy=WebRequest.GetSystemWebProxy();
        try{using(var response=(HttpWebResponse)request.GetResponse()){
            if(response.StatusCode!=HttpStatusCode.OK)throw new Exception("新片场没有返回作品资料。");
            using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)){var b=new char[8192];var text=new StringBuilder();int n;while((n=reader.Read(b,0,b.Length))>0){text.Append(b,0,n);if(text.Length>8*1024*1024)throw new Exception("作品资料过大。");}return text.ToString();}
        }}catch(WebException e){int status=e.Response is HttpWebResponse?(int)((HttpWebResponse)e.Response).StatusCode:0;if(e.Response!=null)e.Response.Close();if(status==401||status==403)throw new Exception("新片场暂不接受直接访问，正在尝试通过浏览器读取。");if(status==429)throw new Exception("新片场请求过于频繁，请稍后重试。");throw new Exception(status>0?"新片场请求失败（"+status+"），请稍后重试。":"无法连接新片场或请求超时，请检查网络。");}
    }
    static string[] XpcNames(object raw){
        if(raw is string)return SourceLabels(((string)raw).Split(','));
        var values=raw as IEnumerable;if(values==null)return new string[0];var names=new List<string>();
        foreach(var value in values){var d=value as Dictionary<string,object>;if(d!=null){foreach(string k in new[]{"name","category_name","categoryName","tag_name","tagName","label"})if(S(d,k)!=""){names.Add(S(d,k));break;}foreach(string k in new[]{"children","child","subcategories"})if(d.ContainsKey(k))names.AddRange(XpcNames(d[k]));}else if(value is string)names.Add((string)value);}
        return SourceLabels(names);
    }
    static string XpcProfile(string raw){Uri u;if(!Uri.TryCreate(raw,UriKind.Absolute,out u)||u.Scheme!="https"||u.UserInfo!=""||u.Port!=443||!new[]{"www.xinpianchang.com","xinpianchang.com"}.Contains(u.Host)||!Regex.IsMatch(u.AbsolutePath,@"^/u[1-9]\d{0,19}/?$"))return "";return "https://www.xinpianchang.com"+u.AbsolutePath.TrimEnd('/');}
    static Dictionary<string,object> XpcContext(Dictionary<string,object> source,string url){
        var x=D(source,"xpc");string vid=S(x,"vid"),appKey=S(x,"appKey");
        if((vid!=""&&!Regex.IsMatch(vid,@"^[a-zA-Z0-9_-]{1,100}$"))||(appKey!=""&&!Regex.IsMatch(appKey,@"^[a-zA-Z0-9_-]{1,160}$")))throw new Exception("新片场播放器资料格式无效。");
        return Obj("xpc",Obj("vid",vid,"appKey",appKey),"pageUrl",Normalize(url),"title",Cut(S(source,"title"),300),"tags",SourceLabels(Strings(source,"tags")),"categories",SourceLabels(Strings(source,"categories")),"description",DescriptionText(S(source,"description")),"descriptionExcerpt",B(source,"descriptionExcerpt"),"descriptionTruncated",B(source,"descriptionTruncated"),"name",Cut(S(source,"name"),120),"account",Cut(S(source,"account"),100),"profileUrl",XpcProfile(S(source,"profileUrl")),"mediaUrl",XpcResource(S(source,"mediaUrl")),"capturedAt",DateTime.UtcNow.ToString("o"));
    }
    static Dictionary<string,object> XpcPageContext(string html,string url){
        var m=Regex.Match(html,@"<script\b[^>]*\bid\s*=\s*[""']__NEXT_DATA__[""'][^>]*>([\s\S]*?)</script>",RegexOptions.IgnoreCase);
        if(!m.Success)throw new Exception("新片场作品页未返回播放器资料，请在浏览器播放后点击轻存的“下载当前视频”。");
        var next=BigJson().Deserialize<Dictionary<string,object>>(m.Groups[1].Value);var detail=D(D(D(next,"props"),"pageProps"),"detail");var video=D(detail,"video");
        string declared=S(detail,"id");if(declared!=""&&declared!=XpcId(url)&&declared!="a"+XpcId(url))throw new Exception("新片场作品编号不匹配。");
        var context=Obj("xpc",Obj("vid",S(video,"vid"),"appKey",S(video,"appKey")),"pageUrl",url,"title",S(detail,"title"),"description",S(detail,"description"));
        context["tags"]=XpcNames(V(detail,"tags",new string[0]));context["categories"]=XpcNames(V(detail,"categories",new string[0]));
        return XpcContext(context,url);
    }
    static Dictionary<string,object> SaveXpcPage(Dictionary<string,object> q){
        string url=Normalize(S(q,"pageUrl"));if(XpcId(url)=="")throw new Exception("请选择新片场单条作品页。");
        var context=XpcContext(q,url);string dir=Path.Combine(Data,"source-cache");Directory.CreateDirectory(dir);Write(Path.Combine(dir,VideoId(url)+".json"),context);
        return Obj("ok",true,"message","已读取新片场作品资料。");
    }
    static Dictionary<string,object> GetXpcContext(string url,Dictionary<string,object> supplied){
        if(XpcPlayable(supplied))return XpcContext(supplied,url);
        string p=Path.Combine(Data,"source-cache",VideoId(url)+".json");
        if(File.Exists(p))try{var c=Read(p);DateTime stamp;if(XpcPlayable(c)&&S(c,"pageUrl")==Normalize(url)&&DateTime.TryParse(S(c,"capturedAt"),out stamp)&&(DateTime.UtcNow-stamp.ToUniversalTime()).TotalHours<2)return XpcContext(c,url);}catch{}
        return XpcPageContext(XpcGet(Normalize(url)),url);
    }
    static Dictionary<string,object> XpcMetadata(Dictionary<string,object> data,Dictionary<string,object> context,string url){
        var owner=D(data,"owner");string uid=S(owner,"id"),profile=Regex.IsMatch(uid,@"^[1-9]\d{0,19}$")?"https://www.xinpianchang.com/u"+uid:"";
        var meta=Obj("id","a"+XpcId(url),"title",S(data,"title")!=""?S(data,"title"):S(context,"title"),"description",S(data,"description"),"webpage_url",Normalize(url),"uploader",S(owner,"username"),"channel",S(owner,"username"),"uploader_id",uid,"uploader_url",profile,"duration",N(data,"duration"),"descriptionOrigin","xinpianchang","descriptionUrl",Normalize(url),"descriptionExcerpt",false);
        if(S(meta,"description")==""||(!B(context,"descriptionExcerpt")&&S(context,"description").Length>S(meta,"description").Length)){meta["description"]=S(context,"description");meta["descriptionExcerpt"]=B(context,"descriptionExcerpt");}
        if(S(meta,"uploader")==""){meta["uploader"]=S(context,"name");meta["channel"]=S(context,"name");meta["uploader_id"]=S(context,"account");meta["uploader_url"]=S(context,"profileUrl");}
        meta["tags"]=SourceLabels(XpcNames(V(data,"keywords",new string[0])).Concat(Strings(context,"tags")));
        meta["categories"]=SourceLabels(XpcNames(V(data,"categories",new string[0])).Concat(Strings(context,"categories")));
        meta["xpcMetadata"]=Obj("schema",1,"tags",data.ContainsKey("keywords")?"read":"page","tagCount",Strings(meta,"tags").Length,"checkedAt",DateTime.UtcNow.ToString("o"),"message","已保存新片场原始标签、分类及可读取的简介与发布账号。");
        return meta;
    }
    static Dictionary<string,object> FetchXpcData(Dictionary<string,object> context){
        var x=D(context,"xpc");if(S(x,"vid")==""||S(x,"appKey")=="")return Obj();
        var response=BigJson().Deserialize<Dictionary<string,object>>(XpcGet("https://mod-api.xinpianchang.com/mod/api/v2/media/"+Uri.EscapeDataString(S(x,"vid"))+"?appKey="+Uri.EscapeDataString(S(x,"appKey"))));
        var data=D(response,"data");if(data.Count==0)throw new Exception("新片场没有返回可播放的视频资料，请重新在网页播放并读取。");return data;
    }
    static Dictionary<string,object> XpcDownloadInfo(string url,Dictionary<string,object> supplied){
        var context=GetXpcContext(url,supplied);Dictionary<string,object> data;
        try{data=FetchXpcData(context);}catch{if(XpcResource(S(context,"mediaUrl"))=="")throw;data=Obj();}
        return XpcInfoFromData(data,context,url);
    }
    static Dictionary<string,object> XpcInfoFromData(Dictionary<string,object> data,Dictionary<string,object> context,string url){
        var meta=XpcMetadata(data,context,url);var formats=new List<Dictionary<string,object>>();var resources=D(data,"resource");
        foreach(var p in Items(resources,"progressive")){string media=XpcResource(S(p,"url"));if(media=="")continue;formats.Add(Obj("format_id","xpc-"+formats.Count,"url",media,"ext","mp4","width",N(p,"width"),"height",N(p,"height"),"http_headers",Obj("Referer",Normalize(url),"Range","bytes=0-")));}
        if(formats.Count==0){string media=XpcResource(S(D(resources,"hls"),"url"));if(media=="")media=XpcResource(S(context,"mediaUrl"));if(media!="")formats.Add(Obj("format_id","xpc-page","url",media,"ext","mp4","protocol",new Uri(media).AbsolutePath.EndsWith(".m3u8",StringComparison.OrdinalIgnoreCase)?"m3u8_native":"https","http_headers",Obj("Referer",Normalize(url),"Range","bytes=0-")));}
        if(formats.Count==0)throw new Exception("新片场没有提供当前可用的视频地址，请在网页播放后点击轻存的“下载当前视频”。");
        meta["formats"]=formats;meta["extractor"]="Xinpianchang";meta["extractor_key"]="Xinpianchang";return meta;
    }
    static Dictionary<string,object> FetchXpcMetadata(string url){var c=GetXpcContext(url,Obj());return XpcMetadata(FetchXpcData(c),c,url);}
}
