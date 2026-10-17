'use strict';
(()=>{
 const $=id=>document.getElementById(id),node=(tag,text,cls)=>{const n=document.createElement(tag);if(text)n.textContent=text;if(cls)n.className=cls;return n;};
 let rows=[],library='',selected=new Set(),page=0,busy=false;const pageSize=30;
 const controller=()=>window.QingcunExisting;
 function note(text,bad=false){$('existingNotice').textContent=text;$('existingNotice').classList.toggle('error',bad);}
 function controls(){const count=selected.size;$('existingCount').textContent='已选 '+count+' 条';for(const id of ['existingBasic','existingAnalyze','existingCustom'])$(id).disabled=busy||!count||count>30;for(const id of ['existingSelected','existingBrowse','existingStop'])$(id).disabled=busy;}
 function filtered(){const s=$('existingSearch').value.trim().toLowerCase();const kind=$('existingType').value;return rows.filter(r=>(!kind||r.mediaType===kind)&&(r.name+' '+r.tags.join(' ')).toLowerCase().includes(s));}
 function render(){
  const found=filtered();page=Math.min(page,Math.max(0,Math.ceil(found.length/pageSize)-1));const visible=found.slice(page*pageSize,(page+1)*pageSize);
  $('existingRows').replaceChildren();$('existingTotal').textContent=found.length+' 项素材';$('existingEmpty').hidden=found.length>0;
  for(const row of visible){
   const tr=node('div','','existing-row'),check=node('input');check.type='checkbox';check.checked=selected.has(row.id);check.setAttribute('aria-label','选择 '+row.name);check.disabled=busy;
   check.onchange=()=>{if(check.checked){if(selected.size>=30){check.checked=false;note('每批最多 30 项素材。',true);return;}selected.add(row.id);}else selected.delete(row.id);controls();};
   const cover=node('div','','existing-cover');if(row.thumbnail){const img=node('img');img.src=row.thumbnail;img.alt='';cover.append(img);}else cover.textContent=row.ext.toUpperCase();
   const info=node('div','','existing-info'),title=node('strong',row.name),tags=node('div','','existing-tags');title.title=row.name;
   for(const tag of row.tags.slice(0,5))tags.append(node('span',tag));if(row.tags.length>5)tags.append(node('span','+'+(row.tags.length-5)));
   info.append(title,tags);const status=node('span',row.status,'existing-status');status.dataset.id=row.id;
   const edit=node('button',row.mediaType==='image'?'编辑 / 查看标签':'查看 / 确认类型');edit.disabled=busy;edit.onclick=()=>open(row.id);
   tr.append(check,cover,info,status,edit);$('existingRows').append(tr);
  }
  $('existingPrev').disabled=page===0;$('existingNext').disabled=(page+1)*pageSize>=found.length;$('existingPage').textContent=(page+1)+' / '+Math.max(1,Math.ceil(found.length/pageSize));controls();
 }
 async function read(selectedOnly){
  if(!controller())return note('插件正在连接 Eagle，请稍后再试。',true);if(busy)return;busy=true;controls();note('正在读取 Eagle 素材…');
  try{const r=await controller().list(selectedOnly);library=r.libraryPath;rows=r.items;selected=new Set(selectedOnly?rows.slice(0,30).map(r=>r.id):[]);page=0;$('existingLibrary').textContent=r.libraryName||'当前资源库';note(rows.length?'已读取 '+rows.length+' 项素材。'+(r.skipped?' 已略过 '+r.skipped+' 个非素材文件。':''):'没有找到素材。先在 Eagle 主窗口选中图片或视频，再点击“读取已选视频”。');}
  catch(e){note(e.message,true);}finally{busy=false;render();}
 }
 async function run(mode){
  if(busy||!controller())return;busy=true;render();const ids=[...selected],labels=$('existingLabels').value.split(/[,，、;\n；]+/).map(s=>s.trim()).filter(Boolean);
  try{const r=await controller().batch(library,ids,mode,labels);note((mode==='analyze'?'已加入分析队列 ':'已处理 ')+r.done+' 条。'+(mode==='analyze'?'结果进入待确认区，确认后写回 Eagle。':'')+(r.errors.length?' '+r.errors.map(e=>(rows.find(row=>row.id===e.id)?.name||'视频')+'：'+e.message).join('；'):''),!!r.errors.length);
   if(mode==='custom')for(const row of rows.filter(r=>ids.includes(r.id)))row.tags=[...new Set([...row.tags,...labels])];
   controller().tick();
  }catch(e){note(e.message,true);}finally{busy=false;render();}
 }
 async function open(id){if(busy)return;busy=true;render();note('正在读取素材资料…');try{const assetId=await controller().open(library,id);window.Qingcun.openAsset(assetId);note('已打开标签编辑，可检查识别结果并确认。');}catch(e){note(e.message,true);}finally{busy=false;render();}}
 window.QingcunExistingReport=r=>{
  if(r.message)note(r.message);if(r.error)note(r.error,true);
  if(r.records){const before=JSON.stringify(rows);for(const row of rows){const rec=Object.values(r.records).find(v=>v.itemId===row.id&&v.libraryPath.toLowerCase()===library.toLowerCase());if(!rec)continue;
   const job=(r.queue||[]).find(j=>j.itemId===row.id&&j.libraryPath.toLowerCase()===library.toLowerCase()&&['waiting','running'].includes(j.state));
   row.status=job?(job.state==='waiting'?'等待分析':job.message||'正在分析'):rec.analysisError||rec.error||rec.status;row.assetId=rec.assetId;if(rec.currentTags)row.tags=rec.currentTags;
  }if(JSON.stringify(rows)!==before)render();}
  if(r.queue){const w=r.queue.filter(j=>j.state==='waiting').length,a=r.queue.filter(j=>j.state==='running').length;$('existingQueue').textContent=a||w?'正在分析 '+a+' 条 · 排队 '+w+' 条':'没有排队中的分析任务';}
 };
 $('existingSelected').onclick=()=>read(true);$('existingBrowse').onclick=()=>read(false);
 $('existingType').onchange=()=>{selected.clear();page=0;render();};
 $('existingSearch').oninput=()=>{page=0;render();};$('existingPrev').onclick=()=>{page--;render();};$('existingNext').onclick=()=>{page++;render();};
 $('existingSelectPage').onclick=()=>{selected=new Set(filtered().slice(page*pageSize,(page+1)*pageSize).map(r=>r.id));render();};$('existingClear').onclick=()=>{selected.clear();render();};
 $('existingBasic').onclick=()=>run('basic');$('existingAnalyze').onclick=()=>run('analyze');$('existingCustom').onclick=()=>run('custom');
 $('existingStop').onclick=async()=>{try{const n=await controller().stop(library);note('已停止 '+n+' 个排队任务。正在分析的素材会继续完成。');controller().tick();}catch(e){note(e.message,true);}};
 window.addEventListener('qingcun-library-changed',()=>{rows=[];selected.clear();library='';render();note('资源库已切换，请重新读取素材。');});render();
})();
