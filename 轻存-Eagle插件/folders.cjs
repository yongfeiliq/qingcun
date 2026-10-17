'use strict';
const ROOT='轻存',LEGACY_ROOT='轻存参考片';
const category=name=>name==='汽车交通'?'汽车':name==='未分类'?'待分类':name||'待分类';
const names=values=>values?.[0]===LEGACY_ROOT||values?.[0]===ROOT?[ROOT,category(values[1]),...values.slice(2)]:values;
async function migrate(sync){
 if(sync.state.folderNamingVersion>=1)return;
 await sync.guard();const tree=await sync.api.folders(),old=tree.filter(f=>f.name===LEGACY_ROOT),current=tree.filter(f=>f.name===ROOT);
 if(old.length>1||current.length>1||old.length&&current.length)throw Error('发现多个轻存归档文件夹，请先核对合并；原分类和素材已保留。');
 const root=old[0]||current[0],plan=[];
 if(root){
  const prior=(root.children||[]).filter(f=>f.name==='汽车交通'),renamed=(root.children||[]).filter(f=>f.name==='汽车');
  if(prior.length>1||renamed.length>1||prior.length&&renamed.length)throw Error('同时存在汽车与汽车交通文件夹，请先核对合并；原分类和素材已保留。');
  if(old[0])plan.push({id:root.id,name:ROOT});
  if(prior[0])plan.push({id:prior[0].id,name:'汽车'});
 }
 for(const change of plan){if(!sync.api.renameFolder)throw Error('请重新载入新版轻存后更新分类名称。');await sync.guard();await sync.api.renameFolder(change.id,change.name);}
 if(plan.length){
  await sync.guard();const flat=t=>t.flatMap(f=>[f,...flat(f.children||[])]),actual=flat(await sync.api.folders());
  for(const x of plan)if(!actual.some(f=>f.id===x.id&&f.name===x.name))throw Error('Eagle 尚未保存分类名称，稍后自动重试。');
 }
 for(const entry of Object.values(sync.state.entries||{}))if(entry.folderNames)entry.folderNames=names(entry.folderNames);
 sync.state.folderNamingVersion=1;sync.save();
}
module.exports={ROOT,LEGACY_ROOT,category,names,migrate};
