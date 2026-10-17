'use strict';
const fs=require('fs'),path=require('path'),crypto=require('crypto'),{pathToFileURL}=require('url');
const imageExtensions=['jpg','jpeg','png','webp','gif','bmp','tif','tiff','avif'];
const extensions=[...imageExtensions,'mp4','mov','mkv','webm','m4v','avi','wmv','mpg','mpeg','ts','mts','m2ts'];
const norm=p=>path.resolve(p||'.').toLowerCase(),unique=a=>[...new Set(a.filter(x=>typeof x==='string'&&x.trim()).map(x=>x.trim()))];
const key=(library,id)=>crypto.createHash('sha256').update(norm(library)+'|'+id).digest('hex').slice(0,28);
const stamp=p=>{const s=fs.statSync(p);return s.size+':'+s.mtimeMs;};
const active=j=>['queued','running'].includes(j?.state);
const time=n=>{n=Math.max(0,Number(n)||0);return Math.floor(n/60)+':'+(n%60).toFixed(2).padStart(5,'0');};
const presentation=require('./presentation.cjs');
const desiredTags=presentation.meaningfulTags;
function shotNote(asset,k){return {begin:'〔轻存已有视频:'+k+'〕',end:'〔/轻存已有视频〕',text:''};}
function mergeNote(old,note){old=String(old||'');const i=old.indexOf(note.begin),j=i<0?-1:old.indexOf(note.end,i);return i>=0&&j>=0?(old.slice(0,i)+old.slice(j+note.end.length)).trim():old;}
class ExistingVideos{
 constructor({api,native,statePath,notify=()=>{}}){
  Object.assign(this,{api,native,statePath,notify});this.tail=Promise.resolve();this.ticking=false;this.state={version:1,records:{},queue:[]};this.load();
 }
 load(){if(fs.existsSync(this.statePath)){try{const s=JSON.parse(fs.readFileSync(this.statePath,'utf8'));if(!s.records||!Array.isArray(s.queue))throw Error();this.state=s;}catch{throw Error('已有素材的标签记录无法读取，请保留文件并联系修复。');}}}
 save(){fs.mkdirSync(path.dirname(this.statePath),{recursive:true});const t=this.statePath+'.'+process.pid+'.tmp';fs.writeFileSync(t,JSON.stringify(this.state));fs.renameSync(t,this.statePath);}
 exclusive(work){
  const run=this.tail.then(async()=>{
   fs.mkdirSync(path.dirname(this.statePath),{recursive:true});const p=this.statePath+'.lock';let lock;
   try{try{lock=fs.openSync(p,'wx');}catch(e){if(e.code!=='EEXIST')throw e;let alive=true;const pid=Number(fs.readFileSync(p,'utf8'));try{process.kill(pid,0);}catch{alive=false;}
    if(alive||!Number.isInteger(pid)||pid<=0)throw Error('另一个轻存窗口正在处理已有素材，请稍后重试。');fs.unlinkSync(p);lock=fs.openSync(p,'wx');}
    fs.writeFileSync(lock,String(process.pid));this.load();return await work();
   }finally{if(lock!==undefined){fs.closeSync(lock);fs.unlinkSync(p);}}
  });this.tail=run.catch(()=>{});return run;
 }
 async guard(library){const current=await this.api.library();if(!current.path||norm(current.path)!==norm(library))throw Error('Eagle 已切换资源库。请返回原库，再重新读取素材。');}
 async list(selected=true){
  const library=await this.api.library();if(!library.path)throw Error('请先在 Eagle 打开资源库。');
  const all=selected?await this.api.selected():await (this.api.media||this.api.videos)();
  await this.guard(library.path);this.load();
  const assets=new Map(this.native.readAssets().map(a=>[a.id,a]));
  const rows=all.filter(i=>!i.isDeleted&&extensions.includes((i.ext||'').toLowerCase())).map(i=>{
   const rec=this.state.records[key(library.path,i.id)],a=assets.get(rec?.assetId),j=this.state.queue.find(j=>j.key===key(library.path,i.id)&&['waiting','running'].includes(j.state));
   return {mediaType:imageExtensions.includes((i.ext||'').toLowerCase())?'image':'video',id:i.id,name:i.name,tags:i.tags||[],ext:i.ext,thumbnail:i.thumbnailPath&&fs.existsSync(i.thumbnailPath)&&!i.noThumbnail?pathToFileURL(i.thumbnailPath).href:'',assetId:rec?.assetId||'',status:j?(j.state==='waiting'?'等待分析':j.message||'正在分析'):(a?.filmTypeAnalysisDraft||a?.imageAnalysisDraft||(a?.tagAnalysisMode!=='film_type'&&a?.analysisDraft))?'分析标签待确认':rec?.analysisError||rec?.error||rec?.status||'待整理'};
  });
  return{libraryPath:library.path,libraryName:library.name,items:rows,skipped:all.length-rows.length};
 }
 validateIds(ids){if(!Array.isArray(ids)||!ids.length||ids.length>30)throw Error('请选择 1 至 30 项素材。');return unique(ids);}
 async item(library,id){await this.guard(library);const i=await this.api.getItem(id);if(!i||i.isDeleted||!extensions.includes((i.ext||'').toLowerCase()))throw Error('素材已移除或格式不支持。');if(!i.filePath||!fs.existsSync(i.filePath))throw Error('素材文件已移动或不可读取。');await this.guard(library);return i;}
 async prepare(library,id){
  const item=await this.item(library,id),k=key(library,id),sourceStamp=stamp(item.filePath);
  const r=await this.native.call({action:'registerEagleAsset',libraryPath:library,itemId:id,path:item.filePath,sourceStamp,title:item.name,url:item.url||'',annotation:presentation.sourceAnnotation(item.annotation),tags:item.tags||[]});
  await this.guard(library);
  const rec=this.state.records[k]||{itemId:id,libraryPath:library,ownedTags:[],suppressed:[]};
  if(r.changed)rec.signature='';
  Object.assign(rec,{assetId:r.asset.id,title:item.name,sourceStamp,sourcePath:item.filePath,status:'可编辑标签',error:'',analysisError:''});this.state.records[k]=rec;this.save();
  return{rec,asset:r.asset,k};
 }
 async apply(rec,asset,k){
  if(!asset||asset.eagle?.itemId!==rec.itemId||norm(asset.eagle.libraryPath)!==norm(rec.libraryPath))throw Error('标签资料与 Eagle 素材不匹配。');
  const item=await this.item(rec.libraryPath,rec.itemId);
  if(stamp(item.filePath)!==asset.eagle.sourceStamp||norm(item.filePath)!==norm(asset.files[0].path))throw Error('素材文件已变化，请重新读取后再打标签。');
  const before=unique(item.tags||[]),owned=new Set(rec.ownedTags||[]);
  const suppressed=unique([...(rec.suppressed||[]),...owned].filter(t=>(rec.suppressed||[]).includes(t)||!before.includes(t)));
  const desired=desiredTags(asset).filter(t=>!suppressed.includes(t));
  const tags=unique([...before.filter(t=>!owned.has(t)),...desired]);
  const note=shotNote(asset,k),annotation=mergeNote(item.annotation,note),patch={};
  if(JSON.stringify(tags)!==JSON.stringify(before))patch.tags=tags;
  if(annotation!==(item.annotation||''))patch.annotation=annotation;
  await this.guard(rec.libraryPath);if(Object.keys(patch).length)await this.api.update(item,patch);
  rec.presentationVersion=2;rec.currentTags=tags;rec.ownedTags=desired.filter(t=>owned.has(t)||!before.includes(t));rec.suppressed=suppressed;rec.error='';rec.status=(asset.filmTypeAnalysisDraft||asset.imageAnalysisDraft||(asset.tagAnalysisMode!=='film_type'&&asset.analysisDraft))?'分析标签待确认':'标签已写回 Eagle';rec.updated=new Date().toISOString();this.save();
  return{added:tags.filter(t=>!before.includes(t)),assetId:asset.id};
 }
 async batch(library,ids,mode,labels=[]){
  ids=this.validateIds(ids);if(!['basic','analyze','custom'].includes(mode))throw Error('操作无效。');
  labels=unique(labels);if(mode==='custom'&&(!labels.length||labels.length>20||labels.some(t=>t.length>80)))throw Error('请输入 1 至 20 个标签，每个最多 80 字。');
  return this.exclusive(async()=>{
   await this.guard(library);
   if(mode==='analyze'){const m=await this.native.call({action:'modelSettings'});if(!m.enabled||!m.configured)throw Error('智能分析目前未开启或未连接。需要时在设置中打开分析开关；下载与手动标签可正常使用。');}
   let done=0;const errors=[];
   for(const [index,id]of ids.entries()){
    try{
     this.notify({message:'正在处理 '+(index+1)+' / '+ids.length});
     if(mode==='custom'){
      const item=await this.item(library,id);await this.guard(library);const tags=unique([...(item.tags||[]),...labels]);await this.api.update(item,{tags});const rec=this.state.records[key(library,id)];if(rec){rec.currentTags=tags;rec.ownedTags=(rec.ownedTags||[]).filter(t=>!labels.includes(t));rec.suppressed=(rec.suppressed||[]).filter(t=>!labels.includes(t));}done++;continue;
     }
     const {rec,asset,k}=await this.prepare(library,id);rec.enabled=true;
     if(mode==='basic'){await this.apply(rec,asset,k);done++;}
     else{
      if(asset.mediaType!=='image'&&(!(asset.files[0]?.duration>0)||asset.files[0].duration>300))throw Error('类型识别支持 5 分钟以内的视频。');
      if(this.state.queue.some(j=>j.key===k&&['waiting','running'].includes(j.state))){done++;continue;}
      this.state.queue=this.state.queue.filter(j=>j.key!==k);
      this.state.queue.push({key:k,assetId:asset.id,itemId:id,libraryPath:library,title:asset.title,state:'waiting'});done++;
     }this.save();
    }catch(e){errors.push({id,message:e.message});if(/切换资源库/.test(e.message))break;}
   }
   this.save();return{done,errors};
  });
 }
 async open(library,id){return this.exclusive(async()=>{const r=await this.prepare(library,id);return r.asset.id;});}
 async edited(assetId){return this.exclusive(async()=>{for(const r of Object.values(this.state.records))if(r.assetId===assetId){await this.guard(r.libraryPath);r.enabled=true;}this.save();});}
 async stop(library){return this.exclusive(async()=>{await this.guard(library);let stopped=0;for(const j of this.state.queue)if(norm(j.libraryPath)===norm(library)&&j.state==='waiting'){j.state='cancelled';stopped++;}this.save();return stopped;});}
 async tick(){
  if(this.ticking)return;this.ticking=true;
  try{return await this.exclusive(async()=>{
   const library=await this.api.library();if(!library.path)return;
   // One active model task at a time, persisted across plugin/window restarts.
   let running=this.state.queue.find(j=>j.state==='running');
   if(running){
    try{const r=await this.native.call({action:'analysisJob',id:running.assetId});running.message=r.job?.message||'正在分析';
     if(!active(r.job)){running.state=r.job?.state==='completed'?'completed':'failed';const rec=this.state.records[running.key];if(rec){rec.analysisError=running.state==='failed'?(r.job?.message||'分析未完成，可重试'):'';if(running.state==='completed')rec.status='分析标签待确认';}}
    }catch(e){running.message=e.message;}this.save();
   }
   if(this.state.queue.some(j=>j.state==='waiting')){const settings=await this.native.call({action:'settings'});if(settings.analysisMode==='basic'){for(const j of this.state.queue)if(j.state==='waiting'){j.state='cancelled';j.message='智能分析已关闭';}this.save();}}
   if(!this.state.queue.some(j=>j.state==='running')){
    const next=this.state.queue.find(j=>j.state==='waiting'&&norm(j.libraryPath)===norm(library.path));
    if(next)try{
     const rec=this.state.records[next.key],item=await this.item(next.libraryPath,next.itemId);
     if(stamp(item.filePath)!==rec.sourceStamp||norm(item.filePath)!==norm(rec.sourcePath))throw Error('素材已变化，请重新读取后再分析。');
     await this.guard(next.libraryPath);const r=await this.native.call({action:(this.native.readAssets().find(a=>a.id===next.assetId)?.mediaType==='image'?'startAnalysis':'startFilmTypeAnalysis'),id:next.assetId,index:0});next.state='running';next.jobId=r.job.id;next.message=r.job.message;
    }catch(e){next.state='failed';const rec=this.state.records[next.key];if(rec)rec.analysisError=e.message;}this.save();
   }
   const assets=new Map(this.native.readAssets().map(a=>[a.id,a]));
   for(const [k,rec]of Object.entries(this.state.records)){
    if(!rec.enabled||norm(rec.libraryPath)!==norm(library.path))continue;
    const a=assets.get(rec.assetId);if(!a)continue;
    const signature=crypto.createHash('sha256').update(JSON.stringify({tags:desiredTags(a),note:shotNote(a,k),presentationVersion:2})).digest('hex');
    if(signature===rec.signature)continue;
    try{await this.apply(rec,a,k);rec.signature=signature;this.save();}catch(e){rec.error=e.message;this.save();}
   }
   this.state.queue=this.state.queue.filter(j=>['waiting','running'].includes(j.state)).concat(this.state.queue.filter(j=>!['waiting','running'].includes(j.state)).slice(-100));this.save();
   this.notify({queue:this.state.queue,records:this.state.records});
  });}catch(e){this.notify({error:e.message});}finally{this.ticking=false;}
 }
}
module.exports={ExistingVideos,extensions,imageExtensions,desiredTags,mergeNote,shotNote,key};
