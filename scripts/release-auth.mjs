import {owner,repo,authorizationHeaders} from './release-metadata.mjs';

const repository=owner+'/'+repo;
const repositoryId='1423916513';
const workflowRef=repository+'/.github/workflows/release.yml@refs/heads/main';

export function actionsReleaseContext(environment,revision) {
 if(environment.GITHUB_ACTIONS!=='true')return null;
 if(!/^[0-9a-f]{40}$/.test(revision)||environment.GITHUB_REPOSITORY!==repository
  ||environment.GITHUB_REPOSITORY_ID!==repositoryId||environment.GITHUB_REPOSITORY_OWNER!==owner
  ||environment.GITHUB_REF!=='refs/heads/main'||environment.GITHUB_SHA!==revision
  ||environment.GITHUB_WORKFLOW_REF!==workflowRef||environment.GITHUB_WORKFLOW_SHA!==revision
  ||environment.GITHUB_SERVER_URL!=='https://github.com'||environment.GITHUB_API_URL!=='https://api.github.com'
  ||environment.RUNNER_OS!=='Windows'||!['push','workflow_dispatch'].includes(environment.GITHUB_EVENT_NAME))
  throw Error('GITHUB_CI_SCOPE');
 return Object.freeze({repository,repositoryId,revision});
}

export function releaseAuthorizationHeaders(context,environment,localHeaders=authorizationHeaders) {
 // A token in a local shell never bypasses the existing credential-helper + account-owner path.
 if(context===null)return localHeaders();
 const token=environment.GITHUB_TOKEN;
 if(typeof token!=='string'||!/^\S{20,4096}$/.test(token))throw Error('GITHUB_AUTH_REQUIRED');
 return{Authorization:'Bearer '+token,Accept:'application/vnd.github+json',
  'X-GitHub-Api-Version':'2022-11-28','User-Agent':'SLX-Windows-Release'};
}

export async function assertMainHead(api,context) {
 if(context===null)return;
 const head=await api('/repos/'+repository+'/git/ref/heads/main');
 if(head.status!==200||head.data.ref!=='refs/heads/main'||head.data.object?.type!=='commit'
  ||head.data.object.sha!==context.revision)throw Error('RELEASE_HEAD_MOVED');
}

export async function verifyReleaseAuthority(api,context) {
 if(context===null){
  const me=await api('/user');
  if(me.status!==200||me.data.login!==owner)throw Error('GITHUB_OWNER_MISMATCH');
  return;
 }
 // Installation tokens do not represent /user. Authorize only the fixed public repository
 // and its exact current main commit; never fall back to this path after a local owner failure.
 const repositoryResponse=await api('/repos/'+repository);
 if(repositoryResponse.status!==200||repositoryResponse.data.full_name!==repository
  ||String(repositoryResponse.data.id)!==repositoryId||repositoryResponse.data.private!==false
  ||repositoryResponse.data.default_branch!=='main')throw Error('GITHUB_CI_SCOPE');
 await assertMainHead(api,context);
}
