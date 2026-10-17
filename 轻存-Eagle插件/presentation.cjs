'use strict';
const unique=a=>[...new Set(a.filter(x=>typeof x==='string'&&x.trim()).map(x=>x.trim()))];
const norm=s=>String(s||'').trim().toLowerCase();
function noise(s){return /^(?:完整参考片|完整视频|待分类|未分类|未识别|待确认|未知|不适用|本地文件|横屏|竖屏|方屏|横图|竖图|方图|横向|竖向|原视频与原声音|原视频|视频|图片|youtube|vimeo|behance|instagram|pinterest|bilibili|b站|哔哩哔哩|抖音|tiktok|新片场|jpg|jpeg|png|webp|gif|bmp|tiff?|avif|mp4|mov|mkv|webm|\d{2,5}\s*[x×*]\s*\d{2,5}|\d+(?:\.\d+)?\s*(?:帧\/秒|fps)|\d{3,4}p|[248]k)$/i.test(String(s||'').trim());}
function legacyContent(body){
 const m=/(?:^|\n)原始简介[：:]\s*\n([\s\S]*?)(?=\n(?:轻存备注[：:]|已确认镜头[（(]|另有分析结果待确认|原文件[：:])|$)/.exec(body);
 const n=/(?:^|\n)轻存备注[：]\s*\n([\s\S]*?)(?=\n(?:已确认镜头[（(]|另有分析结果待确认|原文件[：:])|$)/.exec(body);
 return [m?.[1]?.trim(),n?.[1]?.trim()].filter(Boolean).join('\n\n');
}
function sourceAnnotation(text){
 return String(text||'').replace(/〔轻存资料:[^〕]+〕([\s\S]*?)〔\/轻存资料〕/g,(_,b)=>legacyContent(b)).replace(/〔轻存已有视频:[^〕]+〕[\s\S]*?〔\/轻存已有视频〕/g,'').trim();
}
function productTags(a){
 const facts=(a.sourceFacts||[]).filter(f=>f.label==='产品').map(f=>String(f.value||'').trim()).filter(v=>v&&!noise(v)&&v.length<=160);
 const primary=[a.title||'',...facts].join('\n'),source=[...(a.originalTags||[]),sourceAnnotation(a.description)].join('\n').slice(0,12000);
 const rules=[
  ['香水',/\b(?:perfume|fragrance|eau de parfum|eau de toilette)\b|香水|香氛/i],
  ['智能手表',/\b(?:smartwatch|apple watch)\b|智能手表/i],
  ['手表',/\b(?:wristwatch|timepiece|watches|time-only watch|pilot.s watch|watch (?:case|dial|strap|is completed|with a))\b|腕表|手表/i],
  ['汽车',/\b(?:luxury car|automotive|electric car|cars|suv|sedan|coupe|sports car|concept car|performance car)\b|汽车|轿车|跑车/i],
  ['耳机',/\b(?:headphones|earphones|earbuds|airpods)\b|耳机/i],
  ['手机',/\b(?:smartphone|iphone|mobile phone)\b|手机/i],
  ['相机',/\b(?:digital camera|mirrorless camera|dslr)\b|数码相机|微单相机/i],
  ['运动鞋',/\b(?:sneakers|running shoes|trainers)\b|运动鞋|跑鞋/i],
  ['护肤品',/\b(?:skincare|skin serum|face cream)\b|护肤品|精华液|面霜/i]
 ];
 const inferred=a.classificationSchema>=1&&['手机','手表'].includes(a.productType)?a.productType:'';
 const hit=inferred?[inferred]:rules.find(([,re])=>re.test(primary))||rules.find(([,re])=>re.test(source));
 const identified=a.classificationSchema>=3&&a.productIdentity?.evidence?[a.productIdentity.name,a.productIdentity.tag]:[];
 return unique([...facts,...identified,...(hit?[hit[0]]:[])]);
}
function meaningfulTags(a,f=a.files?.[0]||{}){
 const dismissed=new Set(a.dismissedTags||[]),blocked=(dim,name)=>dismissed.has(dim+'|'+norm(name));
 // Behance's site-wide SEO keywords describe its service, not this work.
 // Filter only automatic source tags; a confirmed/manual tag remains intact.
 const behance=[a.url,...(a.sourcePages||[]).map(p=>p.pageUrl||p.url)].some(u=>{try{return /(^|\.)behance\.net$/.test(new URL(u).hostname);}catch{return false;}});
 const siteKeyword=n=>behance&&/^(?:online portfolio(?: site)?|creative professional platform|creative network|creative community|creative talent)$/i.test(String(n||'').trim());
 const manual=(a.tags||[]).filter(t=>t.status==='confirmed'&&!blocked(t.dimension,t.name)).map(t=>t.name);
 const raw=[...(a.originalTags||[]),...(a.originalHashtags||[]),...(a.tags||[]).filter(t=>t.dimension==='平台关键词'&&t.status==='source').map(t=>t.name)].filter(n=>!blocked('平台关键词',n)&&!siteKeyword(n));
 const categories=[...(a.sourceCategories||[]),...(a.tags||[]).filter(t=>t.dimension==='平台分类'&&t.status==='source').map(t=>t.name)].filter(n=>!blocked('平台分类',n));
 const products=productTags(a).filter(n=>!blocked('产品',n));
 const views=[a.analysisDraft,a.analysis].filter(v=>v&&(!v.filePath||norm(v.filePath)===norm(f.path)));
 const view=views.find(v=>v.shots?.some(s=>s.status==='confirmed'));
 const shotTags=(a.tagAnalysisMode==='film_type'?[]:(view?.shots||[])).filter(s=>s.status==='confirmed').flatMap(s=>[s.movement,s.scale,s.color]);
 const img=a.imageAnalysis;const imageTags=a.mediaType==='image'&&img?.status==='confirmed'&&img.fileKey===f.key?Object.values(img.labels||{}):[];
 const film=a.filmTypeAnalysis;const filmTags=film?.status==='confirmed'&&film.fileKey===f.key&&film.type?[film.type]:[];
 return unique([...raw,...categories,...manual,...[...products,...shotTags,...imageTags,...filmTags].filter(t=>!noise(t))]);
}
// Eagle annotations are plain prose: at most one empty line between paragraphs.
function formatAnnotation(text){
 return String(text||'').replace(/\r\n?|[\u2028\u2029]/g,'\n').split('\n').map(line=>/^[\s\u200B-\u200D\u2060\uFEFF]*$/.test(line)?'':line.replace(/[\t \u00A0]+$/g,'')).join('\n').replace(/\n{3,}/g,'\n\n').trim();
}
function webUrl(text){
 if(!/^(?:https?:\/\/|www\.)\S+$/i.test(text))return false;
 try{const u=new URL(/^www\./i.test(text)?'https://'+text:text);return ['http:','https:'].includes(u.protocol)&&!!u.hostname;}catch{return false;}
}
// Eagle can wrap URL text in an anchor after editing. Only unwrap identical labels;
// named links and actual user edits remain distinguishable from generated text.
function plainAutoLinks(text){
 const decode=s=>s.replace(/&amp;/gi,'&').replace(/&#38;/g,'&').replace(/&quot;/gi,'"').replace(/&#39;/g,"'");
 return String(text||'').replace(/<a\b([^>]*)>([^<>]*)<\/a>/gi,(all,attrs,label)=>{
  const m=/\bhref\s*=\s*(["'])(.*?)\1/i.exec(attrs);if(!m)return all;
  const href=decode(m[2]),name=decode(label);return name===href&&webUrl(href)?href:all;
 });
}
function standaloneUrl(line){
 let text=plainAutoLinks(line).trim().replace(/^(?:[-*•]\s+)/,'');
 text=text.replace(/^(?:来源|原视频(?:网址)?|官网|网站|主页|个人网站|个人主页|source|website|web site|homepage|home page|portfolio|instagram|tiktok|facebook|twitter|threads|youtube|vimeo|behance|pinterest|x)[：:]\s*/i,'');
 const markdown=/^\[[^\]\n]*\]\((\S+)\)$/.exec(text);if(markdown)text=markdown[1];
 if(text.startsWith('<')&&text.endsWith('>'))text=text.slice(1,-1);
 return webUrl(text);
}
function cleanSourceDescription(a){
 return formatAnnotation(sourceAnnotation(a.description).replace(/\r\n?/g,'\n').split('\n').filter(line=>!standaloneUrl(line)).join('\n'));
}
function annotation(a){
 const text=[cleanSourceDescription(a)],body=()=>text.join('\n\n');
 const credits=(a.credits||[]).filter(c=>['source','confirmed'].includes(c.status)&&c.name);
 for(const c of credits)if(!body().toLowerCase().includes(c.name.toLowerCase()))text.push(unique([c.role?c.role+'：'+c.name:c.name,c.account&&!standaloneUrl(c.account)?c.account:'']).join(' · '));
 const publisher=a.publisher||{},profile=publisher.url;
 if(profile&&profile!==a.url&&!body().includes(publisher.name||'\0')){
  const names=unique([publisher.name,publisher.account&&!standaloneUrl(publisher.account)?publisher.account:'']);
  if(names.length)text.push('发布者：'+names.join(' · '));
 }
 if(a.notes&&!body().includes(a.notes))text.push(a.notes);
 return formatAnnotation(text.filter(Boolean).join('\n\n'));
}
// Previous generated text is tracked in local state, never printed as an identifier in Eagle.
function mergeAnnotation(old,part,rec={}){
 old=String(old||'');const begin=part.marker,end=part.end,next=formatAnnotation(part.annotation);
 let i=old.indexOf(begin),changed=false;
 while(i>=0){const j=old.indexOf(end,i);if(j<0)break;old=old.slice(0,i)+(changed?'':next)+old.slice(j+end.length);changed=true;i=old.indexOf(begin);}
 if(changed)return formatAnnotation(old);
 if(old===next||(!old&&!next))return formatAnnotation(old);
 if(rec.annotationDetached)return formatAnnotation(old);
 if(typeof rec.autoAnnotation==='string'&&rec.autoAnnotation){
  let at=old.indexOf(rec.autoAnnotation);
  while(at>=0){
   const end=at+rec.autoAnnotation.length;
   if((at===0||/[\r\n]/.test(old[at-1]))&&(end===old.length||/[\r\n]/.test(old[end])))return formatAnnotation(old.slice(0,at)+next+old.slice(end));
   at=old.indexOf(rec.autoAnnotation,at+1);
  }
  if(formatAnnotation(plainAutoLinks(old))===formatAnnotation(plainAutoLinks(rec.autoAnnotation)))return next;
  // Someone edited the generated prose. Keep their edit rather than append a second description.
  rec.annotationDetached=true;return formatAnnotation(old);
 }
 if(!next||old.includes(next))return formatAnnotation(old);
 return [old,next].filter(Boolean).join('\n\n');
}
module.exports={noise,productTags,meaningfulTags,sourceAnnotation,cleanSourceDescription,annotation,mergeAnnotation,formatAnnotation,standaloneUrl,plainAutoLinks,unique};
