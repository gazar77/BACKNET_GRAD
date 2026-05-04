#Requires -Version 5.1
<#
  HeartCathAPI — publish + FTP upload to MonsterASP.NET (replace full site).

  1) Optionally uploads app_offline.htm (graceful IIS drain)
  2) dotnet publish (Release, win-x64, framework-dependent) → .\publish\
  3) Mirrors all files under .\publish\* to FTP remote wwwroot (replaces matching paths)
  4) Deletes app_offline.htm from remote when done

  Fill in FTP settings below OR pass parameters:
    .\publish.ps1 -FtpHost "site123.siteasp.net" -FtpUser "site123" -FtpPassword "***" `
        -RemoteWebRoot "." -SkipAppOffline
#>
[CmdletBinding()]
param(
    [string] $ProjectPath = "",
    [string] $FtpHost = "site67071.siteasp.net",
    [string] $FtpUser = "site67071",
    [string] $FtpPassword = "9Wi@!8YxE_r5",
    # Remote folder after login. Often FTP root IS the site wwwroot ("." ).
    # If FileZilla shows a "wwwroot" folder above your files, set to "wwwroot".
    [string] $RemoteWebRoot = ".",
    [switch] $SkipAppOffline
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
        $request.EnableSsl = $false
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
        $hostPart = $FtpHost.Trim()
        if ($hostPart -match '^(ftp|ftps)://') {
            $base = $hostPart.TrimEnd('/')
        }
        else {
            $base = "ftp://$($hostPart.TrimEnd('/'))"
        }

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

    Write-Host "[3b] FTP upload mirror from publish\ to remote [$RemoteWebRoot] ..." -ForegroundColor Cyan
    Get-ChildItem -Path $publishOut -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($publishOut.Length).TrimStart('\', '/').Replace('\', '/')
        Write-Host ("  PUT " + $rel)
        Send-FtpFile -LocalPath $_.FullName -RemoteUnixPath $rel
    }

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
