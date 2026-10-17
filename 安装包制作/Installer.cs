using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("轻存 · 一体安装")]
[assembly: AssemblyVersion("2.3.14.0")]
[assembly: AssemblyFileVersion("2.3.14.0")]

public static class InstallCore {
    public const string Version = "2.3.14";
    public const string HostName = "com.qingcun.video_downloader";
    public const string PackageName = "轻存-Eagle-v2.3.14.eagleplugin";
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    public static string Hash(string path) { using(var f=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant(); }
    public static string Inside(string root,string relative) {
        if(String.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(":"))throw new Exception("安装包路径无效。");
        string prefix=Path.GetFullPath(root).TrimEnd('\\')+"\\", full=Path.GetFullPath(Path.Combine(prefix,relative.Replace('/','\\')));
        if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("安装包路径越界。");
        return full;
    }
    public static void Extract(string zip,string target) {
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
        using(var archive=ZipFile.OpenRead(zip)){
            if(archive.Entries.Count>2000)throw new Exception("安装包文件数异常。");
            foreach(var entry in archive.Entries){
                string dest=Inside(target,entry.FullName);
                if(!seen.Add(dest))throw new Exception("安装包存在重名文件。");
                total+=entry.Length;if(total>1024L*1024*1024)throw new Exception("安装包体积异常。");
                if(entry.FullName.EndsWith("/")){Directory.CreateDirectory(dest);continue;}
                Directory.CreateDirectory(Path.GetDirectoryName(dest));entry.ExtractToFile(dest,false);
            }
        }
    }
    public static string Unpack(Action<string> progress) {
        string stage=Path.Combine(Path.GetTempPath(),"QingcunSetup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try{
            progress("正在校验安装包…");
            var assembly=typeof(InstallCore).Assembly;
            string archive=Path.Combine(stage,"payload.zip"), expected;
            using(var input=assembly.GetManifestResourceStream("QingcunPayload.sha256"))using(var r=new StreamReader(input))expected=r.ReadToEnd().Trim().ToLowerInvariant();
            using(var input=assembly.GetManifestResourceStream("QingcunPayload.zip"))using(var output=File.Create(archive))input.CopyTo(output);
            if(Hash(archive)!=expected)throw new Exception("安装包校验失败，请重新复制完整文件。");
            progress("正在准备 Eagle 插件和浏览器扩展…");
            Extract(archive,Path.Combine(stage,"payload"));
            return stage;
        }catch{Cleanup(stage);throw;}
    }
    public static void Cleanup(string stage) {
        // Only our own freshly created, direct child of the system temp directory.
        string parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\');
        if(Path.GetDirectoryName(Path.GetFullPath(stage)).Equals(parent,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(stage).StartsWith("QingcunSetup-",StringComparison.Ordinal))
            try{Directory.Delete(stage,true);}catch{}
    }
    public static Dictionary<string,object> Ping(string host) {
        var info=new ProcessStartInfo(host,"--eagle-rpc"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(host),RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var process=Process.Start(info)){
            var output=Task.Run(()=>{using(var b=new MemoryStream()){process.StandardOutput.BaseStream.CopyTo(b);return b.ToArray();}});
            var error=process.StandardError.ReadToEndAsync();
            var request=Encoding.UTF8.GetBytes("{\"action\":\"ping\"}");process.StandardInput.BaseStream.Write(BitConverter.GetBytes(request.Length),0,4);process.StandardInput.BaseStream.Write(request,0,request.Length);process.StandardInput.Close();
            if(!process.WaitForExit(15000)){try{process.Kill();}catch{}throw new Exception("本地组件连接超时，请稍后重试。");}
            Task.WaitAll(output,error);
            byte[] data=output.Result;
            if(process.ExitCode!=0||data.Length<4||BitConverter.ToInt32(data,0)!=data.Length-4)throw new Exception("本地组件连接失败。");
            var result=Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(data,4,data.Length-4));
            if(!result.ContainsKey("ok")||!Convert.ToBoolean(result["ok"]))throw new Exception("本地组件没有正常启动。");
            return result;
        }
    }
    static void CheckUpgrade(string root,bool browser) {
        string host=Path.Combine(root,"native","QingcunHost.exe");
        // Do not replace a component while a download/analysis/preview process is using it.
        foreach(var process in Process.GetProcessesByName("QingcunHost"))using(process){
            string running="";try{running=process.MainModule.FileName;}catch{}
            if(String.Equals(running,host,StringComparison.OrdinalIgnoreCase))throw new Exception("轻存还有任务或预览正在运行。请完成任务、关闭预览及 Eagle 后再安装。");
        }
        if(File.Exists(host)){
            Dictionary<string,object> status=null;try{status=Ping(host);}catch{}
            if(status!=null&&status.ContainsKey("version")&&new Version(Convert.ToString(status["version"]))>new Version(Version))throw new Exception("这台电脑已经安装了更新版本，无需使用此安装包。");
        }
        string manifest=Path.Combine(root,"extension","manifest.json");
        if(browser&&File.Exists(manifest)){
            var m=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(manifest));
            if(new Version(Convert.ToString(m["version"]))>new Version(Version))throw new Exception("浏览器扩展已有更新版本，本次没有覆盖。");
        }
    }
    sealed class RegBackup {public string Path;public bool KeyExisted,HasValue;public object Value;public RegistryValueKind Kind;}
    // registryBase is empty for real installation. Tests use a separate HKCU subtree.
    public static string Install(string payload,string root,string registryBase,bool browser,Action<string> progress) {
        if(!Environment.Is64BitOperatingSystem)throw new Exception("此安装包适用于 64 位 Windows 10 / 11。");
        root=Path.GetFullPath(root);
        CheckUpgrade(root,browser);
        string package=Path.Combine(payload,PackageName), unpacked=Path.Combine(Path.GetDirectoryName(payload),"eagle");
        if(!Directory.Exists(unpacked))Extract(package,unpacked);
        string engine=Path.Combine(unpacked,"engine");
        foreach(string file in new[]{"QingcunHost.exe","extension-id.txt","tools\\yt-dlp.exe","tools\\ffmpeg.exe","tools\\ffprobe.exe","tools\\deno.exe"})
            if(!File.Exists(Path.Combine(engine,file)))throw new Exception("安装包缺少下载组件："+file);
        string manifestPath=Path.Combine(payload,"extension","manifest.json");
        var extension=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(manifestPath,Encoding.UTF8));
        string id=File.ReadAllText(Path.Combine(engine,"extension-id.txt")).Trim();
        if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-p]{32}$"))throw new Exception("浏览器扩展标识无效。");
        byte[] publicKey=Convert.FromBase64String(Convert.ToString(extension["key"]));
        using(var hash=SHA256.Create()){
            string computed=String.Concat(hash.ComputeHash(publicKey).Take(16).SelectMany(b=>new[]{(char)('a'+(b>>4)),(char)('a'+(b&15))}));
            if(computed!=id)throw new Exception("浏览器扩展与本地组件标识不一致。");
        }
        var changes=new List<KeyValuePair<string,string>>();var regs=new List<RegBackup>();
        string backup=Path.Combine(root,"backups","installer-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
        Action<string,string> copy=(src,dest)=>{
            if(File.Exists(dest)&&Hash(src)==Hash(dest))return;
            if(Directory.Exists(dest))throw new Exception("安装位置存在同名文件夹："+dest);
            string old=null;
            if(File.Exists(dest)){old=Path.Combine(backup,changes.Count.ToString("D4")+".bak");Directory.CreateDirectory(backup);File.Copy(dest,old);}
            changes.Add(new KeyValuePair<string,string>(dest,old));Directory.CreateDirectory(Path.GetDirectoryName(dest));
            string temporary=dest+".qingcun-install-"+Guid.NewGuid().ToString("N");
            try{File.Copy(src,temporary);if(File.Exists(dest))File.Replace(temporary,dest,null);else File.Move(temporary,dest);}finally{if(File.Exists(temporary))File.Delete(temporary);}
        };
        try{
            progress("正在安装本地下载组件…");
            foreach(var src in Directory.GetFiles(engine,"*",SearchOption.AllDirectories))copy(src,Inside(root,"native\\"+src.Substring(engine.Length+1)));
            string installedPackage=Inside(root,"installers\\"+Version+"\\"+PackageName);copy(package,installedPackage);
            if(browser){
                progress("正在准备 Chrome / Edge 扩展…");
                string source=Path.Combine(payload,"extension");
                foreach(var src in Directory.GetFiles(source,"*",SearchOption.AllDirectories))copy(src,Inside(root,"extension\\"+src.Substring(source.Length+1)));
                string hostManifest=Inside(root,"native\\host-manifest.json"), generated=Path.Combine(payload,"native-manifest.generated.json");
                File.WriteAllText(generated,Json.Serialize(new{name=HostName,description="Qingcun Video Downloader",path=Path.Combine(root,"native","QingcunHost.exe"),type="stdio",allowed_origins=new[]{"chrome-extension://"+id+"/"}}),new UTF8Encoding(false));copy(generated,hostManifest);
                foreach(string vendor in new[]{"Google\\Chrome","Microsoft\\Edge"}){
                    string keyPath=registryBase+"Software\\"+vendor+"\\NativeMessagingHosts\\"+HostName;
                    var state=new RegBackup{Path=keyPath};
                    using(var previous=Registry.CurrentUser.OpenSubKey(keyPath)){state.KeyExisted=previous!=null;if(previous!=null){state.HasValue=previous.GetValueNames().Contains("");if(state.HasValue){state.Value=previous.GetValue("");state.Kind=previous.GetValueKind("");}}}
                    regs.Add(state);using(var key=Registry.CurrentUser.CreateSubKey(keyPath))key.SetValue("",hostManifest,RegistryValueKind.String);
                }
            }
            string guide=File.ReadAllText(Path.Combine(payload,"安装说明.html"),Encoding.UTF8).Replace("{{EXTENSION_PATH}}",HttpUtility.HtmlEncode(Path.Combine(root,"extension")));
            string generatedGuide=Path.Combine(payload,"guide.generated.html");File.WriteAllText(generatedGuide,guide,new UTF8Encoding(false));copy(generatedGuide,Inside(root,"安装说明.html"));
            copy(Path.Combine(payload,"实测范围.txt"),Inside(root,"实测范围.txt"));
            progress("正在检测下载组件连接…");
            var response=Ping(Path.Combine(root,"native","QingcunHost.exe"));
            if(Convert.ToString(response["version"])!=Version||!response.ContainsKey("eagleRpc")||!Convert.ToBoolean(response["eagleRpc"]))throw new Exception("组件版本验证未通过。");
            if(changes.Any(c=>c.Value!=null)){Directory.CreateDirectory(backup);File.WriteAllText(Path.Combine(backup,"files.json"),Json.Serialize(changes),new UTF8Encoding(false));}
            return installedPackage;
        }catch(Exception original){
            var rollbackErrors=new List<string>();
            foreach(var state in Enumerable.Reverse(regs))try{using(var key=Registry.CurrentUser.OpenSubKey(state.Path,true)){if(key!=null){if(state.HasValue)key.SetValue("",state.Value,state.Kind);else key.DeleteValue("",false);}}
                if(!state.KeyExisted)using(var key=Registry.CurrentUser.OpenSubKey(state.Path)){if(key!=null&&key.ValueCount==0&&key.SubKeyCount==0){key.Close();Registry.CurrentUser.DeleteSubKey(state.Path,false);}}
            }catch(Exception e){rollbackErrors.Add(e.Message);}
            foreach(var change in Enumerable.Reverse(changes))try{if(change.Value!=null)File.Copy(change.Value,change.Key,true);else if(File.Exists(change.Key))File.Delete(change.Key);}catch(Exception e){rollbackErrors.Add(e.Message);}
            if(rollbackErrors.Count>0)throw new Exception(original.Message+"\r\n部分文件未能恢复，备份保存在："+backup,original);
            throw;
        }
    }
}

