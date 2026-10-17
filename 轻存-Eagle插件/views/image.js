'use strict';
(()=>{
 const $=id=>document.getElementById(id),bridge=window.parent.Qingcun,id=new URLSearchParams(location.hash.slice(1)).get('asset');
 const names={subject:'主体类型',scale:'景别',angle:'视角',composition:'构图',tone:'冷暖色调',lighting:'光线',visual:'表现形式',style:'视觉风格'};
 let asset,options,busy=false,dirty=false,job={},timer,lastRevision='';
 const node=(tag,text)=>{const e=document.createElement(tag);if(text)e.textContent=text;return e;};
 const view=()=>asset.imageAnalysisDraft||asset.imageAnalysis||{};
 const notice=(text,bad=false)=>{$('notice').textContent=text;$('notice').classList.toggle('error',bad);};
 async function api(q){const r=await bridge.call({id,...q});if(!r?.ok)throw Error(r?.error||'操作未完成。');return r;}
 function controls(){for(const e of document.querySelectorAll('button,input,select'))e.disabled=busy;const active=['queued','running'].includes(job.state);$('analyze').disabled=busy||active;$('accept').disabled=busy||active;$('discard').disabled=busy||active;}
 function render(){
  $('title').textContent=asset.title;const f=asset.files[0];$('meta').textContent=[f.width+' × '+f.height,f.orientation,(f.bytes/1048576).toFixed(1)+' MB'].join(' · ');
  const revision=view().updated||'';
  if(!dirty||revision!==lastRevision){
   $('fields').replaceChildren();for(const [key,title]of Object.entries(names)){
    const label=node('label',title),select=node('select');select.id='field-'+key;
    for(const value of options[key]){const option=node('option',value);option.value=value;select.append(option);}
    select.value=view().labels?.[key]||'待确认';select.onchange=()=>dirty=true;label.append(select);$('fields').append(label);
   }lastRevision=revision;dirty=false;
  }
  $('discard').hidden=!asset.imageAnalysisDraft;
  $('state').textContent=['queued','running'].includes(job.state)?job.message:job.state==='failed'?job.message:asset.imageAnalysisDraft?'模型标签 · 待确认':asset.imageAnalysis?'已确认，可继续修改':'尚未分析，也可手动选择标签';
  $('original').replaceChildren();for(const t of asset.eagleOriginalTags||[]){const tag=node('span',t);tag.className='chip';$('original').append(tag);}
  $('tags').replaceChildren();for(const t of asset.tags||[]){
   const row=node('div'),word=node('span',t.name),state=node('small',t.status==='suggested'?'待确认':t.status==='source'?'来源信息':'已确认');row.className='tag-row';row.append(word,state);
   if(t.status==='suggested'){const confirm=node('button','确认');confirm.onclick=()=>work(async()=>{asset=(await api({action:'editAsset',edit:'confirmTag',key:t.dimension+'|'+t.name.toLowerCase()})).asset;render();notice('标签已确认，后台将写回 Eagle。');});row.append(confirm);}
   const remove=node('button','移除');remove.onclick=()=>work(async()=>{asset=(await api({action:'editAsset',edit:'removeTag',key:t.dimension+'|'+t.name.toLowerCase()})).asset;render();notice('已移除轻存标签，原有 Eagle 标签保留。');});row.append(remove);$('tags').append(row);
  }
  let url='';try{const u=new URL(asset.url);if(['https:','http:'].includes(u.protocol)&&!u.username&&!u.password)url=u.href;}catch{}
  $('source').hidden=!url;$('source').onclick=e=>{e.preventDefault();bridge.openExternal(url);};controls();
 }
 async function work(fn){if(busy)return;busy=true;controls();try{await fn();}catch(e){notice(e.message,true);}finally{busy=false;controls();}}
 async function poll(){
  clearTimeout(timer);if(!asset)return;
  try{if(!busy){const r=await api({action:'analysisJob'});const before=job.state;job=r.job||{};if(before!==job.state||['queued','running'].includes(job.state)){asset=(await api({action:'asset'})).asset;render();}}}
  catch(e){notice(e.message,true);}timer=setTimeout(poll,3500);
 }
 $('analyze').onclick=()=>work(async()=>{job=(await api({action:'startAnalysis',index:0})).job;notice('开始分析。新结果先进入待确认区。');render();});
 $('analysisForm').onsubmit=e=>{e.preventDefault();work(async()=>{const labels={};for(const k of Object.keys(names))labels[k]=$('field-'+k).value;asset=(await api({action:'editImageAnalysis',op:'accept',labels,fileKey:asset.files[0].key,revision:view().updated||''})).asset;dirty=false;render();notice('图片标签已确认，后台将写回 Eagle。');});};
 $('discard').onclick=()=>work(async()=>{asset=(await api({action:'editImageAnalysis',op:'discard',fileKey:asset.files[0].key,revision:view().updated||''})).asset;dirty=false;render();notice('已放弃本次模型结果，之前确认的标签保留。');});
 $('tagForm').onsubmit=e=>{e.preventDefault();work(async()=>{asset=(await api({action:'editAsset',edit:'addTag',dimension:'自定义',name:$('tagName').value.trim()})).asset;$('tagName').value='';render();notice('标签已保存，后台将写回 Eagle。');});};
 $('back').onclick=()=>window.parent.document.querySelector('[data-page="existing"]').click();
 $('reveal').onclick=()=>work(()=>api({action:'revealAsset',index:0}));
 (async()=>{try{if(!bridge||!id)throw Error('请从 Eagle 的已有素材页面打开图片。');const r=await api({action:'asset'});asset=r.asset;job=r.job||{};const info=await api({action:'imageInfo'});options=info.options;$('preview').src=info.image;render();poll();}catch(e){notice(e.message,true);for(const e of document.querySelectorAll('button,input,select'))e.disabled=true;$('back').disabled=false;}})();
 window.addEventListener('beforeunload',()=>clearTimeout(timer));
})();
