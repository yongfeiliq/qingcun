'use strict';
(()=>{
 const bridge=window.parent.Qingcun;
 globalThis.QingcunContext={available:!!bridge};
 if(!bridge){document.body.textContent='请从 Eagle 的轻存插件打开此页面。';return;}
 globalThis.QingcunVimeo={handles:()=>false};
 if(!globalThis.chrome)globalThis.chrome={};chrome.runtime={sendNativeMessage:(_host,data,callback)=>{bridge.call(data).then(callback,e=>callback({ok:false,error:e.message}));}};
 document.addEventListener('click',event=>{const a=event.target.closest('a[href]');if(!a)return;const u=new URL(a.href,location.href);if(u.protocol==='https:'||u.protocol==='http:'){event.preventDefault();bridge.openExternal(u.href);}},true);
})();