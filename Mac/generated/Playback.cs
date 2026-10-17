using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using System.Globalization;

partial class Host {
    static readonly string PlaybackDir=Path.Combine(Data,"playback");
    static string PlaybackFile(string session,string suffix){
        if(!Regex.IsMatch(session??"",@"^[a-f0-9]{32}$"))throw new Exception("播放会话无效，请重新打开原视频。");
        return Path.Combine(PlaybackDir,session+suffix);
    }
    static string MediaMime(string path){
        switch(Path.GetExtension(path).ToLowerInvariant()){case ".mp4":return "video/mp4";case ".webm":return "video/webm";case ".mov":return "video/quicktime";case ".mkv":return "video/x-matroska";default:throw new Exception("暂不支持此文件格式。");}
    }
    static Dictionary<string,object> OpenPlayback(Dictionary<string,object> q){
        var a=GetAsset(S(q,"id"));var f=MediaFile(a,(int)N(q,"index"));string key=MediaKey(f);MediaMime(S(f,"path"));
        Directory.CreateDirectory(PlaybackDir);
        string session=Guid.NewGuid().ToString("N"),request=PlaybackFile(session,".json"),ready=PlaybackFile(session,".ready");
        Write(request,Obj("assetId",S(a,"id"),"index",(int)N(q,"index"),"filePath",S(f,"path"),"fileKey",key,"created",DateTime.UtcNow.ToString("o")));
        File.WriteAllText(PlaybackFile(session,".lease"),"");
        try{
            using(var process=Process.Start(new ProcessStartInfo(Path.Combine(Root,ExecutableName("QingcunHost")),"--playback "+session){UseShellExecute=false,WorkingDirectory=Root})){
                for(int n=0;n<100;n++){
                    if(File.Exists(ready)){var r=Read(ready);if(S(r,"error")!="")throw new Exception(S(r,"error"));return Obj("ok",true,"session",session,"url",S(r,"url"),"key",key,"bytes",new FileInfo(S(f,"path")).Length,"mimeType",MediaMime(S(f,"path")),"original",true);}
                    if(process.HasExited)break;Thread.Sleep(100);
                }
            }
            throw new Exception("本地原视频播放未能启动，请重新打开预览。");
        }catch{try{File.WriteAllText(PlaybackFile(session,".stop"),"");}catch{}throw;}
    }
    static Dictionary<string,object> KeepPlayback(Dictionary<string,object> q){
        string session=S(q,"session"),lease=PlaybackFile(session,".lease");
        if(!File.Exists(lease)||!File.Exists(PlaybackFile(session,".ready"))||File.Exists(PlaybackFile(session,".stop")))throw new Exception("播放连接已结束，请重新打开原视频。");
        File.SetLastWriteTimeUtc(lease,DateTime.UtcNow);return Obj("ok",true);
    }
    static Dictionary<string,object> ClosePlayback(Dictionary<string,object> q){
        string session=S(q,"session");if(File.Exists(PlaybackFile(session,".json")))File.WriteAllText(PlaybackFile(session,".stop"),"");
        return Obj("ok",true);
    }
    static Dictionary<string,object> OpenOriginal(Dictionary<string,object> q){
        var f=MediaFile(GetAsset(S(q,"id")),(int)N(q,"index"));MediaMime(S(f,"path"));
        Process.Start(new ProcessStartInfo(S(f,"path")){UseShellExecute=true});return Obj("ok",true);
    }
    static bool ByteRange(string value,long length,out long first,out long last){
        first=0;last=length-1;
        if(value=="")return length>0;
        var m=Regex.Match(value,@"^bytes=(\d*)-(\d*)$",RegexOptions.IgnoreCase);long a,b;
        if(!m.Success||length<=0)return false;
        if(m.Groups[1].Value==""){
            if(!Int64.TryParse(m.Groups[2].Value,out b)||b<=0)return false;first=Math.Max(0,length-b);return true;
        }
        if(!Int64.TryParse(m.Groups[1].Value,out a)||a<0||a>=length)return false;
        first=a;if(m.Groups[2].Value=="")return true;
        if(!Int64.TryParse(m.Groups[2].Value,out b)||b<a)return false;last=Math.Min(b,length-1);return true;
    }
    static void MediaHeaders(Stream stream,int status,string reason,long length,string origin,string extra=""){
        string h="HTTP/1.1 "+status+" "+reason+"\r\nContent-Length: "+length.ToString(CultureInfo.InvariantCulture)+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n";
        if(origin!="")h+="Access-Control-Allow-Origin: "+origin+"\r\nVary: Origin\r\nAccess-Control-Allow-Methods: GET, HEAD, OPTIONS\r\nAccess-Control-Allow-Headers: Range\r\nAccess-Control-Expose-Headers: Accept-Ranges, Content-Range, Content-Length\r\nAccess-Control-Allow-Private-Network: true\r\n";
        byte[] bytes=Encoding.ASCII.GetBytes(h+extra+"\r\n");stream.Write(bytes,0,bytes.Length);
    }
    static void ServeMedia(TcpClient client,string session,string file,string key,string origin,int port){
        using(client)using(var stream=client.GetStream()){
            stream.ReadTimeout=5000;stream.WriteTimeout=15000;
            var bytes=new List<byte>();int n;
            while(bytes.Count<16384&&(n=stream.ReadByte())>=0){bytes.Add((byte)n);int c=bytes.Count;if(c>=4&&bytes[c-4]==13&&bytes[c-3]==10&&bytes[c-2]==13&&bytes[c-1]==10)break;}
            if(bytes.Count>=16384){MediaHeaders(stream,431,"Request Header Fields Too Large",0,"");return;}
            string[] lines=Encoding.ASCII.GetString(bytes.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);
            string[] request=lines[0].Split(' ');var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string line in lines.Skip(1)){int colon=line.IndexOf(':');if(colon>0){string name=line.Substring(0,colon).Trim();if(headers.ContainsKey(name)){MediaHeaders(stream,400,"Bad Request",0,"");return;}headers[name]=line.Substring(colon+1).Trim();}}
            string requestOrigin=headers.ContainsKey("Origin")?headers["Origin"]:"";
            if(request.Length!=3||request[1]!="/"+session+"/video"||!headers.ContainsKey("Host")||headers["Host"]!="127.0.0.1:"+port||(requestOrigin!=""&&requestOrigin!=origin)){MediaHeaders(stream,403,"Forbidden",0,"");return;}
            if(!new[]{"GET","HEAD","OPTIONS"}.Contains(request[0])){MediaHeaders(stream,405,"Method Not Allowed",0,origin,"Allow: GET, HEAD, OPTIONS\r\n");return;}
            if(request[0]=="OPTIONS"){MediaHeaders(stream,204,"No Content",0,origin);return;}
            if(!File.Exists(file)||MediaKey(Obj("path",file))!=key){MediaHeaders(stream,410,"Gone",0,origin);return;}
            // Open only the one registered file. Deletion sharing lets brand organization proceed.
            using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                string range=headers.ContainsKey("Range")?headers["Range"]:"";long start,end;
                if(!ByteRange(range,input.Length,out start,out end)){MediaHeaders(stream,416,"Range Not Satisfiable",0,origin,"Content-Range: bytes */"+input.Length+"\r\n");return;}
                string extra="Content-Type: "+MediaMime(file)+"\r\nAccept-Ranges: bytes\r\n";
                if(range!="")extra+="Content-Range: bytes "+start+"-"+end+"/"+input.Length+"\r\n";
                MediaHeaders(stream,range!=""?206:200,range!=""?"Partial Content":"OK",end-start+1,origin,extra);
                if(request[0]=="HEAD")return;
                input.Position=start;long remaining=end-start+1;byte[] buffer=new byte[65536];
                while(remaining>0&&!File.Exists(PlaybackFile(session,".stop"))){int read=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));if(read<=0)break;stream.Write(buffer,0,read);remaining-=read;}
            }
        }
    }
    static void RunPlayback(string session){
        string request=PlaybackFile(session,".json"),ready=PlaybackFile(session,".ready"),lease=PlaybackFile(session,".lease"),stop=PlaybackFile(session,".stop");
        TcpListener listener=null;var clients=new List<TcpClient>();object sync=new object();
        try{
            var q=Read(request);var f=MediaFile(GetAsset(S(q,"assetId")),(int)N(q,"index"));
            if(S(f,"path")!=S(q,"filePath")||MediaKey(f)!=S(q,"fileKey"))throw new Exception("原文件已经变化，请重新打开预览。");
            string origin="chrome-extension://"+File.ReadAllText(Path.Combine(Root,"extension-id.txt")).Trim(),file=S(f,"path"),key=S(q,"fileKey");
            listener=new TcpListener(IPAddress.Loopback,0);listener.Start(8);int port=((IPEndPoint)listener.LocalEndpoint).Port;
            Write(ready,Obj("url","http://127.0.0.1:"+port+"/"+session+"/video","pid",Process.GetCurrentProcess().Id));
            DateTime started=DateTime.UtcNow;
            while(!File.Exists(stop)&&File.Exists(lease)&&(DateTime.UtcNow-File.GetLastWriteTimeUtc(lease)).TotalMinutes<5&&(DateTime.UtcNow-started).TotalHours<24){
                if(!listener.Pending()){Thread.Sleep(100);continue;}
                var client=listener.AcceptTcpClient();lock(sync){if(clients.Count>=8){client.Close();continue;}clients.Add(client);}
                ThreadPool.QueueUserWorkItem(_=>{try{ServeMedia(client,session,file,key,origin,port);}catch{}finally{lock(sync)clients.Remove(client);}});
            }
        }catch{try{Write(ready,Obj("error","无法读取本地原视频，请检查文件位置后重新打开。"));Thread.Sleep(1200);}catch{}}
        finally{
            if(listener!=null)listener.Stop();lock(sync){foreach(var client in clients)try{client.Close();}catch{}}
            foreach(string p in new[]{request,ready,lease,stop})try{if(File.Exists(p))File.Delete(p);}catch{}
        }
    }
}
