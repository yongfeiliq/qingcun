using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

class InstallTests {
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static string root,reg,stage;
 static void Status(string message){Console.WriteLine("  "+message);}
 static string RegPath(string vendor){return reg+"Software\\"+vendor+"\\NativeMessagingHosts\\"+InstallCore.HostName;}
 static void Registered(string vendor,string expected){using(var k=Registry.CurrentUser.OpenSubKey(RegPath(vendor)))Assert(k!=null&&Convert.ToString(k.GetValue(""))==expected,vendor+" registered in isolated HKCU subtree");}
 [STAThread] static int Main(string[] args){
  root=Path.GetFullPath(args[0]);reg=@"Software\QingcunInstallerTests\"+Guid.NewGuid().ToString("N")+@"\";
  try{
   Directory.CreateDirectory(root);
   stage=InstallCore.Unpack(Status);string payload=Path.Combine(stage,"payload");
   string both=Path.Combine(root,"新电脑 有空格","QingcunVideo");
   string package=InstallCore.Install(payload,both,reg,true,Status);
   Assert(File.Exists(package),"Eagle package available for official installation");
   var ping=InstallCore.Ping(Path.Combine(both,@"native\QingcunHost.exe"));
   Assert(Convert.ToString(ping["version"])==InstallCore.Version,"bundled native matches installer version");
   Assert(!Convert.ToString(ping["folder"]).StartsWith("F:",StringComparison.OrdinalIgnoreCase),"fresh install does not inherit old download drive");
   Registered(@"Google\Chrome",Path.Combine(both,@"native\host-manifest.json"));Registered(@"Microsoft\Edge",Path.Combine(both,@"native\host-manifest.json"));
   string host=File.ReadAllText(Path.Combine(both,@"native\host-manifest.json"));
   Assert(host.Contains("ikbihjphodgkkollcgcocjibbgkehldo")&&host.Contains("新电脑"),"native manifest uses stable extension ID and actual install path");
   Assert(File.ReadAllText(Path.Combine(both,"安装说明.html")).Contains("新电脑 有空格"),"guide has destination path");
   Assert(File.ReadAllText(Path.Combine(both,"实测范围.txt")).Contains("抖音"),"scope and platform access limits are installed");
   foreach(string source in new[]{"background.js","browser-session.js","xpc-assist.js","platforms.js"})
    Assert(File.Exists(Path.Combine(both,"extension",source)),"developer-mode extension includes "+source);
   Assert(!File.Exists(Path.Combine(both,@"native\data\settings.json")),"fresh install has no old model or download settings");
   foreach(string tool in new[]{"yt-dlp.exe","ffmpeg.exe","ffprobe.exe","deno.exe"}){
    var pi=new ProcessStartInfo(Path.Combine(both,@"native\tools",tool),tool.StartsWith("ff")?"-version":"--version"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    using(var p=Process.Start(pi)){var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(15000)){p.Kill();throw new Exception(tool+" timed out");}Assert(p.ExitCode==0,tool+" runs without separately installed runtimes");}
   }
   string data=Path.Combine(both,@"native\data");
   File.WriteAllText(Path.Combine(data,"settings.json"),"{\"folder\":\"D:\\\\我的参考片\",\"provider\":\"gemini\",\"analysisEnabled\":false}");
   File.WriteAllText(Path.Combine(data,"personal-note.txt"),"人工标签及镜头资料必须保留");
   string original=File.ReadAllText(Path.Combine(data,"settings.json"));
   string ui=Path.Combine(both,@"extension\popup.html");File.WriteAllText(ui,"previous custom file");
   InstallCore.Install(payload,both,reg,true,Status);
   Assert(File.ReadAllText(Path.Combine(data,"settings.json"))==original&&File.ReadAllText(Path.Combine(data,"personal-note.txt")).Contains("必须保留"),"repeat install preserves all data");
   Assert(File.ReadAllText(ui)!="previous custom file"&&Directory.GetFiles(Path.Combine(both,"backups"),"*.bak",SearchOption.AllDirectories).Length>0,"upgrade replaces program files and saves backups");
   string installedHash=InstallCore.Hash(Path.Combine(both,@"native\QingcunHost.exe"));
   // Fail after files and registry have changed; verify transaction restores them.
   using(var k=Registry.CurrentUser.CreateSubKey(RegPath(@"Google\Chrome")))k.SetValue("","previous-manifest");
   string stagedHost=Path.Combine(stage,@"eagle\engine\QingcunHost.exe");
   byte[] good=File.ReadAllBytes(stagedHost);File.WriteAllText(stagedHost,"not a valid executable");
   bool failed=false;try{InstallCore.Install(payload,both,reg,true,Status);}catch{failed=true;}finally{File.WriteAllBytes(stagedHost,good);}
   Assert(failed,"broken native component cannot produce success");
   Assert(InstallCore.Hash(Path.Combine(both,@"native\QingcunHost.exe"))==installedHash,"failed upgrade restores native binary");
   Registered(@"Google\Chrome","previous-manifest");
   Assert(File.ReadAllText(Path.Combine(data,"settings.json"))==original,"failed upgrade leaves personal settings intact");
   string eagleOnly=Path.Combine(root,"仅 Eagle","QingcunVideo");
   InstallCore.Install(payload,eagleOnly,reg+"EagleOnly\\",false,Status);
   Assert(!Directory.Exists(Path.Combine(eagleOnly,"extension")),"Eagle-only choice skips browser files");
   using(var key=Registry.CurrentUser.OpenSubKey(reg+@"EagleOnly\Software\Google\Chrome\NativeMessagingHosts\"+InstallCore.HostName))Assert(key==null,"Eagle-only choice does not register browser");
   string unsafeZip=Path.Combine(root,"unsafe.zip");
   using(var zip=ZipFile.Open(unsafeZip,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("../escaped.txt").Open()))writer.Write("no");
   bool blocked=false;try{InstallCore.Extract(unsafeZip,Path.Combine(root,"safe"));}catch{blocked=true;}
   Assert(blocked&&!File.Exists(Path.Combine(root,"escaped.txt")),"archive traversal is rejected");
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var form=new SetupWindow()){
    form.CreateControl();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(root,"installer-start.png"));}
    form.ShowComplete(true,true);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(root,"installer-finish.png"));}
   }
   Assert(SetupWindow.BrowserPath(true)!="","Edge installation discovered");
   Console.WriteLine("ALL INSTALLER TESTS PASSED");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
  finally{
   try{Registry.CurrentUser.DeleteSubKeyTree(reg.TrimEnd('\\'),false);}catch{}
   if(stage!=null)InstallCore.Cleanup(stage);
  }
 }
}
