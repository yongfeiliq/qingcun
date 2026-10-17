'use strict';
(()=>{
 const $=id=>document.getElementById(id),bridge=window.parent.Qingcun,id=new URLSearchParams(location.hash.slice(1)).get('asset');
 let asset,job={},busy=false,dirty=false,timer,revision='';
 const view=()=>asset?.filmTypeAnalysisDraft||asset?.filmTypeAnalysis||{};
 const active=()=>['queued','running'].includes(job.state);
 const notice=(text,bad=false)=>{$('notice').textContent=text;$('notice').classList.toggle('error',bad);};
 async function api(q){const r=await bridge.call({id,...q});if(!r?.ok)throw Error(r?.error||'操作未完成。');return r;}
 function controls(){for(const key of ['analyze','accept','discard','type'])$(key).disabled=busy||active();$('reveal').disabled=busy;}
 function render(){
  $('title').textContent=asset.title;
  if(!dirty||revision!==(view().updated||'')){$('type').value=view().type||'待确认';dirty=false;revision=view().updated||'';}
  $('discard').hidden=!asset.filmTypeAnalysisDraft;
  $('state').textContent=active()?job.message:job.kind==='film_type'&&job.state==='failed'?job.message:asset.filmTypeAnalysisDraft?(view().type==='待确认'?'暂时无法判断，可手动选择或保持无标签。':'识别结果待确认：'+view().type):asset.filmTypeAnalysis?(view().type==='待确认'?'已选择不添加类型标签。':'已确认：'+view().type):'尚未识别，也可直接选择片子类型。';
  $('original').replaceChildren();
  for(const t of asset.eagleOriginalTags||[]){const e=document.createElement('span');e.className='chip';e.textContent=t;$('original').append(e);}
  let url='';try{const u=new URL(asset.url);if(['http:','https:'].includes(u.protocol)&&!u.username&&!u.password)url=u.href;}catch{}
  $('source').hidden=!url;$('source').onclick=e=>{e.preventDefault();bridge.openExternal(url);};controls();
 }
 async function work(fn){if(busy)return;busy=true;controls();try{await fn();}catch(e){notice(e.message,true);}finally{busy=false;controls();}}
 async function poll(){
  clearTimeout(timer);
  try{if(asset&&!busy){job=(await api({action:'analysisJob'})).job||{};asset=(await api({action:'asset'})).asset;render();}}
  catch(e){notice(e.message,true);}timer=setTimeout(poll,3500);
 }
 $('type').onchange=()=>dirty=true;
 $('analyze').onclick=()=>work(async()=>{job=(await api({action:'startFilmTypeAnalysis',index:0})).job;notice('已开始识别整片类型，结果需要确认后才写入 Eagle。');render();});
 $('typeForm').onsubmit=e=>{e.preventDefault();work(async()=>{const type=$('type').value;asset=(await api({action:'editFilmTypeAnalysis',op:'accept',type,fileKey:asset.files[0].key,revision})).asset;dirty=false;render();notice(type==='待确认'?'已保存，不添加类型标签。':'已确认“'+type+'”，后台将写回 Eagle。');});};
 $('discard').onclick=()=>work(async()=>{asset=(await api({action:'editFilmTypeAnalysis',op:'discard',fileKey:asset.files[0].key,revision})).asset;dirty=false;render();notice('本次结果已放弃，之前确认的类型保留。');});
 $('reveal').onclick=()=>work(()=>api({action:'revealAsset',index:0}));
 $('back').onclick=()=>window.parent.document.querySelector('[data-page="existing"]').click();
 $('preview').onerror=()=>notice('此格式无法直接预览，可点击“定位原文件”播放；仍可识别类型。',true);
 (async()=>{try{
  if(!bridge||!id)throw Error('请从 Eagle 已有素材页面打开视频。');
  asset=(await api({action:'asset'})).asset;job=(await api({action:'analysisJob'})).job||{};
  const info=await api({action:'filmTypeInfo'});for(const type of info.options){const o=document.createElement('option');o.value=type;o.textContent=type==='待确认'?'不确定 / 不添加标签':type;$('type').append(o);}
  $('preview').src=(await api({action:'openPlayback',index:0})).url;render();poll();
 }catch(e){notice(e.message,true);for(const k of ['analyze','accept','discard','type'])$(k).disabled=true;}})();
 window.addEventListener('beforeunload',()=>clearTimeout(timer));
})();
