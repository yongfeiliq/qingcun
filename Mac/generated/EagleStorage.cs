using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

partial class Host {
    static bool SamePath(string a,string b){return a!=""&&b!=""&&String.Equals(Path.GetFullPath(a),Path.GetFullPath(b),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);}
    static bool EagleStorageFile(Dictionary<string,object> f){return D(f,"eagleStorage").Count>0;}
    static string StorageStamp(string p){var f=new FileInfo(p);return f.Length+":"+f.LastWriteTimeUtc.Ticks;}
    static Dictionary<string,object> StorageApi(string endpoint){
        string origin="http://127.0.0.1:41595";
#if QINGCUN_TESTS
        string test=Environment.GetEnvironmentVariable("QINGCUN_TEST_EAGLE_API");Uri testUri;
        if(test!=null&&Uri.TryCreate(test,UriKind.Absolute,out testUri)&&testUri.IsLoopback)origin=test;
#endif
        var req=(HttpWebRequest)WebRequest.Create(origin+"/api/"+endpoint);req.Proxy=null;req.AllowAutoRedirect=false;req.Timeout=10000;req.ReadWriteTimeout=10000;
        using(var response=req.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){var r=Json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());if(S(r,"status")!="success")throw new Exception("Eagle 尚未就绪，文件已保留。");return D(r,"data");}
    }
    static string StorageLibrary(string expected){var actual=S(D(StorageApi("library/info"),"library"),"path");if(!SamePath(actual,expected))throw new Exception("请打开已绑定的 Eagle 资源库后继续，暂存视频已保留。");return actual;}
    static string StorageItemPath(string library,string itemId){
        StorageLibrary(library);if(!Regex.IsMatch(itemId,@"^[A-Za-z0-9_-]+$"))throw new Exception("Eagle 素材编号无效。");
        var item=StorageApi("item/info?id="+Uri.EscapeDataString(itemId));if(S(item,"id")!=itemId||B(item,"isDeleted"))throw new Exception("Eagle 素材已移除，外部文件未清理。");
        string ext=S(item,"ext").ToLowerInvariant();if(!new[]{"mp4","mov","mkv","webm"}.Contains(ext))throw new Exception("Eagle 素材不是支持的视频。");
        string dir=Path.Combine(library,"images",itemId+".info");
        // Read the official item's media; never write or rename inside the library.
        var candidates=Directory.Exists(dir)?Directory.GetFiles(dir).Where(p=>Path.GetExtension(p).Equals("."+ext,StringComparison.OrdinalIgnoreCase)).ToArray():new string[0];
        if(candidates.Length!=1||!UnderFolder(candidates[0],library)||(File.GetAttributes(candidates[0])&FileAttributes.ReparsePoint)!=0)throw new Exception("无法唯一核对 Eagle 原文件，暂存视频已保留。");
        if(new FileInfo(candidates[0]).Length!=N(item,"size"))throw new Exception("Eagle 文件还未写入完成，稍后自动重试。");return candidates[0];
    }
    static void StorageIdle(Dictionary<string,object> a){
        if(Recent(Int32.MaxValue).Any(j=>new[]{"queued","running"}.Contains(S(j,"status"))))throw new Exception("等待下载或字幕任务完成后整理文件。");
        if(new[]{"queued","running"}.Contains(S(ReadAnalysisJob(S(a,"analysisJobId")),"state")))throw new Exception("等待当前分析完成后整理文件。");
    }
    static void SafeStorageSource(string source,string root,string library){
        if(root==""||!UnderFolder(source,root)||UnderFolder(source,library)||source.IndexOf(".library"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0||!new[]{".mp4",".mov",".mkv",".webm"}.Contains(Path.GetExtension(source).ToLowerInvariant()))throw new Exception("仅清理下载目录中已核验的视频副本。");
        if(File.Exists(source)&&(File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new Exception("链接文件不参与自动清理。");
    }
    static string StorageHash(Stream stream){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    // Only explicitly recorded generated files are eligible; never sweep a directory by suffix.
    static void StorageRememberSidecar(Dictionary<string,object> media,string path,string kind){
        var files=Items(media,"managedSidecars");files.RemoveAll(x=>SamePath(S(x,"path"),path));
        files.Add(Obj("path",Path.GetFullPath(path),"kind",kind,"sha256",FileHash(path)));media["managedSidecars"]=files;
    }
    static void StorageForgetSidecar(Dictionary<string,object> media,string path){
        var files=Items(media,"managedSidecars");files.RemoveAll(x=>SamePath(S(x,"path"),path));media["managedSidecars"]=files;
    }
    static bool StorageSidecarPath(Dictionary<string,object> side,string source,string root,string library){
        string path=S(side,"path"),kind=S(side,"kind");
        if(path==""||root==""||!UnderFolder(path,root)||UnderFolder(path,library)||path.IndexOf(".library"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0||!SamePath(Path.GetDirectoryName(path),Path.GetDirectoryName(source)))return false;
        if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
        return kind=="info"?SamePath(path,Path.ChangeExtension(source,"info.json")):kind=="qingcun"?SamePath(path,Path.ChangeExtension(source,"qingcun.json")):kind=="description"&&Regex.IsMatch(Path.GetFileName(path),@"\.简介(?: \[[a-f0-9]{8}\])?\.txt$");
    }
    static List<Dictionary<string,object>> StorageSidecars(Dictionary<string,object> media,string source,string root,string library){
        return Items(media,"managedSidecars").Where(x=>StorageSidecarPath(x,source,root,library)&&Regex.IsMatch(S(x,"sha256"),@"^[a-f0-9]{64}$"))
            .Select(x=>Obj("path",S(x,"path"),"kind",S(x,"kind"),"sha256",S(x,"sha256"))).ToList();
    }
    static List<Dictionary<string,object>> CleanupStorageSidecars(Dictionary<string,object> pending,string library){
        var results=new List<Dictionary<string,object>>();string source=S(pending,"sourcePath"),root=S(pending,"root"),batch=S(pending,"batch");
        if(!Regex.IsMatch(batch,@"^[A-Za-z0-9-]+$"))throw new Exception("清理记录无效，附带文件已保留。");
        foreach(var side in Items(pending,"sidecars")){
            string path=S(side,"path"),sha=S(side,"sha256");
            if(!StorageSidecarPath(side,source,root,library)){results.Add(Obj("path",path,"status","preserved","reason","outside-managed-scope"));continue;}
            if(!File.Exists(path)){results.Add(Obj("path",path,"status","missing"));continue;}
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
                if(StorageHash(file)!=sha){results.Add(Obj("path",path,"status","preserved","reason","modified"));continue;}
                // The current library record and Eagle original are durable; cleanup creates no backups.
                if(!StorageSidecarPath(side,source,root,library)||FileHash(path)!=sha)throw new Exception("附带文件发生变化，稍后重新核对。");
                StorageLibrary(library);File.Delete(path);results.Add(Obj("path",path,"status","removed","sha256",sha));
            }
        }
        return results;
    }
    // Prune only the verified file's ancestor chain. Non-recursive deletion cannot remove a new file or sibling job.
    static List<string> CleanupEmptyDownloadFolders(string directory,string root,string library=""){
        var removed=new List<string>();
        if(String.IsNullOrWhiteSpace(directory)||String.IsNullOrWhiteSpace(root))return removed;
        try{
            root=Path.GetFullPath(root);directory=Path.GetFullPath(directory);
            while(!SamePath(directory,root)&&UnderFolder(directory,root)){
                if(directory.EndsWith(".library",StringComparison.OrdinalIgnoreCase)||directory.IndexOf(".library"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0
                    ||(library!=""&&(SamePath(directory,library)||UnderFolder(directory,library))))break;
                if(Directory.Exists(directory)){
                    if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(directory).Any())break;
                    Directory.Delete(directory,false);removed.Add(directory);
                }
                directory=Path.GetDirectoryName(directory);if(String.IsNullOrEmpty(directory))break;
            }
        }catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}
        return removed;
    }
    static void StorageRebase(Dictionary<string,object> a,string oldPath,string newPath,string oldKey,string newKey){
        var paths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{oldPath,newPath}};var keys=new Dictionary<string,string>();if(oldKey!="")keys[oldKey]=newKey;
        foreach(string field in new[]{"analysis","analysisDraft","filmTypeAnalysis","filmTypeAnalysisDraft","imageAnalysis","imageAnalysisDraft","subtitleFiles","descriptionExports"})if(a.ContainsKey(field))a[field]=RemapPaths(a[field],paths,keys);
        foreach(var f in Items(a,"files"))if(SamePath(S(f,"derivedFrom"),oldPath))f["derivedFrom"]=newPath;
    }
    static Dictionary<string,object> AdoptEagleCopy(Dictionary<string,object> q){return LibraryLock(()=>{
        string id=S(q,"id"),source=S(q,"sourcePath"),lib=S(q,"libraryPath"),itemId=S(q,"itemId"),syncKey=S(q,"syncKey");
        var a=Read(AssetPath(id));StorageIdle(a);
        var already=Items(a,"files").FirstOrDefault(x=>S(x,"syncKey")==syncKey&&EagleStorageFile(x)&&S(D(x,"eagleStorage"),"itemId")==itemId);
        var f=Items(a,"files").FirstOrDefault(x=>SamePath(S(x,"path"),source)&&(B(x,"managed")||(B(q,"includeRegisteredCopy")&&!B(x,"eagleOwned"))));
        if(f==null){if(already!=null&&(!File.Exists(source)||SamePath(S(D(already,"pendingSourceCleanup"),"sourcePath"),source)))return Obj("ok",true,"file",already,"adopted",true);throw new Exception("待整理的下载记录已变化。");}
        string root=S(f,"root");SafeStorageSource(source,root,lib);string oldKey=MediaKey(f);if(oldKey!=S(q,"sourceKey"))throw new Exception("下载文件已变化，未清理。");
        string target=StorageItemPath(lib,itemId);if(SamePath(source,target))throw new Exception("源文件已经是 Eagle 原文件。");
        string before=StorageStamp(source),targetBefore=StorageStamp(target),sha=FileHash(source);if(sha!=FileHash(target)||before!=StorageStamp(source)||targetBefore!=StorageStamp(target))throw new Exception("两份视频内容不一致，未清理任何文件。");
        string batch=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
        var binding=Obj("libraryPath",lib,"itemId",itemId,"sha256",sha);string newKey=MediaKey(Obj("path",target));
        var cleanup=Obj("sourcePath",source,"root",root,"sourceKey",oldKey,"sha256",sha,"batch",batch,"sidecars",StorageSidecars(f,source,root,lib));
        // Persist every alias before granting permission to delete the old physical file.
        var all=Directory.GetFiles(LibraryDir,"*.json").Select(p=>Read(p)).Where(x=>Items(x,"files").Any(m=>SamePath(S(m,"path"),source))).ToList();
        foreach(var asset in all)StorageIdle(asset);
        Dictionary<string,object> primary=null;
        foreach(var asset in all){
            var variants=Items(asset,"files");var changed=variants.First(x=>SamePath(S(x,"path"),source));string replacementId=S(D(changed,"eagleReplacement"),"itemId");
            foreach(var previous in variants.Where(x=>x!=changed&&(S(D(x,"eagleStorage"),"itemId")==itemId||SamePath(S(x,"path"),target))))if(D(previous,"pendingSourceCleanup").Count>0)throw new Exception("请先完成上一份副本的清理，再合并其它副本。");
            variants.RemoveAll(x=>x!=changed&&(S(D(x,"eagleStorage"),"itemId")==itemId||SamePath(S(x,"path"),target)));
            foreach(var media in variants.Where(x=>SamePath(S(x,"path"),source))){media["path"]=target;media["key"]=newKey;media["managed"]=false;media["eagleOwned"]=true;media["eagleStorage"]=binding;media["syncKey"]=syncKey;media["pendingSourceCleanup"]=cleanup;media.Remove("eagleReplacement");}
            variants.Remove(changed);variants.Insert(0,changed);asset["files"]=variants;StorageRebase(asset,source,target,oldKey,newKey);if(S(asset,"id")==id)primary=asset;else Write(AssetPath(S(asset,"id")),asset);
        }
        var maps=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{source,target}};var keymap=new Dictionary<string,string>{{oldKey,newKey}};
        foreach(string dir in new[]{Jobs,Path.Combine(Data,"analysis-jobs"),Path.Combine(Data,"download-history")})if(Directory.Exists(dir))foreach(string p in Directory.GetFiles(dir,"*.json")){var record=Read(p);string beforeJson=Json.Serialize(record);var remapped=RemapPaths(record,maps,keymap);if(Json.Serialize(remapped)!=beforeJson){Write(p,remapped);}}
        if(primary!=null)Write(AssetPath(id),primary);
        return Obj("ok",true,"file",Items(Read(AssetPath(id)),"files").First(x=>SamePath(S(x,"path"),target)),"adopted",true);
    });}
    static Dictionary<string,object> CleanupEagleCopy(Dictionary<string,object> q){return LibraryLock(()=>{
        var a=Read(AssetPath(S(q,"id")));StorageIdle(a);var f=Items(a,"files").FirstOrDefault(x=>S(x,"syncKey")==S(q,"syncKey")&&EagleStorageFile(x));if(f==null)throw new Exception("找不到 Eagle 文件关联。");
        var pending=D(f,"pendingSourceCleanup");if(pending.Count==0)return Obj("ok",true,"removed",false);
        var binding=D(f,"eagleStorage");string source=S(pending,"sourcePath"),lib=S(binding,"libraryPath"),target=StorageItemPath(lib,S(binding,"itemId"));SafeStorageSource(source,S(pending,"root"),lib);
        // A partial adoption must be completed before removing its source.
        foreach(string p in Directory.GetFiles(LibraryDir,"*.json"))if(Items(Read(p),"files").Any(x=>SamePath(S(x,"path"),source)))throw new Exception("仍有素材引用外部文件，暂不清理。");
        long bytes=0;bool removed=false;List<Dictionary<string,object>> sidecars;
        using(var eagleFile=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read)){
            // Required even on retries where the external video has already been removed.
            if(StorageHash(eagleFile)!=S(pending,"sha256"))throw new Exception("Eagle 文件核验不一致，附带文件已保留。");
            if(File.Exists(source)){
                if(MediaKey(Obj("path",source))!=S(pending,"sourceKey"))throw new Exception("外部视频已被修改，保留文件供核对。");
                using(var original=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
                    if(StorageHash(original)!=S(pending,"sha256")||MediaKey(Obj("path",source))!=S(pending,"sourceKey"))throw new Exception("文件核验不一致，未清理。");
                    StorageLibrary(lib);bytes=original.Length;File.Delete(source);removed=true;
                }
            }
            sidecars=CleanupStorageSidecars(pending,lib);
        }
        var directories=CleanupEmptyDownloadFolders(Path.GetDirectoryName(source),S(pending,"root"),lib);
        string log=Path.Combine(Data,"storage-cleanup",S(pending,"batch"));Directory.CreateDirectory(log);Write(Path.Combine(log,"cleanup.json"),Obj("sourcePath",source,"eaglePath",target,"itemId",S(binding,"itemId"),"sha256",S(pending,"sha256"),"removed",removed,"bytes",bytes,"sidecars",sidecars,"directories",directories,"time",DateTime.UtcNow.ToString("o")));
        var gone=sidecars.Where(x=>S(x,"status")=="removed"||S(x,"status")=="missing").Select(x=>S(x,"path")).ToArray();
        foreach(string dir in new[]{Jobs,Path.Combine(Data,"download-history")})if(Directory.Exists(dir))foreach(string p in Directory.GetFiles(dir,"*.json")){
            var record=Read(p);bool changed=false;
            foreach(string field in new[]{"files","descriptionFiles"}){var paths=Strings(record,field);var kept=paths.Where(x=>!gone.Any(g=>SamePath(g,x))).ToArray();if(kept.Length!=paths.Length){record[field]=kept;changed=true;}}
            if(changed){Write(p,record);}
        }
        foreach(string p in Directory.GetFiles(LibraryDir,"*.json")){
            var record=Read(p);bool changed=false;
            foreach(var media in Items(record,"files"))if(SamePath(S(D(media,"pendingSourceCleanup"),"sourcePath"),source)){media.Remove("pendingSourceCleanup");media.Remove("managedSidecars");changed=true;}
            var exports=Items(record,"descriptionExports");if(exports.RemoveAll(x=>gone.Any(g=>SamePath(g,S(x,"path"))))>0){record["descriptionExports"]=exports;changed=true;}
            if(changed)Write(p,record);
        }
        return Obj("ok",true,"removed",removed,"bytes",bytes,"path",target,"sidecars",sidecars,"directories",directories);
    });}
    static void ResolveEagleStorage(Dictionary<string,object> a){
        bool changed=false;foreach(var f in Items(a,"files")){if(!EagleStorageFile(f)||File.Exists(S(f,"path")))continue;
            var b=D(f,"eagleStorage");string target=StorageItemPath(S(b,"libraryPath"),S(b,"itemId"));if(FileHash(target)!=S(b,"sha256"))throw new Exception("Eagle 视频已被替换，请重新关联后分析。");
            string old=S(f,"path"),oldKey=S(f,"key"),newKey=MediaKey(Obj("path",target));StorageRebase(a,old,target,oldKey,newKey);f["path"]=target;f["key"]=newKey;changed=true;
        }if(changed)Write(AssetPath(S(a,"id")),a);
    }
    static Dictionary<string,object> ConfigureEagleStorage(Dictionary<string,object> q){return LibraryLock(()=>{string lib=StorageLibrary(S(q,"libraryPath"));var s=Settings();if(S(s,"storageMode")!="eagle"||!SamePath(S(s,"storageLibraryPath"),lib)){s["storageMode"]="eagle";s["storageLibraryPath"]=lib;Write(Path.Combine(Data,"settings.json"),s);}return Obj("ok",true,"storageMode","eagle");});}
}
