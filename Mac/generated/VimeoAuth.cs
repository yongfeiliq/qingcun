using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Text.RegularExpressions;

partial class Host {
    static string VimeoVault(){string dir=Path.Combine(Data,"vimeo-private");Directory.CreateDirectory(dir);if(OperatingSystem.IsMacOS())File.SetUnixFileMode(dir,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);return dir;}
    static string VimeoSessionPath(string id){if(!Regex.IsMatch(id??"",@"^[a-f0-9]{32}$"))throw new Exception("Vimeo 连接标识无效，请重新下载以同步当前浏览器。");return Path.Combine(VimeoVault(),id+".bin");}
    static bool VimeoDomain(string domain){return Regex.IsMatch((domain??"").TrimStart('.'),@"^(?:[a-z0-9-]+\.)*vimeo\.com$",RegexOptions.IgnoreCase);}
    static List<Dictionary<string,object>> VimeoCookies(Dictionary<string,object> q){
        var list=new List<Dictionary<string,object>>();double now=(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;
        foreach(var c in Items(q,"cookies").Take(151)){
            string domain=S(c,"domain").ToLowerInvariant(),name=S(c,"name"),value=S(c,"value"),path=S(c,"path");
            if(!VimeoDomain(domain)||!Regex.IsMatch(name,@"^[A-Za-z0-9_.-]{1,128}$")||value.Length>16384||Regex.IsMatch(value+path,@"[\r\n\t\0]")||!path.StartsWith("/")||path.Length>2048)throw new Exception("Vimeo 登录资料格式不正确，请重新下载以同步当前浏览器。");
            double expires=N(c,"expirationDate");if(Double.IsNaN(expires)||Double.IsInfinity(expires)||expires<0||expires>253402300799)throw new Exception("Vimeo 登录有效期无效。");if(expires>0&&expires<=now)continue;
            list.Add(Obj("domain",domain,"name",name,"value",value,"path",path,"secure",B(c,"secure"),"httpOnly",B(c,"httpOnly"),"hostOnly",B(c,"hostOnly"),"expirationDate",expires));
        }
        if(list.Count>150)throw new Exception("Vimeo 登录资料过多，请刷新 Vimeo 后重新下载。");
        if(!list.Any(c=>S(c,"name")=="vimeo"&&S(c,"value")!=""&&new[]{"vimeo.com",".vimeo.com"}.Contains(S(c,"domain"))))throw new Exception("当前浏览器没有有效的 Vimeo 登录。请先在 Vimeo 登录并确认视频可播放，再重新下载。");
        return list;
    }
    static Dictionary<string,object> ReadVimeoSession(string id){
        string path=VimeoSessionPath(id);if(!File.Exists(path))throw new Exception("Vimeo 登录连接已移除，请重新下载以同步当前浏览器。");
        try{var bytes=ProtectedData.Unprotect(File.ReadAllBytes(path),Encoding.UTF8.GetBytes("Qingcun Vimeo"),DataProtectionScope.CurrentUser);var d=BigJson().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(bytes));d["cookies"]=VimeoCookies(d);return d;}
        catch{throw new Exception("Vimeo 登录已过期或无法读取，请在 Vimeo 网站确认登录后重新下载。");}
    }
    static Dictionary<string,object> SaveVimeoSession(Dictionary<string,object> q){
        var cookies=VimeoCookies(q);string id=S(q,"vimeoSession");if(id=="")id=Guid.NewGuid().ToString("N");
        string target=VimeoSessionPath(id),temp=Path.Combine(VimeoVault(),Guid.NewGuid().ToString("N")+".tmp");
        byte[] plain=Encoding.UTF8.GetBytes(Json.Serialize(Obj("cookies",cookies,"updated",DateTime.UtcNow.ToString("o"))));
        if(plain.Length>750000)throw new Exception("Vimeo 登录资料过大，请刷新页面后重试。");
        try{File.WriteAllBytes(temp,ProtectedData.Protect(plain,Encoding.UTF8.GetBytes("Qingcun Vimeo"),DataProtectionScope.CurrentUser));if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{Array.Clear(plain,0,plain.Length);if(File.Exists(temp))File.Delete(temp);}
        return Obj("ok",true,"vimeoSession",id,"connected",true,"message","已连接当前浏览器的 Vimeo 登录，可以重试下载。");
    }
    static Dictionary<string,object> VimeoStatus(Dictionary<string,object> q){
        string id=S(q,"vimeoSession");if(id=="")return Obj("ok",true,"connected",false,"message","需要登录的视频，请连接当前浏览器的 Vimeo 登录。");
        try{var d=ReadVimeoSession(id);return Obj("ok",true,"connected",true,"updated",S(d,"updated"),"message","已连接当前浏览器的 Vimeo 登录");}
        catch{return Obj("ok",true,"connected",false,"message","Vimeo 登录已失效，请重新下载以同步当前浏览器。");}
    }
    static Dictionary<string,object> ClearVimeoSession(Dictionary<string,object> q){string id=S(q,"vimeoSession");if(id!=""){string p=VimeoSessionPath(id);if(File.Exists(p))File.Delete(p);}return Obj("ok",true,"connected",false);}
    sealed class VimeoLease:IDisposable {
        string file="";
        public VimeoLease(string url,string id,List<string> args){
            if(SourcePlatform(url)!="Vimeo视频"||id=="")return;
            var d=ReadVimeoSession(id);var text=new StringBuilder("# Netscape HTTP Cookie File\n");
            foreach(var c in Items(d,"cookies")){
                string domain=S(c,"domain");bool sub=!B(c,"hostOnly");if(sub&&!domain.StartsWith("."))domain="."+domain;
                text.Append(B(c,"httpOnly")?"#HttpOnly_":"").Append(domain).Append('\t').Append(sub?"TRUE":"FALSE").Append('\t').Append(S(c,"path")).Append('\t').Append(B(c,"secure")?"TRUE":"FALSE").Append('\t').Append(((long)N(c,"expirationDate")).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t').Append(S(c,"name")).Append('\t').Append(S(c,"value")).Append('\n');
            }
            string dir=VimeoVault();
            foreach(string old in Directory.GetFiles(dir,"*.cookies"))try{int pid;string prefix=Path.GetFileName(old).Split('-')[0];if(Int32.TryParse(prefix,out pid)){try{using(var p=Process.GetProcessById(pid)){if(p.StartTime.ToUniversalTime()<=File.GetCreationTimeUtc(old))continue;}}catch{}File.Delete(old);}}catch{}
            file=Path.Combine(dir,Process.GetCurrentProcess().Id+"-"+Guid.NewGuid().ToString("N")+".cookies");
            try{File.WriteAllText(file,text.ToString(),new UTF8Encoding(false));args.InsertRange(0,new[]{"--cookies",file,"--no-cache-dir"});}catch{Dispose();throw;}
        }
        public void Dispose(){if(file!=""){try{if(File.Exists(file))File.Delete(file);}catch{}file="";}}
    }
}
