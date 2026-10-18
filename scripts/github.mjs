import {spawnSync} from 'node:child_process';
const owner='Sooliks', repo='slx-windows', mode=process.argv[2];
const env=Object.fromEntries(['PATH','SystemRoot','TEMP','TMP','USERPROFILE','USERNAME','USERDOMAIN','HOMEDRIVE','HOMEPATH','APPDATA','LOCALAPPDATA','ProgramData','COMSPEC'].filter(k=>process.env[k]).map(k=>[k,process.env[k]]));
Object.assign(env,{GIT_TERMINAL_PROMPT:'0',GCM_INTERACTIVE:'never'});
let token;
try {
 const r=spawnSync('C:/Program Files/Git/cmd/git.exe',['credential','fill'],{input:'protocol=https\nhost=github.com\n\n',encoding:'utf8',windowsHide:true,timeout:15000,env,stdio:['pipe','pipe','ignore']});
 const fields=Object.fromEntries((r.stdout??'').trim().split(/\r?\n/).map(s=>{const i=s.indexOf('=');return[s.slice(0,i),s.slice(i+1)]}));
 token=fields.password;if(r.status!==0||!token)throw Error('GITHUB_AUTH_REQUIRED');
 const headers={Authorization:'Bearer '+token,Accept:'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28','User-Agent':'SLX-Windows-Release'};
 const api=async(path,method='GET',body)=>{const r=await fetch('https://api.github.com'+path,{method,headers:{...headers,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(20000)});return{status:r.status,data:await r.json()}};
 const me=await api('/user');if(me.status!==200||me.data.login!==owner)throw Error('GITHUB_OWNER_MISMATCH');
 if(mode==='status'){console.log(JSON.stringify({authenticated:true,owner}));}
 else if(mode==='create'){
  let r=await api('/repos/'+owner+'/'+repo);
  if(r.status===404)r=await api('/user/repos','POST',{name:repo,description:'Open-source SLX VPN client for Windows: per-application routing, Hysteria2 and VLESS/XHTTP.',private:false,has_issues:true,has_wiki:false,auto_init:false});
  if(![200,201].includes(r.status)||r.data.full_name!==owner+'/'+repo||r.data.private!==false)throw Error('GITHUB_REPOSITORY_SCOPE');
  console.log(JSON.stringify({repository:r.data.html_url,visibility:'public'}));
 }else throw Error('MODE_SCOPE');
}catch(e){console.log(JSON.stringify({failure:['GITHUB_AUTH_REQUIRED','GITHUB_OWNER_MISMATCH','GITHUB_REPOSITORY_SCOPE','MODE_SCOPE'].includes(e.message)?e.message:'GITHUB_OPERATION_FAILED'}));process.exitCode=1}
finally{token=undefined}
