'use strict';
const http=require('http');
// Eagle's documented local API; no direct writes to library files or preferences.
function request(route,data){return new Promise((resolve,reject)=>{
 const body=data===undefined?null:Buffer.from(JSON.stringify(data));
 const q=http.request({hostname:'127.0.0.1',port:41595,path:'/api/library/'+route,method:body?'POST':'GET',headers:body?{'Content-Type':'application/json','Content-Length':body.length}:{}},r=>{
  const chunks=[];let size=0;r.on('data',b=>{size+=b.length;if(size>16*1024*1024){r.destroy(Error('Eagle 返回资料过大。'));return;}chunks.push(b);});
  r.on('error',reject);r.on('end',()=>{try{const result=JSON.parse(Buffer.concat(chunks));if(r.statusCode!==200||result.status!=='success')throw Error(result.message||'Eagle 资源库操作未完成。');resolve(result.data);}catch(e){reject(e);}});
 });
 q.setTimeout(15000,()=>q.destroy(Error('Eagle 资源库操作超时，请检查当前打开的库后重试。')));
 q.on('error',reject);q.end(body);
});}
module.exports={history:async()=>{const rows=await request('history');return Array.isArray(rows)?rows.filter(p=>typeof p==='string'):[];},switchLibrary:libraryPath=>request('switch',{libraryPath})};
