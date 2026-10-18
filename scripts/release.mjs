import {spawnSync} from 'node:child_process';
import {readFile,readdir,stat} from 'node:fs/promises';
import {resolve,basename} from 'node:path';
const owner='Sooliks',repo='slx-windows',tag='v0.1.0-alpha.1';
let token;
const safe=['GITHUB_AUTH_REQUIRED','GITHUB_OWNER_MISMATCH','RELEASE_SCOPE','ASSET_FAILED','WORKTREE_NOT_CLEAN'];
try {
 const env=Object.fromEntries(['PATH','SystemRoot','TEMP','TMP','USERPROFILE','USERNAME','USERDOMAIN','HOMEDRIVE','HOMEPATH','APPDATA','LOCALAPPDATA','ProgramData','COMSPEC'].filter(k=>process.env[k]).map(k=>[k,process.env[k]]));
 Object.assign(env,{GIT_TERMINAL_PROMPT:'0',GCM_INTERACTIVE:'never'});
 const credential=spawnSync('git',['credential','fill'],{input:'protocol=https\nhost=github.com\n\n',encoding:'utf8',windowsHide:true,timeout:15000,env,stdio:['pipe','pipe','ignore']});
 token=(credential.stdout??'').split(/\r?\n/).find(line=>line.startsWith('password='))?.slice(9);
 if(credential.status!==0||!token)throw Error('GITHUB_AUTH_REQUIRED');
 const headers={Authorization:'Bearer '+token,Accept:'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28','User-Agent':'SLX-Windows-Release'};
 const api=async(path,method='GET',body)=>{
  const r=await fetch('https://api.github.com'+path,{method,headers:{...headers,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(30000)});
  return{status:r.status,data:await r.json()};
 };
 const me=await api('/user');if(me.status!==200||me.data.login!==owner)throw Error('GITHUB_OWNER_MISMATCH');
 const dirty=spawnSync('git',['status','--porcelain'],{encoding:'utf8',windowsHide:true});
 if(dirty.status!==0||dirty.stdout.trim())throw Error('WORKTREE_NOT_CLEAN');
 const revision=spawnSync('git',['rev-parse','HEAD'],{encoding:'utf8',windowsHide:true}).stdout.trim();
 const directory=resolve('artifacts/release');
 const names=(await readdir(directory)).filter(name=>/^(SLX-VPN-0\.1\.0-alpha\.1-[A-Za-z0-9.-]+\.(exe|zip)|SHA256SUMS\.txt|provenance\.json)$/.test(name));
 if(!names.some(name=>name.endsWith('setup.exe'))||!names.includes('SHA256SUMS.txt'))throw Error('RELEASE_SCOPE');
 let release=await api('/repos/'+owner+'/'+repo+'/releases/tags/'+tag);
 if(release.status===404)release=await api('/repos/'+owner+'/'+repo+'/releases','POST',{tag_name:tag,target_commitish:revision,name:'SLX VPN 0.1.0 alpha 1 — Windows x64',body:await readFile('docs/RELEASE_NOTES.md','utf8'),draft:true,prerelease:true});
 if(![200,201].includes(release.status)||!release.data.draft)throw Error('RELEASE_SCOPE');
 for(const name of names){
  if(release.data.assets?.some(asset=>asset.name===name))throw Error('RELEASE_SCOPE');
  const path=resolve(directory,name),info=await stat(path);
  if(!info.isFile()||info.size>300*1024*1024)throw Error('RELEASE_SCOPE');
  const endpoint='https://uploads.github.com/repos/'+owner+'/'+repo+'/releases/'+release.data.id+'/assets?name='+encodeURIComponent(basename(path));
  const upload=await fetch(endpoint,{method:'POST',headers:{...headers,'Content-Type':'application/octet-stream','Content-Length':String(info.size)},body:await readFile(path),signal:AbortSignal.timeout(180000)});
  if(upload.status!==201)throw Error('ASSET_FAILED');
  console.log(JSON.stringify({uploaded:name,bytes:info.size}));
 }
 const published=await api('/repos/'+owner+'/'+repo+'/releases/'+release.data.id,'PATCH',{draft:false,prerelease:true});
 if(published.status!==200)throw Error('RELEASE_SCOPE');
 console.log(JSON.stringify({release:published.data.html_url,commit:revision}));
}catch(e){console.log(JSON.stringify({failure:safe.includes(e.message)?e.message:'RELEASE_FAILED'}));process.exitCode=1}
finally{token=undefined}