public class SetupWindow : Form {
    readonly Color Green=Color.FromArgb(39,87,70);
    Label status,detail;
    CheckBox browser;
    Button install;
    ProgressBar progress;
    FlowLayoutPanel actions;
    bool busy;
    string installedPackage, root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QingcunVideo");
    public SetupWindow() {
        Text="轻存 · 一体安装 2.3.14";ClientSize=new Size(650,570);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;BackColor=Color.FromArgb(246,247,242);Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        AddLabel("轻存",30,24,580,52,29,Green);
        AddLabel("把参考片带到下一台电脑",33,81,580,35,16,Color.FromArgb(32,43,39));
        AddLabel("Eagle 插件 + 本地下载组件 + 可选浏览器扩展",34,123,580,28,10,Color.DimGray);
        var card=new Panel{Left=30,Top=173,Width=590,Height=125,BackColor=Color.White};Controls.Add(card);
        var title=new Label{Text="Eagle 是主入口",Left=18,Top=15,AutoSize=true,Font=new Font(Font,FontStyle.Bold)};card.Controls.Add(title);
        card.Controls.Add(new Label{Text="下载整条视频，自动分类并进入 Eagle。",Left=18,Top=46,Width=550,Height=26});
        browser=new CheckBox{Text="同时准备 Chrome / Edge 扩展（方便从网页下载）",Left=18,Top=84,Width=555,Height=28,Checked=true};card.Controls.Add(browser);
        status=AddLabel("准备就绪",32,318,587,28,12,Green);
        detail=AddLabel("适用于 64 位 Windows 10 / 11，Eagle 4 或更新版本。\r\n不需要管理员权限；已有视频、标签和个人设置会保留。",33,354,583,54,9,Color.DimGray);
        progress=new ProgressBar{Left=33,Top=419,Width=584,Height=7,Visible=false,Style=ProgressBarStyle.Marquee};Controls.Add(progress);
        install=new Button{Text="开始安装",Left=33,Top=450,Width=584,Height=48,BackColor=Green,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};install.FlatAppearance.BorderSize=0;install.Click+=async(s,e)=>await RunInstall();Controls.Add(install);
        actions=new FlowLayoutPanel{Left=30,Top=444,Width=594,Height=112,Visible=false,WrapContents=true};Controls.Add(actions);
        var link=new LinkLabel{Text="还没有 Eagle？打开官方下载页",Left=33,Top=521,Width=420,Height=25,LinkColor=Green};link.LinkClicked+=(s,e)=>Open("https://eagle.cool/download/");Controls.Add(link);link.Name="downloadLink";
        FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
    }
    Label AddLabel(string text,int x,int y,int width,int height,float size,Color color) {
        var l=new Label{Text=text,Left=x,Top=y,Width=width,Height=height,Font=new Font("Microsoft YaHei UI",size),ForeColor=color};Controls.Add(l);return l;
    }
    void ActionButton(string text,Action action,int width) {
        var b=new Button{Text=text,Width=width,Height=37,BackColor=Color.White,FlatStyle=FlatStyle.Flat,Margin=new Padding(3,3,3,5)};b.FlatAppearance.BorderColor=Color.FromArgb(206,218,208);b.Click+=(s,e)=>action();actions.Controls.Add(b);
    }
    void Open(string file) {try{Process.Start(new ProcessStartInfo(file){UseShellExecute=true});}catch{MessageBox.Show(this,"未能打开，请通过安装说明手动打开。\r\n"+file,"轻存",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    public static string BrowserPath(bool edge) {
        string name=edge?"msedge.exe":"chrome.exe";
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})using(var key=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+name)){
            string p=key==null?"":Convert.ToString(key.GetValue("")).Trim('"');if(File.Exists(p))return p;
        }
        string suffix=edge?@"Microsoft\Edge\Application\msedge.exe":@"Google\Chrome\Application\chrome.exe";
        foreach(string baseDir in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}){
            string p=Path.Combine(baseDir,suffix);if(File.Exists(p))return p;
        }
        return "";
    }
    void OpenBrowser(bool edge) {
        string path=BrowserPath(edge);
        if(path==""){MessageBox.Show(this,"没有检测到"+(edge?" Edge":" Chrome")+"。可以使用另一个浏览器，或先安装浏览器。","轻存");return;}
        try{Clipboard.SetText(Path.Combine(root,"extension"));Process.Start(new ProcessStartInfo(path,edge?"edge://extensions":"chrome://extensions"){UseShellExecute=true});}
        catch(Exception e){MessageBox.Show(this,e.Message,"轻存");}
    }
    bool TryOpenEagle() {
        using(var key=Registry.ClassesRoot.OpenSubKey(".eagleplugin")){
            string type=key==null?"":Convert.ToString(key.GetValue(""));
            using(var command=String.IsNullOrEmpty(type)?null:Registry.ClassesRoot.OpenSubKey(type+@"\shell\open\command")){
                if(command==null)return false;
            }
        }
        try{Process.Start(new ProcessStartInfo(installedPackage){UseShellExecute=true});return true;}catch{return false;}
    }
    async Task RunInstall() {
        busy=true;install.Enabled=false;browser.Enabled=false;progress.Visible=true;
        string stage=null;bool withBrowser=browser.Checked;
        Action<string> update=text=>BeginInvoke((Action)(()=>status.Text=text));
        try{
            installedPackage=await Task.Run(()=>{
                stage=InstallCore.Unpack(update);
                return InstallCore.Install(Path.Combine(stage,"payload"),root,"",withBrowser,update);
            });
            bool opened=TryOpenEagle();
            ShowComplete(withBrowser,opened);
        }catch(Exception e){status.Text="安装未完成";detail.Text=e.Message;install.Enabled=true;browser.Enabled=true;MessageBox.Show(this,e.Message,"轻存 · 安装未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally{progress.Visible=false;busy=false;}
        if(stage!=null)await Task.Run(()=>InstallCore.Cleanup(stage));
    }
    public void ShowComplete(bool withBrowser,bool opened) {
        status.Text="组件已就绪 · 请完成最后"+(withBrowser?"两步":"一步");
        detail.Text=(opened?"1. 已打开 Eagle 安装提示，请点击“安装”。":"1. 先安装 Eagle，再点击下方“安装 Eagle 插件”。")+"\r\n"+(withBrowser?"2. 浏览器开启开发者模式 → 加载已解压的扩展程序。":"打开 Eagle 的插件面板，进入“轻存 · 参考片入库”。");
        install.Visible=false;Controls["downloadLink"].Visible=false;actions.Visible=true;
        ActionButton("安装 Eagle 插件",()=>{if(!TryOpenEagle()){Open(Path.GetDirectoryName(installedPackage));MessageBox.Show(this,"请先安装 Eagle 4 或更新版本，再双击文件夹中的插件包。","轻存");}},184);
        if(withBrowser){
            ActionButton("打开 Chrome",()=>OpenBrowser(false),184);ActionButton("打开 Edge",()=>OpenBrowser(true),184);
            ActionButton("复制扩展文件夹",()=>{Clipboard.SetText(Path.Combine(root,"extension"));status.Text="路径已复制 · 加载扩展时粘贴到文件夹地址栏";},279);
            ActionButton("打开安装说明",()=>Open(Path.Combine(root,"安装说明.html")),279);
        }else ActionButton("打开安装说明",()=>Open(Path.Combine(root,"安装说明.html")),375);
    }
}
public static class EntryPoint {
    [STAThread] public static void Main() {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(!Environment.Is64BitOperatingSystem){MessageBox.Show("此安装包适用于 64 位 Windows 10 / 11。","轻存");return;}
        bool created;using(var mutex=new System.Threading.Mutex(true,@"Local\QingcunVideoInstaller",out created)){
            if(!created){MessageBox.Show("轻存安装程序已经打开。","轻存");return;}
            Application.Run(new SetupWindow());
        }
    }
}
