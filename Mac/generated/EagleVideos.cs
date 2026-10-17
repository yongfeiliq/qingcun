using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

partial class Host {
    static bool EagleAsset(Dictionary<string,object> a){return D(a,"eagle").Count>0;}
    static Dictionary<string,object> RegisterEagleVideo(Dictionary<string,object> q){
        string library=Path.GetFullPath(S(q,"libraryPath")).TrimEnd(Path.DirectorySeparatorChar),itemId=S(q,"itemId"),file=Path.GetFullPath(S(q,"path"));
        if(!library.EndsWith(".library",StringComparison.OrdinalIgnoreCase)||!Directory.Exists(library)||!Regex.IsMatch(itemId,@"^[A-Za-z0-9_-]{1,100}$"))throw new Exception("Eagle 资源库或素材标识无效。");
        string itemFolder=Path.Combine(library,"images",itemId+".info");
        if(!String.Equals(Path.GetDirectoryName(file),itemFolder,StringComparison.OrdinalIgnoreCase)||!File.Exists(file)||
           !(ImageExtensions.Concat(new[]{".mp4",".mov",".mkv",".webm",".m4v",".avi",".wmv",".mpg",".mpeg",".ts",".mts",".m2ts"}).Contains(Path.GetExtension(file).ToLowerInvariant())))throw new Exception("这条 Eagle 素材没有可读取的本地图片或视频。");
        string id="local_"+HashText("eagle|"+library.ToLowerInvariant()+"|"+itemId).Substring(0,24);
        var meta=Obj("title",Cut(S(q,"title"),300),"description",Cut(S(q,"annotation"),DescriptionLimit),"webpage_url",SourceWeb(S(q,"url")),"tags",new string[0]);
        bool image=ImageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant());
        var media=Probe(file,meta);if(image){media["duration"]=0;media["fps"]=0;media["mediaType"]="image";
        media["orientation"]=N(media,"width")==N(media,"height")?"方图":N(media,"width")>N(media,"height")?"横图":"竖图";}media["managed"]=false;media["eagleOwned"]=true;media["root"]="";media["key"]=MediaKey(media);
        string category;var generated=BaseTags(meta,out category);if(image)generated=generated.Where(t=>S(t,"dimension")!="视频类型").ToList();
        return LibraryLock(()=>{
            string p=AssetPath(id);bool fresh=!File.Exists(p);var a=fresh?Obj("id",id,"created",DateTime.UtcNow.ToString("o"),"workflow","未整理","favorite",false,"notes","","tags",new object[0],"dismissedTags",new string[0]):Read(p);
            var oldFile=Items(a,"files").FirstOrDefault();bool changed=oldFile!=null&&S(oldFile,"key")!=S(media,"key");
            if(changed){
                var job=ReadAnalysisJob(S(a,"analysisJobId"));if(new[]{"running","queued"}.Contains(S(job,"state")))throw new Exception("此素材正在分析，完成后再重新读取。");
                if(D(a,"filmTypeAnalysis").Count>0||D(a,"filmTypeAnalysisDraft").Count>0||D(a,"analysis").Count>0||D(a,"analysisDraft").Count>0||D(a,"imageAnalysis").Count>0||D(a,"imageAnalysisDraft").Count>0){var history=Items(a,"eagleAnalysisHistory");history.Insert(0,Obj("filmTypeAnalysis",D(a,"filmTypeAnalysis"),"filmTypeAnalysisDraft",D(a,"filmTypeAnalysisDraft"),"imageAnalysis",D(a,"imageAnalysis"),"imageAnalysisDraft",D(a,"imageAnalysisDraft"),"analysis",D(a,"analysis"),"analysisDraft",D(a,"analysisDraft"),"saved",DateTime.UtcNow.ToString("o")));a["eagleAnalysisHistory"]=history.Take(3).ToList();}
                a.Remove("filmTypeAnalysis");a.Remove("filmTypeAnalysisDraft");a.Remove("imageAnalysis");a.Remove("imageAnalysisDraft");a.Remove("analysis");a.Remove("analysisDraft");a.Remove("analysisJobId");a["analysisState"]="not_analyzed";
            }
            a["mediaType"]=image?"image":"video";a["eagle"]=Obj("itemId",itemId,"libraryPath",library,"sourceStamp",S(q,"sourceStamp"));a["files"]=new[]{media};
            a["title"]=S(meta,"title")!=""?S(meta,"title"):Path.GetFileNameWithoutExtension(file);
            if(!B(a,"categoryManual"))a["category"]=category;
            if(fresh)a["url"]=S(meta,"webpage_url");
            // Keep Eagle's own labels intact. Suggestions stay suggestions until the user confirms them.
            var tags=Items(a,"tags");var dismissed=new HashSet<string>(Strings(a,"dismissedTags"));
            foreach(var tag in generated)if(!dismissed.Contains(TagKey(tag))&&!tags.Any(t=>TagKey(t)==TagKey(tag)))tags.Add(tag);
            a["tags"]=tags;ApplyBrand(a,meta,Obj());a["brandSchema"]=1;
            if(fresh){a["eagleOriginalTags"]=Strings(q,"tags");a["eagleOriginalAnnotation"]=Cut(S(q,"annotation"),DescriptionLimit);EnrichAsset(a,meta,Obj());}
            string cover=Path.Combine(CoverDir,id+".jpg");
            if(!File.Exists(cover)||changed)try{RunTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),new[]{"-y","-hide_banner","-loglevel","error","-ss",Math.Min(2,N(media,"duration")*.2).ToString("0.###",CultureInfo.InvariantCulture),"-i",file,"-frames:v","1","-vf","scale=320:-2","-q:v","6",cover},30000);}catch{}
            SaveAsset(a);return Obj("ok",true,"asset",a,"changed",changed);
        });
    }
}
