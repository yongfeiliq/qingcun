using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

partial class Host {
    static Dictionary<string,object> D(Dictionary<string,object> d,string key){return d.ContainsKey(key)&&d[key] is Dictionary<string,object>?(Dictionary<string,object>)d[key]:Obj();}
    static object V(Dictionary<string,object> d,string key,object fallback){return d.ContainsKey(key)?d[key]:fallback;}
    static readonly string[] CreditRoles={"制作与后期","导演","联合导演","摄影指导","摄影","制片","制作公司","CG监督","动画","视觉特效","剪辑","调色","声音设计","编舞","出演","创作者","视频署名","发布者","品牌","制作人员","发行","执行制片","编剧","动画导演","美术指导","配乐"};
    static readonly string[] ProductionTypes={"待确认","实拍","三维动画","二维动画","人工智能生成","实拍与三维混合","三维与人工智能混合","混合制作"};
    static string SafeWeb(string value){
        Uri u;if(String.IsNullOrWhiteSpace(value))return "";
        if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out u)||u.Scheme!="https"||u.UserInfo!=""||u.Port!=443)throw new Exception("请输入完整的 HTTPS 主页或来源链接。");
        return u.GetLeftPart(UriPartial.Path);
    }
    static string SourceWeb(string value){try{Uri u=new Uri(value);try{string n=Normalize(value);return n.StartsWith("https://www.youtube.com/watch?v=")||SourcePlatform(n)=="哔哩哔哩视频"?n:new Uri(n).GetLeftPart(UriPartial.Path);}catch{}return SafeWeb(value);}catch{return "";}}
    static string SourcePlatform(string url){Uri u;if(!Uri.TryCreate(url,UriKind.Absolute,out u))return "本地视频";string h=u.Host;return h=="youtu.be"||h=="youtube.com"||h.EndsWith(".youtube.com")?"油管视频":h=="instagram.com"||h.EndsWith(".instagram.com")?"照片墙视频":h=="behance.net"||h.EndsWith(".behance.net")?"设计作品平台":Domain(h,"pinterest.com")?"灵感收藏平台":Domain(h,"vimeo.com")?"Vimeo视频":Domain(h,"bilibili.com")?"哔哩哔哩视频":Domain(h,"douyin.com")||Domain(h,"iesdouyin.com")?"抖音视频":Domain(h,"tiktok.com")?"TikTok视频":Domain(h,"xinpianchang.com")?"新片场视频":"其他来源";}
    static string ProfileFor(string handle,string source){
        if(SourcePlatform(source)=="新片场视频"&&Regex.IsMatch(handle,@"^[1-9]\d{0,19}$"))return "https://www.xinpianchang.com/u"+handle;
        string h=handle.TrimStart('@');
        if(SourcePlatform(source)=="照片墙视频"&&Regex.IsMatch(h,@"^[A-Za-z0-9_.]{1,30}$"))return "https://www.instagram.com/"+h+"/";
        if(SourcePlatform(source)=="油管视频"&&Regex.IsMatch(h,@"^[\p{L}\p{N}_.-]{1,80}$"))return "https://www.youtube.com/@"+Uri.EscapeDataString(h);
        if(SourcePlatform(source)=="哔哩哔哩视频"&&Regex.IsMatch(h,@"^[1-9]\d{0,19}$"))return "https://space.bilibili.com/"+h;
        if(SourcePlatform(source)=="TikTok视频"&&Regex.IsMatch(h,@"^[A-Za-z0-9_.-]{1,100}$"))return "https://www.tiktok.com/@"+h;
        if(SourcePlatform(source)=="抖音视频"&&Regex.IsMatch(h,@"^MS4w[\w-]{12,200}$"))return "https://www.douyin.com/user/"+h;
        return "";
    }
    static string NameKey(string s){return Regex.Replace(new string((s??"").Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant(),@"[^a-z0-9]","");}
    static List<Dictionary<string,object>> ParseCredits(string description,string url){
        var result=new List<Dictionary<string,object>>();
        string[][] roles={
            new[]{"制作与后期",@"Production\s*&\s*Post[- ]production|制作与后期"},
            new[]{"CG监督",@"CG\s*supervisor|CG监督|CG总监"},
            new[]{"摄影指导",@"DOP|DP|Director of Photography|Cinematographer|摄影指导"},
            new[]{"导演",@"Directors?|Directed\s+by|导演"},
            new[]{"联合导演",@"Co[- ]?directors?|联合导演"},
            new[]{"制作公司",@"Produced\s+by|Production\s+company|制作公司"},
            new[]{"摄影",@"Shot\s+by|Filmed\s+by|摄影"},
            new[]{"创作者",@"Created\s+by|创作者"},
            new[]{"剪辑",@"Edited\s+by|Editor|剪辑"},
            new[]{"调色",@"Colorist|Colourist|调色"},
            new[]{"声音设计",@"Sound\s+design|声音设计"},
            new[]{"视频署名",@"📹|🎥"}
        };
        foreach(string line in (description??"").Replace("\r","").Split('\n')){
            if(result.Count>=30)break;
            foreach(var r in roles){
                Match m=Regex.Match(line.Trim(),@"^(?:"+r[1]+@")\s*(?:[:：\-–—]\s*)?(.+)$",RegexOptions.IgnoreCase);
                if(!m.Success)continue;
                foreach(string part in Regex.Split(m.Groups[1].Value,@"\s+[&＆]\s+|[；;]")){
                    string name=part.Trim();Match hm=Regex.Match(name,@"@([A-Za-z0-9_.]{1,80})");
                    string account=hm.Success?"@"+hm.Groups[1].Value.TrimEnd('.'):"",profile=account!=""?ProfileFor(account,url):"";
                    name=Regex.Replace(name,@"https?://\S+|@[A-Za-z0-9_.]+","").Trim(' ','-',':','：','·');
                    if(account==""&&name!=""){
                        var candidates=Regex.Matches(description??"", @"@([A-Za-z0-9_.]{1,30})").Cast<Match>().Select(x=>"@"+x.Groups[1].Value.TrimEnd('.')).Distinct().Where(x=>NameKey(x)==NameKey(name)).ToArray();
                        if(candidates.Length==1){account=candidates[0];profile=ProfileFor(account,url);}
                    }
                    if(name==""&&account=="")continue;
                    result.Add(Obj("id",Guid.NewGuid().ToString("N"),"role",r[0],"name",Cut(name,100),"account",Cut(account,100),"url",profile,"source","metadata","status","source","accountStatus",hm.Success?"来源明确":account!=""?"待确认":"未提供","evidence",Cut(line.Trim(),180),"sourceUrl",url));
                }break;
            }
        }
        return result.GroupBy(x=>S(x,"role")+"|"+S(x,"name")+"|"+S(x,"account")).Select(g=>g.First()).Take(30).ToList();
    }
    static Dictionary<string,object> Publisher(Dictionary<string,object> meta,string url){string account=S(meta,"uploader_id"),p=S(meta,"uploader_url");string stable=S(meta,"channel_id");if(SourcePlatform(url)=="照片墙视频"){stable=S(meta,"uploader_id");account=S(meta,"channel");if(account==""){account=S(meta,"uploader_id");if(Regex.IsMatch(account,@"^\d+$"))account="";}}if(SourcePlatform(url)=="TikTok视频"||SourcePlatform(url)=="抖音视频"){stable=S(meta,"uploader_id");account=S(meta,"uploader");if(account=="")account=S(meta,"channel");}
        if(p=="")p=S(meta,"channel_url");if(p==""&&SourcePlatform(url)=="油管视频"&&Regex.IsMatch(S(meta,"channel_id"),@"^UC[A-Za-z0-9_-]{22}$"))p="https://www.youtube.com/channel/"+S(meta,"channel_id");if(p=="")p=ProfileFor(account,url);try{p=SafeWeb(p);}catch{p="";}return Obj("name",Cut(S(meta,"channel")!=""?S(meta,"channel"):S(meta,"uploader"),120),"account",Cut(account,100),"url",p,"platform",PlatformLabel(url),"stableId",Cut(stable,120));}
    static Dictionary<string,object> MergePublisher(Dictionary<string,object> current,Dictionary<string,object> incoming){
        var result=new Dictionary<string,object>(current);
        if(current.Count==0&&incoming.Count>0)return new Dictionary<string,object>(incoming);
        if(B(current,"manual"))return result;
        foreach(string key in new[]{"name","account","url","platform","stableId"})if(S(result,key)==""&&S(incoming,key)!="")result[key]=incoming[key];
        return result;
    }
    static bool SourceMatches(Dictionary<string,object> a,Dictionary<string,object> meta){
        string origin=SourceWeb(S(meta,"webpage_url")),expected=SourceWeb(S(a,"url"));
        if(origin!=""&&expected!="")return origin==expected;
        return S(meta,"id")!=""&&S(meta,"id")==S(a,"id");
    }
    static Dictionary<string,object> ReadSourceFile(string path,Dictionary<string,object> a){
        if(!path.EndsWith(".info.json",StringComparison.OrdinalIgnoreCase)||!File.Exists(path)||new FileInfo(path).Length>32*1024*1024)throw new Exception("请选择此视频对应的来源资料文件。");
        var meta=BigJson().Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));
        if(!SourceMatches(a,meta))throw new Exception("来源资料与当前视频不一致，未修改平台链接。");
        return meta;
    }
    static Dictionary<string,object> ReadAdjacentSource(Dictionary<string,object> a){
        var meta=Obj("id",S(a,"id"),"title",S(a,"title"),"channel",S(a,"channel"),"description",S(a,"description"),"webpage_url",S(a,"url"));
        foreach(var f in Items(a,"files"))try{
            var richer=ReadSourceFile(Path.ChangeExtension(S(f,"path"),"info.json"),a);
            foreach(string key in new[]{"channel","channel_id","channel_url","uploader","uploader_id","uploader_url","description","webpage_url"})if(S(meta,key)==""&&S(richer,key)!="")meta[key]=richer[key];
        foreach(string key in new[]{"vimeoCredits","vimeoMetadata"})if(richer.ContainsKey(key)&&!meta.ContainsKey(key))meta[key]=richer[key];
        }catch{}
        return meta;
    }
    static Dictionary<string,object> FetchSourceMetadata(string url,string session=""){
        if(SourcePlatform(url)=="新片场视频")return FetchXpcMetadata(url);
        if(SourcePlatform(url)=="Vimeo视频")return FetchVimeoMetadata(url,session);
        string normalized=EngineUrl(Normalize(url),session),exe=Path.Combine(Root,"tools",ExecutableName("yt-dlp"));
        var args=new List<string>{"--ignore-config","--no-playlist","--skip-download","--dump-single-json","--ignore-no-formats-error","--no-warnings","--socket-timeout","15","--retries","1"};
        string deno=Path.Combine(Root,"tools",ExecutableName("deno"));if(File.Exists(deno)){args.Add("--js-runtimes");args.Add("deno:"+deno);}
        using(var auth=new VimeoLease(url,session,args)){
        args.Add("--");args.Add(normalized);
        var pi=new System.Diagnostics.ProcessStartInfo(exe,String.Join(" ",args.Select(Quote))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        using(var process=System.Diagnostics.Process.Start(pi)){
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(60000)){try{process.Kill();}catch{}throw new Exception("获取平台资料超时，已有链接保留。可以稍后重试或手动填写。");}
            System.Threading.Tasks.Task.WaitAll(stdout,stderr);
            if(process.ExitCode!=0||stdout.Result.Length>32*1024*1024)throw new Exception("暂时无法获取平台资料，已有链接保留。请检查网络或手动填写。");
            try{return BigJson().Deserialize<Dictionary<string,object>>(stdout.Result);}
            catch{throw new Exception("平台未返回可用资料，已有链接保留。");}
        }
    }
    }
    static Dictionary<string,object> RefreshSource(Dictionary<string,object> q){
        var original=GetAsset(S(q,"id"));Dictionary<string,object> meta;
        if(S(q,"metadataPath")!="")meta=ReadSourceFile(S(q,"metadataPath"),original);
        else {
            meta=ReadAdjacentSource(original);var publisher=MergePublisher(D(original,"publisher"),Publisher(meta,S(original,"url")));
            if(new[]{"Vimeo视频","新片场视频"}.Contains(SourcePlatform(S(original,"url")))||B(q,"refreshDescription")||(S(publisher,"url")==""&&!B(publisher,"manual"))){
                if(SourcePlatform(S(original,"url"))=="设计作品平台")throw new Exception(B(q,"refreshDescription")?"请先在 Behance 原作品页展开介绍，再点击轻存的“摘录此页简介到素材库”。":"请从打开的 Behance 作品页下载以保存作者链接，或在此补充主页。");
                meta=FetchSourceMetadata(S(original,"url"),S(q,"vimeoSession"));
            }
        }
        if(!SourceMatches(original,meta))throw new Exception("返回的来源资料与视频不一致，已有链接保留。");
        return LibraryLock(()=>{
            var a=Read(AssetPath(S(q,"id")));if(S(a,"url")!=S(original,"url"))throw new Exception("来源已变化，请重新打开视频再试。");
            EnrichAsset(a,meta,Obj());SaveAsset(a);
            if(new[]{"Vimeo视频","新片场视频"}.Contains(SourcePlatform(S(a,"url")))){var export=ExportDescriptions(a);return Obj("ok",true,"asset",a,"paths",Strings(export,"paths"),"errors",Strings(export,"errors"),"message",S(D(a,SourcePlatform(S(a,"url"))=="新片场视频"?"xpcMetadata":"vimeoMetadata"),"message"));}
            if(B(q,"refreshDescription")){var export=ExportDescriptions(a);return Obj("ok",true,"asset",a,"paths",Strings(export,"paths"),"errors",Strings(export,"errors"),"message",S(meta,"description").Trim()!=""?(Strings(export,"paths").Length>0?"已更新平台简介，并保存 TXT。":"已更新平台简介。"):"平台没有返回新的简介，已保留原有内容。");}
            return Obj("ok",true,"asset",a,"message",S(D(a,"publisher"),"url")!=""?"平台来源与账号主页已补全。":"资料已检查，但没有提供账号主页；可以手动补充。");
        });
    }
    static Dictionary<string,object> EditSource(Dictionary<string,object> q){
        return LibraryLock(()=>{
            var a=Read(AssetPath(S(q,"id")));string raw=S(q,"url").Trim(),url=SourceWeb(raw);
            if(raw!=""&&url=="")throw new Exception("请输入有效的 HTTPS 原视频地址。");
            if(Regex.IsMatch(S(a,"id"),@"^[A-Za-z0-9_-]{11}$")&&url!=""&&(SourcePlatform(url)!="油管视频"||VideoId(url)!=S(a,"id")))throw new Exception("原视频网址与这个素材的视频标识不一致。");
            string profile=SafeWeb(S(q,"profileUrl").Trim());
            var p=Obj("name",Cut(S(q,"name").Trim(),120),"account",Cut(S(q,"account").Trim(),100),"url",profile,"platform",PlatformLabel(url),"stableId",S(D(a,"publisher"),"stableId"),"manual",true);
            a["url"]=url;a["publisher"]=p;SaveAsset(a);return Obj("ok",true,"asset",a);
        });
    }
    static void EnrichAsset(Dictionary<string,object> a,Dictionary<string,object> meta,Dictionary<string,object> portable){
        string url=SourceWeb(S(meta,"webpage_url"));if(url=="")url=SourceWeb(S(portable,"sourceUrl"));if(url=="")url=S(a,"url");
        a["url"]=url;MergeDescription(a,meta,portable);if(!a.ContainsKey("sourcePages"))a["sourcePages"]=Items(portable,"sourcePages");if(S(a,"requestKey")==""&&S(portable,"requestKey")!="")a["requestKey"]=S(portable,"requestKey");
        if(!a.ContainsKey("credits"))a["credits"]=Items(portable,"credits").Where(c=>CreditRoles.Contains(S(c,"role"))).Take(50).ToList();
        if(!a.ContainsKey("dismissedCredits"))a["dismissedCredits"]=Strings(portable,"dismissedCredits").Take(100).ToArray();
        MergePlatformTags(a,meta,portable);
        a["publisher"]=MergePublisher(MergePublisher(D(a,"publisher"),D(portable,"publisher")),Publisher(meta,url));
        MergeSourceCredits(a,ParseCredits(S(meta,"description"),url).Concat(Items(meta,"vimeoCredits")).ToList());
        LinkNamedPublisherCredits(a);
        if(D(meta,"xpcMetadata").Count>0)a["xpcMetadata"]=D(meta,"xpcMetadata");
        if(D(meta,"vimeoMetadata").Count>0)a["vimeoMetadata"]=D(meta,"vimeoMetadata");
        foreach(string key in new[]{"filmTypeAnalysis","filmTypeAnalysisDraft"})if(!a.ContainsKey(key)&&D(portable,key).Count>0)a[key]=D(portable,key);
        if(!a.ContainsKey("tagAnalysisMode")&&S(portable,"tagAnalysisMode")=="film_type")a["tagAnalysisMode"]="film_type";
        if(!a.ContainsKey("analysis")&&D(portable,"analysis").Count>0)a["analysis"]=D(portable,"analysis");
        if(!a.ContainsKey("analysisDraft")&&D(portable,"analysisDraft").Count>0)a["analysisDraft"]=D(portable,"analysisDraft");
        if(!a.ContainsKey("analysisState"))a["analysisState"]="not_analyzed";
        UpgradeClassification(a,true);
    }
    static void UpgradeMetadata(Dictionary<string,object> a){
        if(UpgradeClassification(a))SaveAsset(a);
        UpgradeBrandTags(a);
        var publisher=D(a,"publisher");if((S(publisher,"name")!=""&&S(publisher,"url")!="")||B(publisher,"manual"))return;
        string before=Json.Serialize(a);var meta=ReadAdjacentSource(a);EnrichAsset(a,meta,Obj());
        if(Json.Serialize(a)!=before)SaveAsset(a);
    }
    static string CreditKey(Dictionary<string,object> c){return S(c,"role")+"|"+S(c,"name")+"|"+S(c,"account");}
    static object PortableAsset(Dictionary<string,object> a){return Obj("schema",9,"classificationSchema",N(a,"classificationSchema"),"productType",S(a,"productType"),"tagAnalysisMode",S(a,"tagAnalysisMode"),"filmTypeAnalysis",D(a,"filmTypeAnalysis"),"filmTypeAnalysisDraft",D(a,"filmTypeAnalysisDraft"),"sourceCategories",Strings(a,"sourceCategories"),"sourceFacts",Items(a,"sourceFacts"),"xpcMetadata",D(a,"xpcMetadata"),"vimeoMetadata",D(a,"vimeoMetadata"),"brand",D(a,"brand"),"originalTags",Strings(a,"originalTags"),"originalHashtags",Strings(a,"originalHashtags"),"description",S(a,"description"),"descriptionOrigin",S(a,"descriptionOrigin"),"descriptionUrl",S(a,"descriptionUrl"),"descriptionExcerpt",B(a,"descriptionExcerpt"),"descriptionTruncated",B(a,"descriptionTruncated"),"subtitleFiles",Items(a,"subtitleFiles"),"sourcePages",Items(a,"sourcePages"),"id",S(a,"id"),"requestKey",S(a,"requestKey"),"title",S(a,"title"),"category",S(a,"category"),"categoryManual",B(a,"categoryManual"),"dismissedTags",Strings(a,"dismissedTags"),"tags",V(a,"tags",new object[0]),"workflow",S(a,"workflow"),"favorite",B(a,"favorite"),"notes",S(a,"notes"),"sourceUrl",S(a,"url"),"publisher",D(a,"publisher"),"credits",Items(a,"credits"),"dismissedCredits",Strings(a,"dismissedCredits"),"analysis",D(a,"analysis"),"analysisDraft",D(a,"analysisDraft"));}
    static Dictionary<string,object> EditCredit(Dictionary<string,object> q){
        return LibraryLock(()=>{
            string p=AssetPath(S(q,"id"));var a=Read(p);var credits=Items(a,"credits");string cid=S(q,"creditId");
            var old=credits.FirstOrDefault(c=>S(c,"id")==cid);if(cid!=""&&old==null)throw new Exception("人员信息已变化，请刷新后重试。");
            if(old!=null){credits.Remove(old);a["dismissedCredits"]=Strings(a,"dismissedCredits").Concat(new[]{CreditKey(old)}).Distinct().Take(100).ToArray();}
            if(!B(q,"remove")){
                string role=S(q,"role"),name=Cut(S(q,"name").Trim(),100),account=Cut(S(q,"account").Trim(),100),url=SafeWeb(S(q,"url"));
                if(!CreditRoles.Contains(role))throw new Exception("请选择人员职务。");
                if(name==""&&account=="")throw new Exception("请填写姓名或原始账号。");
                if(credits.Count>=50)throw new Exception("每条视频最多保存 50 位人员。");
                credits.Add(Obj("id",cid!=""?cid:Guid.NewGuid().ToString("N"),"role",role,"name",name,"account",account,"url",url,"source","manual","status","confirmed","accountStatus",url!=""?"人工确认":"未提供","evidence",Cut(S(q,"evidence"),180),"sourceUrl",S(a,"url")));
            }
            a["credits"]=credits;SaveAsset(a);return Obj("ok",true,"asset",a);
        });
    }
    static string PeopleText(Dictionary<string,object> a){return String.Join(" ",Items(a,"credits").Select(c=>S(c,"role")+" "+S(c,"name")+" "+S(c,"account")))+" "+S(D(a,"publisher"),"name")+" "+S(D(a,"publisher"),"account");}
    static Dictionary<string,object> VisibleAnalysis(Dictionary<string,object> a){if(ImageAsset(a))return D(a,"imageAnalysisDraft").Count>0?D(a,"imageAnalysisDraft"):D(a,"imageAnalysis");return D(a,"analysisDraft").Count>0?D(a,"analysisDraft"):D(a,"analysis");}
    static string VisualText(Dictionary<string,object> a){var v=VisibleAnalysis(a);if(ImageAsset(a))return String.Join(" ",D(v,"labels").Values.Select(Convert.ToString));return S(v,"summary")+" "+S(v,"production")+" "+String.Join(" ",Strings(v,"colors"))+" "+String.Join(" ",Items(v,"shots").Select(s=>S(s,"description")+" "+S(s,"scale")+" "+S(s,"angle")+" "+S(s,"movement")+" "+S(s,"color")));}
}
