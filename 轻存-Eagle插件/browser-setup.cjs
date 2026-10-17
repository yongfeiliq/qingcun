'use strict';
const fs=require('fs'),path=require('path'),os=require('os'),crypto=require('crypto');
const extensionId='ikbihjphodgkkollcgcocjibbgkehldo';
function noLinks(file){
 for(let p=path.resolve(file);;p=path.dirname(p)){
  try{if(fs.lstatSync(p).isSymbolicLink())throw Error('浏览器助手安装位置包含链接，请使用默认用户目录。');}catch(e){if(e.code!=='ENOENT')throw e;}
  if(path.dirname(p)===p)break;
 }
}
function compareVersion(a,b){const x=a.split('.').map(Number),y=b.split('.').map(Number);for(let i=0;i<Math.max(x.length,y.length);i++){const d=(x[i]||0)-(y[i]||0);if(d)return d;}return 0;}
function manifest(dir){
 const m=JSON.parse(fs.readFileSync(path.join(dir,'manifest.json'),'utf8'));
 const id=crypto.createHash('sha256').update(Buffer.from(m.key||'','base64')).digest('hex').slice(0,32).replace(/[0-9a-f]/g,c=>String.fromCharCode(97+parseInt(c,16)));
 if(id!==extensionId||!/^\d+(\.\d+){1,3}$/.test(m.version))throw Error('浏览器助手标识或版本校验失败。');
 return m;
}
// Prepare only Qingcun's files and the documented native-messaging registration.
// Chrome owns extension installation, reload and website-access confirmation.
function prepareMacBrowser(pluginPath,{support=path.join(os.homedir(),'Library/Application Support'),base=path.join(support,'QingcunVideo')}={}){
 const source=path.join(pluginPath,'extension'),target=path.join(base,'extension');
 noLinks(source);noLinks(target);
 const incoming=manifest(source),existing=fs.existsSync(path.join(target,'manifest.json'))?manifest(target):null;
 const files=[];
 function collect(dir,relative=''){
  for(const entry of fs.readdirSync(dir,{withFileTypes:true})){
   const rel=path.join(relative,entry.name),p=path.join(dir,entry.name);
   if(entry.isSymbolicLink())throw Error('浏览器助手包包含目录链接。');
   if(entry.isDirectory())collect(p,rel);else if(entry.isFile())files.push([path.join(target,rel),fs.readFileSync(p)]);
  }
 }
 if(!existing||compareVersion(incoming.version,existing.version)>=0)collect(source);
 const registration=Buffer.from(JSON.stringify({name:'com.qingcun.video_downloader',description:'轻存视频下载组件',path:path.join(base,'native/QingcunHost'),type:'stdio',allowed_origins:['chrome-extension://'+extensionId+'/']},null,2));
 for(const vendor of ['Google/Chrome','Microsoft Edge'])files.push([path.join(support,vendor,'NativeMessagingHosts/com.qingcun.video_downloader.json'),registration]);
 const guide=path.join(pluginPath,'help.html');if(fs.existsSync(guide))files.push([path.join(base,'安装说明.html'),fs.readFileSync(guide)]);
 for(const [file] of files)noLinks(file);
 let changed=0;
 for(const [file,bytes] of files){
  if(fs.existsSync(file)&&fs.readFileSync(file).equals(bytes))continue;
  fs.mkdirSync(path.dirname(file),{recursive:true});
  const tmp=file+'.qingcun-'+crypto.randomBytes(8).toString('hex');
  try{fs.writeFileSync(tmp,bytes,{flag:'wx',mode:0o600});fs.renameSync(tmp,file);}finally{if(fs.existsSync(tmp))fs.unlinkSync(tmp);}
  changed++;
 }
 return {version:existing&&compareVersion(existing.version,incoming.version)>0?existing.version:incoming.version,changed,extensionPath:target,requiresChromeConfirmation:true};
}
module.exports={prepareMacBrowser};
