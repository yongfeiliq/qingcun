using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class Host {
    static readonly string LibraryDir=Path.Combine(Data,"library");
    static readonly string CoverDir=Path.Combine(Data,"covers");
    static readonly string[] Categories={"香水香氛","美妆护肤","鞋服穿搭","汽车","手机","手表","数码科技","食品饮料","珠宝腕表","家居生活","建筑空间","自然旅行","影视动画","教程知识","待分类"};
    static readonly string[] Dimensions={"行业产品","品牌","产品","系列","视频类型","平台分类","制作方式","软件","平台关键词","来源","视觉形式","主体场景","材质","色彩灯光","构图景别","运镜剪辑","动画特效","声音","参考用途","自定义"};
    static readonly string[] Workflows={"未整理","待拆解","待复刻","已学习","已复刻"};
    static T LibraryLock<T>(Func<T> work) {
        using(var m=new Mutex(false,"QingcunLibrary_"+UserKey())){
            bool held=false;try{try{held=m.WaitOne(45000);}catch(AbandonedMutexException){held=true;}
                if(!held)throw new Exception("素材库正在处理，请稍后重试。");
                Directory.CreateDirectory(LibraryDir);Directory.CreateDirectory(CoverDir);return work();
            }finally{if(held)m.ReleaseMutex();}
        }
    }
    static Dictionary<string,object> Settings(){string p=Path.Combine(Data,"settings.json");return File.Exists(p)?Read(p):Obj();}
    static bool B(Dictionary<string,object>d,string k,bool fallback=false){return d.ContainsKey(k)&&d[k] is bool?(bool)d[k]:fallback;}
    static double N(Dictionary<string,object>d,string k){double v;return Double.TryParse(S(d,k),NumberStyles.Any,CultureInfo.InvariantCulture,out v)?v:0;}
    static List<Dictionary<string,object>> Items(Dictionary<string,object>d,string k){return d.ContainsKey(k)&&d[k] is IEnumerable&&!(d[k] is string)?((IEnumerable)d[k]).Cast<object>().OfType<Dictionary<string,object>>().ToList():new List<Dictionary<string,object>>();}
    static string[] Strings(Dictionary<string,object>d,string k){return d.ContainsKey(k)&&d[k] is IEnumerable&&!(d[k] is string)?((IEnumerable)d[k]).Cast<object>().Select(Convert.ToString).ToArray():new string[0];}
    static string Cut(string s,int n){return (s??"").Length>n?s.Substring(0,n):s??"";}
    static string AssetPath(string id){if(!Regex.IsMatch(id??"",@"^(?:[A-Za-z0-9_-]{11}|(?:local|src)_[a-f0-9]{24})$"))throw new Exception("无效的视频标识。");return Path.Combine(LibraryDir,id+".json");}
    static string Canonical(string value){string s=Regex.Replace((value??"").Normalize(NormalizationForm.FormKC).Trim().TrimStart('#'),@"\s+"," ");switch(s.ToLowerInvariant()){case "cg":case "3d":case "3dcg":case "三维":case "三维动画":case "三维cg":return "三维动画";case "tomford":case "tom ford":return "汤姆福特";case "慢镜头":case "慢动作":return "慢动作";case "特写镜头":return "特写";case "耐克":case "nike":return "耐克";case "adidas":case "阿迪达斯":return "阿迪达斯";case "chanel":return "香奈儿";case "dior":return "迪奥";case "apple":return "苹果";case "samsung":return "三星";case "sony":return "索尼";case "mercedes-benz":return "奔驰";case "bmw":return "宝马";case "porsche":return "保时捷";case "rolex":return "劳力士";case "perfume":case "fragrance":return "香水";case "beauty":case "tomfordbeauty":return "美妆";case "skincare":return "护肤";case "fashion":return "时尚";default:return Regex.IsMatch(s,@"\p{IsCJKUnifiedIdeographs}")?Cut(s,40):"";}}
    static string TagKey(Dictionary<string,object> t){return S(t,"dimension")+"|"+TagName(S(t,"dimension"),S(t,"name")).ToLowerInvariant();}
    static Dictionary<string,object> Tag(string dim,string name,string source,string evidence){return Obj("dimension",dim,"name",TagName(dim,name),"source",source,"status",source=="manual"?"confirmed":source=="metadata"?"source":"suggested","evidence",Cut(evidence,220));}
    static bool Matches(string text,string expression){return Regex.IsMatch(text??"",expression,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
    static string CategoryName(string value){return value=="汽车交通"?"汽车":value=="未分类"?"待分类":value;}
    static string ProductCategory(Dictionary<string,object> meta){
        Func<string,string> detect=text=>{
            text=Regex.Replace(text??"", @"(?im)^.*\b(?:shot on|filmed with|captured with)\b[^\r\n]*$", "");
            text=Regex.Replace(text,@"https?://\S+"," ");
            bool phone=Matches(text,@"\b(?:smartphones?|i[\s-]?phone(?:\s*\d+)?|(?:mobile|cell(?:ular)?)\s+phones?|galaxy\s+(?:[sa]\s*\d+|z\s*(?:fold|flip)\s*\d+)|(?:google\s+)?pixel\s+\d+)\b|智能手机|手机");
            bool watch=Matches(text,@"\b(?:wristwatch(?:es)?|smartwatch(?:es)?|timepieces?|apple watch|galaxy watch|pixel watch|time[- ]only watch|pilot.s watch|(?:luxury|mechanical|automatic|sports?|diving) watch|watch (?:case|dial|strap|movement|is completed|with a))\b|腕表|手表");
            if(phone==watch)return "";return phone?"手机":"手表";
        };
        string result=detect(S(meta,"title"));if(result!="")return result;
        result=detect(String.Join("\n",Items(meta,"sourceFacts").Where(f=>S(f,"label")=="产品").Select(f=>S(f,"value"))));if(result!="")return result;
        result=detect(String.Join("\n",Strings(meta,"tags").Concat(Strings(meta,"originalTags")).Concat(Strings(meta,"originalHashtags"))));if(result!="")return result;
        return detect(Cut(S(meta,"description"),20000));
    }
    static bool UpgradeClassification(Dictionary<string,object> a,bool force=false){
        if(!force&&N(a,"classificationSchema")>=1&&S(a,"category")!="汽车交通"&&S(a,"category")!="未分类")return false;
        string before=Json.Serialize(a);a["category"]=CategoryName(S(a,"category"));
        var meta=new Dictionary<string,object>(a);meta["tags"]=Strings(a,"originalTags");a["productType"]=ProductCategory(meta);
        if(!B(a,"categoryManual")&&new[]{"","待分类","数码科技","珠宝腕表"}.Contains(S(a,"category"))){
            string product=ProductCategory(meta);if(product!="")a["category"]=product;
        }
        var brand=D(a,"brand");
        if(!B(brand,"manual")&&BrandName(S(brand,"name"))=="")a["brand"]=Obj();
        var tags=Items(a,"tags");tags.RemoveAll(t=>S(t,"dimension")=="品牌"&&BrandName(S(t,"name"))==""&&S(t,"source")!="manual"&&S(t,"status")!="confirmed");a["tags"]=tags;
        ApplyBrand(a,meta,Obj());a["classificationSchema"]=1;
        return Json.Serialize(a)!=before;
    }
    static Dictionary<string,object> UpgradeCategories(Dictionary<string,object> q){return LibraryLock(()=>{
        var changed=new List<Dictionary<string,object>>();
        foreach(string path in Directory.GetFiles(LibraryDir,"*.json")){
            var a=Read(path);if(S(q,"id")!=""&&S(q,"id")!=S(a,"id"))continue;
            if(UpgradeClassification(a)){SaveAsset(a);changed.Add(Obj("id",S(a,"id"),"category",S(a,"category"),"brand",S(D(a,"brand"),"name")));}
        }
        return Obj("ok",true,"changed",changed,"schema",1);
    });}
    static List<Dictionary<string,object>> BaseTags(Dictionary<string,object> meta,out string category){
        string title=S(meta,"title"),description=Cut(S(meta,"description"),20000),channel=S(meta,"channel");var tags=new List<Dictionary<string,object>>();
        string[][] rules={
            new[]{"香水香氛","香水",@"\b(perfume|fragrance|parfum|eau de parfum|eau de toilette|cologne|oud voyager|grey vetiver)\b|香水|香氛|香薰"},
            new[]{"美妆护肤","美妆护肤",@"\b(skincare|serum|moisturizer|lipstick|mascara|foundation makeup|cosmetic|sunscreen)\b|护肤|精华|面霜|口红|彩妆|防晒"},
            new[]{"鞋服穿搭","鞋服穿搭",@"\b(sneaker|sneakers|footwear|running shoe|fashion|garment|apparel|outfit)\b|运动鞋|球鞋|服装|穿搭|时装"},
            new[]{"汽车","汽车",@"\b(automotive|supercar|electric vehicle|sports car|car commercial|sedan|SUV)\b|汽车|跑车|摩托车"},
            new[]{"数码科技","数码科技",@"\b(smartphone|iphone|macbook|headphone|laptop|smartwatch|earbuds|electronics)\b|数码|手机|耳机|笔记本电脑"},
            new[]{"食品饮料","食品饮料",@"\b(food|beverage|drink|coffee|chocolate|soda|recipe|cooking)\b|食品|饮料|咖啡|巧克力|烹饪"},
            new[]{"珠宝腕表","珠宝腕表",@"\b(jewelry|jewellery|diamond|luxury watch|wristwatch|rolex)\b|珠宝|钻石|腕表"},
            new[]{"家居生活","家居生活",@"\b(furniture|sofa|home appliance|home decor)\b|家具|家电|家居"},
            new[]{"建筑空间","建筑空间",@"\b(architecture|interior design|architectural)\b|建筑设计|室内设计|建筑可视化"},
            new[]{"自然旅行","自然旅行",@"\b(travel|landscape|wildlife|nature documentary)\b|旅行|自然风光|野生动物"},
            new[]{"影视动画","影视动画",@"\b(animated short|animation short|short film|movie trailer)\b|动画短片|电影预告|影视短片"},
            new[]{"教程知识","教程",@"\b(tutorial|how to|course|lesson)\b|教程|教学|课程"}
        };
        category="待分类";int best=0;string industry="";
        foreach(var r in rules){int score=(Matches(title,r[2])?6:0)+(Matches(description,r[2])?2:0)+(Matches(channel,r[2])?2:0);if(score>best){best=score;category=r[0];industry=r[1];}}
        // Explicit fragrance wording beats ingredient words such as chocolate or coffee.
        if(Matches(title+" "+description,rules[0][2])){category=rules[0][0];industry=rules[0][1];}
        string productCategory=ProductCategory(meta);if(productCategory!=""&&(best<6||category=="数码科技"||category=="珠宝腕表"||ProductCategory(Obj("title",title))!="")){category=productCategory;industry=productCategory;}
        if(industry!="")tags.Add(Tag("行业产品",industry,"rule","依据标题/简介的产品关键词，分类为建议，可修改。"));
        var brandResult=DetectBrand(meta,category);string primaryBrand=S(brandResult,"name");
        foreach(string brand in Strings(brandResult,"candidates"))tags.Add(Tag("品牌",brand,"rule",S(brandResult,"evidence")));
        if(primaryBrand=="TOM FORD"){
            foreach(string series in new[]{"PRIVATE BLEND","SIGNATURE"})if(Matches(title,@"\b"+series+@"\b"))tags.Add(Tag("系列",series,"metadata","标题中的系列名"));
            string product=title.Split('|')[0].Trim();if(title.Contains("|")&&product.Length<=80&&product!=primaryBrand)tags.Add(Tag("产品",product,"metadata","标题中的产品名"));
        }
        if(Matches(title,@"\b(tutorial|how to|course|lesson)\b|教程|教学"))tags.Add(Tag("视频类型","教程","rule","标题关键词"));
        else if(Matches(title,@"\b(unboxing|unbox)\b|开箱"))tags.Add(Tag("视频类型","开箱","rule","标题关键词"));
        else if(Matches(title,@"\b(behind the scenes|making of|breakdown)\b|制作花絮|幕后|制作解析"))tags.Add(Tag("视频类型","制作花絮","rule","标题关键词"));
        else if(Matches(title,@"\b(commercial|advertisement|campaign|product film)\b|广告|品牌宣传")||(primaryBrand!=""&&category!="待分类"&&title.Contains("|")))tags.Add(Tag("视频类型","产品广告","rule","根据标题与品牌信息推断，待确认。"));
        foreach(string raw in SourceLabels(Strings(meta,"tags"))){string name=TagName("平台关键词",raw);if(name!="")tags.Add(Tag("平台关键词",name,"metadata","平台原始关键词"));}
        foreach(Match m in Regex.Matches(description,@"(?<!\w)#([\p{L}\p{N}_]{2,40})").Cast<Match>().Take(12))tags.Add(Tag("平台关键词",m.Groups[1].Value,"metadata","简介中的话题词"));
        tags.Add(Tag("来源",SourcePlatform(SourceWeb(S(meta,"webpage_url"))!=""?SourceWeb(S(meta,"webpage_url")):Regex.IsMatch(S(meta,"id"),@"^[A-Za-z0-9_-]{11}$")?"https://www.youtube.com/watch?v="+S(meta,"id"):""),"metadata","依据来源视频标识"));return tags.Where(t=>S(t,"name")!="").GroupBy(TagKey).Select(g=>g.First()).Take(1100).ToList();
    }
    static string HashText(string s){using(var sha=SHA256.Create()){return String.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(b=>b.ToString("x2")));}}
    static string FileHash(string p){using(var sha=SHA256.Create())using(var f=File.OpenRead(p)){return String.Concat(sha.ComputeHash(f).Select(b=>b.ToString("x2")));}}
    static string RunTool(string tool,IEnumerable<string> args,int timeout,bool requireSuccess=true){
        var pi=new ProcessStartInfo(tool,String.Join(" ",args.Select(Quote))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        using(var p=Process.Start(pi)){var a=p.StandardOutput.ReadToEndAsync();var b=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(timeout)){try{p.Kill();}catch{}throw new Exception("媒体信息处理超时。");}System.Threading.Tasks.Task.WaitAll(a,b);if(requireSuccess&&p.ExitCode!=0)throw new Exception("无法读取媒体文件。");return a.Result+"\n"+b.Result;}
    }
    static Dictionary<string,object> Probe(string file,Dictionary<string,object> meta){
        var info=Obj("width",N(meta,"width"),"height",N(meta,"height"),"fps",N(meta,"fps"),"duration",N(meta,"duration"));
        string text=RunTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),new[]{"-hide_banner","-i",file},30000,false);
        Match size=Regex.Match(text,@"Video:[^\r\n]*?\b(\d{2,5})x(\d{2,5})\b");if(!size.Success)throw new Exception("无法读取有效的视频画面，请检查文件是否损坏。");if(size.Success){info["width"]=Int32.Parse(size.Groups[1].Value);info["height"]=Int32.Parse(size.Groups[2].Value);}
        Match fps=Regex.Match(text,@"(\d+(?:\.\d+)?) fps");if(fps.Success)info["fps"]=Double.Parse(fps.Groups[1].Value,CultureInfo.InvariantCulture);
        Match dur=Regex.Match(text,@"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)");if(dur.Success)info["duration"]=Int32.Parse(dur.Groups[1].Value)*3600+Int32.Parse(dur.Groups[2].Value)*60+Double.Parse(dur.Groups[3].Value,CultureInfo.InvariantCulture);
        info["path"]=Path.GetFullPath(file);info["bytes"]=new FileInfo(file).Length;
        double w=N(info,"width"),h=N(info,"height");info["orientation"]=w==h?"方形":w>h?"横屏":"竖屏";
        return info;
    }

    static string Relocate(string source,string folder) {
        Directory.CreateDirectory(folder);string dest=Path.Combine(folder,Path.GetFileName(source));
        if(String.Equals(Path.GetFullPath(source),Path.GetFullPath(dest),StringComparison.OrdinalIgnoreCase))return source;
        if(File.Exists(dest)){
            if(new FileInfo(source).Length==new FileInfo(dest).Length&&FileHash(source)==FileHash(dest)){File.Delete(source);return dest;}
            dest=Path.Combine(folder,Path.GetFileNameWithoutExtension(source)+" ["+Guid.NewGuid().ToString("N").Substring(0,8)+"]"+Path.GetExtension(source));
        }
        File.Move(source,dest);return dest;
    }
    static void SaveAsset(Dictionary<string,object> asset) {
        asset["updated"]=DateTime.UtcNow.ToString("o");Write(AssetPath(S(asset,"id")),asset);
        foreach(var f in Items(asset,"files")){string p=S(f,"path");if(!File.Exists(p)||EagleAsset(asset)||B(f,"eagleOwned"))continue;
            try{string side=Path.ChangeExtension(p,"qingcun.json");Write(side,PortableAsset(asset));StorageRememberSidecar(f,side,"qingcun");}
            catch{ /* Library remains authoritative if a sidecar cannot be written. */ }
        }
        Write(AssetPath(S(asset,"id")),asset);
    }
    static Dictionary<string,object> IndexVideo(string file,string downloadRoot,bool organize,bool downloaded=false,Dictionary<string,object> decision=null) {
        file=Path.GetFullPath(file);if(!File.Exists(file)||!new[]{".mp4",".mkv",".webm",".mov"}.Contains(Path.GetExtension(file).ToLowerInvariant()))throw new Exception("请选择本机可读取的视频文件。");
        string metadataPath=Path.ChangeExtension(file,"info.json");Dictionary<string,object> meta=Obj();
        if(File.Exists(metadataPath)){try{meta=new JavaScriptSerializer{MaxJsonLength=64*1024*1024}.Deserialize<Dictionary<string,object>>(File.ReadAllText(metadataPath,Encoding.UTF8));}catch{}}
        if(S(meta,"title")=="")meta["title"]=Path.GetFileNameWithoutExtension(file);
        string sourceUrl=SourceWeb(S(meta,"webpage_url"));string id=S(meta,"id");if(sourceUrl!=""&&SourcePlatform(sourceUrl)!="油管视频")id="src_"+HashText(sourceUrl).Substring(0,24);else if(!Regex.IsMatch(id,@"^[A-Za-z0-9_-]{11}$"))id="local_"+HashText(file.ToLowerInvariant()).Substring(0,24);
        if(Regex.IsMatch(S(meta,"qingcun_id"),@"^src_[a-f0-9]{24}$"))id=S(meta,"qingcun_id");
        Dictionary<string,object> portable=Obj();string sidecar=Path.ChangeExtension(file,"qingcun.json");if(File.Exists(sidecar))try{portable=Read(sidecar);}catch{}if(S(meta,"id")==""&&Regex.IsMatch(S(portable,"id"),@"^(?:[A-Za-z0-9_-]{11}|(?:local|src)_[a-f0-9]{24})$"))id=S(portable,"id");var media=Probe(file,meta);string category;var generated=BaseTags(meta,out category);
        return LibraryLock(()=>{
            string p=AssetPath(id);var asset=File.Exists(p)?Read(p):Obj("id",id,"created",DateTime.UtcNow.ToString("o"),"workflow","未整理","favorite",false,"notes","","tags",new object[0],"files",new object[0],"dismissedTags",new string[0]);
            if(!File.Exists(p)&&portable.Count>0){var restored=Items(portable,"tags").Where(t=>Dimensions.Contains(S(t,"dimension"))&&TagName(S(t,"dimension"),S(t,"name"))!="").Take(1200).Select(t=>{t["name"]=TagName(S(t,"dimension"),S(t,"name"));return t;}).ToList();asset["tags"]=restored;asset["favorite"]=B(portable,"favorite");asset["notes"]=Cut(S(portable,"notes"),2000);if(Workflows.Contains(S(portable,"workflow")))asset["workflow"]=S(portable,"workflow");if(B(portable,"categoryManual")&&Categories.Contains(CategoryName(S(portable,"category")))){asset["categoryManual"]=true;asset["category"]=CategoryName(S(portable,"category"));}asset["dismissedTags"]=Strings(portable,"dismissedTags").Take(300).ToArray();}if(B(asset,"categoryManual"))category=S(asset,"category");
            asset["category"]=category;ApplyBrand(asset,meta,portable,decision!=null&&S(decision,"brandOverride")!=""?S(decision,"brandOverride"):null);category=S(asset,"category");
            string original=file;
            if(downloaded){if(decision==null||S(decision,"videoId")!=id)throw new Exception("下载来源与确认的视频不一致，文件已保留在暂存目录。");file=InstallDownload(file,ArchiveFolder(downloadRoot,category,S(D(asset,"brand"),"name"),organize),asset,decision);}
            media["path"]=file;media["root"]=downloadRoot;media["managed"]=downloaded;media["key"]=MediaKey(media);if(decision!=null&&D(decision,"eagleReplacement").Count>0)media["eagleReplacement"]=D(decision,"eagleReplacement");
            var files=Items(asset,"files");files.RemoveAll(f=>String.Equals(S(f,"path"),file,StringComparison.OrdinalIgnoreCase));files.Insert(0,media);asset["files"]=files;
            var tags=Items(asset,"tags");var dismissed=new HashSet<string>(Strings(asset,"dismissedTags"));
            foreach(var t in generated)if(!dismissed.Contains(TagKey(t))&&!tags.Any(x=>TagKey(x)==TagKey(t)))tags.Add(t);
            asset["tags"]=tags.ToList();asset["category"]=category;asset["title"]=Cut(S(meta,"title"),300);MergeDescription(asset,meta,portable);
            asset["channel"]=Cut(S(meta,"channel"),120);asset["uploadDate"]=S(meta,"upload_date");asset["originalTags"]=SourceLabels(Strings(asset,"originalTags").Concat(Strings(meta,"tags")));
            asset["originalHashtags"]=Regex.Matches(S(meta,"description"),@"(?<!\w)#([\p{L}\p{N}_]{2,40})").Cast<Match>().Select(m=>m.Groups[1].Value).Take(20).ToArray();
            if(S(meta,"requestKey")!="")asset["requestKey"]=S(meta,"requestKey");
            if(Items(meta,"sourcePages").Count>0)asset["sourcePages"]=MergeDescriptionPages(Items(asset,"sourcePages"),Items(meta,"sourcePages"));
            asset["sourceCategories"]=SourceLabels(Strings(asset,"sourceCategories").Concat(Strings(meta,"categories")));asset["url"]=sourceUrl!=""?sourceUrl:id.StartsWith("local_")||id.StartsWith("src_")?"":"https://www.youtube.com/watch?v="+id;EnrichAsset(asset,meta,portable);
            string cover=Path.Combine(CoverDir,id+".jpg");
            if(!File.Exists(cover))try{double seek=Math.Max(0,Math.Min(N(media,"duration")*.2,3));RunTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),new[]{"-y","-hide_banner","-loglevel","error","-ss",seek.ToString("0.###",CultureInfo.InvariantCulture),"-i",file,"-frames:v","1","-vf","scale=320:-2","-q:v","6",cover},30000);}catch{}
            SaveAsset(asset);
            // Keep only useful, portable source information, without signed media URLs.
            if(downloaded&&File.Exists(metadataPath)){
                try{Write(Path.ChangeExtension(file,"info.json"),Obj("id",id,"qingcun_id",id,"sourcePages",Items(asset,"sourcePages"),"requestKey",S(asset,"requestKey"),"title",asset["title"],"description",S(asset,"description"),"descriptionOrigin",S(asset,"descriptionOrigin"),"descriptionUrl",S(asset,"descriptionUrl"),"descriptionExcerpt",B(asset,"descriptionExcerpt"),"descriptionTruncated",B(asset,"descriptionTruncated"),"channel",asset["channel"],"tags",asset["originalTags"],"categories",asset["sourceCategories"],"duration",media["duration"],"width",media["width"],"height",media["height"],"fps",media["fps"],"upload_date",asset["uploadDate"],"webpage_url",asset["url"],"uploader_id",S(meta,"uploader_id"),"uploader_url",S(meta,"uploader_url"),"uploader",S(meta,"uploader"),"channel_id",S(meta,"channel_id"),"channel_url",S(meta,"channel_url")));StorageRememberSidecar(media,Path.ChangeExtension(file,"info.json"),"info");if(!String.Equals(metadataPath,Path.ChangeExtension(file,"info.json"),StringComparison.OrdinalIgnoreCase))File.Delete(metadataPath);}catch{}
            }
            if(downloaded&&S(decision,"mode")=="replace"&&!SamePath(original,file)&&File.Exists(original))File.Delete(original);
            var descriptionResult=downloaded?ExportDescriptions(asset,true):Obj();
            if(downloaded&&S(decision,"mode")=="replace"&&S(D(asset,"brand"),"name")!=""&&!String.Equals(Path.GetDirectoryName(file),ArchiveFolder(downloadRoot,category,S(D(asset,"brand"),"name"),organize),StringComparison.OrdinalIgnoreCase)){
                MoveBrandAsset(asset);file=S(Items(asset,"files").First(),"path");descriptionResult["paths"]=Items(asset,"descriptionExports").Select(x=>S(x,"path")).ToArray();
            }
            SaveAsset(asset);
            return Obj("id",id,"path",file,"category",category,"descriptionFiles",Strings(descriptionResult,"paths"),"descriptionErrors",DescriptionDocument(asset)!=""?Strings(descriptionResult,"errors"):new string[0]);
        });
    }
    static List<Dictionary<string,object>> AllAssets(){
        Directory.CreateDirectory(LibraryDir);var all=new List<Dictionary<string,object>>();
        foreach(string p in Directory.GetFiles(LibraryDir,"*.json"))try{var a=Read(p);UpgradeMetadata(a);all.Add(a);}catch{}
        return all.OrderByDescending(x=>S(x,"updated")).ToList();
    }
    static Dictionary<string,object> LibraryList(Dictionary<string,object> q) {
        return LibraryLock(()=>{
            var all=AllAssets();IEnumerable<Dictionary<string,object>> filtered=all;string search=S(q,"search").Trim();
            if(search!=""){var terms=Regex.Split(search.ToLowerInvariant(),@"\s+");filtered=filtered.Where(a=>{string text=(S(a,"title")+" "+S(a,"channel")+" "+S(a,"category")+" "+S(a,"notes")+" "+PeopleText(a)+" "+VisualText(a)+" "+String.Join(" ",Items(a,"tags").Select(t=>S(t,"name")))).ToLowerInvariant();return terms.All(text.Contains);});}
            if(S(q,"person")!="")filtered=filtered.Where(a=>Items(a,"credits").Any(c=>S(c,"name")==S(q,"person")||S(c,"account")==S(q,"person")));
            if(S(q,"production")!="")filtered=filtered.Where(a=>S(VisibleAnalysis(a),"production")==S(q,"production"));
            if(S(q,"analysis")=="ready")filtered=filtered.Where(a=>VisibleAnalysis(a).Count>0);
            if(S(q,"analysis")=="empty")filtered=filtered.Where(a=>VisibleAnalysis(a).Count==0);
            if(S(q,"shotSearch").Trim()!=""){var shotTerms=Regex.Split(S(q,"shotSearch").Trim().ToLowerInvariant(),@"\s+");filtered=filtered.Where(a=>Items(VisibleAnalysis(a),"shots").Any(s=>shotTerms.All(term=>Json.Serialize(s).ToLowerInvariant().Contains(term))));}
            if(S(q,"category")!="")filtered=filtered.Where(a=>S(a,"category")==S(q,"category"));
            if(S(q,"workflow")!="")filtered=filtered.Where(a=>S(a,"workflow")==S(q,"workflow"));
            if(B(q,"favorite"))filtered=filtered.Where(a=>B(a,"favorite"));
            string[] wanted=Strings(q,"tags").Select(OriginalName).Where(x=>x!="").ToArray();if(wanted.Length>0)filtered=filtered.Where(a=>wanted.All(t=>Items(a,"tags").Any(x=>String.Equals(S(x,"name"),t,StringComparison.OrdinalIgnoreCase))));
            // All technical criteria must match the same downloaded variant.
            if(S(q,"orientation")!=""||N(q,"minHeight")>0||N(q,"maxDuration")>0)filtered=filtered.Where(a=>Items(a,"files").Any(f=>(S(q,"orientation")==""||S(f,"orientation")==S(q,"orientation"))&&(N(q,"minHeight")==0||N(f,"height")>=N(q,"minHeight"))&&(N(q,"maxDuration")==0||N(f,"duration")<=N(q,"maxDuration"))));
            var matches=filtered.ToList();int page=Math.Max(0,(int)N(q,"page"));int count=12;var cards=new List<object>();
            foreach(var a in matches.Skip(page*count).Take(count)){
                var tags=Items(a,"tags").GroupBy(t=>S(t,"name"),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();var f=Items(a,"files").FirstOrDefault()??Obj();string cover=Path.Combine(CoverDir,S(a,"id")+".jpg"),image="";
                if(File.Exists(cover)&&new FileInfo(cover).Length<36000)image="data:image/jpeg;base64,"+Convert.ToBase64String(File.ReadAllBytes(cover));
                cards.Add(Obj("id",S(a,"id"),"title",S(a,"title"),"category",S(a,"category"),"tags",tags.Take(8).ToList(),"tagCount",tags.Count,"workflow",S(a,"workflow"),"favorite",B(a,"favorite"),"media",f,"fileCount",Items(a,"files").Count,"mediaType",ImageAsset(a)?"image":"video","thumbnail",image,"analysisState",S(a,"analysisState"),"credits",Items(a,"credits"),"publisher",D(a,"publisher"),"analysisSummary",Obj("production",S(VisibleAnalysis(a),"production"),"colors",Strings(VisibleAnalysis(a),"colors"),"shotCount",Items(VisibleAnalysis(a),"shots").Count,"status",S(VisibleAnalysis(a),"status"))));
            }
            var tagFacets=all.SelectMany(a=>Items(a,"tags").Select(t=>S(t,"name")).Distinct()).GroupBy(x=>x).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).Take(120).Select(g=>Obj("name",g.Key,"count",g.Count())).ToArray();
            return Obj("ok",true,"items",cards,"total",matches.Count,"libraryTotal",all.Count,"page",page,"pageSize",count,"categories",Categories,"dimensions",Dimensions,"workflows",Workflows,"tagFacets",tagFacets,"analyzedTotal",all.Count(a=>VisibleAnalysis(a).Count>0),"directorTotal",all.SelectMany(a=>Items(a,"credits").Where(c=>new[]{"导演","联合导演"}.Contains(S(c,"role"))).Select(c=>S(c,"account")!=""?S(c,"account"):S(c,"name"))).Distinct().Count(),"people",all.SelectMany(a=>Items(a,"credits").Select(c=>S(c,"name")!=""?S(c,"name"):S(c,"account"))).Distinct().Where(x=>x!="").OrderBy(x=>x).ToArray(),"productionTypes",ProductionTypes,"categoryCounts",all.GroupBy(a=>S(a,"category")).Select(g=>Obj("name",g.Key,"count",g.Count())).ToArray());
        });
    }
    static Dictionary<string,object> GetAsset(string id){return LibraryLock(()=>{string p=AssetPath(id);if(!File.Exists(p))throw new Exception("找不到这条素材记录。");var a=Read(p);ResolveEagleStorage(a);UpgradeMetadata(a);return a;});}
    static Dictionary<string,object> EditAsset(Dictionary<string,object> q) {
        return LibraryLock(()=>{
            string p=AssetPath(S(q,"id"));if(!File.Exists(p))throw new Exception("找不到这条素材记录。");var a=Read(p);var tags=Items(a,"tags");string edit=S(q,"edit");
            if(edit=="addTag"||edit=="replaceTag"){
                string dim=S(q,"dimension"),name=TagName(dim,S(q,"name"));if(name=="")throw new Exception("请填写有效标签。品牌、平台和平台关键词保留原名；其他标签使用中文。");if(!Dimensions.Contains(dim))throw new Exception("请选择标签类型。");
                if(edit=="replaceTag"){string old=S(q,"key");if(!tags.Any(x=>TagKey(x)==old))throw new Exception("原标签已变更，请刷新。");tags.RemoveAll(x=>TagKey(x)==old);a["dismissedTags"]=Strings(a,"dismissedTags").Concat(new[]{old}).Distinct().Take(300).ToArray();}var t=Tag(dim,name,"manual","手动添加或修改");tags.RemoveAll(x=>TagKey(x)==TagKey(t));if(tags.Count>=1200)throw new Exception("当前素材标签过多，请先整理后再添加。");tags.Add(t);a["dismissedTags"]=Strings(a,"dismissedTags").Where(x=>x!=TagKey(t)).ToArray();
            }else if(edit=="removeTag"){
                string key=S(q,"key");tags.RemoveAll(t=>TagKey(t)==key);a["dismissedTags"]=Strings(a,"dismissedTags").Concat(new[]{key}).Distinct().Take(300).ToArray();
            }else if(edit=="confirmTag"){
                string key=S(q,"key");var t=tags.FirstOrDefault(x=>TagKey(x)==key);if(t==null)throw new Exception("标签已变更，请刷新后重试。");t["status"]="confirmed";
            }else if(edit=="flags"){
                if(q.ContainsKey("favorite"))a["favorite"]=B(q,"favorite");
                if(q.ContainsKey("workflow")){if(!Workflows.Contains(S(q,"workflow")))throw new Exception("无效的整理状态。");a["workflow"]=S(q,"workflow");}
                if(q.ContainsKey("notes"))a["notes"]=Cut(S(q,"notes"),2000);
            }else if(edit=="category"){
                string category=CategoryName(S(q,"category"));if(!Categories.Contains(category))throw new Exception("无效的分类。");
                // Classification edits are metadata-only; never move user's existing files implicitly.
                a["category"]=category;a["categoryManual"]=true;
            }else throw new Exception("未知的素材编辑操作。");
            a["tags"]=tags;SaveAsset(a);return Obj("ok",true,"asset",a);
        });
    }
    static Dictionary<string,object> ImportFiles(Dictionary<string,object> q) {
        string[] paths=Strings(q,"paths");
        if(paths.Length==0){using(var dialog=new OpenFileDialog()){dialog.Title="导入已有视频（保留原文件位置）";dialog.Filter="视频文件|*.mp4;*.mkv;*.webm;*.mov";dialog.Multiselect=true;if(dialog.ShowDialog()!=DialogResult.OK)return Obj("ok",true,"imported",0,"errors",new string[0]);paths=dialog.FileNames;}}
        if(paths.Length>30)throw new Exception("每次最多导入30个视频。");var errors=new List<string>();int imported=0;
        foreach(string p in paths){try{IndexVideo(p,Path.GetDirectoryName(Path.GetFullPath(p)),false);imported++;}catch(Exception e){errors.Add(Path.GetFileName(p)+"："+e.Message);}}
        return Obj("ok",true,"imported",imported,"errors",errors);
    }
    static Dictionary<string,object> LibraryAction(Dictionary<string,object> q) {
        switch(S(q,"action")){
            case "library":return LibraryList(q);
            case "asset":{var a=GetAsset(S(q,"id"));foreach(var f in Items(a,"files"))if(File.Exists(S(f,"path")))f["key"]=MediaKey(f);return Obj("ok",true,"asset",a,"dimensions",Dimensions,"workflows",Workflows,"categories",Categories,"roles",CreditRoles,"productionTypes",ProductionTypes,"job",ReadAnalysisJob(S(a,"analysisJobId")));}
            case "organizeBrands":return OrganizeBrands(q);
            case "upgradeCategories":return UpgradeCategories(q);
            case "editAsset":return EditAsset(q);
            case "registerEagleAsset":
            case "registerEagleVideo":return RegisterEagleVideo(q);
            case "importFiles":return ImportFiles(q);
            case "revealAsset":{
                var a=GetAsset(S(q,"id"));var files=Items(a,"files");int n=(int)N(q,"index");if(n<0||n>=files.Count)throw new Exception("文件记录不存在。");string p=S(files[n],"path");if(!File.Exists(p))throw new Exception("文件已移动或删除，请重新导入新的位置。");MacOpen(p,true);return Obj("ok",true);
            }
            case "settings":return LibraryLock(()=>Obj("ok",true,"folder",Folder(),"storageMode",S(Settings(),"storageMode"),"autoClassify",B(Settings(),"autoClassify",true),"analysisMode",B(Settings(),"analysisEnabled")?"video":"basic","analysisConfigured",ModelReady(Settings())));
            case "saveSettings":return LibraryLock(()=>{var s=Settings();s["autoClassify"]=B(q,"autoClassify",true);Write(Path.Combine(Data,"settings.json"),s);return Obj("ok",true);});
            default:return AnalysisDispatch(q);
        }
    }
}
