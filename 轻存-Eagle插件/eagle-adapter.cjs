'use strict';
function deadline(p,label,ms=120000){let timer;return Promise.race([p,new Promise((_,reject)=>{timer=setTimeout(()=>reject(Error(label+'超时；资料会保留，下次自动核对。')),ms);})]).finally(()=>clearTimeout(timer));}
module.exports=eagle=>({
 libraries:()=>require('./library-choice.cjs').history(),
 switchLibrary:libraryPath=>require('./library-choice.cjs').switchLibrary(libraryPath),
 library:async()=>({path:eagle.library.path,name:eagle.library.name||require('path').basename(eagle.library.path||'').replace(/\.library$/i,'')}),
 media:async()=>{const rows=[];for(const ext of require('./existing.cjs').extensions)rows.push(...await deadline(eagle.item.get({ext}),'读取已有素材'));return [...new Map(rows.map(i=>[i.id,i])).values()];},
 selected:()=>deadline(eagle.item.getSelected(),'读取已选视频'),
 videos:async()=>{const rows=[];for(const ext of ['mp4','mov','mkv','webm','m4v','avi','wmv','mpg','mpeg','ts','mts','m2ts'])rows.push(...await deadline(eagle.item.get({ext}),'读取已有视频'));return [...new Map(rows.map(i=>[i.id,i])).values()];},
 folders:()=>deadline(eagle.folder.getAll(),'读取文件夹'),
 renameFolder:async(id,name)=>{const f=await deadline(eagle.folder.getById(id),'读取分类');if(!f)throw Error('原分类已变化，请刷新重试。');f.name=name;await deadline(f.save(),'更新分类名称');},
 createFolder:(name,parent)=>deadline(eagle.folder.create({name,...(parent?{parent}:{})}),'创建文件夹'),
 getItem:async id=>(await deadline(eagle.item.get({ids:[id]}),'读取素材'))[0]||null,
 findItems:marker=>deadline(eagle.item.get({annotation:marker}),'核对素材'),
 findImported:async(data,ext)=>(await deadline(eagle.item.get({ext}),'核对上次导入')).filter(i=>i.name===data.name&&i.url===data.website&&!i.isDeleted),
 add:(file,data)=>deadline(eagle.item.addFromPath(file,data),'Eagle 导入'),
 update:async(item,patch)=>{Object.assign(item,patch);await deadline(item.save(),'更新资料');},
 replace:async(item,file)=>{if(typeof item.replaceFile!=='function')throw Error('请升级 Eagle 后重试替换。');if((await deadline(item.replaceFile(file),'更新视频'))===false)throw Error('Eagle 文件替换未完成。');}
});
