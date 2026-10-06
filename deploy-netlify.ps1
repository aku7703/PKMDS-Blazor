<#
.SYNOPSIS
    Publishes Pkmds.Web and deploys it to Netlify (used for the iPhone single-device build).

.DESCRIPTION
    Mirrors .github/workflows/main.yml, then deploys with the Netlify CLI:
      1. dotnet publish (Release) to release/
      2. index.html -> 404.html, stamp %%CACHE_VERSION%% in the service worker
      3. drop the .br/.gz siblings (Netlify compresses on the fly and index.html loads defaultUri)
      4. write Netlify _headers/_redirects
      5. netlify deploy (--prod only with -Prod)

    Requires `netlify login` once and a linked site (`-SiteId` or `netlify link`).

.EXAMPLE
    ./deploy-netlify.ps1 -SiteId <site-id>            # draft deploy, prints a preview URL
    ./deploy-netlify.ps1 -SiteId <site-id> -Prod      # production deploy
#>
param(
    [string]$SiteId,
    [switch]$Prod,
    [switch]$SkipPublish
)

# 'Continue', not 'Stop': Windows PowerShell 5.1 turns native stderr (netlify's progress output) into
# terminating errors under 'Stop'. Native failures are caught through $LASTEXITCODE below.
$ErrorActionPreference = 'Continue'
$root = $PSScriptRoot
$out = Join-Path $root 'release'
$www = Join-Path $out 'wwwroot'

if (-not $SkipPublish) {
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    dotnet publish (Join-Path $root 'Pkmds.Web/Pkmds.Web.csproj') -c Release -o $out --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
    # Publish regenerates the tracked tailwind.css; keep the working tree clean.
    git -C $root checkout -- Pkmds.Rcl/wwwroot/css/tailwind.css 2>$null
}

Copy-Item (Join-Path $www 'index.html') (Join-Path $www '404.html') -Force

$cacheVersion = Get-Date -Format 'yyyyMMddHHmmss'
foreach ($sw in 'service-worker.js', 'service-worker.published.js') {
    $path = Join-Path $www $sw
    if (Test-Path $path) {
        (Get-Content $path -Raw).Replace('%%CACHE_VERSION%%', $cacheVersion) | Set-Content $path -NoNewline -Encoding utf8
    }
}

Get-ChildItem $www -Recurse -File -Include *.br, *.gz | Remove-Item -Force

@'
/service-worker.js
  Cache-Control: no-cache, no-store, must-revalidate
/service-worker.published.js
  Cache-Control: no-cache, no-store, must-revalidate
/index.html
  Cache-Control: no-cache
/_framework/*
  Cache-Control: public, max-age=31536000, immutable
/*.wasm
  Content-Type: application/wasm
'@ | Set-Content (Join-Path $www '_headers') -Encoding utf8

'/*    /index.html    200' | Set-Content (Join-Path $www '_redirects') -Encoding utf8

$files = (Get-ChildItem $www -Recurse -File | Measure-Object -Property Length -Sum)
Write-Host ("Deploying {0} files, {1:N1} MB, cache version {2}" -f $files.Count, ($files.Sum / 1MB), $cacheVersion)

$deployArgs = @('deploy', '--dir', $www, '--message', "pkmds iphone-single-device $cacheVersion")
if ($SiteId) { $deployArgs += @('--site', $SiteId) }
if ($Prod) { $deployArgs += '--prod' }
netlify @deployArgs
if ($LASTEXITCODE -ne 0) { throw "netlify deploy failed ($LASTEXITCODE)" }
