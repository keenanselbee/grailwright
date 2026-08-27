[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$testsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '.codex-temp\tests')).TrimEnd('\') + '\'
$scratchRoot = [System.IO.Path]::GetFullPath((Join-Path $testsRoot 'vortex-nexus-metadata'))
. (Join-Path $PSScriptRoot 'VortexNexusMetadataPromotions.ps1')

if (-not $scratchRoot.StartsWith($testsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Scratch path escaped the repository test root: $scratchRoot"
}

function Assert-Contract {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "Vortex Nexus metadata promotion contract failed: $Message"
    }
}

function New-TestReceipt {
    param([string]$ArchivePath)
    $item = Get-Item -LiteralPath $ArchivePath
    return [pscustomobject]@{
        packageName = 'FixtureMod'
        displayName = 'Fixture Mod'
        version = '1.2.3'
        nexus = [pscustomobject]@{
            source = 'nexus'
            url = 'https://www.nexusmods.com/taintedgrailthefallofavalon/mods/276'
            gameDomain = 'taintedgrailthefallofavalon'
            gameScopedModId = '276'
            vortexFileId = '1381'
            logicalFileName = 'Fixture Mod'
            remoteArchiveName = 'Fixture Mod 276 1.2.3.zip'
            category = 'main'
            isPrimary = $true
            nxmUri = 'nxm://taintedgrailthefallofavalon/mods/276/files/1381'
            v3VersionId = '25280177505637'
        }
        archive = [pscustomobject]@{
            fileName = $item.Name
            sizeBytes = [int64]$item.Length
            md5 = (Get-FileHash -LiteralPath $item.FullName -Algorithm MD5).Hash.ToLowerInvariant()
            sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        verification = [pscustomobject]@{ status = 'exact-version-id' }
    }
}

try {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
    $packageRoot = Join-Path $scratchRoot 'package\FixtureMod'
    $stagingRoot = Join-Path $scratchRoot 'staging'
    $stagedPath = Join-Path $stagingRoot 'Fixture Mod 1.2.3'
    $bridgeRoot = Join-Path $scratchRoot 'bridge'
    New-Item -ItemType Directory -Path $packageRoot, $stagedPath -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $packageRoot 'FixtureMod.dll'), 'fixture-dll')
    [System.IO.File]::WriteAllText((Join-Path $packageRoot 'README.txt'), 'fixture-readme')
    Copy-Item -LiteralPath $packageRoot -Destination $stagedPath -Recurse

    $archivePath = Join-Path $scratchRoot 'Fixture Mod 1.2.3.zip'
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath
    $receipt = New-TestReceipt -ArchivePath $archivePath
    $queued = Queue-VortexNexusMetadataPromotion -Receipt $receipt -ArchivePath $archivePath -VortexModsRoot $stagingRoot -BridgeRoot $bridgeRoot
    Assert-Contract ($queued.Status -eq 'queued') 'exact staged release was not queued.'
    Assert-Contract (Test-Path -LiteralPath $queued.RequestPath -PathType Leaf) 'promotion request was not written.'

    $request = Get-Content -LiteralPath $queued.RequestPath -Raw | ConvertFrom-Json
    Assert-Contract ($request.nexus.modId -eq 276 -and $request.nexus.fileId -eq 1381) 'Nexus IDs were not retained.'
    Assert-Contract (@($request.payload).Count -eq 2) 'payload manifest did not retain both files.'
    Assert-Contract (@($request.payload | Where-Object path -eq 'FixtureMod/README.txt').Count -eq 1) 'payload manifest did not preserve the staged package folder.'
    Assert-Contract (Test-Path -LiteralPath $request.archivePath -PathType Leaf) 'exact archive was not retained for Vortex import.'
    Assert-Contract ((Get-FileHash -LiteralPath $request.archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -eq $receipt.archive.sha256) 'queued archive bytes changed.'

    $repeat = Queue-VortexNexusMetadataPromotion -Receipt $receipt -ArchivePath $archivePath -VortexModsRoot $stagingRoot -BridgeRoot $bridgeRoot
    Assert-Contract ($repeat.RequestId -eq $queued.RequestId) 'same immutable release did not produce an idempotent request ID.'

    $localModRoot = Join-Path $scratchRoot 'local-mod'
    $localStagedPath = Join-Path $stagingRoot 'Fixture Mod 1.2.4'
    New-Item -ItemType Directory -Path $localModRoot, $localStagedPath -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $localModRoot 'API.txt'), @'
NexusUrl=https://www.nexusmods.com/taintedgrailthefallofavalon/mods/276
FileName=Fixture Mod
'@)
    [System.IO.File]::WriteAllText((Join-Path $localModRoot 'mod.json'), @'
{
  "id": "FixtureMod",
  "packageName": "FixtureMod",
  "displayName": "Fixture Mod",
  "version": "1.2.4"
}
'@)
    $localManifest = [pscustomobject]@{
        id = 'FixtureMod'
        packageName = 'FixtureMod'
        displayName = 'Fixture Mod'
        version = '1.2.4'
    }
    $localGrouping = Queue-VortexLocalMetadataGrouping `
        -Manifest $localManifest `
        -ModRoot $localModRoot `
        -StagedModId 'Fixture Mod 1.2.4' `
        -StagedPath $localStagedPath `
        -VortexModsRoot $stagingRoot `
        -BridgeRoot $bridgeRoot
    Assert-Contract ($localGrouping.Status -eq 'queued') 'ordinary local stage was not queued for grouping.'
    Assert-Contract ($localGrouping.RequestPath.Contains('grouping-requests')) 'local grouping request used the Nexus promotion queue.'
    $localRequest = Get-Content -LiteralPath $localGrouping.RequestPath -Raw | ConvertFrom-Json
    Assert-Contract ($localRequest.requestType -eq 'local-grouping') 'local request type was not retained.'
    Assert-Contract ($localRequest.grouping.source -eq 'grailwright-local') 'local stage was not truthfully marked as local.'
    Assert-Contract ($localRequest.grouping.modId -eq 276) 'real Nexus page ID was not resolved for grouping.'
    Assert-Contract ($localRequest.grouping.logicalFileName -eq 'Fixture Mod') 'stable logical filename was not retained for grouping.'
    Assert-Contract (-not ($localRequest.PSObject.Properties.Name -contains 'archive')) 'local grouping request fabricated Nexus archive metadata.'
    $localAckRoot = Join-Path $bridgeRoot 'acknowledgements'
    New-Item -ItemType Directory -Path $localAckRoot -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $localAckRoot "$($localGrouping.RequestId).json"), '{"status":"local-grouped"}')
    $repairGrouping = Queue-VortexLocalMetadataGrouping `
        -Manifest $localManifest `
        -ModRoot $localModRoot `
        -StagedModId 'Fixture Mod 1.2.4' `
        -StagedPath $localStagedPath `
        -VortexModsRoot $stagingRoot `
        -BridgeRoot $bridgeRoot `
        -Repair
    Assert-Contract ($repairGrouping.Status -eq 'queued' -and (Test-Path -LiteralPath $repairGrouping.RequestPath)) 'repair did not bypass an older successful acknowledgement.'
    Assert-Contract (-not (Test-Path -LiteralPath (Join-Path $localAckRoot "$($repairGrouping.RequestId).json"))) 'repair retained a stale grouping acknowledgement.'
    [System.IO.File]::WriteAllText((Join-Path $localAckRoot "$($repairGrouping.RequestId).json"), (@{
        requestId = $repairGrouping.RequestId
        status = 'local-grouped'
        activation = @{ status = 'switched'; deployment = 'completed' }
    } | ConvertTo-Json -Depth 5))
    $observedGrouping = Wait-VortexLocalGroupingAcknowledgement -RequestId $repairGrouping.RequestId -BridgeRoot $bridgeRoot -TimeoutSeconds 0
    Assert-Contract ($observedGrouping.Status -eq 'local-grouped' -and $observedGrouping.Acknowledgement.activation.deployment -eq 'completed') 'fresh grouping acknowledgement was not observed.'
    $catalogRecords = @(Get-ChildItem -LiteralPath (Join-Path $bridgeRoot 'catalog-records') -File -Filter '*.json')
    Assert-Contract ($catalogRecords.Count -eq 1) 'ordinary local grouping did not retain its authored catalog identity.'
    $catalogRecord = Get-Content -LiteralPath $catalogRecords[0].FullName -Raw | ConvertFrom-Json
    Assert-Contract ($catalogRecord.packageName -eq 'FixtureMod' -and $catalogRecord.stagedNamePrefix -eq 'Fixture Mod') 'retained catalog identity was malformed.'
    Assert-Contract (-not (Test-Path -LiteralPath (Join-Path $bridgeRoot 'catalog-complete.json'))) 'one local stage incorrectly claimed to be a complete authored catalog.'

    $catalogRequest = Queue-VortexLocalGroupingCatalog -Entries @([pscustomobject]@{
        packageName = 'FixtureMod'
        displayName = 'Fixture Mod'
        stagedNamePrefix = 'Fixture Mod'
        modId = 276
        logicalFileName = 'Fixture Mod'
        nexusUrl = 'https://www.nexusmods.com/taintedgrailthefallofavalon/mods/276'
    }) -BridgeRoot $bridgeRoot
    Assert-Contract ($catalogRequest.Status -eq 'queued') 'state-record grouping catalog was not queued.'
    $catalog = Get-Content -LiteralPath $catalogRequest.RequestPath -Raw | ConvertFrom-Json
    Assert-Contract ($catalog.requestType -eq 'local-grouping-catalog' -and @($catalog.mods).Count -eq 1) 'state-record grouping catalog was malformed.'
    $catalogCompletion = Get-Content -LiteralPath (Join-Path $bridgeRoot 'catalog-complete.json') -Raw | ConvertFrom-Json
    Assert-Contract (@($catalogCompletion.packageNames).Count -eq 1 -and $catalogCompletion.packageNames[0] -eq 'FixtureMod') 'complete authored catalog marker was not retained.'

    $scopedCatalogBridge = Join-Path $scratchRoot 'scoped-catalog-bridge'
    & (Join-Path $PSScriptRoot 'Update-VortexStagedModGrouping.ps1') `
        -Mod 'BloodMagicExpansion' `
        -VortexModsRoot $stagingRoot `
        -VortexMetadataBridgeRoot $scopedCatalogBridge | Out-Null
    $scopedCatalogCompletion = Get-Content -LiteralPath (Join-Path $scopedCatalogBridge 'catalog-complete.json') -Raw | ConvertFrom-Json
    Assert-Contract (@($scopedCatalogCompletion.packageNames).Count -gt 1) 'scoped grouping update incorrectly replaced the complete authored catalog with one mod.'
    Assert-Contract (@($scopedCatalogCompletion.packageNames | Where-Object { $_ -eq 'BloodMagicExpansion' }).Count -eq 1) 'scoped grouping update omitted its target from the complete authored catalog.'

    $localArchivePath = Join-Path $scratchRoot 'Fixture Mod 1.2.4.zip'
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $localArchivePath
    $stageIntegrationRoot = Join-Path $scratchRoot 'stage-integration'
    $stageIntegrationBridge = Join-Path $scratchRoot 'stage-integration-bridge'
    $staleStageAckRoot = Join-Path $stageIntegrationBridge 'acknowledgements'
    New-Item -ItemType Directory -Path $staleStageAckRoot -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $staleStageAckRoot "$($localGrouping.RequestId).json"), (@{
        requestId = $localGrouping.RequestId
        status = 'local-grouped'
        activation = @{ status = 'unchanged'; reason = 'group-disabled' }
    } | ConvertTo-Json -Depth 5))
    $stageResult = & (Join-Path $PSScriptRoot 'Stage-VortexMod.ps1') `
        -ModRoot $localModRoot `
        -PackageArchive $localArchivePath `
        -VortexModsRoot $stageIntegrationRoot `
        -VortexMetadataBridgeRoot $stageIntegrationBridge `
        -GroupingAcknowledgementWaitSeconds 0 |
        Select-Object -Last 1
    Assert-Contract ($stageResult.GroupingStatus -eq 'queued') 'ordinary Vortex staging did not queue grouping metadata.'
    Assert-Contract ($stageResult.GroupingAcknowledgementStatus -eq 'pending') 'ordinary Vortex staging reused a stale grouping acknowledgement.'
    Assert-Contract (Test-Path -LiteralPath (Join-Path $stageIntegrationBridge "grouping-requests\$($stageResult.GroupingRequestId).json") -PathType Leaf) 'same-version restage did not retain a fresh grouping request.'
    Assert-Contract (-not (Test-Path -LiteralPath (Join-Path $staleStageAckRoot "$($stageResult.GroupingRequestId).json"))) 'same-version restage retained its stale activation result.'
    Assert-Contract (Test-Path -LiteralPath $stageResult.VortexPath -PathType Container) 'ordinary Vortex staging did not retain its version folder.'

    [System.IO.File]::WriteAllText((Join-Path $stagedPath 'FixtureMod\README.txt'), 'changed-after-upload')
    $mismatchError = ''
    try {
        Queue-VortexNexusMetadataPromotion -Receipt $receipt -ArchivePath $archivePath -VortexModsRoot $stagingRoot -BridgeRoot (Join-Path $scratchRoot 'mismatch-bridge') | Out-Null
    }
    catch {
        $mismatchError = $_.Exception.Message
    }
    Assert-Contract ($mismatchError.Contains('differs from the uploaded archive')) 'changed staging payload was not rejected.'

    $receipt.nexus.vortexFileId = ''
    $pending = Queue-VortexNexusMetadataPromotion -Receipt $receipt -ArchivePath $archivePath -VortexModsRoot $stagingRoot -BridgeRoot $bridgeRoot
    Assert-Contract ($pending.Status -eq 'pending-vortex-file-id') 'unresolved Nexus file ID was not left pending.'

    $extensionRoot = Join-Path $PSScriptRoot 'vortex-extension\grailwright-nexus-metadata'
    & node --check (Join-Path $extensionRoot 'index.js')
    if ($LASTEXITCODE -ne 0) { throw 'Vortex extension index.js failed node --check.' }
    & node --check (Join-Path $extensionRoot 'promotion-core.js')
    if ($LASTEXITCODE -ne 0) { throw 'Vortex extension promotion-core.js failed node --check.' }

    $mockModuleRoot = Join-Path $scratchRoot 'node_modules\vortex-api'
    New-Item -ItemType Directory -Path $mockModuleRoot -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $mockModuleRoot 'index.js'), 'module.exports={actions:{setModAttribute:(gameId,modId,key,value)=>({type:"attribute",gameId,modId,key,value}),setModEnabled:(profileId,modId,enable)=>({profileId,modId,enable})},selectors:{activeGameId:(state)=>state.activeGameId,activeProfile:(state)=>state.profile,installPathForGame:(state)=>state.stagingRoot},util:{getVortexPath:()=>"mock-vortex-user-data"},log:()=>{}};')
    $previousNodePath = $env:NODE_PATH
    try {
        $env:NODE_PATH = Join-Path $scratchRoot 'node_modules'
        $indexPath = (Join-Path $extensionRoot 'index.js').Replace('\', '\\')
        & node -e "const e=require('$indexPath'); if(typeof e.default!=='function'||e.default({once:()=>{}})!==true) process.exit(1); if(e.getVortexUserDataPath({api:{}})!=='mock-vortex-user-data') process.exit(2); if(e.getVortexUserDataPath({api:{getVortexPath:()=> 'legacy-user-data'}})!=='legacy-user-data') process.exit(3);"
        if ($LASTEXITCODE -ne 0) { throw 'Vortex extension entry point did not load against the API boundary.' }
        $escapedStagingRoot = $stagingRoot.Replace('\', '\\')
        $escapedLocalStagedPath = $localStagedPath.Replace('\', '\\')
        & node -e "const e=require('$indexPath'); const game='taintedgrailthefallofavalon'; const state={activeGameId:game,stagingRoot:'$escapedStagingRoot',persistent:{mods:{[game]:{}}}}; const api={getState:()=>state,events:{emit:(name,eventGame,mod,cb)=>{if(name!=='create-mod'||eventGame!==game) process.exit(4); state.persistent.mods[game][mod.id]=mod; cb(null);}}}; const request={requestType:'local-grouping',gameId:game,stagedModId:'Fixture Mod 1.2.4',stagingPath:'$escapedLocalStagedPath',displayName:'Fixture Mod',version:'1.2.4',grouping:{source:'grailwright-local',modId:276,logicalFileName:'Fixture Mod',nexusUrl:'https://example.invalid'}}; (async()=>{const created=await e.discoverStagedMod(api,request); const mod=state.persistent.mods[game]['Fixture Mod 1.2.4']; if(!created||mod?.state!=='installed'||mod?.installationPath!=='Fixture Mod 1.2.4') process.exit(5); if(mod.attributes?.name!=='Fixture Mod'||mod.attributes?.logicalFileName!=='Fixture Mod'||mod.attributes?.modId!==276||mod.attributes?.source!=='grailwright-local') process.exit(6); if(await e.discoverStagedMod(api,request)) process.exit(7);})().catch(()=>process.exit(8));"
        if ($LASTEXITCODE -ne 0) { throw 'Vortex extension live staged-mod discovery failed its API boundary contract.' }
        & node -e "const e=require('$indexPath'); const game='taintedgrailthefallofavalon'; const mod={id:'Fixture Mod 1.2.4',attributes:{name:'Fixture Mod 1.2.4'}}; const state={activeGameId:game,persistent:{mods:{[game]:{[mod.id]:mod}}}}; const api={getState:()=>state,store:{dispatch:(a)=>{state.persistent.mods[a.gameId][a.modId].attributes[a.key]=a.value;}}}; (async()=>{const result=await e.setAndVerifyModAttributes(api,game,mod.id,{name:'Fixture Mod',version:'1.2.4',logicalFileName:'Fixture Mod',modId:276}); if(!result.changed||mod.attributes.logicalFileName!=='Fixture Mod'||mod.attributes.modId!==276) process.exit(4);})().catch(()=>process.exit(5));"
        if ($LASTEXITCODE -ne 0) { throw 'Vortex extension did not verify persisted grouping attributes.' }
        & node -e "const e=require('$indexPath'); const game='taintedgrailthefallofavalon'; const mod={id:'Fixture Mod 1.2.4',attributes:{name:'Fixture Mod 1.2.4',version:'1.2.4',source:'grailwright-local',modId:999}}; const state={activeGameId:game,persistent:{mods:{[game]:{[mod.id]:mod}}}}; const api={getState:()=>state,store:{dispatch:(a)=>{state.persistent.mods[a.gameId][a.modId].attributes[a.key]=a.value;}}}; const catalog={available:true,entries:[{displayName:'Fixture Mod',stagedNamePrefix:'Fixture Mod',logicalFileName:'Fixture Mod',modId:276,nexusUrl:'https://example.invalid'}]}; (async()=>{const repaired=await e.reconcileGroupingCatalog(api,catalog); if(repaired!==1||mod.attributes.name!=='Fixture Mod'||mod.attributes.logicalFileName!=='Fixture Mod'||mod.attributes.modId!==276) process.exit(4);})().catch(()=>process.exit(5));"
        if ($LASTEXITCODE -ne 0) { throw 'Vortex extension catalog reconciliation failed.' }
        & node -e "const e=require('$indexPath'); const dispatched=[]; const events=[]; const old={id:'Fixture Mod 1.2.3',attributes:{name:'Fixture Mod',version:'1.2.3',logicalFileName:'Fixture Mod',modId:276}}; const newer={id:'Fixture Mod 1.2.4',attributes:{name:'Fixture Mod',version:'1.2.4',logicalFileName:'Fixture Mod',modId:276}}; const state={profile:{id:'profile',gameId:'taintedgrailthefallofavalon',modState:{[old.id]:{enabled:true},[newer.id]:{enabled:false}}},persistent:{mods:{taintedgrailthefallofavalon:{[old.id]:old,[newer.id]:newer}}}}; const api={getState:()=>state,store:{dispatch:(action)=>dispatched.push(action)},events:{emit:(name,...args)=>{events.push(name); if(name==='deploy-mods') args[0](null);}}}; (async()=>{const result=await e.activateNewLocalVersion(api,{gameId:'taintedgrailthefallofavalon',stagedModId:newer.id},newer.attributes); if(result.deployment!=='completed'||dispatched.length!==2||events.filter(x=>x==='deploy-mods').length!==1) process.exit(4);})().catch(()=>process.exit(5));"
        if ($LASTEXITCODE -ne 0) { throw 'Vortex extension automatic version activation failed its API boundary contract.' }
        & node -e "const e=require('$indexPath'); const game='taintedgrailthefallofavalon'; const old={id:'Fixture Mod 1.2.3',attributes:{name:'Fixture Mod',version:'1.2.3',logicalFileName:'Fixture Mod',modId:276}}; const middle={id:'Fixture Mod 1.2.4',attributes:{name:'Fixture Mod',version:'1.2.4',logicalFileName:'Fixture Mod',modId:276}}; const newest={id:'Fixture Mod 1.2.5',attributes:{name:'Fixture Mod',version:'1.2.5',logicalFileName:'Fixture Mod',modId:276}}; const state={profile:{id:'profile',gameId:game,modState:{[old.id]:{enabled:true},[middle.id]:{enabled:false},[newest.id]:{enabled:false}}},persistent:{mods:{[game]:{[old.id]:old,[middle.id]:middle,[newest.id]:newest}}}}; let deploys=0; const api={getState:()=>state,store:{dispatch:(action)=>{state.profile.modState[action.modId]={enabled:action.enable};}},events:{emit:(name,...args)=>{if(name==='mods-enabled') return; if(name!=='deploy-mods') process.exit(4); deploys+=1; args[0](null);}}}; const completion=(mod)=>({request:{gameId:game,stagedModId:mod.id},attributes:mod.attributes,targetAttributes:mod.attributes}); (async()=>{const results=await e.activateLocalGroupingBatch(api,[completion(middle),completion(newest)],0); if(deploys!==1||state.profile.modState[old.id].enabled||state.profile.modState[middle.id].enabled||!state.profile.modState[newest.id].enabled) process.exit(5); const latest=results.find(x=>x.request.stagedModId===newest.id); const superseded=results.find(x=>x.request.stagedModId===middle.id); if(latest.activation.status!=='switched'||latest.activation.deployment!=='completed'||superseded.activation.reason!=='enabled-version-not-older') process.exit(6);})().catch(()=>process.exit(7));"
        if ($LASTEXITCODE -ne 0) { throw 'Vortex extension batched version activation failed its backlog regression contract.' }
        & node -e "const e=require('$indexPath'); const game='taintedgrailthefallofavalon'; const mod=(id,version,name,modId)=>({id,attributes:{name,version,logicalFileName:name,modId}}); const a0=mod('Fixture A 1.0.0','1.0.0','Fixture A',101),a1=mod('Fixture A 1.0.1','1.0.1','Fixture A',101),b0=mod('Fixture B 2.0.0','2.0.0','Fixture B',102),b1=mod('Fixture B 2.0.1','2.0.1','Fixture B',102); const state={profile:{id:'profile',gameId:game,modState:{[a0.id]:{enabled:true},[a1.id]:{enabled:false},[b0.id]:{enabled:true},[b1.id]:{enabled:false}}},persistent:{mods:{[game]:{[a0.id]:a0,[a1.id]:a1,[b0.id]:b0,[b1.id]:b1}}}}; let deploys=0; const api={getState:()=>state,store:{dispatch:(action)=>{state.profile.modState[action.modId]={enabled:action.enable};}},events:{emit:(name,...args)=>{if(name==='mods-enabled') return; if(name!=='deploy-mods') process.exit(4); deploys+=1; args[0](null);}}}; const completion=(entry)=>({request:{gameId:game,stagedModId:entry.id},attributes:entry.attributes,targetAttributes:entry.attributes}); (async()=>{await e.activateLocalGroupingBatch(api,[completion(a1),completion(b1)],0); if(deploys!==1||!state.profile.modState[a1.id].enabled||!state.profile.modState[b1.id].enabled||state.profile.modState[a0.id].enabled||state.profile.modState[b0.id].enabled) process.exit(5);})().catch(()=>process.exit(6));"
        if ($LASTEXITCODE -ne 0) { throw 'Vortex extension did not collapse multiple mod switches into one deployment.' }
    }
    finally {
        $env:NODE_PATH = $previousNodePath
    }

    $corePath = (Join-Path $extensionRoot 'promotion-core.js').Replace('\', '\\')
    & node -e "const c=require('$corePath'); const r={version:'1.2.3',displayName:'Fixture Mod',gameId:'taintedgrailthefallofavalon',archive:{md5:'11111111111111111111111111111111',sha256:'$('2' * 64)',sizeBytes:10,fileName:'x.zip',remoteFileName:''},nexus:{modId:276,fileId:1381,logicalFileName:'Fixture Mod',url:'https://example.invalid',category:'main',isPrimary:true}}; const m={source:'nexus',fileMD5:r.archive.md5,fileSizeBytes:10,details:{modId:'276',fileId:'1381'}}; if(!c.findMatchingNexusMetadata([{value:m}],r)) process.exit(1); const a=c.buildModAttributes(r,m,'x.zip'); if(a.source!=='nexus'||a.modId!==276||a.fileId!==1381) process.exit(2);"
    if ($LASTEXITCODE -ne 0) { throw 'Vortex extension core metadata contracts failed.' }
    & node -e "const c=require('$corePath'); const r={schemaVersion:1,requestType:'local-grouping',requestId:'$('a' * 24)',gameId:'taintedgrailthefallofavalon',stagedModId:'Fixture Mod 1.2.4',stagingPath:'x',displayName:'Fixture Mod',version:'1.2.4',grouping:{source:'grailwright-local',modId:276,logicalFileName:'Fixture Mod',nexusUrl:'https://example.invalid'}}; c.validateRequest(r); const a=c.buildLocalGroupingAttributes(r,{}); if(a.source!=='grailwright-local'||a.modId!==276||a.grailwrightCollectionReady!==false) process.exit(1); const p=c.buildLocalGroupingAttributes(r,{source:'nexus',modId:276,fileId:1381}); if(p.source!==undefined||p.grailwrightCollectionReady!==true) process.exit(2);"
    if ($LASTEXITCODE -ne 0) { throw 'Vortex extension local grouping metadata contracts failed.' }
    & node -e "const c=require('$corePath'); const mod=(id,v,name='Fixture Mod',modId=276)=>({id,attributes:{name,logicalFileName:name,version:v,modId}}); const old=mod('old','1.2.3'), target=mod('target','1.2.4'), future=mod('future','1.2.5'), unrelated=mod('other','9.9.9','Other Mod',999); const mods={old,target,future,unrelated}; let p=c.buildVariantActivationPlan(mods,{old:{enabled:true},target:{enabled:false},other:{enabled:true}},'target',target.attributes); if(!p.switch||p.disableModIds.length!==1||p.disableModIds[0]!=='old'||!p.enableTarget) process.exit(1); p=c.buildVariantActivationPlan(mods,{old:{enabled:false},target:{enabled:false}},'target',target.attributes); if(p.switch||p.reason!=='group-disabled') process.exit(2); p=c.buildVariantActivationPlan(mods,{future:{enabled:true},target:{enabled:false}},'target',target.attributes); if(p.switch||p.reason!=='enabled-version-not-older') process.exit(3); p=c.buildVariantActivationPlan(mods,{old:{enabled:true},target:{enabled:true}},'target',target.attributes); if(!p.switch||p.enableTarget||p.disableModIds[0]!=='old') process.exit(4);"
    if ($LASTEXITCODE -ne 0) { throw 'Vortex automatic version activation planning contracts failed.' }
    & node -e "const c=require('$corePath'); const catalog=[{packageName:'FixtureMod',displayName:'Fixture Mod',stagedNamePrefix:'Fixture Mod',modId:276,logicalFileName:'Fixture Mod'}]; const mods={local:{id:'Fixture Mod 1.2.4',attributes:{name:'Fixture Mod',version:'1.2.4',source:'grailwright-local',modId:276,grailwrightCollectionReady:false}},release:{id:'Fixture Mod 1.2.3',attributes:{name:'Fixture Mod',version:'1.2.3',source:'nexus',modId:276,fileId:1381,grailwrightCollectionReady:true}},native:{id:'Fixture Mod 276 1.2.3 timestamp',attributes:{name:'Fixture Mod',version:'1.2.3',logicalFileName:'Fixture Mod',source:'nexus',modId:276,fileId:1381}},missed:{id:'Fixture Mod 1.2.5',attributes:{name:'Fixture Mod',version:'1.2.5'}},wrong:{id:'Fixture Mod 1.2.6',attributes:{name:'Fixture Mod',version:'1.2.6',source:'nexus',modId:999,fileId:2000,grailwrightCollectionReady:true}},thirdParty:{id:'Third Party 1',attributes:{name:'Third Party',version:'1.0.0',source:'nexus',modId:500,fileId:600}}}; const blocked=c.buildCollectionReadiness(mods,{local:{enabled:true}},catalog); if(blocked.allReady||blocked.entries.length!==1||blocked.entries[0].coverage!=='covered') process.exit(1); const ready=c.buildCollectionReadiness(mods,{release:{enabled:true},thirdParty:{enabled:true}},catalog); if(!ready.allReady||ready.entries.length!==1||ready.entries[0].fileId!==1381) process.exit(2); const native=c.buildCollectionReadiness(mods,{native:{enabled:true}},catalog); if(!native.allReady||native.entries[0].coverage!=='covered') process.exit(3); const missed=c.buildCollectionReadiness(mods,{missed:{enabled:true}},catalog); if(missed.allReady||missed.unaccountedEnabledCount!==1||missed.entries[0].coverage!=='missing-metadata') process.exit(4); const wrong=c.buildCollectionReadiness(mods,{wrong:{enabled:true}},catalog); if(wrong.allReady||wrong.entries[0].coverage!=='nexus-id-mismatch'||wrong.entries[0].modId!==999||wrong.entries[0].expectedModId!==276) process.exit(5); const stale=c.buildCollectionReadiness(mods,{'Fixture Mod 1.2.7':{enabled:true}},catalog); if(stale.allReady||stale.entries[0].coverage!=='missing-vortex-record') process.exit(6); const noCatalog=c.buildCollectionReadiness(mods,{release:{enabled:true}},{entries:catalog,available:false}); if(noCatalog.allReady||noCatalog.entries[0].coverage!=='covered') process.exit(7); const empty=c.buildCollectionReadiness(mods,{},catalog); if(empty.allReady||empty.entries.length!==0) process.exit(8); const invalid=c.buildCollectionReadiness(mods,{release:{enabled:true}},{entries:catalog,available:true,invalidRecordCount:1}); if(invalid.allReady) process.exit(9);"
    if ($LASTEXITCODE -ne 0) { throw 'Vortex collection readiness contracts failed.' }
    & node -e "const c=require('$corePath'); const previous={reason:'waiting',loggedAt:1000}; if(c.shouldLogPending(previous,'waiting',2000,300000)) process.exit(1); if(!c.shouldLogPending(previous,'changed',2000,300000)) process.exit(2); if(!c.shouldLogPending(previous,'waiting',301000,300000)) process.exit(3);"
    if ($LASTEXITCODE -ne 0) { throw 'Vortex pending-log rate limit contracts failed.' }
    & node -e "const c=require('$corePath'); c.validateRequest({schemaVersion:1,requestType:'local-grouping-catalog',requestId:'$('b' * 24)',gameId:'taintedgrailthefallofavalon',mods:[{packageName:'FixtureMod',displayName:'Fixture Mod',stagedNamePrefix:'Fixture Mod',modId:276,logicalFileName:'Fixture Mod'}]});"
    if ($LASTEXITCODE -ne 0) { throw 'Vortex state-record grouping catalog contract failed.' }

    $readinessBridge = Join-Path $scratchRoot 'readiness-bridge'
    New-Item -ItemType Directory -Path $readinessBridge -Force | Out-Null
    $readinessPath = Join-Path $readinessBridge 'collection-readiness.json'
    [System.IO.File]::WriteAllText($readinessPath, @'
{
  "schemaVersion": 2,
  "gameId": "taintedgrailthefallofavalon",
  "catalogAvailable": false,
  "catalogModCount": 0,
  "invalidCatalogRecordCount": 0,
  "unaccountedEnabledCount": 0,
  "entries": []
}
'@)
    $readinessError = ''
    try {
        & (Join-Path $PSScriptRoot 'Test-VortexCollectionReadiness.ps1') -VortexMetadataBridgeRoot $readinessBridge | Out-Null
    }
    catch {
        $readinessError = $_.Exception.Message
    }
    Assert-Contract ($readinessError.Contains('No authored Grailwright grouping catalog')) 'collection gate did not fail closed without a catalog.'
    [System.IO.File]::WriteAllText($readinessPath, @'
{
  "schemaVersion": 2,
  "gameId": "taintedgrailthefallofavalon",
  "catalogAvailable": true,
  "catalogModCount": 1,
  "invalidCatalogRecordCount": 0,
  "unaccountedEnabledCount": 0,
  "entries": [
    {
      "displayName": "Fixture Mod",
      "version": "1.2.3",
      "source": "nexus",
      "modId": 276,
      "fileId": 1381,
      "coverage": "covered",
      "ready": true
    }
  ]
}
'@)
    & (Join-Path $PSScriptRoot 'Test-VortexCollectionReadiness.ps1') -VortexMetadataBridgeRoot $readinessBridge | Out-Null

    $extensionBuild = & (Join-Path $PSScriptRoot 'Build-VortexNexusMetadataExtension.ps1') -DestinationDirectory (Join-Path $scratchRoot 'extension-build') | Select-Object -Last 1
    Assert-Contract (Test-Path -LiteralPath $extensionBuild.ArchivePath -PathType Leaf) 'extension package was not created.'
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    $extensionArchive = [System.IO.Compression.ZipFile]::OpenRead([string]$extensionBuild.ArchivePath)
    try {
        $extensionEntries = @($extensionArchive.Entries | Where-Object { -not $_.FullName.EndsWith('/') } | ForEach-Object FullName | Sort-Object)
    }
    finally {
        $extensionArchive.Dispose()
    }
    Assert-Contract (($extensionEntries -join ',') -eq 'index.js,info.json,promotion-core.js') 'extension package contains unexpected files or layout.'

    $extensionInstall = & (Join-Path $PSScriptRoot 'Install-VortexNexusMetadataExtension.ps1') -VortexPluginsRoot (Join-Path $scratchRoot 'plugins') | Select-Object -Last 1
    Assert-Contract (Test-Path -LiteralPath (Join-Path $extensionInstall.InstalledPath 'index.js') -PathType Leaf) 'extension installer did not place index.js.'
    Assert-Contract ([bool]$extensionInstall.RestartVortex) 'extension installer did not report the required Vortex restart.'

    $existingInstallError = ''
    try {
        & (Join-Path $PSScriptRoot 'Install-VortexNexusMetadataExtension.ps1') -VortexPluginsRoot (Join-Path $scratchRoot 'plugins') | Out-Null
    }
    catch {
        $existingInstallError = $_.Exception.Message
    }
    Assert-Contract ($existingInstallError.Contains('-UpdateExisting')) 'extension installer did not guard an existing installation.'
    $updatedExtension = & (Join-Path $PSScriptRoot 'Install-VortexNexusMetadataExtension.ps1') -VortexPluginsRoot (Join-Path $scratchRoot 'plugins') -UpdateExisting | Select-Object -Last 1
    Assert-Contract (Test-Path -LiteralPath (Join-Path $updatedExtension.InstalledPath 'index.js') -PathType Leaf) 'extension update did not retain index.js.'
    Assert-Contract (@($updatedExtension.ReplacedVersions).Count -eq 1) 'extension update did not report the replaced installation.'

    Write-Host 'Vortex Nexus metadata promotion contracts passed: complete live discovery metadata, persisted-attribute verification, automatic grouping repair, active-version handoff, authored-only readiness, wrong-page refusal, stale-record detection, rate-limited pending logs, exact promotion, repairable queues, package layout, and guarded updates.'
}
finally {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
}
