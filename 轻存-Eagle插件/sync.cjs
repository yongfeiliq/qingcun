'use strict';
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const norm=p=>process.platform==='darwin'?path.resolve(p||'.'):path.resolve(p||'.').toLowerCase(),digest=s=>crypto.createHash('sha256').update(s).digest('hex');
const unique=a=>[...new Set(a.filter(x=>typeof x==='string'&&x.trim()).map(x=>x.trim()))];
function platform(raw){try{const h=new URL(raw).hostname.toLowerCase();for(const [domain,name]of [['youtube.com','YouTube'],['youtu.be','YouTube'],['instagram.com','Instagram'],['pinterest.com','Pinterest'],['pin.it','Pinterest'],['behance.net','Behance'],['vimeo.com','Vimeo'],['bilibili.com','bilibili'],['b23.tv','bilibili'],['douyin.com','抖音'],['tiktok.com','TikTok'],['xinpianchang.com','新片场']])if(h===domain||h.endsWith('.'+domain))return name;}catch{}return'';}
const clock=t=>{t=Math.max(0,Number(t)||0);return Math.floor(t/60)+':'+(t%60).toFixed(2).padStart(5,'0');};
const presentation=require('./presentation.cjs'),folderLayout=require('./folders.cjs');
function payload(a,f,key){
 const brand=typeof a.brand==='string'?a.brand:a.brand?.name||'';
 return {marker:'〔轻存资料:'+key+'〕',end:'〔/轻存资料〕',name:(a.title||path.basename(f.path,path.extname(f.path)))+(f.subtitleBurned?' [字幕版]':''),website:a.url||'',tags:presentation.meaningfulTags(a,f),annotation:presentation.annotation(a),folderNames:[folderLayout.ROOT,folderLayout.category(a.category),...(brand&&!presentation.noise(brand)?[brand]:[])]};
}
const mergeAnnotation=presentation.mergeAnnotation;
async function fileHash(file){const h=crypto.createHash('sha256');for await(const b of fs.createReadStream(file))h.update(b);return h.digest('hex');}
class Sync{
 constructor({api,statePath,readAssets,isBusy=()=>false,notify=()=>{},storage=null}){
  Object.assign(this,{api,statePath,readAssets,isBusy,notify,storage});this.busy=false;
  this.state={version:2,enabled:true,libraryPath:'',libraryName:'',entries:{},errors:[],libraries:{}};this.load();
 }
 load(){
  if(!fs.existsSync(this.statePath))return;
  try{const s=JSON.parse(fs.readFileSync(this.statePath,'utf8'));if(!s.entries||typeof s.entries!=='object'||Array.isArray(s.entries))throw Error();
   this.state={...s,version:2,libraries:s.libraries||{},errors:s.errors||[]};this.remember();
  }catch{throw Error('入库记录损坏，请保留记录并联系修复，已停止自动入库。');}
 }
 remember(){
  if(!this.state.libraryPath)return;
  const fields=['libraryPath','libraryName','entries','errors','classificationVersion','folderNamingVersion','presentationVersion','lastSync'];
  this.state.libraries[norm(this.state.libraryPath)]=Object.fromEntries(fields.map(k=>[k,this.state[k]]));
 }
 activate(library){
  this.remember();const prior=this.state.libraries[norm(library.path)]||{};
  for(const k of ['classificationVersion','folderNamingVersion','presentationVersion','lastSync'])delete this.state[k];
  Object.assign(this.state,prior,{libraryPath:library.path,libraryName:library.name||path.basename(library.path).replace(/\.library$/i,''),entries:prior.entries||{},errors:prior.errors||[]});
 }
 save(){this.remember();fs.mkdirSync(path.dirname(this.statePath),{recursive:true});const t=this.statePath+'.'+process.pid+'.tmp';fs.writeFileSync(t,JSON.stringify(this.state,null,2));fs.renameSync(t,this.statePath);}
 async bind(){const l=await this.api.library();if(!l.path)throw Error('请先在 Eagle 中打开资源库。');if(this.state.libraryPath&&norm(l.path)!==norm(this.state.libraryPath))throw Error('当前资源库不是已选择的“'+this.state.libraryName+'”，请点击“选择资源库”。');this.activate(l);this.save();}
 async guard(){const l=await this.api.library();if(!l.path||norm(l.path)!==norm(this.state.libraryPath))throw Error('等待打开 Eagle 资源库“'+this.state.libraryName+'”；也可点击“选择资源库”更改入库位置。');}
 async exclusive(work,selection=false){
  if(this.busy){if(selection)throw Error('正在同步入库，请完成后再选择资源库。');return;}
  this.busy=true;let lock;
  try{
   fs.mkdirSync(path.dirname(this.statePath),{recursive:true});const lockPath=this.statePath+'.lock';
   try{lock=fs.openSync(lockPath,'wx');}
   catch(e){if(e.code!=='EEXIST')throw e;const pid=Number(fs.readFileSync(lockPath,'utf8'));let alive=true;try{process.kill(pid,0);}catch{alive=false;}
    if(!alive&&Number.isInteger(pid)&&pid>0){fs.unlinkSync(lockPath);lock=fs.openSync(lockPath,'wx');}
    else {if(selection)throw Error('另一个轻存窗口正在同步，请完成后再选择资源库。');return;}}
   fs.writeFileSync(lock,String(process.pid));this.load();return await work();
  }finally{if(lock!==undefined){fs.closeSync(lock);fs.unlinkSync(this.statePath+'.lock');}this.busy=false;}
 }
 async selectLibrary(selected){
  return this.exclusive(async()=>{
   if(typeof selected!=='string'||!selected.trim())throw Error('请选择一个 Eagle 资源库。');
   const target=path.resolve(selected.trim());
   if(!/\.library$/i.test(target)||!fs.existsSync(target)||!fs.statSync(target).isDirectory())throw Error('所选资源库未连接或路径无效，请连接磁盘，或重新选择 .library 资源库。');
   let current=await this.api.library();
   if(!current.path||norm(current.path)!==norm(target)){
    if(!this.api.switchLibrary)throw Error('请先在 Eagle 中打开所选资源库，再点击“使用当前库”。');
    await this.api.switchLibrary(target);
    const until=Date.now()+15000;
    do{current=await this.api.library();if(current.path&&norm(current.path)===norm(target))break;await new Promise(r=>setTimeout(r,200));}while(Date.now()<until);
   }
   if(!current.path||norm(current.path)!==norm(target))throw Error('Eagle 尚未切换到所选资源库，原入库设置已保留，请重试。');
   this.activate(current);this.save();this.notify({message:'后续下载与待入库视频将进入“'+this.state.libraryName+'”，旧素材保留在原库。',state:this.state});
   return this.state;
  },true);
 }
 belongsHere(a,f){
  const owner=f.eagleReplacement?.libraryPath||f.eagleStorage?.libraryPath;
  if(owner)return norm(owner)===norm(this.state.libraryPath);
  // Legacy records may have an item ID before single-copy adoption completed.
  for(const library of Object.values(this.state.libraries))if(norm(library.libraryPath)!==norm(this.state.libraryPath)){
   for(const [k,r]of Object.entries(library.entries||{}))if(r.itemId&&r.assetId===a.id&&((f.syncKey&&k===f.syncKey)||(r.sourcePath&&norm(r.sourcePath)===norm(f.path))))return false;
  }
  return true;
 }
 async folder(names){let parent='',tree=await this.api.folders();for(const name of names){await this.guard();let found=tree.find(f=>f.name===name);if(!found){found=await this.api.createFolder(name,parent);if(!found?.id)throw Error('Eagle 未返回新文件夹。');}parent=found.id;tree=found.children||[];}return parent;}
 async finishStorage(a,f,key,rec){
  if(!this.storage)return;
  await this.guard();if(f.eagleStorage?.libraryPath&&norm(f.eagleStorage.libraryPath)!==norm(this.state.libraryPath))throw Error('素材属于其它资源库，未跨库修改或清理。');
  let adopted=f;
  if(!f.eagleStorage){const result=await this.storage.adopt({id:a.id,sourcePath:f.path,libraryPath:this.state.libraryPath,itemId:rec.itemId,syncKey:key});adopted=result.file;}
  if(!adopted?.eagleStorage)throw Error('Eagle 原文件尚未关联，下载副本已保留。');
  const st=fs.statSync(adopted.path);Object.assign(rec,{sourcePath:adopted.path,downloadPath:rec.downloadPath||f.pendingSourceCleanup?.sourcePath||f.path,storage:'eagle',stamp:st.size+':'+st.mtimeMs,sha:adopted.eagleStorage.sha256});
  this.state.entries[key]=rec;this.save();
  await this.guard();await this.storage.cleanup({id:a.id,syncKey:key});
 }
 async run(){
  try{return await this.exclusive(async()=>{
   if(!this.state.libraryPath)await this.bind();await this.guard();if(this.storage)await this.storage.configure(this.state.libraryPath);
   if(await this.isBusy()){this.notify({message:'下载完成后自动入库',state:this.state});return;}
   if(this.storage?.upgrade&&this.state.classificationVersion!==2){await this.storage.upgrade();this.state.classificationVersion=2;this.save();}
   await folderLayout.migrate(this);
   if(!this.state.enabled){this.notify({message:'自动入库已暂停',state:this.state});return;}
   this.state.errors=[];const assets=this.readAssets(),seen=new Set(),handled=new Set();let imported=0,updated=0;
   for(const a of assets)for(const f of a.files||[]){
    if((!f.managed&&!f.eagleStorage)||!f.path||!this.belongsHere(a,f)||!['.mp4','.mov','.mkv','.webm'].includes(path.extname(f.path).toLowerCase()))continue;
    try{await this.guard();
     if(f.eagleStorage&&this.storage&&!fs.existsSync(f.path)){const fresh=await this.storage.resolve(a.id);const found=fresh.files.find(x=>x.syncKey===f.syncKey);if(found)Object.assign(f,found);}
     if(!fs.existsSync(f.path))throw Error('视频文件无法读取，请检查 Eagle 资源库位置。');
     const stable=f.eagleReplacement?.syncKey||f.syncKey;if(stable&&handled.has(stable))continue;if(f.eagleReplacement)handled.add(stable);const stat=fs.statSync(f.path),stamp=stat.size+':'+stat.mtimeMs;let key=stable||digest(a.id+'|'+norm(f.path)).slice(0,28),rec=this.state.entries[key];
     if(!rec){const match=Object.entries(this.state.entries).find(([,v])=>v.assetId===a.id&&norm(v.sourcePath)===norm(f.path));if(match)[key,rec]=match;}
     const sha=rec?.stamp===stamp&&norm(rec.sourcePath)===norm(f.path)?rec.sha:await fileHash(f.path);
     if(f.eagleStorage&&sha!==f.eagleStorage.sha256)throw Error('Eagle 原文件内容发生变化，请重新关联素材。');
     if(f.eagleStorage&&!rec)rec={itemId:f.eagleStorage.itemId,assetId:a.id,sha,autoTags:[],storage:'eagle'};
     if(seen.has(a.id+'|'+sha))continue;seen.add(a.id+'|'+sha);
     if(!rec){const moved=Object.entries(this.state.entries).find(([,v])=>v.assetId===a.id&&v.sha===sha&&!fs.existsSync(v.sourcePath));if(moved){[key,rec]=moved;}}
     const data=payload(a,f,key),sig=digest(JSON.stringify(data));
     if(rec?.signature===sig&&rec.stamp===stamp&&rec.status==='已入库'&&!f.eagleReplacement){await this.finishStorage(a,f,key,rec);handled.add(key);continue;}
     let item=rec?.itemId?await this.api.getItem(rec.itemId):null;
     if(rec?.itemId&&(item?.isDeleted||(!item&&!rec.pendingImport))){rec.status='Eagle 中已移除，未自动重新添加';this.save();continue;}
     if(!item){const found=await this.api.findItems(data.marker);item=found.find(x=>(x.annotation||'').includes(data.marker)&&!x.isDeleted);}
     if(!item&&rec?.pendingImport){
      if(!this.api.findImported)throw Error('等待核对上次导入记录，请重新打开插件。');
      const candidates=(await this.api.findImported(data,path.extname(f.path).slice(1))).filter(i=>!i.isDeleted&&i.name===data.name&&i.url===data.website);
      const matches=[];for(const i of candidates)if(i.filePath&&fs.existsSync(i.filePath)&&await fileHash(i.filePath)===sha)matches.push(i);
      if(matches.length>1)throw Error('找到多个相同素材，请在 Eagle 中核对，暂不重复导入。');
      if(matches.length===1)item=matches[0];
      else if(Date.now()-Date.parse(rec.pendingImport.started)<120000)throw Error('等待 Eagle 完成上次导入，稍后自动核对。');
     }
     const folder=item&&f.eagleStorage&&!rec?.folderId?'':item&&rec?.folderId&&(!rec.folderNames||JSON.stringify(rec.folderNames)===JSON.stringify(data.folderNames))?rec.folderId:await this.folder(data.folderNames);await this.guard();
     if(!item){rec={...rec,assetId:a.id,sourcePath:f.path,stamp,sha,pendingImport:{started:new Date().toISOString(),name:data.name,url:data.website},autoAnnotation:data.annotation,autoTags:[...data.tags]};this.state.entries[key]=rec;this.save();
      const id=await this.api.add(f.path,{...data,folders:[folder]});if(!id)throw Error('Eagle 没有返回素材编号。');rec.itemId=id;this.state.entries[key]=rec;this.save();await this.guard();
      item=await this.api.getItem(id);if(!item)throw Error('Eagle 素材尚未就绪，下一次将自动核对。');imported++;
     }else{
      if(rec&&rec.sha!==sha){
       if(f.eagleStorage)throw Error('Eagle 文件内容发生变化，未自动覆盖。');
       if(f.eagleReplacement){
        if(f.eagleReplacement.itemId!==item.id||norm(f.eagleReplacement.libraryPath)!==norm(this.state.libraryPath))throw Error('待替换的 Eagle 素材关联已变化。');
        const current=item.filePath&&fs.existsSync(item.filePath)?await fileHash(item.filePath):'';
        if(current!==sha&&current!==f.eagleReplacement.oldSha)throw Error('Eagle 原视频已经改变，请重新确认替换。');
        if(current!==sha){await this.guard();await this.api.replace(item,f.path);}
        item=await this.api.getItem(item.id);if(!item)throw Error('等待 Eagle 更新文件。');
       }else {await this.guard();await this.api.replace(item,f.path);}
      }
      const suppressed=unique([...(rec?.suppressed||[]),...(rec?.autoTags||[]).filter(t=>!(item.tags||[]).includes(t))]);
      const oldTags=new Set(rec?.autoTags||[]);const tags=unique([...(item.tags||[]).filter(t=>!oldTags.has(t)||data.tags.includes(t)&&!suppressed.includes(t)),...data.tags.filter(t=>!suppressed.includes(t))]);
      const patch={tags,annotation:mergeAnnotation(item.annotation,data,rec||{}),folders:(!folder||folder===rec?.folderId)?[...(item.folders||[])]:unique([...(item.folders||[]).filter(id=>id!==rec?.folderId),folder])};
      if(!rec||item.url===rec.url)patch.url=data.website;
      if(!rec||item.name===rec.name)patch.name=data.name;
      await this.guard();await this.api.update(item,patch);rec={...rec,suppressed};updated++;
     }
     const after=fs.statSync(f.path);if(after.size+':'+after.mtimeMs!==stamp)throw Error('源文件发生变化，下一次将重新核对。');
     this.state.entries[key]={...rec,itemId:item.id,assetId:a.id,sourcePath:f.path,stamp,sha,signature:sig,folderId:folder,folderNames:[...data.folderNames],pendingImport:undefined,presentationVersion:2,autoAnnotation:data.annotation,autoTags:[...data.tags],name:data.name,url:data.website,title:a.title,status:'已入库',updated:new Date().toISOString()};
     this.save();await this.finishStorage(a,f,key,this.state.entries[key]);handled.add(key);this.notify({message:'已入库 '+Object.values(this.state.entries).filter(e=>e.status==='已入库').length+' 条完整视频',state:this.state});
    }catch(e){this.state.errors.push({title:a.title,path:f.path,message:e.message||String(e)});if(/等待打开/.test(e.message))throw e;}
   }
   this.state.presentationVersion=2;this.state.lastSync=new Date().toISOString();this.save();this.notify({message:this.state.errors.length?this.state.errors.length+' 条视频待重试':'自动入库已就绪',state:this.state,imported,updated});return{imported,updated,errors:this.state.errors};
  });}catch(e){this.notify({message:e.message,error:true,state:this.state});return{error:e.message};}
 }
}
module.exports={Sync,payload,mergeAnnotation,platform,fileHash};
