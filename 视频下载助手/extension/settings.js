(() => {
'use strict';
if (!globalThis.QingcunContext?.available) return;
const host='com.qingcun.video_downloader',$=id=>document.getElementById(id);
let savedModel=null,modelBusy=false;
function call(action,data={}){return new Promise((resolve,reject)=>chrome.runtime.sendNativeMessage(host,{action,...data},r=>{if(chrome.runtime.lastError)return reject(Error('未连接到本地组件，请先运行新版 Install.cmd。'));if(!r?.ok)return reject(Error(r?.error||'操作失败'));resolve(r);}));}
function show(text,error=false,id='message'){$(id).textContent=text;$(id).classList.toggle('error',error);}
function setBusy(value){modelBusy=value;for(const id of ['saveModel','provider','sampleFps','model','apiKey'])$(id).disabled=value;$('analysisEnabled').disabled=value||!savedModel;$('autoAnalyze').disabled=value||!savedModel?.enabled;$('testModel').disabled=value||!savedModel?.enabled;$('removeKey').disabled=value||!savedModel?.hasKey;}
function renderProvider(){
 const codex=$('provider').value==='codex',enabled=!!savedModel?.enabled,activeCodex=savedModel?.provider!=='gemini';
 $('codexFields').hidden=!codex;$('geminiFields').hidden=codex;$('model').required=!codex;
 $('toggleTitle').textContent=activeCodex?'启用 Codex 分析':'启用 Gemini 分析';
 $('enabledInfo').hidden=!enabled;
 $('toggleHint').textContent=enabled?'已开启。可随时关闭；下载、入库和已有标签不受影响。':'已关闭。下载、入库和手动标签正常使用，无需登录或配置模型。';
 $('samplingHint').textContent='仅影响镜头分析；Eagle 整片类型识别不需要调整。'+(codex?'本地抽帧，每段最多 96 张。':'按每秒 2 / 4 / 8 帧采样，更密通常消耗更多额度。');
 $('uploadNotice').textContent=activeCodex?'开始分析时，素材画面会通过本机 Codex 发送给 OpenAI，使用你的 Codex 额度。原文件保留在本机。':'开始分析时，素材画面会发送到 Google，使用你的 Gemini 额度。原文件保留在本机。';
}
function applyModel(r){savedModel=r;$('provider').value=r.provider||'codex';$('model').value=r.model||'';$('analysisEnabled').checked=!!r.enabled;$('autoAnalyze').checked=!!r.autoAnalyze;$('sampleFps').value=String(r.sampleFps||4);$('keyState').textContent=r.hasKey?'密钥已加密保存 · 留空可保留':'尚未保存密钥';$('codexState').textContent=r.provider==='gemini'?'使用已保存的 Gemini 配置。':(r.codex?.message||'使用本机 Codex 登录。');$('modelStatus').textContent=!r.enabled?'已关闭 · 可选':r.configured?'已开启 · '+(r.provider==='codex'?'Codex':'Gemini'):'已开启 · 连接暂不可用';$('apiKey').value='';renderProvider();setBusy(modelBusy);}
async function load(){setBusy(true);const results=await Promise.allSettled([call('settings'),call('modelSettings')]);if(results[0].status==='fulfilled'){const r=results[0].value;$('path').textContent=r.folder;$('autoClassify').checked=r.autoClassify;if(r.storageMode==='eagle'){$('storageTitle').textContent='下载暂存与 Eagle 分类';$('storageHint').textContent='上方位置用于临时下载。核验入库后只保留 Eagle 中的视频，分类和品牌文件夹仍保留在 Eagle。Eagle 关闭或入库失败时，暂存文件会保留。';}}else show(results[0].reason.message,true);if(results[1].status==='fulfilled')applyModel(results[1].value);else show('暂时无法读取分析开关。'+results[1].reason.message,true,'modelMessage');setBusy(false);}
$('analysisEnabled').addEventListener('change',async()=>{
 if(modelBusy||!savedModel)return;const enabled=$('analysisEnabled').checked;setBusy(true);show(enabled?'正在开启分析…':'正在关闭分析…',false,'modelMessage');
 try{applyModel(await call('setAnalysisEnabled',{enabled,provider:savedModel.provider}));show(enabled?'已开启并自动保存，需要时可开始分析。':'已关闭并自动保存。已有标签和模型配置保留，下载与入库可正常使用。',false,'modelMessage');}
 catch(e){$('analysisEnabled').checked=!!savedModel.enabled;renderProvider();show(e.message,true,'modelMessage');}
 finally{setBusy(false);}
});
$('provider').addEventListener('change',()=>{renderProvider();show('服务选择在保存高级设置后生效。',false,'modelMessage');});
$('pick').addEventListener('click',async()=>{$('pick').disabled=true;show('请在文件夹选择窗口中确认保存位置。');try{const r=await call('chooseFolder');$('path').textContent=r.folder;show('保存位置已确认。');}catch(e){show(e.message,true);}finally{$('pick').disabled=false;}});
$('open').addEventListener('click',async()=>{try{await call('openFolder');}catch(e){show(e.message,true);}});
$('save').addEventListener('click',async()=>{$('save').disabled=true;try{await call('saveSettings',{autoClassify:$('autoClassify').checked});show('分类设置已保存。');}catch(e){show(e.message,true);}finally{$('save').disabled=false;}});
function modelValues(){return {provider:$('provider').value,model:$('model').value.trim(),apiKey:$('apiKey').value.trim(),sampleFps:Number($('sampleFps').value),enabled:$('analysisEnabled').checked,autoAnalyze:$('autoAnalyze').checked};}
$('modelForm').addEventListener('submit',async e=>{e.preventDefault();if(modelBusy)return;const values=modelValues();setBusy(true);try{const r=await call('saveModelSettings',values);applyModel(r);show(r.enabled?'高级设置已保存。':'高级设置已保存，智能分析仍处于关闭状态。',false,'modelMessage');}catch(e){show(e.message,true,'modelMessage');}finally{setBusy(false);}});
$('testModel').addEventListener('click',async()=>{if(modelBusy||!savedModel?.enabled)return;if($('provider').value!==savedModel.provider||($('provider').value==='gemini'&&($('apiKey').value.trim()||$('model').value.trim()!==savedModel.model)))return show('请先保存修改后的配置，再检测。',true,'modelMessage');setBusy(true);show('正在检查已保存的连接…',false,'modelMessage');try{const r=await call('testModel');applyModel(await call('modelSettings'));show(r.message,false,'modelMessage');}catch(e){show(e.message,true,'modelMessage');}finally{setBusy(false);}});
$('removeKey').addEventListener('click',async()=>{if(modelBusy)return;const values=modelValues();setBusy(true);try{const r=await call('saveModelSettings',{...values,apiKey:'',removeKey:true,enabled:false,autoAnalyze:false});applyModel(r);show('Gemini 密钥已移除，已有分析结果保留。',false,'modelMessage');}catch(e){show(e.message,true,'modelMessage');}finally{setBusy(false);}});
load();
})();
