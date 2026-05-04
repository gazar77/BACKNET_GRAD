#Requires -Version 5.1
<#
  HeartCathAPI — publish + FTP upload to MonsterASP.NET (replace full site).

  1) Optionally uploads app_offline.htm (graceful IIS drain)
  2) dotnet publish (Release, win-x64, framework-dependent) → .\publish\
  3) FTPs only files that changed since last run (MD5 manifest: .publish-manifest.json), or all with -ForceFullUpload
  4) Deletes app_offline.htm from remote when done

  Fill in FTP settings below OR pass parameters:
    .\publish.ps1 -FtpHost "site123.siteasp.net" -FtpUser "site123" -FtpPassword "***" `
        -RemoteWebRoot "wwwroot" -SkipAppOffline

  First run or full resync: .\publish.ps1 -ForceFullUpload

  MonsterASP: login often lands in the account root (e.g. mftp), NOT inside the site folder.
  Use -RemoteWebRoot "wwwroot" (default) so files go to .../wwwroot/web.config etc.
  Use -RemoteWebRoot "." ONLY if your FTP client already opens inside wwwroot when you connect.

  "Unable to connect to the remote server" = TCP could not reach the FTP host (wrong host/port,
  firewall/VPN blocking outbound FTP, or server requires FTPS: try -UseFtpSsl).
#>
[CmdletBinding()]
param(
    [string] $ProjectPath = "",
    [string] $FtpHost = "site67071.siteasp.net",
    [string] $FtpUser = "site67071",
    [string] $FtpPassword = "9Wi@!8YxE_r5",
    # Default 21; override if control panel shows another port, or use hostname:port in -FtpHost
    [int] $FtpPort = 21,
    # Some hosts require explicit TLS (try if plain FTP connects but login fails, or docs say FTPS)
    [switch] $UseFtpSsl,
    # Path UNDER your FTP login directory: MonsterASP = "wwwroot" (matches control panel Website Root \wwwroot).
    [string] $RemoteWebRoot = "wwwroot",
    [switch] $SkipAppOffline,
    # Skip TCP check if you know the host is correct but ICMP/Test-NetConnection is blocked
    [switch] $SkipFtpConnectivityCheck,
    # Upload every file (ignore .publish-manifest.json delta); seeds or refreshes the manifest
    [switch] $ForceFullUpload
)

# --- Default FTP config (edit here if you prefer not using parameters) ------------
if ([string]::IsNullOrWhiteSpace($FtpHost)) {
    $FtpHost = "siteXXX.siteasp.net"             # <-- MonsterASP FTP host from control panel
}
if ([string]::IsNullOrWhiteSpace($FtpUser)) {
    $FtpUser = "siteXXX"                         # <-- FTP username
}
if ([string]::IsNullOrWhiteSpace($FtpPassword)) {
    $FtpPassword = "REPLACE_WITH_FTP_PASSWORD"   # <-- FTP password (keep file private / out of git)
}
# ----------------------------------------------------------------------------------

$repoRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }
Push-Location $repoRoot

try {
    if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
        $ProjectPath = Join-Path $repoRoot "HeartCathAPI.csproj"
    }

    $publishOut = Join-Path $repoRoot "publish"
    $offlineLocal = Join-Path $repoRoot "app_offline.htm"
    $ManifestPath = Join-Path $repoRoot ".publish-manifest.json"

    Write-Host "[1] Cleaning previous publish folder..." -ForegroundColor Cyan
    if (Test-Path $publishOut) {
        Remove-Item $publishOut -Recurse -Force
    }

    Write-Host "[2] dotnet publish (Release, win-x64)..." -ForegroundColor Cyan
    & dotnet publish $ProjectPath `
        --configuration Release `
        --verbosity minimal `
        /p:PublishProfile=MonsterASP

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path $publishOut)) {
        throw "Publish output not found at: $publishOut"
    }

    $cred = New-Object System.Net.NetworkCredential($FtpUser, $FtpPassword.Trim())

    # ----- Resolve ftp://authority (host, optional :port) and whether to use TLS -----------------
    $ftpRaw = $FtpHost.Trim()
    $ftpTls = [bool]$UseFtpSsl
    if ($ftpRaw -match '^(?<scheme>ftps)://') {
        $ftpTls = $true
        $ftpRaw = $ftpRaw.Substring($matches[0].Length)
    }
    elseif ($ftpRaw -match '^ftp://') {
        $ftpRaw = $ftpRaw.Substring(6)
    }
    $ftpRaw = $ftpRaw.Trim().TrimEnd('/')
    if ($ftpRaw -match '^([^/]+)') { $ftpRaw = $matches[1] }

    $ftpHostOnly = $ftpRaw
    $ftpPortEffective = $FtpPort
    if ($ftpRaw -match '^(.+):(\d+)$') {
        $ftpHostOnly = $matches[1].Trim()
        $ftpPortEffective = [int]$matches[2]
    }

    if ($ftpHostOnly -match '^\[(.+)\]$') {
        $ftpHostOnly = $matches[1]
    }

    if ([string]::IsNullOrWhiteSpace($ftpHostOnly)) {
        throw "FtpHost is empty. Set -FtpHost to the FTP server from MonsterASP (e.g. site123.siteasp.net)."
    }

    $FtpAuthorityBase = if ($ftpPortEffective -ne 21) {
        "ftp://$($ftpHostOnly):$ftpPortEffective"
    } else {
        "ftp://$ftpHostOnly"
    }
    $FtpUseSsl = $ftpTls

    function Test-FtpTcpReachable {
        param(
            [string]$ServerHost,
            [int] $Port,
            [int] $TimeoutMs = 10000
        )
        $c = New-Object System.Net.Sockets.TcpClient
        try {
            $iar = $c.BeginConnect($ServerHost, $Port, $null, $null)
            if (-not $iar.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) {
                return $false
            }
            $c.EndConnect($iar)
            return $c.Connected
        }
        catch {
            return $false
        }
        finally {
            try { $c.Close() } catch { }
        }
    }

    function New-FtpRequest {
        param(
            [Parameter(Mandatory)][string]$Uri,
            [Parameter(Mandatory)][string]$Method,
            [long]$ContentLength = 0
        )
        $request = [System.Net.FtpWebRequest]::Create($Uri)
        $request = [System.Net.FtpWebRequest]$request
        $request.Method = $Method
        $request.Credentials = $cred
        $request.EnableSsl = $FtpUseSsl
        $request.UsePassive = $true
        $request.UseBinary = $true
        $request.KeepAlive = $false
        if ($ContentLength -gt 0) {
            $request.ContentLength = $ContentLength
        }
        return $request
    }

    function Normalize-RemoteSegments {
        param([string]$PathLike)
        if ([string]::IsNullOrWhiteSpace($PathLike) -or $PathLike -eq ".") { return @() }
        $segments = $PathLike -replace '\\', '/' -split '/' | Where-Object { $_ -ne "" -and $_ -ne "." }
        return $segments
    }

    function Build-FtpUri {
        param([string]$RemoteRelativeUnix)
        $base = $FtpAuthorityBase.TrimEnd('/')

        $rootSegs = Normalize-RemoteSegments $RemoteWebRoot
        $relSegs = Normalize-RemoteSegments $RemoteRelativeUnix
        $all = @()
        $all += $rootSegs
        $all += $relSegs

        foreach ($segment in $all) {
            $enc = [Uri]::EscapeDataString($segment) -replace '\+', '%20'
            $base = "$base/$enc"
        }
        return $base
    }

    # mkdir ftp://host/s1/s2/... relative to FTP login root (no RemoteWebRoot prefix)
    function Build-FtpUriFromLoginRoot {
        param([string]$RelativePath)
        $base = $FtpAuthorityBase.TrimEnd('/')
        foreach ($segment in (Normalize-RemoteSegments $RelativePath)) {
            $base = "$base/$([Uri]::EscapeDataString($segment))"
        }
        return $base
    }

    function Ensure-FtpDirectoryUnderLoginRoot {
        param([string]$RelativePath)
        $segs = Normalize-RemoteSegments $RelativePath
        if ($segs.Count -eq 0) { return }
        $built = ""
        foreach ($seg in $segs) {
            if ($built) { $built = "$built/$seg" }
            else { $built = $seg }
            try {
                $uri = Build-FtpUriFromLoginRoot $built
                $req = New-FtpRequest -Uri $uri -Method ([System.Net.WebRequestMethods+Ftp]::MakeDirectory)
                $resp = $req.GetResponse()
                $resp.Close() | Out-Null
            }
            catch [System.Net.WebException] {
                # folder may already exist
            }
        }
    }

    function Invoke-FtpCreateDirectorySafely {
        param([string]$RemoteUnixPath)
        if ([string]::IsNullOrWhiteSpace($RemoteUnixPath)) { return }

        # Create each segment cumulatively: a/b/c
        $segments = Normalize-RemoteSegments $RemoteUnixPath
        $built = ""
        foreach ($segment in $segments) {
            if ($built) { $built = "$built/$segment" }
            else { $built = $segment }

            $uri = Build-FtpUri $built
            try {
                $req = New-FtpRequest -Uri $uri -Method ([System.Net.WebRequestMethods+Ftp]::MakeDirectory)
                $resp = $req.GetResponse()
                $resp.Close() | Out-Null
            }
            catch [System.Net.WebException] {
                # Ignore "already exists"; rethrow unexpected errors
                if ($_.Exception.Response -eq $null) { throw $_ }
                $resp = try { [System.Net.FtpWebResponse]$_.Exception.Response } catch { $null }
                $codeOk = $false
                if ($resp -ne $null -and $resp.StatusCode.ToString()) {
                    $txt = [string]$resp.StatusCode.ToString().ToUpperInvariant()
                    if ($txt.Contains("EXIST") -or $txt.Contains("FILENAME") ) { $codeOk = $true }
                    $resp.Close() | Out-Null
                }
                if (-not $codeOk) {
                    Write-Warning ("Could not create remote directory (may exist): $uri -> " + ($_.Exception.Message))
                }
            }
        }
    }

    function Send-FtpFile {
        param(
            [Parameter(Mandatory)][string]$LocalPath,
            [Parameter(Mandatory)][string]$RemoteUnixPath     # folder/file relative under RemoteWebRoot
        )
        $parent = Split-Path $RemoteUnixPath -Parent
        if (-not [string]::IsNullOrWhiteSpace($parent)) {
            Invoke-FtpCreateDirectorySafely $parent
        }

        $uri = Build-FtpUri $RemoteUnixPath
        $bytes = [System.IO.File]::ReadAllBytes($LocalPath)
        $req = New-FtpRequest -Uri $uri -Method ([System.Net.WebRequestMethods+Ftp]::UploadFile) -ContentLength $bytes.Length
        $stream = $req.GetRequestStream()
        try {
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Close() | Out-Null
        }
        $resp = $req.GetResponse()
        $resp.Close() | Out-Null
    }

    function Remove-FtpFile {
        param([Parameter(Mandatory)][string]$RemoteUnixPath)
        $uri = Build-FtpUri $RemoteUnixPath
        try {
            $req = New-FtpRequest -Uri $uri -Method ([System.Net.WebRequestMethods+Ftp]::DeleteFile)
            $resp = $req.GetResponse()
            $resp.Close() | Out-Null
        }
        catch {
            Write-Warning "Could not delete remote file $RemoteUnixPath : $($_.Exception.Message)"
        }
    }

    function Get-FileHashMD5 {
        param([Parameter(Mandatory)][string]$FilePath)
        return (Get-FileHash -LiteralPath $FilePath -Algorithm MD5).Hash
    }

    function Load-Manifest {
        param([Parameter(Mandatory)][string]$ManifestPath)
        if (-not (Test-Path -LiteralPath $ManifestPath)) {
            return @{}
        }
        try {
            $raw = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8
            if ([string]::IsNullOrWhiteSpace($raw)) {
                return @{}
            }
            $obj = $raw | ConvertFrom-Json
            if ($null -eq $obj) {
                return @{}
            }
            $ht = @{}
            foreach ($p in $obj.PSObject.Properties) {
                $ht[$p.Name] = [string]$p.Value
            }
            return $ht
        }
        catch {
            Write-Warning "Could not load manifest ($ManifestPath); uploading all publish files. $($_.Exception.Message)"
            return @{}
        }
    }

    function Save-Manifest {
        param(
            [Parameter(Mandatory)][string]$ManifestPath,
            [Parameter(Mandatory)][hashtable]$Manifest
        )
        $Manifest | ConvertTo-Json -Depth 2 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
    }

    Write-Host "[2b] FTP target: $FtpAuthorityBase  (TLS: $FtpUseSsl)  remote subdir: '$RemoteWebRoot'" -ForegroundColor DarkGray

    if (-not $SkipFtpConnectivityCheck) {
        Write-Host "[2c] Checking TCP ${ftpHostOnly}:$ftpPortEffective ..." -ForegroundColor Cyan
        if (-not (Test-FtpTcpReachable -ServerHost $ftpHostOnly -Port $ftpPortEffective)) {
            throw @"
Cannot reach FTP server at ${ftpHostOnly}:$ftpPortEffective (TCP connect failed).

Try:
  - Hostname from MonsterASP FTP page (often siteNNNNN.siteasp.net), no accidental spaces
  - Another network / disable VPN or try VPN if FTP is blocked (many Wi‑Fi/corp networks block outbound port 21)
  - Correct port: -FtpPort 2121 or -FtpHost 'hostname:2121' if the control panel shows a custom port
  - FTPS if required: -UseFtpSsl
  - Skip this check only if you are sure: -SkipFtpConnectivityCheck
"@
        }
    }

    if ($RemoteWebRoot -ne ".") {
        Ensure-FtpDirectoryUnderLoginRoot $RemoteWebRoot
    }

    function Send-AppOfflineIfNeeded {
        if ($SkipAppOffline) { Write-Host "Skipping app_offline.htm (--SkipAppOffline)." -ForegroundColor Yellow; return }
        if (-not (Test-Path $offlineLocal)) {
            Write-Warning "app_offline.htm not found at $offlineLocal - continuing without IIS offline shield."
            return
        }
        Write-Host "[3a] Uploading app_offline.htm ..." -ForegroundColor Cyan
        Send-FtpFile -LocalPath $offlineLocal -RemoteUnixPath "app_offline.htm"
        Start-Sleep -Seconds 2
    }

    Send-AppOfflineIfNeeded

    if ($ForceFullUpload) {
        Write-Host "[3b] FTP upload (full) from publish\ to remote [$RemoteWebRoot] ..." -ForegroundColor Cyan
    }
    else {
        Write-Host "[3b] FTP upload (delta) from publish\ to remote [$RemoteWebRoot] ..." -ForegroundColor Cyan
        Write-Host "      Manifest: $ManifestPath" -ForegroundColor DarkGray
    }

    $manifest = Load-Manifest -ManifestPath $ManifestPath
    $newManifest = @{}
    $uploadCount = 0
    $skipCount = 0

    Get-ChildItem -Path $publishOut -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($publishOut.Length).TrimStart('\', '/').Replace('\', '/')
        $hash = Get-FileHashMD5 -FilePath $_.FullName
        $newManifest[$rel] = $hash

        $prev = $null
        if ($manifest.ContainsKey($rel)) {
            $prev = [string]$manifest[$rel]
        }
        $same = ($null -ne $prev) -and (
            [string]::Equals($prev.Trim(), $hash, [StringComparison]::OrdinalIgnoreCase)
        )

        if ($ForceFullUpload -or -not $same) {
            Write-Host ("  PUT " + $rel)
            Send-FtpFile -LocalPath $_.FullName -RemoteUnixPath $rel
            $uploadCount++
        }
        else {
            Write-Host ("  SKIP (unchanged) " + $rel) -ForegroundColor DarkGray
            $skipCount++
        }
    }

    Save-Manifest -ManifestPath $ManifestPath -Manifest $newManifest
    Write-Host ("  Uploaded: {0}  Skipped: {1}" -f $uploadCount, $skipCount) -ForegroundColor Cyan

    if (-not $SkipAppOffline) {
        Write-Host "[4] Removing remote app_offline.htm ..." -ForegroundColor Cyan
        Remove-FtpFile "app_offline.htm"
    }

    Write-Host ""
    Write-Host "Publish + FTP mirror complete." -ForegroundColor Green
}
finally {
    Pop-Location
}
