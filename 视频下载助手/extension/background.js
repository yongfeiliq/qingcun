importScripts('platforms.js', 'xpc-assist.js', 'browser-session.js');

'use strict';
const HOST='com.qingcun.video_downloader';
const actions=new Set(['download','subtitle','refreshDescription','refreshSource']);
const access={permissions:['cookies']};
function native(q){return new Promise((resolve,reject)=>chrome.runtime.sendNativeMessage(HOST,q,r=>{
 if(chrome.runtime.lastError)return reject(Error('本地下载组件未连接，请重新安装最新版轻存。'));
 if(!r?.ok)return reject(Error(r?.error||'下载组件没有返回有效结果。'));resolve(r);
}));}
function vimeoUrl(value){try{const u=new URL(value);return u.protocol==='https:'&&['vimeo.com','www.vimeo.com','player.vimeo.com'].includes(u.hostname)&&!u.username&&!u.password&&(!u.port||u.port==='443');}catch{return false;}}
let synchronization=Promise.resolve();
function session(){
 const next=synchronization.then(async()=>{
  if(!await chrome.permissions.contains(access))return '';
  const saved=(await chrome.storage.local.get('vimeoConnection')).vimeoConnection||{};
  const list=await chrome.cookies.getAll({domain:'vimeo.com'});
  const cookies=list.filter(c=>!c.partitionKey&&/(^|\.)vimeo\.com$/i.test(c.domain)).map(c=>({domain:c.domain,name:c.name,value:c.value,path:c.path,secure:c.secure,httpOnly:c.httpOnly,hostOnly:c.hostOnly,expirationDate:c.expirationDate||0}));
  if(!cookies.some(c=>c.name==='vimeo'&&c.value&&['vimeo.com','.vimeo.com'].includes(c.domain))){
   if(saved.id)await native({action:'clearVimeoSession',vimeoSession:saved.id});
   await chrome.storage.local.remove('vimeoConnection');return '';
  }
  const r=await native({action:'saveVimeoSession',vimeoSession:saved.id||'',cookies});
  await chrome.storage.local.set({vimeoConnection:{id:r.vimeoSession,enabled:true}});return r.vimeoSession;
 });synchronization=next.catch(()=>{});return next;
}
async function dispatch(message){
 const q=message.request,request={...q};delete request.vimeoSession;delete request.captureTabId;
 const urls=Array.isArray(q.urls)&&q.urls.length?q.urls:[message.sourceUrl];
 if(!q.importLocal&&!q.metadataPath)await QingcunBrowserSession.syncUrls(urls,native);
 if(!q.importLocal&&!q.metadataPath&&urls.some(vimeoUrl)){
  const allowed=await chrome.permissions.contains(access);
  if(allowed){const id=await session();if(id)request.vimeoSession=id;}
 }
 if(['download','subtitle'].includes(q.action)&&Number.isInteger(q.captureTabId)&&urls.length===1&&/^https:\/\/(?:vimeo\.com\/|www\.instagram\.com\/p\/)/.test(QingcunPlatforms.normalize(urls[0])||'')){
  try{
   const url=QingcunPlatforms.normalize(urls[0]),tab=await chrome.tabs.get(q.captureTabId);
   if(url&&QingcunPlatforms.normalize(tab.url)===url){
    const page=await QingcunPlatforms.read(chrome,tab.id);
    if(page&&QingcunPlatforms.normalize(page.pageUrl)===url){
     const current=await chrome.tabs.get(tab.id);
     if(QingcunPlatforms.normalize(current.url)===url){
      request.contexts=[...(q.contexts||[]).filter(c=>QingcunPlatforms.normalize(c.url||'')!==url),{...page,url,pageUrl:url}];
      // A duplicate may be skipped: still enrich that exact existing asset,
      // without downloading or replacing its video or manual metadata.
      if(page.tags?.length||page.categories?.length||page.labelReadStatus==='read')try{await native({action:'savePageLabels',pageUrl:url,tags:page.tags||[],categories:page.categories||[],labelReadStatus:page.labelReadStatus,categoryReadStatus:page.categoryReadStatus,labelOrigin:page.labelOrigin,origin:'browser'});}catch{}
     }
    }
   }
  }catch{/* Native extraction remains available; do not discard an existing video. */}
 }
 return native(request);
}
chrome.runtime.onMessage.addListener((message,sender,respond)=>{
 if(sender.id!==chrome.runtime.id||!String(sender.url||'').startsWith(chrome.runtime.getURL(''))||!message||!/^[a-f0-9-]{36}$/.test(message.ticket||''))return false;
 if(message.type!=='qingcun.nativeTask'||!actions.has(message.request?.action))return false;
 dispatch(message).then(respond,error=>respond({ok:false,error:error.message||'后台下载未完成，请重新尝试。'}));return true;
});

const xpcAssist=createXpcAssist({chrome,native,platforms:QingcunPlatforms,getSession:session});
chrome.alarms.onAlarm.addListener(a=>{if(a.name===xpcAssist.alarm)xpcAssist.poll();});
chrome.runtime.onStartup.addListener(()=>xpcAssist.start().catch(()=>{}));
chrome.runtime.onInstalled.addListener(()=>xpcAssist.start().catch(()=>{}));
chrome.tabs.onUpdated.addListener((id,change,tab)=>{if(change.status==='complete'&&/^https:\/\/www\.(xinpianchang\.com|behance\.net)\//.test(tab.url||''))xpcAssist.wake();});
xpcAssist.start().catch(()=>{});
chrome.runtime.onStartup.addListener(()=>QingcunBrowserSession.syncAll(native).catch(()=>{}));
chrome.runtime.onInstalled.addListener(()=>QingcunBrowserSession.syncAll(native).catch(()=>{}));
chrome.tabs.onUpdated.addListener((id,change,tab)=>{
 if(change.status==='complete'||change.url)QingcunBrowserSession.syncUrls([tab.url],native).catch(()=>{});
});
chrome.cookies.onChanged.addListener(({cookie})=>QingcunBrowserSession.changed(cookie,native));
chrome.alarms.create('qingcun.browserSessions',{periodInMinutes:30});
chrome.alarms.onAlarm.addListener(alarm=>{
 if(alarm.name==='qingcun.browserSessions')QingcunBrowserSession.syncAll(native).catch(()=>{});
});
QingcunBrowserSession.syncAll(native).catch(()=>{});
