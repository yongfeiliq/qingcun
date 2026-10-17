from pathlib import Path
import zipfile, gzip, json, shutil, hashlib, struct, plistlib, stat, datetime, subprocess, tarfile
from macho_signatures import inspect_signature
ROOT=Path(__file__).resolve().parent.parent
BUILD=ROOT/'tmp/release-2.3.3-mac-r1'; DOWNLOAD=ROOT/'tmp/release-2.3.1/downloads'; OUTPUT=ROOT/'分发安装包'; OUTPUT.mkdir(exist_ok=True)
PACKAGE_BUILD=ROOT/'tmp/release-2.3.3-mac-r1'; PACKAGE_BUILD.mkdir(parents=True,exist_ok=True)
STAGE=PACKAGE_BUILD/'mac-package'; PLUGIN=STAGE/'plugin'; PLUGIN.mkdir(parents=True,exist_ok=True)
def digest(p):
    h=hashlib.sha256()
    with open(p,'rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
    return h.hexdigest()
def copy(a,b):
    b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(a,b)
def macho(p):
    b=p.read_bytes(); magic=b[:4]
    if magic==b'\xca\xfe\xba\xbe':
        count=struct.unpack_from('>I',b,4)[0];return {'fat':True,'cpus':[struct.unpack_from('>I',b,8+i*20)[0] for i in range(count)]}
    if magic!=b'\xcf\xfa\xed\xfe': raise ValueError(f'Not Mach-O: {p}')
    cpu=struct.unpack_from('<I',b,4)[0];n=struct.unpack_from('<I',b,16)[0];at=32;deps=[];minimum=None
    for _ in range(n):
        cmd,size=struct.unpack_from('<II',b,at)
        if cmd in [0xc,0x80000018,0x8000001f]:
            off=struct.unpack_from('<I',b,at+8)[0];deps.append(b[at+off:at+size].split(b'\0')[0].decode())
        if cmd==0x32: minimum=struct.unpack_from('<I',b,at+12)[0]
        if cmd==0x24: minimum=struct.unpack_from('<I',b,at+8)[0]
        at+=size
    return {'cpu':cpu,'minimum':None if minimum is None else f'{minimum>>16}.{(minimum>>8)&255}.{minimum&255}','dependencies':deps}
def zipped(source,dest):
    with zipfile.ZipFile(dest,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=5,allowZip64=True) as z:
        for p in sorted(source.rglob('*')):
            if not p.is_file(): continue
            rel=p.relative_to(source).as_posix()
            executable=p.suffix in ['.command','.sh','.dylib'] or p.name in ['QingcunHost','createdump','dotnet','yt-dlp','ffmpeg','ffprobe','deno','install-qingcun']
            info=zipfile.ZipInfo(rel);info.create_system=3;info.external_attr=(stat.S_IFREG|(0o755 if executable else 0o644))<<16;info.compress_type=zipfile.ZIP_STORED if p.suffix=='.eagleplugin' else zipfile.ZIP_DEFLATED
            with p.open('rb') as inp,z.open(info,'w',force_zip64=True) as out: shutil.copyfileobj(inp,out,1024*1024)
for name in ['manifest.json','index.html','app.js','native.cjs','sync.cjs','folders.cjs','storage.cjs','presentation.cjs','eagle-adapter.cjs','existing.cjs','existing-ui.js','style.css','logo.png']:
    copy(ROOT/'轻存-Eagle插件'/name,PLUGIN/name)
shutil.copytree(ROOT/'轻存-Eagle插件/views',PLUGIN/'views',dirs_exist_ok=True)
shutil.copytree(ROOT/'视频下载助手/extension',PLUGIN/'extension',dirs_exist_ok=True)
m=json.loads((PLUGIN/'manifest.json').read_text(encoding='utf-8-sig'));m.update(platform='mac',arch='all',version='2.3.3');(PLUGIN/'manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8')
for p in list(PLUGIN.glob('*.cjs')):
    s=p.read_text(encoding='utf-8-sig').replace("p=>path.resolve(p||'.').toLowerCase()","p=>path.resolve(p||'.')")
    if p.name=='native.cjs':
        assert 'if(r.categorySchema>=1&&' in s
        s=s.replace('if(r.categorySchema>=1&&','if(r.macInstallRevision>=3&&r.categorySchema>=1&&')
    if p.name=='storage.cjs':s=s.replace('f.path.toLowerCase()===q.sourcePath.toLowerCase()','path.resolve(f.path)===path.resolve(q.sourcePath)').replace("'use strict';","'use strict';const path=require('path');")
    p.write_text(s,encoding='utf-8')
for p in list((PLUGIN/'views').glob('*.js'))+list((PLUGIN/'extension').glob('*.js')):
    s=p.read_text(encoding='utf-8-sig').replace('Install.cmd','Mac 安装器').replace('当前 Windows 用户','当前 Mac 用户');p.write_text(s,encoding='utf-8')
for p in [PLUGIN/'help.html',PLUGIN/'extension/help.html',PLUGIN/'views/help.html']:copy(ROOT/'Mac/安装说明.html',p)
licenses=PLUGIN/'licenses';licenses.mkdir(exist_ok=True)
for name in ['DENO-LICENSE.txt','GPL-3.0.txt','YT-DLP-LICENSE.txt']:copy(ROOT/'视频下载助手'/name,licenses/name)
copy(DOWNLOAD/'lock.json',licenses/'dependency-downloads.json')
(licenses/'THIRD-PARTY.txt').write_text('''Qingcun 2.3.3 macOS preview: bundled standalone tools and runtimes.
.NET 8 runtime (MIT): https://github.com/dotnet/runtime/tree/v8.0.28
Deno (MIT): https://github.com/denoland/deno/tree/v2.9.7
  Includes upstream notices in the DENO-LICENSE.txt file.
yt-dlp 2026.08.19 (Unlicense; bundled dependencies have separate licenses): https://github.com/yt-dlp/yt-dlp/releases/tag/2026.08.19
FFmpeg / FFprobe (LGPL/GPL as configured): https://github.com/eugeneware/ffmpeg-static/releases/tag/b6.1.1
  Build scripts/source references: https://github.com/eugeneware/ffmpeg-static
  FFmpeg source: https://ffmpeg.org/releases/ffmpeg-6.1.1.tar.xz
Runtime components are separate executables. Native runtime and tools are not Windows executables.
Original publisher metadata and SHA-256/SHA-512 values are in dependency-downloads.json.
''',encoding='utf-8')
runtime_cache=ROOT/'tmp/mac-official-runtime-8.0.28'
runtime_lock=json.loads((runtime_cache/'lock.json').read_text(encoding='utf-8-sig'))
copy(runtime_cache/'lock.json',licenses/'microsoft-runtime-downloads.json')
report={}
signer_cache=ROOT/'tmp/release-2.3.2-mac-r2/build-tools'
signer=signer_cache/'rcodesign.exe'
signer_lock=json.loads((signer_cache/'lock.json').read_text(encoding='utf-8-sig'))
assert digest(signer_cache/signer_lock['name'])==signer_lock['sha256']
with zipfile.ZipFile(signer_cache/signer_lock['name']) as sz:
    signer_entry=next(n for n in sz.namelist() if n.endswith('/rcodesign.exe') or n=='rcodesign.exe')
    assert hashlib.sha256(sz.read(signer_entry)).hexdigest()==digest(signer)
assert signer.exists(),'Missing verified build-time signer'
for arch,cpu,target in [('arm64',0x100000c,'aarch64'),('x64',0x1000007,'x86_64')]:
    engine=PLUGIN/'engine'/arch;shutil.copytree(BUILD/('mac-'+arch),engine,dirs_exist_ok=True)
    if (engine/'data').exists():raise ValueError('runtime contains data')
    assert not (BUILD/('mac-'+arch)/'QingcunHost').exists(),'Custom apphost must not be shipped'
    published={x.name for x in (BUILD/('mac-'+arch)).iterdir() if x.is_file()}
    assert published=={'QingcunHost.dll','QingcunHost.deps.json','QingcunHost.runtimeconfig.json'},published
    (engine/'QingcunHost').write_bytes((ROOT/'Mac/QingcunHost.sh').read_bytes().replace(b'\r\n',b'\n'))
    entry=next(x for x in runtime_lock['files'] if x['rid']=='osx-'+arch)
    archive=runtime_cache/entry['url'].split('/')[-1]
    assert hashlib.sha512(archive.read_bytes()).hexdigest()==entry['hash']
    runtime=engine/'runtime';runtime.mkdir(exist_ok=True)
    with tarfile.open(archive,'r:gz') as tar:
        for member in tar.getmembers():
            rel=Path(member.name)
            assert not rel.is_absolute() and '..' not in rel.parts and not member.issym() and not member.islnk(),member.name
            if member.isdir(): (runtime/rel).mkdir(parents=True,exist_ok=True)
            else:
                assert member.isfile(),member.name
                dst=runtime/rel;dst.parent.mkdir(parents=True,exist_ok=True)
                with tar.extractfile(member) as inp,dst.open('wb') as out:shutil.copyfileobj(inp,out)
    runtime_original={x.relative_to(runtime).as_posix():digest(x) for x in runtime.rglob('*') if x.is_file()}
    tools=engine/'tools';tools.mkdir(exist_ok=True);copy(ROOT/'视频下载助手/native/extension-id.txt',engine/'extension-id.txt')
    copy(DOWNLOAD/'yt-dlp_macos',tools/'yt-dlp')
    for tool in ['ffmpeg','ffprobe']:
        with gzip.open(DOWNLOAD/f'{tool}-darwin-{arch}.gz','rb') as f,(tools/tool).open('wb') as out:shutil.copyfileobj(f,out)
    with zipfile.ZipFile(DOWNLOAD/f'deno-{target}-apple-darwin.zip') as z:(tools/'deno').write_bytes(z.read('deno'))
    for name in [f'darwin-{arch}.LICENSE',f'darwin-{arch}.README']:copy(DOWNLOAD/name,licenses/name)
    for candidate in engine.rglob('*'):
        if not candidate.is_file():continue
        with candidate.open('rb') as probe: magic=probe.read(4)
        if magic not in [b'\xcf\xfa\xed\xfe',b'\xca\xfe\xba\xbe']:continue
        signature=inspect_signature(candidate.read_bytes())
        if candidate.is_relative_to(runtime):assert signature.get('signed'),'Official runtime signature is missing'
        if not signature.get('fat') and not signature.get('signed'):
            identifier='com.qingcun.host' if candidate.name=='QingcunHost' else 'com.qingcun.tool.'+candidate.name
            command=[str(signer),'sign','--config-file','/dev/null','--timestamp-url','none','--binary-identifier',identifier]
            if candidate.name=='QingcunHost':command+=['--entitlements-xml-file','Mac/entitlements.plist']
            result=subprocess.run(command+[str(candidate)],cwd=ROOT,capture_output=True,text=True,encoding='utf-8',errors='replace')
            if result.returncode:raise RuntimeError(result.stderr)
            signature=inspect_signature(candidate.read_bytes())
        slices=signature.get('slices',[signature]);assert all(i.get('signed') for i in slices),candidate
        if candidate==runtime/'dotnet':
            assert signature['entitlements'].get('com.apple.security.cs.allow-jit') is True
            assert all(not d['flags']&2 for d in signature['directories']),'Official dotnet must retain publisher signature'
        report[arch+'/signature/'+candidate.relative_to(engine).as_posix()]=signature
    for rel,expected in runtime_original.items():assert digest(runtime/rel)==expected,'Official runtime bytes changed: '+rel
    report[arch+'/runtime-original']={'files':len(runtime_original),'unchanged':True,'version':runtime_lock['runtimeVersion'],'rid':entry['rid'],'archiveSHA512':entry['hash']}
    for name in ['runtime/dotnet','tools/yt-dlp','tools/ffmpeg','tools/ffprobe','tools/deno']:
        info=macho(engine/name)
        if info.get('fat'):assert cpu in info['cpus']
        else:assert info['cpu']==cpu
        for dep in info.get('dependencies',[]):assert dep.startswith(('/usr/lib/','/System/Library/','@rpath','@loader_path','@executable_path')),dep
        report[arch+'/'+name]=info
# Never include test harnesses or user state.
for p in PLUGIN.rglob('*'):
    if p.is_file():
        assert not set(p.relative_to(PLUGIN).parts)&{'data','tmp','backups','profiles','node_modules'}
        assert p.name not in ['settings.json','state.json','cookies.json','auth.json','.local-secret-key']
        assert p.suffix.lower() not in ['.bin','.log','.mp4','.mov','.mkv','.pem']
checks={p.relative_to(PLUGIN).as_posix():digest(p) for p in sorted(PLUGIN.rglob('*')) if p.is_file() and p.name!='integrity.json'}
(PLUGIN/'integrity.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
package=STAGE/'轻存-Eagle-v2.3.3-Mac.eagleplugin';zipped(PLUGIN,package)
release=STAGE/'release';app=release/'轻存安装器.app';resources=app/'Contents/Resources';resources.mkdir(parents=True,exist_ok=True)
copy(package,resources/package.name)
copy(ROOT/'Mac/安装说明.html',resources/'安装说明.html')
(resources/'verify-payload.sh').write_bytes((ROOT/'Mac/verify-payload.sh').read_bytes().replace(b'\r\n',b'\n'))
launcher=(ROOT/'Mac/launcher.sh').read_text(encoding='utf-8-sig').replace('\r\n','\n')
launcher=launcher.replace('app.displayDialog(a[0],','app.displayDialog(a[0].split("\\\\n").join("\\n"),')
execpath=app/'Contents/MacOS/install-qingcun';execpath.parent.mkdir(parents=True,exist_ok=True);execpath.write_text(launcher,encoding='utf-8',newline='\n')
(resources/'payload.sha256').write_bytes((digest(package)+' *'+package.name+'\n').encode('utf-8'))
plist={'CFBundleIdentifier':'com.qingcun.installer.mac','CFBundleName':'轻存安装器','CFBundleDisplayName':'轻存安装器','CFBundleVersion':'2.3.3.1','CFBundleShortVersionString':'2.3.3','CFBundleExecutable':'install-qingcun','CFBundlePackageType':'APPL','LSUIElement':True,'LSMinimumSystemVersion':'13.0','NSHighResolutionCapable':True}
(app/'Contents/Info.plist').write_bytes(plistlib.dumps(plist))
copy(ROOT/'Mac/先看这里.txt',release/'先看这里.txt');copy(ROOT/'Mac/安装说明.html',release/'安装说明.html')
out=OUTPUT/'轻存一体安装-v2.3.3-Mac通用-修复1-预览版.zip';zipped(release,out)
(out.with_name(out.name+'.sha256')).write_bytes((digest(out)+' *'+out.name+'\n').encode('utf-8'))
report.update(result='PASS',macExecuted=False,package=str(out),bytes=out.stat().st_size,sha256=digest(out),files=len(checks),minimumOS='13.0')
(PACKAGE_BUILD/'mac-package-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'package':str(out),'bytes':out.stat().st_size,'files':len(checks),'sha256':digest(out)},ensure_ascii=False))
