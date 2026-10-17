using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Encodings.Web;

// Mac-only platform layer. Windows builds continue using the Framework implementation.
namespace System.Web.Script.Serialization {
 public sealed class JavaScriptSerializer {
  public int MaxJsonLength{get;set;}=2097152;public int RecursionLimit{get;set;}=100;
  public string Serialize(object value){var s=JsonSerializer.Serialize(value,new JsonSerializerOptions{MaxDepth=RecursionLimit,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping});if(s.Length>MaxJsonLength)throw new Exception("JSON 数据过大。");return s;}
  public T Deserialize<T>(string value){if(value.Length>MaxJsonLength)throw new Exception("JSON 数据过大。");using(var doc=JsonDocument.Parse(value,new JsonDocumentOptions{MaxDepth=RecursionLimit}))return (T)ConvertValue(doc.RootElement);}
  static object ConvertValue(JsonElement e){switch(e.ValueKind){case JsonValueKind.Object:var d=new Dictionary<string,object>();foreach(var p in e.EnumerateObject())d[p.Name]=ConvertValue(p.Value);return d;case JsonValueKind.Array:return e.EnumerateArray().Select(ConvertValue).ToArray();case JsonValueKind.String:return e.GetString();case JsonValueKind.Number:if(e.TryGetInt64(out var n))return n;return e.GetDouble();case JsonValueKind.True:return true;case JsonValueKind.False:return false;default:return null;}}
 }
}
static class MacUi {
 public static string Execute(string program,IEnumerable<string> args){var p=new ProcessStartInfo(program){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in args)p.ArgumentList.Add(a);using(var child=Process.Start(p)){var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();if(!child.WaitForExit(120000)){child.Kill(true);throw new Exception("窗口等待超时，请重试。");}System.Threading.Tasks.Task.WaitAll(output,error);if(child.ExitCode!=0){if(error.Result.Contains("-128")||error.Result.Contains("User canceled"))return null;throw new Exception("无法打开文件选择窗口："+error.Result);}return output.Result.Trim();}}
 public static string Choose(string prompt,bool folder,bool multiple){string script="function run(argv){var app=Application.currentApplication();app.includeStandardAdditions=true;var v="+(folder?"app.chooseFolder({withPrompt:argv[0]})":"app.chooseFile({withPrompt:argv[0],multipleSelectionsAllowed:argv[1]==='1'})")+";return JSON.stringify((Array.isArray(v)?v:[v]).map(function(p){return p.toString()}));}";return Execute("/usr/bin/osascript",new[]{"-l","JavaScript","-e",script,"--",prompt,multiple?"1":"0"});}
}
namespace System.Windows.Forms {
 public enum DialogResult{OK,Cancel}
 public class FolderBrowserDialog:IDisposable{public string Description{get;set;}public string SelectedPath{get;set;}public DialogResult ShowDialog(){var s=MacUi.Choose(Description,true,false);if(s==null)return DialogResult.Cancel;SelectedPath=JsonSerializer.Deserialize<string[]>(s)[0];return DialogResult.OK;}public void Dispose(){}}
 public class OpenFileDialog:IDisposable{public string Title{get;set;}public string Filter{get;set;}public bool Multiselect{get;set;}public string[] FileNames{get;private set;}=new string[0];public string FileName=>FileNames.FirstOrDefault()??"";public DialogResult ShowDialog(){var s=MacUi.Choose(Title,false,Multiselect);if(s==null)return DialogResult.Cancel;FileNames=JsonSerializer.Deserialize<string[]>(s);return DialogResult.OK;}public void Dispose(){}}
}
namespace System.Security.Cryptography {
 public enum DataProtectionScope{CurrentUser}
 public static class ProtectedData {
  static byte[] Key(){string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");Directory.CreateDirectory(dir);if(OperatingSystem.IsMacOS())File.SetUnixFileMode(dir,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);string p=Path.Combine(dir,".local-secret-key");if(!File.Exists(p)){var key=RandomNumberGenerator.GetBytes(32);try{var options=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write};if(OperatingSystem.IsMacOS())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;using(var f=new FileStream(p,options))f.Write(key);}catch(IOException){if(!File.Exists(p))throw;}}var stored=File.ReadAllBytes(p);if(stored.Length!=32)throw new Exception("本机加密配置损坏，请保留数据后联系修复。");return stored;}
  public static byte[] Protect(byte[] data,byte[] entropy,DataProtectionScope scope){byte[] nonce=RandomNumberGenerator.GetBytes(12),cipher=new byte[data.Length],tag=new byte[16];using(var aes=new AesGcm(Key(),16))aes.Encrypt(nonce,data,cipher,tag,entropy);return nonce.Concat(tag).Concat(cipher).ToArray();}
  public static byte[] Unprotect(byte[] data,byte[] entropy,DataProtectionScope scope){if(data.Length<28)throw new Exception("无效的加密数据。");var plain=new byte[data.Length-28];using(var aes=new AesGcm(Key(),16))aes.Decrypt(data.AsSpan(0,12),data.AsSpan(28),data.AsSpan(12,16),plain,entropy);return plain;}
 }
}
partial class Host {
 static string UserKey(){return HashText(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Substring(0,24);}
 static string ExecutableName(string name){return OperatingSystem.IsWindows()?name+".exe":name;}
 static void MacOpen(string file,bool reveal=false){var p=new ProcessStartInfo("/usr/bin/open"){UseShellExecute=false};if(reveal)p.ArgumentList.Add("-R");p.ArgumentList.Add(file);Process.Start(p)?.Dispose();}
 static bool PngValid(string path){using(var f=File.OpenRead(path)){byte[] b=new byte[24];if(f.Read(b,0,b.Length)!=b.Length)return false;return b[0]==137&&b[1]==80&&b[2]==78&&b[3]==71&&b.Skip(16).Take(4).Any(x=>x!=0)&&b.Skip(20).Take(4).Any(x=>x!=0);}}
 static void MacJpeg(string png,string target){RunTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),new[]{"-y","-hide_banner","-loglevel","error","-i",png,"-filter_complex","[0:v]format=rgba,split[a][b];[b]lutrgb=r=255:g=255:b=255,format=rgb24[bg];[bg][a]overlay=shortest=1,format=yuvj420p[out]","-map","[out]","-frames:v","1","-threads","1","-q:v","3",target},60000);}
 static void MacSheet(string[] frames,string[] labels,string target){var args=new List<string>{"-y","-hide_banner","-loglevel","error"};foreach(var f in frames){args.Add("-i");args.Add(f);}var filters=new List<string>();string font=new[]{"/System/Library/Fonts/Helvetica.ttc","/System/Library/Fonts/Supplemental/Arial.ttf"}.FirstOrDefault(File.Exists);if(font==null)throw new Exception("未找到系统字体，无法生成带时间标记的抽帧。");for(int n=0;n<frames.Length;n++){string label=labels[n].Replace("\\","\\\\").Replace("'","\\'").Replace(":","\\:");filters.Add("["+n+":v]scale=320:180:force_original_aspect_ratio=decrease,pad=320:208:(ow-iw)/2:(180-ih)/2:color=0x12161c,drawtext=fontfile='"+font+"':text='"+label+"':fontcolor=white:fontsize=14:x=6:y=183[v"+n+"]");}string stack=String.Join("",Enumerable.Range(0,frames.Length).Select(n=>"[v"+n+"]"))+"xstack=inputs="+frames.Length+":layout="+String.Join("|",Enumerable.Range(0,frames.Length).Select(n=>(n%4*320)+"_"+(n/4*208)))+":fill=0x12161c,pad=1280:624:0:0:color=0x12161c[out]";if(frames.Length==1)stack="[v0]pad=1280:624:0:0:color=0x12161c[out]";filters.Add(stack);args.AddRange(new[]{"-filter_complex_threads","1","-filter_complex",String.Join(";",filters),"-map","[out]","-frames:v","1","-q:v","3",target});RunTool(Path.Combine(Root,"tools",ExecutableName("ffmpeg")),args,90000);}
}
