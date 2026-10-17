using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

partial class Host {
    const int DescriptionLimit=60000;
    static string DescriptionText(string value){return Cut((value??"").Replace("\0","").Replace("\r\n","\n").Replace("\r","\n").Trim(),DescriptionLimit);}
    static void MergeDescription(Dictionary<string,object> a,Dictionary<string,object> meta,Dictionary<string,object> portable){
        var source=S(meta,"description").Trim()!=""?meta:portable;string raw=S(source,"description"),text=DescriptionText(raw);
        if(text=="")return;
        bool excerpt=B(source,"descriptionExcerpt")||B(source,"descriptionTruncated")||raw.Length>DescriptionLimit;
        if(excerpt&&S(a,"description")!=""&&!B(a,"descriptionExcerpt")&&!B(a,"descriptionTruncated"))return;
        a["description"]=text;a["descriptionOrigin"]=S(source,"descriptionOrigin")!=""?S(source,"descriptionOrigin"):"platform";
        a["descriptionUrl"]=SourceWeb(S(source,"descriptionUrl"))!=""?SourceWeb(S(source,"descriptionUrl")):SourceWeb(S(source,"webpage_url"))!=""?SourceWeb(S(source,"webpage_url")):S(a,"url");
        a["descriptionExcerpt"]=B(source,"descriptionExcerpt");a["descriptionTruncated"]=B(source,"descriptionTruncated")||raw.Length>DescriptionLimit;
    }
    static List<Dictionary<string,object>> MergeDescriptionPages(List<Dictionary<string,object>> existing,List<Dictionary<string,object>> incoming){
        foreach(var page in incoming){var old=existing.FirstOrDefault(p=>S(p,"pageUrl")==S(page,"pageUrl"));if(old==null){existing.Add(page);continue;}
            bool weak=(B(page,"descriptionExcerpt")||B(page,"descriptionTruncated"))&&S(old,"description")!=""&&!B(old,"descriptionExcerpt")&&!B(old,"descriptionTruncated");
            foreach(var pair in page){if(pair.Key.StartsWith("description")&&(weak||S(page,"description")==""))continue;if(pair.Value!=null&&Convert.ToString(pair.Value)!="")old[pair.Key]=pair.Value;}
        }return existing.Take(20).ToList();
    }
    static string DescriptionDocument(Dictionary<string,object> a){
        var parts=new List<string>();
        string text=DescriptionText(S(a,"description"));if(text!="")parts.Add("简介原文"+(B(a,"descriptionExcerpt")?"（页面摘要，可能不完整）":B(a,"descriptionTruncated")?"（已截取）":"")+"\n"+text);
        foreach(var page in Items(a,"sourcePages")){
            string more=DescriptionText(S(page,"description"));if(more==""||more==text)continue;
            if(text!=""&&!B(a,"descriptionExcerpt")&&!B(a,"descriptionTruncated")&&(B(page,"descriptionExcerpt")||B(page,"descriptionTruncated"))&&SameVideo(S(page,"pageUrl"),S(a,"descriptionUrl")!=""?S(a,"descriptionUrl"):S(a,"url")))continue;
            parts.Add("作品页简介"+(B(page,"descriptionExcerpt")?"（页面摘要，可能不完整）":B(page,"descriptionTruncated")?"（已截取）":"")+"\n"+S(page,"pageUrl")+"\n"+more);
        }
        return String.Join("\n\n",parts);
    }
    static string DescriptionExportText(Dictionary<string,object> a){
        return "标题："+S(a,"title")+"\n平台："+PlatformLabel(S(a,"url"))+"\n发布者："+S(D(a,"publisher"),"name")+"\n原视频："+S(a,"url")+"\n简介来源："+(S(a,"descriptionUrl")!=""?S(a,"descriptionUrl"):S(a,"url"))+"\n\n"+DescriptionDocument(a)+"\n";
    }
    static Dictionary<string,object> ExportDescriptions(Dictionary<string,object> a,bool automatic=false){
        if(DescriptionDocument(a)=="")return Obj("paths",new string[0],"errors",new[]{"尚未读取到简介。可先点击“更新简介”，或在原视频页面展开简介后摘录。"});
        string text=DescriptionExportText(a);var outputs=Items(a,"descriptionExports");var paths=new List<string>();var errors=new List<string>();
        foreach(var media in Items(a,"files").Where(f=>File.Exists(S(f,"path"))&&(!automatic||!B(f,"eagleOwned")))){
            string video=S(media,"path"),dest="",temp="";
            string exportDir=(EagleAsset(a)||B(media,"eagleOwned"))?Path.Combine(Data,"exports",S(a,"id")):Path.GetDirectoryName(video);Directory.CreateDirectory(exportDir);
            try{
                var old=outputs.FirstOrDefault(x=>S(x,"videoPath")==video);
                if(old!=null&&File.Exists(S(old,"path"))&&String.Equals(Path.GetDirectoryName(Path.GetFullPath(S(old,"path"))),Path.GetFullPath(exportDir),StringComparison.OrdinalIgnoreCase)&&S(old,"path").EndsWith(".txt",StringComparison.OrdinalIgnoreCase)){
                    if(File.ReadAllText(S(old,"path"),Encoding.UTF8)==text){if(automatic&&B(old,"automatic"))StorageRememberSidecar(media,S(old,"path"),"description");else if(!automatic){old["automatic"]=false;StorageForgetSidecar(media,S(old,"path"));}paths.Add(S(old,"path"));continue;}
                    if(S(old,"hash")==FileHash(S(old,"path"))&&(!automatic||B(old,"automatic")))dest=S(old,"path");
                }
                if(dest=="")dest=SubtitleOutput(exportDir,Path.GetFileNameWithoutExtension(video),".简介",".txt");
                temp=Path.Combine(Path.GetDirectoryName(dest),".qingcun-desc-"+Guid.NewGuid().ToString("N")+".tmp");
                File.WriteAllText(temp,text,new UTF8Encoding(true));
                if(File.Exists(dest))File.Replace(temp,dest,null);else File.Move(temp,dest);
                outputs.RemoveAll(x=>S(x,"videoPath")==video);outputs.Add(Obj("videoPath",video,"path",dest,"hash",FileHash(dest),"automatic",automatic));if(automatic)StorageRememberSidecar(media,dest,"description");else StorageForgetSidecar(media,dest);paths.Add(dest);
            }catch{errors.Add("简介已保存在素材库，但未能写入文本文件："+video);}
            finally{if(temp!=""&&File.Exists(temp))try{File.Delete(temp);}catch{}}
        }
        a["descriptionExports"]=outputs;SaveAsset(a);return Obj("paths",paths,"errors",errors);
    }
    static Dictionary<string,object> ExportDescriptionAction(Dictionary<string,object> q){
        return LibraryLock(()=>{var a=GetAsset(S(q,"id"));var result=ExportDescriptions(a);return Obj("ok",true,"asset",a,"paths",Strings(result,"paths"),"errors",Strings(result,"errors"),"message",Strings(result,"paths").Length>0?(EagleAsset(a)?"简介 TXT 已保存到轻存资料目录。":"简介 TXT 已保存到视频旁。"):"没有可导出的简介或本地视频文件。");});
    }
    static Dictionary<string,object> CaptureDescriptionContext(Dictionary<string,object> raw){
        string page=Normalize(S(raw,"pageUrl"));string source=S(raw,"description").Replace("\0","");string text=Cut(source,12000).Trim();
        return Obj("pageUrl",page,"title",Cut(S(raw,"title"),250),"tags",SourceLabels(Strings(raw,"tags")),"categories",SourceLabels(Strings(raw,"categories")),"description",text,"descriptionOrigin","page","descriptionUrl",page,"descriptionExcerpt",B(raw,"descriptionExcerpt"),"descriptionTruncated",B(raw,"descriptionTruncated")||source.Length>12000);
    }
    static Dictionary<string,object> SavePageDescription(Dictionary<string,object> q){
        var page=CaptureDescriptionContext(q);string url=S(page,"pageUrl");if(S(page,"description")=="")throw new Exception("页面没有返回简介。请先展开原视频的简介，再重新摘录。");
        return LibraryLock(()=>{
            var matches=AllAssets().Where(a=>S(a,"requestKey")==VideoId(url)||S(a,"id")==VideoId(url)||SameVideo(S(a,"url"),url)||Items(a,"sourcePages").Any(p=>SameVideo(S(p,"pageUrl"),url))).ToList();
            if(matches.Count==0)throw new Exception("素材库还没有这个视频。请先下载；下载时会一并保存能读取到的简介。");
            var files=new List<string>();var errors=new List<string>();
            foreach(var a in matches){
                var pages=Items(a,"sourcePages");var existing=pages.FirstOrDefault(p=>SameVideo(S(p,"pageUrl"),url));
                bool weak=B(page,"descriptionExcerpt")||B(page,"descriptionTruncated");
                if(existing==null){existing=new Dictionary<string,object>(page);pages.Add(existing);}
                else if(!weak||S(existing,"description")==""||B(existing,"descriptionExcerpt")||B(existing,"descriptionTruncated"))foreach(string k in new[]{"description","descriptionOrigin","descriptionUrl","descriptionExcerpt","descriptionTruncated"})existing[k]=page[k];
                a["sourcePages"]=pages;
                bool projectSeparate=SourcePlatform(url)=="设计作品平台"&&S(a,"description")!=""&&S(a,"descriptionUrl")!=""&&!SameVideo(S(a,"descriptionUrl"),url);
                if(!projectSeparate)MergeDescription(a,page,Obj());MergePlatformTags(a,page,Obj());
                SaveAsset(a);var result=ExportDescriptions(a);files.AddRange(Strings(result,"paths"));errors.AddRange(Strings(result,"errors"));
            }
            return Obj("ok",true,"count",matches.Count,"paths",files,"errors",errors,"message","简介已摘录到 "+matches.Count+" 条素材。"+(B(page,"descriptionExcerpt")||B(page,"descriptionTruncated")?"当前页面只提供摘要或截取内容，已标注；完整旧简介会保留。":"原始换行、署名和话题词已保留。"));
        });
    }
}
