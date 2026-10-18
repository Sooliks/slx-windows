import {owner,repo,authorizationHeaders,githubApi} from './release-metadata.mjs';
const mode=process.argv[2];
try {
 if(!['status','create'].includes(mode))throw Error('MODE_SCOPE');
 const api=githubApi(authorizationHeaders());
 const me=await api('/user');if(me.status!==200||me.data.login!==owner)throw Error('GITHUB_OWNER_MISMATCH');
 if(mode==='status'){console.log(JSON.stringify({authenticated:true,owner}));}
 else {
  let response=await api('/repos/'+owner+'/'+repo);
  if(response.status===404)response=await api('/user/repos','POST',{
   name:repo,description:'Open-source SLX VPN client for Windows: per-application routing, Hysteria2 and VLESS/XHTTP.',
   private:false,has_issues:true,has_wiki:false,auto_init:false});
  if(![200,201].includes(response.status)||response.data.full_name!==owner+'/'+repo||response.data.private!==false)
   throw Error('GITHUB_REPOSITORY_SCOPE');
  console.log(JSON.stringify({repository:response.data.html_url,visibility:'public'}));
 }
}catch(error){
 const safe=['GITHUB_AUTH_REQUIRED','GITHUB_OWNER_MISMATCH','GITHUB_REPOSITORY_SCOPE','MODE_SCOPE'];
 console.log(JSON.stringify({failure:safe.includes(error.message)?error.message:'GITHUB_OPERATION_FAILED'}));process.exitCode=1;
}
