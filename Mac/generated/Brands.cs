using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

partial class Host {
    static readonly string[][] BrandCatalog={
        new[]{"Volvo","汽车","沃尔沃"},new[]{"McLaren","汽车","迈凯伦"},
        new[]{"Mercedes-Benz","汽车","Mercedes Benz","奔驰"},new[]{"BMW","汽车","宝马"},
        new[]{"Porsche","汽车","保时捷"},new[]{"Audi","汽车","奥迪"},new[]{"Volkswagen","汽车","大众汽车"},
        new[]{"Toyota","汽车","丰田"},new[]{"Lexus","汽车","雷克萨斯"},new[]{"Honda","汽车","本田"},
        new[]{"Ferrari","汽车","法拉利"},new[]{"Lamborghini","汽车","兰博基尼"},new[]{"Chevrolet","汽车","雪佛兰"},
        new[]{"Bentley","汽车","宾利"},new[]{"Rolls-Royce","汽车","Rolls Royce","劳斯莱斯"},
        new[]{"Land Rover","汽车","路虎"},new[]{"Range Rover","汽车","揽胜"},
        new[]{"Tesla","汽车","特斯拉"},new[]{"Polestar","汽车","极星"},new[]{"BYD","汽车","比亚迪"},
        new[]{"NIO","汽车","蔚来"},new[]{"XPENG","汽车","小鹏"},new[]{"Hyundai","汽车","现代汽车"},
        new[]{"TOM FORD","","TomFord","汤姆福特"},new[]{"Nike","鞋服穿搭","耐克"},
        new[]{"adidas","鞋服穿搭","阿迪达斯"},new[]{"Chanel","","香奈儿"},new[]{"Dior","","迪奥"},
        new[]{"Apple","数码科技","苹果","iPhone"},new[]{"Samsung","数码科技","三星"},new[]{"Sony","数码科技","索尼"},
        new[]{"DJI","数码科技","大疆"},new[]{"Rolex","手表","劳力士"},new[]{"Cartier","珠宝腕表","卡地亚"},
        new[]{"Montblanc","","万宝龙"},new[]{"OMEGA","手表","欧米茄"},new[]{"IKEA","家居生活","宜家"},new[]{"Dyson","家居生活","戴森"},
        new[]{"Coca-Cola","食品饮料","Coca Cola","可口可乐"},new[]{"Pepsi","食品饮料","百事可乐"},
        new[]{"Starbucks","食品饮料","星巴克"},new[]{"LEGO","家居生活","乐高"}
    };
    static string OriginalName(string value){return Cut(Regex.Replace((value??"").Replace("\0","").Trim().TrimStart('#'),@"\s+"," "),80);}
    static IEnumerable<string> BrandAliases(string[] b){return new[]{b[0]}.Concat(b.Skip(2));}
    static string BrandName(string value){
        string name=OriginalName(value);if(Regex.IsMatch(name,@"^(?:未识别|未知|待确认|待分类|无品牌|无|unknown|unidentified|none|n/?a)$",RegexOptions.IgnoreCase))return "";
        foreach(var b in BrandCatalog)if(BrandAliases(b).Any(x=>String.Equals(x,name,StringComparison.OrdinalIgnoreCase)))return b[0];
        string registry=Path.Combine(Data,"brands.json");
        if(File.Exists(registry))try{var old=Strings(Read(registry),"names").FirstOrDefault(x=>String.Equals(x,name,StringComparison.OrdinalIgnoreCase));if(old!=null)return old;}catch{}
        return name;
    }
    static string BrandCategory(string name){var b=BrandCatalog.FirstOrDefault(x=>x[0]==name);return b==null?"":b[1];}
    static string PlatformOriginal(string name){
        switch(name){case "油管视频":return "YouTube";case "照片墙视频":return "Instagram";case "设计作品平台":case "Behance":return "behance";case "灵感收藏平台":return "Pinterest";case "Vimeo视频":return "Vimeo";case "哔哩哔哩视频":return "bilibili";case "抖音视频":return "抖音";case "TikTok视频":return "TikTok";case "新片场视频":return "新片场";default:return name;}
    }
    static string PlatformLabel(string url){return PlatformOriginal(SourcePlatform(url));}
    static string TagName(string dimension,string value){return dimension=="来源"?PlatformOriginal(OriginalName(value)):dimension=="品牌"?BrandName(value):new[]{"平台关键词","平台分类","制作方式","软件","产品","系列"}.Contains(dimension)?SourceLabel(value):Canonical(value);}
    static bool BrandMention(string text,string alias){return Regex.IsMatch(text??"",@"(?<![\p{L}\p{N}])"+Regex.Escape(alias)+@"(?![\p{L}\p{N}])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
    static Dictionary<string,object> DetectBrand(Dictionary<string,object> meta,string category){
        var found=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        Action<string,string> add=(name,evidence)=>{name=BrandName(name);if(name!=""&&!found.ContainsKey(name))found[name]=evidence;};
        foreach(string key in new[]{"brand","client"})if(meta.ContainsKey(key)&&meta[key] is string&&S(meta,key).Length<=80)add(S(meta,key),"平台的品牌 / 客户字段");
        var pages=Items(meta,"sourcePages");
        foreach(string description in new[]{S(meta,"description")}.Concat(pages.Select(p=>S(p,"description"))))
            foreach(Match match in Regex.Matches(description,@"(?im)^\s*(?:brand|client|品牌|客户)\s*[:：]\s*([^\r\n]{1,80})$")){
                string value=match.Groups[1].Value.Trim();
                if(Regex.IsMatch(value,@"^(?:personal|personnal|self[- ]initiated)\s+project\b|^个人项目$|^个人作品$",RegexOptions.IgnoreCase))continue;
                if(Regex.IsMatch(value,@"https?://|[/\\<>]|[;；]|(?:\s[&+]\s)|、"))continue;
                add(value,"简介明确署名："+Cut(match.Value.Trim(),100));
            }
        var titles=new[]{S(meta,"title")}.Concat(pages.Select(p=>S(p,"title"))).ToArray();
        var keywords=Strings(meta,"tags").Concat(Strings(meta,"originalTags")).Concat(pages.SelectMany(p=>Strings(p,"tags"))).ToArray();
        foreach(var b in BrandCatalog){
            if(b[0]=="Apple"&&!new[]{"数码科技","手机","手表"}.Contains(category)&&!found.ContainsKey(b[0]))continue;
            if(b[0]=="OMEGA"&&!new[]{"珠宝腕表","手表"}.Contains(category)&&!found.ContainsKey(b[0]))continue;
            if(BrandAliases(b).Any(x=>titles.Any(t=>BrandMention(t,x))))add(b[0],"视频 / 作品标题包含品牌名");
            else if(BrandAliases(b).Any(x=>keywords.Any(t=>String.Equals(OriginalName(t),x,StringComparison.OrdinalIgnoreCase))))add(b[0],"平台原始关键词明确包含品牌");
        }
        string registry=Path.Combine(Data,"brands.json");
        if(File.Exists(registry))try{foreach(string name in Strings(Read(registry),"names").Where(n=>!BrandCatalog.Any(b=>b[0]==n)))if(titles.Any(t=>BrandMention(t,name))||keywords.Any(t=>String.Equals(t,name,StringComparison.OrdinalIgnoreCase)))add(name,"标题 / 原始关键词匹配已确认的品牌");}catch{}
        var candidates=found.Keys.OrderBy(x=>x).ToArray();
        return Obj("name",candidates.Length==1?candidates[0]:"","candidates",candidates,"evidence",candidates.Length==1?found[candidates[0]]:candidates.Length>1?"检测到多个品牌，请指定归档品牌。":"未找到明确品牌，可手动填写。","manual",false);
    }
    static void RememberBrand(string name){
        if(name=="")return;string p=Path.Combine(Data,"brands.json");
        var names=File.Exists(p)?Strings(Read(p),"names").ToList():new List<string>();
        if(!names.Any(x=>String.Equals(x,name,StringComparison.OrdinalIgnoreCase))){names.Add(name);Write(p,Obj("names",names.Take(2000).ToArray()));}
    }
    static void ApplyBrand(Dictionary<string,object> a,Dictionary<string,object> meta,Dictionary<string,object> portable,string manual=null){
        var brand=D(a,"brand");if(brand.Count==0&&D(portable,"brand").Count>0)brand=D(portable,"brand");
        if(manual!=null){string name=BrandName(manual);brand=Obj("name",name,"candidates",name!=""?new[]{name}:new string[0],"manual",true,"evidence",name!=""?"手动指定的归档品牌":"手动设为无品牌");RememberBrand(name);}
        else if(!B(brand,"manual")){
            var detected=DetectBrand(meta,S(a,"category"));
            if(S(detected,"name")!=""||Strings(detected,"candidates").Length>1||brand.Count==0)brand=detected;
        }
        a["brand"]=brand;string primary=S(brand,"name"),suggested=BrandCategory(primary);
        if(primary!=""&&S(brand,"evidence").StartsWith("简介明确署名")&&!BrandCatalog.Any(b=>b[0]==primary))RememberBrand(primary);
        if(!B(a,"categoryManual")&&suggested!=""&&(S(a,"category")=="待分类"||suggested=="汽车"||(S(a,"category")=="珠宝腕表"&&suggested=="手表")))a["category"]=suggested;
        var tags=Items(a,"tags");tags.RemoveAll(t=>S(t,"dimension")=="品牌"&&S(t,"source")=="brand");
        if(primary!=""&&!tags.Any(t=>S(t,"dimension")=="品牌"&&BrandName(S(t,"name"))==primary)){
            var t=Tag("品牌",primary,B(brand,"manual")?"manual":"rule",S(brand,"evidence"));t["source"]="brand";tags.Insert(0,t);
        }
        a["tags"]=tags;
    }
    static void UpgradeBrandTags(Dictionary<string,object> a){
        if(N(a,"brandSchema")>=1)return;
        string before=Json.Serialize(a);var tags=Items(a,"tags");
        foreach(var t in tags)t["name"]=TagName(S(t,"dimension"),S(t,"name"));
        var original=Strings(a,"originalTags").Concat(Strings(a,"originalHashtags")).Concat(Items(a,"sourcePages").SelectMany(p=>Strings(p,"tags"))).Where(x=>OriginalName(x)!="").Take(70).ToArray();
        if(original.Length>0){tags.RemoveAll(t=>S(t,"dimension")=="平台关键词"&&S(t,"source")!="manual");foreach(string raw in original)tags.Add(Tag("平台关键词",raw,"metadata","平台原始关键词 / 话题词"));}
        a["tags"]=tags.Where(t=>S(t,"name")!="").GroupBy(TagKey).Select(g=>g.First()).ToList();
        D(a,"publisher")["platform"]=PlatformLabel(S(a,"url"));
        ApplyBrand(a,a,Obj());a["brandSchema"]=1;
        if(Json.Serialize(a)!=before)SaveAsset(a);
    }
    static string BrandFolderName(string name){
        string safe=Regex.Replace(name,@"[<>:""/\\|?*\x00-\x1f]","_").Trim().TrimEnd('.',' ');
        if(safe==""||Regex.IsMatch(safe,@"^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase))safe="_"+safe;
        bool changed=safe!=name||safe.Length>60;safe=Cut(safe,60);
        return changed?safe+"-"+HashText(name).Substring(0,8):safe;
    }
    static string ArchiveFolder(string root,string category,string brand,bool classify){
        if(S(Settings(),"storageMode")=="eagle")return Path.Combine(Path.GetFullPath(root),"_等待入库");
        string folder=classify?Path.Combine(Path.GetFullPath(root),Categories.Contains(category)?category:"待分类"):Path.GetFullPath(root);
        if(brand!="")folder=Path.Combine(folder,BrandFolderName(brand));return folder;
    }
    static string ArchiveFile(string folder,string name,string extension,string suffix=""){
        int limit=Math.Min(150,230-folder.Length-extension.Length-suffix.Length-1);
        if(limit<20)throw new Exception("保存路径太长，请在设置中选择较短的保存位置。");
        if(name.Length>limit)name=Cut(name,limit-9)+"-"+HashText(name).Substring(0,8);
        return Path.Combine(folder,name+suffix+extension);
    }
    static bool UnderFolder(string file,string folder){
        string root=Path.GetFullPath(folder).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(file);
        if(!full.StartsWith(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))return false;
        var d=new DirectoryInfo(Path.GetDirectoryName(full));
        while(d!=null){if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)return false;d=d.Parent;}
        return true;
    }
    static object RemapPaths(object value,Dictionary<string,string> paths,Dictionary<string,string> keys){
        var d=value as Dictionary<string,object>;
        if(d!=null){
            string key=S(d,"fileKey");if(keys.ContainsKey(key))d["fileKey"]=keys[key];
            foreach(string k in d.Keys.ToArray())if(k!="fileKey")d[k]=RemapPaths(d[k],paths,keys);
            return d;
        }
        if(value is string){string s=(string)value;return paths.ContainsKey(s)?paths[s]:s;}
        if(value is IEnumerable)return ((IEnumerable)value).Cast<object>().Select(x=>RemapPaths(x,paths,keys)).ToArray();
        return value;
    }
    static Dictionary<string,object> MoveBrandAsset(Dictionary<string,object> a){
        string brand=S(D(a,"brand"),"name");if(brand=="")return Obj("id",S(a,"id"),"moved",0,"skipped",Items(a,"files").Count,"reason",S(D(a,"brand"),"evidence"));
        if(new[]{"queued","running"}.Contains(S(ReadAnalysisJob(S(a,"analysisJobId")),"state")))throw new Exception("当前视频正在分析，请完成后再整理。");
        var moves=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var oldKeys=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var reserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int count=0,skipped=0;
        foreach(var f in Items(a,"files")){
            string source=S(f,"path"),root=S(f,"root");
            if(!B(f,"managed")||root==""||!File.Exists(source)||!UnderFolder(source,root)){skipped++;continue;}
            string folder=ArchiveFolder(root,S(a,"category"),brand,B(Settings(),"autoClassify",true));
            if(!UnderFolder(Path.Combine(folder,"check.mp4"),root))throw new Exception("品牌目录不在原保存位置内，未移动文件。");
            if(String.Equals(Path.GetDirectoryName(source),folder,StringComparison.OrdinalIgnoreCase))continue;
            string dest=ArchiveFile(folder,Path.GetFileNameWithoutExtension(source),Path.GetExtension(source));
            if(File.Exists(dest)||reserved.Contains(dest)||File.Exists(Path.ChangeExtension(dest,"info.json"))||File.Exists(Path.ChangeExtension(dest,"qingcun.json")))dest=ArchiveFile(folder,Path.GetFileNameWithoutExtension(source),Path.GetExtension(source)," ["+Guid.NewGuid().ToString("N").Substring(0,8)+"]");
            moves[source]=dest;reserved.Add(dest);oldKeys[source]=MediaKey(f);count++;
            foreach(string ext in new[]{"info.json","qingcun.json"}){string side=Path.ChangeExtension(source,ext);if(File.Exists(side))moves[side]=Path.ChangeExtension(dest,ext);}
            foreach(var extra in Items(a,"subtitleFiles").Concat(Items(a,"descriptionExports"))){
                string old=S(extra,"path");if(!String.Equals(S(extra,"videoPath"),source,StringComparison.OrdinalIgnoreCase)||!File.Exists(old)||!UnderFolder(old,root)||!String.Equals(Path.GetDirectoryName(old),Path.GetDirectoryName(source),StringComparison.OrdinalIgnoreCase))continue;
                if(moves.ContainsKey(old))continue;
                string to=ArchiveFile(folder,Path.GetFileNameWithoutExtension(old),Path.GetExtension(old));
                if(File.Exists(to)||reserved.Contains(to))to=ArchiveFile(folder,Path.GetFileNameWithoutExtension(old),Path.GetExtension(old)," ["+Guid.NewGuid().ToString("N").Substring(0,8)+"]");
                moves[old]=to;reserved.Add(to);
            }
        }
        if(moves.Count==0)return Obj("id",S(a,"id"),"moved",0,"skipped",skipped);
        string backup=Path.Combine(Data,"brand-archive-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(backup);
        var backups=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        Action<string> save=p=>{if(File.Exists(p)&&!backups.ContainsKey(p)){string b=Path.Combine(backup,backups.Count+".json");File.Copy(p,b,false);backups[p]=b;}};
        save(AssetPath(S(a,"id")));
        foreach(var f in Items(a,"files"))save(Path.ChangeExtension(S(f,"path"),"qingcun.json"));
        var records=new List<string>();
        foreach(string dir in new[]{Jobs,Path.Combine(Data,"download-history"),AnalysisDir})if(Directory.Exists(dir))records.AddRange(Directory.GetFiles(dir,"*.json"));
        var updates=new Dictionary<string,Dictionary<string,object>>();
        foreach(string p in records){var record=Read(p);string text=Json.Serialize(record);if(moves.Keys.Any(x=>text.Contains(Json.Serialize(x)))||S(record,"assetId")==S(a,"id")){save(p);updates[p]=record;}}
        string journal=Path.Combine(backup,"journal.json");Write(journal,Obj("state","prepared","moves",moves,"backups",backups));
        var completed=new List<KeyValuePair<string,string>>();
        try{
            foreach(var move in moves){Directory.CreateDirectory(Path.GetDirectoryName(move.Value));File.Move(move.Key,move.Value);completed.Add(move);}
            var keys=new Dictionary<string,string>();foreach(var old in oldKeys)keys[old.Value]=MediaKey(Obj("path",moves[old.Key]));
            RemapPaths(a,moves,keys);
            foreach(var f in Items(a,"files"))if(File.Exists(S(f,"path")))f["key"]=MediaKey(f);
            foreach(var pair in updates){RemapPaths(pair.Value,moves,keys);Write(pair.Key,pair.Value);}
            SaveAsset(a);Write(journal,Obj("state","completed","moves",moves,"backups",backups));
            return Obj("id",S(a,"id"),"moved",count,"skipped",skipped,"backup",backup);
        }catch{
            var errors=new List<string>();
            foreach(var move in completed.AsEnumerable().Reverse())try{if(File.Exists(move.Value)&&!File.Exists(move.Key))File.Move(move.Value,move.Key);}catch{errors.Add(move.Key);}
            foreach(var copy in backups)try{File.Copy(copy.Value,copy.Key,true);}catch{errors.Add(copy.Key);}
            Write(journal,Obj("state",errors.Count==0?"rolled_back":"needs_recovery","moves",moves,"backups",backups,"errors",errors));
            throw new Exception(errors.Count==0?"品牌整理未完成，文件与记录已恢复原位置。":"部分文件整理失败，恢复记录保存在："+backup);
        }
    }
    static Dictionary<string,object> OrganizeBrands(Dictionary<string,object> q){
        using(var mutex=new Mutex(false,"QingcunJobs_"+UserKey())){
            bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new Exception("下载组件忙碌，请稍后整理。");
                return LibraryLock(()=>{
                    if(Recent(Int32.MaxValue).Any(j=>new[]{"queued","running"}.Contains(S(j,"status"))))throw new Exception("请等正在下载的视频和字幕完成后，再整理已有文件。");
                    var ids=Strings(q,"ids");var assets=S(q,"id")!=""?new List<Dictionary<string,object>>{GetAsset(S(q,"id"))}:AllAssets().Where(a=>ids.Length==0||ids.Contains(S(a,"id"))).ToList();
                    var results=new List<Dictionary<string,object>>();var errors=new List<string>();
                    foreach(var a in assets)try{
                        if(S(q,"id")!=""&&q.ContainsKey("brand")){ApplyBrand(a,a,Obj(),S(q,"brand"));SaveAsset(a);}
                        string before=Json.Serialize(a);ApplyBrand(a,a,Obj());if(Json.Serialize(a)!=before)SaveAsset(a);
                        results.Add(MoveBrandAsset(a));
                    }catch(Exception e){errors.Add(S(a,"title")+"："+e.Message);}
                    int moved=results.Sum(x=>(int)N(x,"moved"));return Obj("ok",true,"results",results,"errors",errors,"moved",moved,"message","已按品牌整理 "+moved+" 个视频。"+(results.Sum(x=>N(x,"skipped"))>0?"未识别品牌、文件缺失或导入的文件保留原位置。":""));
                });
            }finally{if(held)mutex.ReleaseMutex();}
        }
    }
}
