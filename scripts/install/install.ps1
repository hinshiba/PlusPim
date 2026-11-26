#Requires -Version 5.1
<#
.SYNOPSIS
Installs PlusPim for the current user on Windows.

.DESCRIPTION
Extracts the release archive to %LOCALAPPDATA%\Programs\PlusPim and adds it to the user PATH.
Runs on Windows PowerShell 5.1 and PowerShell 7.
This file is ASCII only because Windows PowerShell 5.1 reads BOM-less files in the ANSI code page.

Environment variables:
    PLUSPIM_VERSION      Version to install (default: latest release)
    PLUSPIM_TARGET       Release target such as win-arm64 (default: detected)
    PLUSPIM_INSTALL_DIR  Install directory (default: %LOCALAPPDATA%\Programs\PlusPim)

See doc/release.md for the release asset contract.
#>

# Everything runs inside functions so that a truncated download does nothing.

function Resolve-PlusPimVersion([string]$Repo) {
    # The redirect target of /releases/latest avoids the API rate limit.
    $res = Invoke-WebRequest -Uri "$Repo/releases/latest" -Method Head -UseBasicParsing
    # Strict mode forbids reading a property the type lacks, so branch on the type.
    $uri = if ($res.BaseResponse -is [Net.HttpWebResponse]) {
        $res.BaseResponse.ResponseUri # Windows PowerShell 5.1
    } else {
        $res.BaseResponse.RequestMessage.RequestUri # PowerShell 7
    }
    $tag = $uri.Segments[-1]
    if ($tag -notmatch '^v\d') {
        throw "cannot resolve the latest release from '$uri'"
    }
    $tag.Substring(1)
}

function Get-PlusPimTarget {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    switch ($arch) {
        'X64' { 'win-x64' }
        'Arm64' { 'win-arm64' }
        default { throw "unsupported architecture: $arch" }
    }
}

function Add-UserPath([string]$Dir) {
    # SetEnvironmentVariable expands %USERPROFILE% and the like and rewrites the value as REG_SZ.
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    try {
        $raw = $key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $entries = @($raw -split ';' | Where-Object { $_ })
        $normalized = $Dir.TrimEnd('\')
        foreach ($e in $entries) {
            if ([Environment]::ExpandEnvironmentVariables($e).TrimEnd('\') -ieq $normalized) {
                return $false
            }
        }
        $key.SetValue('Path', (($entries + $Dir) -join ';'), [Microsoft.Win32.RegistryValueKind]::ExpandString)
    } finally {
        $key.Dispose()
    }

    # Tell running processes such as Explorer that the environment changed.
    if (-not ('PlusPimInstaller.Native' -as [type])) {
        Add-Type -Namespace PlusPimInstaller -Name Native -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
'@
    }
    $result = [UIntPtr]::Zero
    # HWND_BROADCAST, WM_SETTINGCHANGE, SMTO_ABORTIFHUNG
    [void][PlusPimInstaller.Native]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$result)
    $true
}

function Install-PlusPim {
    # Function scope keeps the caller's session untouched when run through iex.
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    # The progress bar makes Invoke-WebRequest very slow on Windows PowerShell 5.1.
    $ProgressPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $repo = 'https://github.com/hinshiba/PlusPim'
    $target = if ($env:PLUSPIM_TARGET) { $env:PLUSPIM_TARGET } else { Get-PlusPimTarget }
    $version = if ($env:PLUSPIM_VERSION) { $env:PLUSPIM_VERSION } else { Resolve-PlusPimVersion $repo }
    $version = $version -replace '^v', ''
    # Reject values outside the contract because they end up in URLs, paths and PATH.
    if ($version -cnotmatch '\A[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.-]+)?\z') {
        throw "invalid version: $version"
    }
    if ($target -cnotin 'win-x64', 'win-arm64') {
        throw "invalid target: $target"
    }
    $dir = if ($env:PLUSPIM_INSTALL_DIR) { $env:PLUSPIM_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA 'Programs\PlusPim' }
    if ($dir.Contains(';')) {
        throw "PLUSPIM_INSTALL_DIR must not contain ';'"
    }
    $name = "pluspim-$version-$target.zip"
    $base = "$repo/releases/download/v$version"

    $tmp = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    $null = New-Item -ItemType Directory -Path $tmp
    try {
        Write-Host "pluspim-install: downloading $name"
        $archive = Join-Path $tmp $name
        $sums = Join-Path $tmp 'SHA256SUMS'
        Invoke-WebRequest -Uri "$base/$name" -OutFile $archive -UseBasicParsing
        Invoke-WebRequest -Uri "$base/SHA256SUMS" -OutFile $sums -UseBasicParsing

        $expected = $null
        foreach ($line in Get-Content $sums) {
            $hash, $file = $line -split '\s+', 2
            if ($file -ceq $name) {
                $expected = $hash
            }
        }
        if (-not $expected) {
            throw "$name is not listed in SHA256SUMS"
        }
        if ((Get-FileHash -Algorithm SHA256 $archive).Hash -ne $expected) {
            throw "checksum mismatch: $name"
        }

        $extracted = Join-Path $tmp 'x'
        Expand-Archive -Path $archive -DestinationPath $extracted

        $null = New-Item -ItemType Directory -Path $dir -Force
        $exe = Join-Path $dir 'pluspim.exe'
        # Stage next to the target so that an interrupted install never leaves a broken binary.
        $staged = Join-Path $dir '.pluspim.exe.new'
        Copy-Item (Join-Path $extracted 'pluspim.exe') $staged -Force
        try {
            Move-Item $staged $exe -Force
        } catch {
            Remove-Item $staged -Force -ErrorAction SilentlyContinue
            throw "cannot replace $exe; close running PlusPim and retry"
        }
        foreach ($doc in 'LICENSE', 'README.md') {
            Copy-Item -LiteralPath (Join-Path $extracted $doc) -Destination $dir -Force
        }

        $installed = & $exe --version
        if ($LASTEXITCODE -ne 0) {
            throw "$exe does not run; set PLUSPIM_TARGET if '$target' is wrong"
        }
        Write-Host "pluspim-install: installed $installed to $exe"
        if (Add-UserPath $dir) {
            Write-Host "pluspim-install: added $dir to the user PATH; restart terminals to use it"
        }
        if (($env:Path -split ';') -notcontains $dir) {
            $env:Path = "$env:Path;$dir"
        }
    } finally {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Install-PlusPim
