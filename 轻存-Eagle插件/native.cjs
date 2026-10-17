'use strict';
const fs=require('fs'),path=require('path'),cp=require('child_process'),{pathToFileURL}=require('url');
const isMac=process.platform==='darwin';
const base=isMac?path.join(require('os').homedir(),'Library/Application Support/QingcunVideo'):path.join(process.env.LOCALAPPDATA,'QingcunVideo'),native=path.join(base,'native');
const hostName=isMac?'QingcunHost':'QingcunHost.exe';
function macArch(){try{if(cp.execFileSync('/usr/sbin/sysctl',['-n','hw.optional.arm64'],{encoding:'utf8'}).trim()==='1')return 'arm64';}catch{}return process.arch==='arm64'?'arm64':'x64';}
function executableFiles(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){if(entry.name==='data')continue;const p=path.join(dir,entry.name);if(entry.isDirectory())executableFiles(p);else if(entry.isFile()&&(!path.extname(entry.name)||entry.name.endsWith('.dylib')))fs.chmodSync(p,0o755);}}
function rpc(q,host=path.join(native,hostName)){
 return new Promise((resolve,reject)=>{
  const p=cp.spawn(host,['--eagle-rpc'],{windowsHide:true,cwd:path.dirname(host)});let chunks=[],size=0,done=false;
  const end=(err,val)=>{if(done)return;done=true;clearTimeout(timer);err?reject(err):resolve(val);};
  const timer=setTimeout(()=>{p.kill();end(Error('本地组件响应超时，请稍后重试。'));},180000);
  p.on('error',()=>end(Error('本地组件无法启动，请重新安装轻存 Eagle 插件。')));
  p.stdout.on('data',b=>{chunks.push(b);size+=b.length;if(size>40*1024*1024){p.kill();return end(Error('组件响应过大。'));}});
  p.on('close',code=>{try{const b=Buffer.concat(chunks);if(code!==0||b.length<4||b.readUInt32LE(0)!==b.length-4)throw Error('组件版本不兼容或已退出。');
    const r=JSON.parse(b.subarray(4).toString('utf8'));if(!r.ok)throw Error(r.error||'操作未完成');end(null,r);
   }catch(e){end(e);}});
  const b=Buffer.from(JSON.stringify(q)),h=Buffer.alloc(4);h.writeUInt32LE(b.length);p.stdin.on('error',()=>{});p.stdin.end(Buffer.concat([h,b]));
 });
}
function readAssets(){const dir=path.join(native,'data/library');if(!fs.existsSync(dir))return[];
 return fs.readdirSync(dir).filter(n=>n.endsWith('.json')).map(n=>{try{return JSON.parse(fs.readFileSync(path.join(dir,n),'utf8'));}catch{return null;}}).filter(Boolean);
}
function jobsBusy(){const dir=path.join(native,'data/jobs');if(!fs.existsSync(dir))return false;
 return fs.readdirSync(dir).filter(n=>n.endsWith('.json')).some(n=>{try{const j=JSON.parse(fs.readFileSync(path.join(dir,n),'utf8'));return ['queued','running'].includes(j.status);}catch{return false;}});
}
async function ensureEngine(pluginPath){
 const prepareBrowser=()=>{if(isMac)require(path.join(__dirname,'browser-setup.cjs')).prepareMacBrowser(pluginPath);};
 let current=false;const host=path.join(native,hostName);if(fs.existsSync(host)){try{const r=await rpc({action:'ping'});if((!isMac||r.macInstallRevision>=4)&&r.browserSessionSchema>=2&&r.platformAuthSchema>=2&&r.sourceLabelSchema>=3&&r.categorySchema>=3&&r.behanceUrlSchema>=1&&r.behanceBrowserBridge&&r.modelToggle&&r.eagleStorage&&r.xpcBrowserBridge&&r.filmTypeAnalysis&&r.eagleRpc&&String(r.version||'').split('.').reduce((v,n)=>v*1000+(Number(n)||0),0)>=2003014)current=true;}catch{}}
 if(current){prepareBrowser();return;}
 if(jobsBusy())throw Error('下载任务仍在运行，完成后请重新打开插件以更新组件。');
 const engine=isMac?path.join(pluginPath,'engine',macArch()):path.join(pluginPath,'engine');
 if(!fs.existsSync(path.join(engine,hostName)))throw Error('安装包缺少本地下载组件。');
 if(isMac){try{cp.execFileSync(path.join(engine,hostName),['--mac-runtime-check'],{timeout:30000,stdio:'pipe'});cp.execFileSync(path.join(engine,hostName),['--mac-self-test'],{timeout:140000,stdio:'pipe'});}catch{throw Error('Mac 运行时或下载工具未通过启动检查，请运行 Mac 安装器查看日志；原有组件和资料未更新。');}}
 fs.mkdirSync(native,{recursive:true});
 for(const name of fs.readdirSync(engine)){if(name==='data')continue;const from=path.join(engine,name),to=path.join(native,name);
  if(fs.statSync(from).isDirectory())fs.cpSync(from,to,{recursive:true});
  else{const temp=to+'.eagle-upgrade';fs.copyFileSync(from,temp);fs.renameSync(temp,to);}}
 if(isMac)executableFiles(native);
 await rpc({action:'ping'});
 prepareBrowser();
}
async function call(q){
 if(q.action==='imageInfo'){const r=await rpc(q);return {...r,image:pathToFileURL(r.previewPath).href};}
 if(q.action==='openPlayback'){const r=await rpc({action:'asset',id:q.id}),f=r.asset.files[q.index||0];
  if(!f||!fs.existsSync(f.path))throw Error('原视频已移动，请重新关联文件。');return{ok:true,url:pathToFileURL(f.path).href,session:''};}
 if(['closePlayback','keepPlayback'].includes(q.action))return{ok:true};
 return rpc(q);
}
module.exports={base,native,rpc,call,readAssets,jobsBusy,ensureEngine};
