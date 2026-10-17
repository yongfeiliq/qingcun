const fs=require('fs'),path=require('path');const src=path.resolve('视频下载助手/native'),dest=path.resolve('Mac/generated');fs.mkdirSync(dest,{recursive:true});
const replace=(s,a,b)=>{if(!s.includes(a))throw Error('Missing Mac transform: '+a.slice(0,90));return s.replace(a,b);};
for(const name of fs.readdirSync(src).filter(n=>n.endsWith('.cs'))){let s=fs.readFileSync(path.join(src,name),'utf8');
s=s.replaceAll('System.Security.Principal.WindowsIdentity.GetCurrent().User.Value','UserKey()').replaceAll('"Local\\\\Qingcun','"Qingcun');
s=s.replace(/"(QingcunHost|ffmpeg|yt-dlp|deno|codex|test-codex)\.exe"/g,'ExecutableName("$1")').replace(/UseShellExecute=true,WindowStyle=(?:System.Diagnostics.)?ProcessWindowStyle.Hidden/g,'UseShellExecute=false');
if(name==='Host.cs'){s=replace(s,'Process.Start(new ProcessStartInfo("explorer.exe",Quote(f)){UseShellExecute=true});','MacOpen(f);');s=replace(s,'Directory.CreateDirectory(Jobs);','Directory.CreateDirectory(Jobs);if(OperatingSystem.IsMacOS())File.SetUnixFileMode(Data,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);');}
if(name==='Library.cs')s=replace(s,'Process.Start(new ProcessStartInfo("explorer.exe","/select,"+Quote(p)){UseShellExecute=true});','MacOpen(p,true);');
if(name==='Subtitles.cs'){s=replace(s,'Process.Start(new ProcessStartInfo("explorer.exe","/select,"+Quote(S(file,"path"))){UseShellExecute=true});','MacOpen(S(file,"path"),true);');s=s.replace(/try\{using\(var killer=Process.Start\(new ProcessStartInfo\("taskkill.exe"[\s\S]*?catch\{try\{p.Kill\(\);\}catch\{\}\}/,'try{p.Kill(true);}catch{}');}
if(name==='VimeoAuth.cs')s=s.replace(/static string VimeoVault\(\)\{[^\n]*\}/,'static string VimeoVault(){string dir=Path.Combine(Data,"vimeo-private");Directory.CreateDirectory(dir);if(OperatingSystem.IsMacOS())File.SetUnixFileMode(dir,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);return dir;}');
if(name==='Analysis.cs')s=replace(s,'using(var picture=System.Drawing.Image.FromFile(temp)){if(picture.Width<=0||picture.Height<=0)continue;}','if(!PngValid(temp))continue;');
if(name==='ImageAnalysis.cs')s=s.replace(/using\(var source=System.Drawing.Image.FromFile\(png\)\)[\s\S]*?canvas.Save\(temp,System.Drawing.Imaging.ImageFormat.Jpeg\);\s*\}/,'MacJpeg(png,temp);');
if(name==='EagleVideos.cs')s=s.replace(/try\{using\(var pic=System.Drawing.Image.FromFile\(file\)\)[\s\S]*?catch\{\}/,'');
if(name==='CodexAnalysis.cs'){
s=s.replace('using System.Drawing;','').replace('using System.Drawing.Imaging;','');
s=replace(s,'string bin=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");','foreach(string candidate in new[]{"/Applications/Codex.app/Contents/Resources/codex","/opt/homebrew/bin/codex","/usr/local/bin/codex",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".local/bin/codex")})if(File.Exists(candidate))return candidate;\n        string bin=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");');
const start=s.indexOf('            const int width=320'),end=s.indexOf('            var schema=',start);if(start<0||end<0)throw Error('sheet marker');
s=s.slice(0,start)+`            for(int page=0;page*12<all.Length;page++){
                string sheet=Path.Combine(dir,"sheet-"+page.ToString("D2")+".jpg");created.Add(sheet);var frames=new List<string>();var labels=new List<string>();
                for(int slot=0;slot<12;slot++){int n=page*12+slot;if(n>=all.Length)break;CheckAnalysisEnabled(job);string frame=Path.Combine(dir,"frame-"+n+".png");created.Add(frame);double actual=ExtractFramePng(S(file,"path"),all[n],duration,frame,320,180);if(Math.Abs(actual-all[n])>.001)adjusted++;string label="#"+(n+1).ToString("D3")+" "+(actual-start).ToString("0.000",CultureInfo.InvariantCulture)+"s"+(actual<start?" CONTEXT":"");frames.Add(frame);labels.Add(label);map.Append(label+"; ");}
                MacSheet(frames.ToArray(),labels.ToArray(),sheet);images.Add(sheet);job["stage"]="frames";job["message"]="正在准备带时间标记的抽帧";Write(AnalysisJobPath(S(job,"id")),job);
            }
`+s.slice(end);
}
if(name==='EagleStorage.cs')s=s.replace('String.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase)','String.Equals(Path.GetFullPath(a),Path.GetFullPath(b),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)');
if(name==='Brands.cs')s=s.replace('full.StartsWith(root,StringComparison.OrdinalIgnoreCase)','full.StartsWith(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)');
if(/System\.Drawing|explorer\.exe|taskkill\.exe|WindowsIdentity/.test(s))throw Error('Unported dependency in '+name);
if(name==='Host.cs')s=replace(s,'            Directory.CreateDirectory(Jobs);','            if(args.Length==1&&args[0]=="--mac-self-test")return MacSelfTest();\n            if(args.Length==4&&args[0]=="--mac-install")return MacInstall(args[1],args[2],args[3]=="browser");\n            Directory.CreateDirectory(Jobs);');
if(name==='Host.cs'){
 s=replace(s,'[STAThread] static int Main(string[] args)','[STAThread] internal static int Main(string[] args)');
 s=replace(s,'} catch {return 1;}','} catch(Exception e) {Console.Error.WriteLine(e);return 1;}');
 s=replace(s,'"version","2.3.3","xpcBrowserBridge"','"version","2.3.3","macInstallRevision",3,"xpcBrowserBridge"');
}
fs.writeFileSync(path.join(dest,name),s);
}
console.log('Generated Mac backend from shared source');
