'use strict';
// Only jobs explicitly queued in the downloader may read a work page. No browsing-history collection.
function createXpcAssist({chrome,native,platforms,getSession=async()=>''}){
 const alarm='qingcun.xpc.download';
 let busy=false,wakeTimer;
 const canonical=url=>{const n=platforms.normalize(url||'');return n&&(/^https:\/\/www\.xinpianchang\.com\/a/.test(n)||/^https:\/\/www\.behance\.net\/gallery\/\d+/.test(n)||/^https:\/\/vimeo\.com\/\d+/.test(n)||/^https:\/\/www\.instagram\.com\/p\//.test(n))?n:null;};
 async function client(){let c=(await chrome.storage.local.get('xpcBridgeClient')).xpcBridgeClient;if(!c){c=crypto.randomUUID();await chrome.storage.local.set({xpcBridgeClient:c});}return c;}
 async function openWorkTab(url){
  // An extension service worker has no implicit "current window". Choose a
  // real browser window explicitly; if the browser is running headless in the
  // background, open one work window for the requested page.
  const windows=await chrome.windows.getAll({windowTypes:['normal']});
  const parent=windows.find(w=>w.focused)||windows[0];
  if(parent)return chrome.tabs.create({windowId:parent.id,url,active:false});
  const created=await chrome.windows.create({url,type:'normal',focused:true});
  const tab=created?.tabs?.[0]||(created?.id?(await chrome.tabs.query({windowId:created.id}))[0]:null);
  if(!tab?.id)throw Error('浏览器未能打开作品页，请保持 Chrome 或 Edge 运行后重试。');
  return tab;
 }
 async function poll(){
  if(busy)return;busy=true;
  try{
   const c=await client(),r=await native({action:'xpcBrowserPoll',client:c,behance:true,vimeoTags:true,instagramTags:true,extensionVersion:chrome.runtime?.getManifest?.().version||'',labelParserVersion:platforms.labelParserVersion||0});
   for(const req of r.requests||[]){
    const url=canonical(req.url);if(!url||url!==req.url)continue;
    const vimeo=new URL(url).hostname==='vimeo.com',instagram=new URL(url).hostname==='www.instagram.com',behance=new URL(url).hostname==='www.behance.net',origin=new URL(url).origin;
    const sameWork=value=>{const n=canonical(value);if(!n)return false;return behance?n.split('/').slice(0,5).join('/')===url.split('/').slice(0,5).join('/'):n===url;};
    if(!await chrome.permissions.contains({origins:[origin+'/*']})){
     await native({action:'xpcBrowserResult',client:c,id:req.id,state:'failed',message:'请在 Chrome 扩展管理页重新加载轻存并允许访问 '+(behance?'Behance':vimeo?'Vimeo':instagram?'Instagram':'新片场')+'，再重新下载。'});continue;
    }
    const key='xpcRequest_'+req.id,send=(state,extra={})=>native({action:'xpcBrowserResult',client:c,id:req.id,state,...extra});
    let record=(await chrome.storage.local.get(key))[key],tab;
    try{
     if(record){try{tab=await chrome.tabs.get(record.tabId);}catch{throw Error('作品页面已关闭，本次读取已停止；重新添加链接即可重试。');}
      if(!sameWork(tab.url)&&tab.status!=='loading'&&new URL(tab.url).origin!==origin)throw Error('作品页面已跳转，本次读取已停止。');
     }else{
      const tabs=await chrome.tabs.query({url:origin+'/*'});tab=tabs.find(t=>sameWork(t.url));
      const created=!tab;if(!tab)tab=await openWorkTab(url);
      record={tabId:tab.id,created,expires:req.expires};await chrome.storage.local.set({[key]:record});
     }
     if(tab.status!=='complete')continue;
     await send('reading',{message:'正在读取作品页及内嵌播放器。'});
     let page,captureError;try{page=await platforms.read(chrome,tab.id);}catch(e){captureError=e;}
     const embeds=(page?.urls||[]).map(u=>platforms.normalize(u)).filter(u=>u&&(/^https:\/\/vimeo\.com\//.test(u)||u.startsWith('https://www.youtube.com/watch?v=')||/^https:\/\/[^/]*behance\.net\/.*\.(mp4|m3u8)(\?|$)/i.test(u)));
     if(sameWork(page?.pageUrl)&&(vimeo||instagram?(page.labelReadStatus==='read'||page.tags?.length>0||page.categories?.length>0):behance?embeds.length>0:((page.xpc?.vid&&page.xpc?.appKey)||platforms.pageMedia(page.mediaUrl||'',url)))){ 
      const vimeoSession=(vimeo||behance&&embeds.some(u=>u.startsWith('https://vimeo.com/')))?await getSession():'';
      await send('ready',{page,vimeoSession});
      if(record.created){const current=await chrome.tabs.get(tab.id);if(!current.active&&sameWork(current.url))await chrome.tabs.remove(tab.id);}
      await chrome.storage.local.remove(key);continue;
     }
     if(captureError?.code==='PAGE_READ_TIMEOUT')throw captureError;
     if(behance&&page?.frameReadIncomplete)throw Error(page.frameReadReason==='timeout'?'作品页已读取，但内嵌播放器未在限定时间内返回；未能确认视频入口，请重试。':'作品页已读取，但内嵌播放器暂不可读取；未能确认视频入口。');
     const statusRequest=chrome.scripting.executeScript({target:{tabId:tab.id},injectImmediately:true,func:()=>({challenge:/验证码|安全验证|人机验证|访问验证|访问过于频繁|captcha|verify you are human|security verification/i.test((document.title||'')+'\n'+(document.body?.innerText||'').slice(0,3000))})});
     const status=await platforms.within(statusRequest,4000,'页面状态检查超时，请重试。');
     const challenge=status[0]?.result?.challenge;
     if(!challenge&&!sameWork(tab.url))throw Error('作品页面已跳转，本次读取已停止。');
     if(!challenge&&captureError)throw captureError;
     await send(challenge?'verification':'reading',{message:challenge?'作品页要求验证。':'页面已读取，暂未识别到可下载的视频入口。'});
     if(challenge&&!record.shown){await chrome.tabs.update(tab.id,{active:true});record.shown=true;await chrome.storage.local.set({[key]:record});}
    }catch(e){await send('failed',{message:String(e.message||'网页读取未完成').slice(0,180)}).catch(()=>{});}
   }
   const saved=await chrome.storage.local.get(null);
   const old=Object.keys(saved).filter(k=>k.startsWith('xpcRequest_')&&Date.parse(saved[k]?.expires||'')<Date.now());
   if(old.length)await chrome.storage.local.remove(old);
  }catch{/* Browser/native component may be updating. The queued job owns its timeout and status. */}
  finally{busy=false;}
 }
 function wake(){clearTimeout(wakeTimer);wakeTimer=setTimeout(()=>poll(),500);}
 async function start(){if(!await chrome.alarms.get(alarm))await chrome.alarms.create(alarm,{periodInMinutes:.5});await poll();}
 return {poll,start,wake,alarm};
}
if(typeof module!=='undefined')module.exports={createXpcAssist};
