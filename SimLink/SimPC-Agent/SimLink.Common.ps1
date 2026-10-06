# SimLink.Common.ps1
# Shared protocol + crypto for SimLink (agent on the simulator PC, controller on the main PC).
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.
#
# Wire format (every request and response):
#   HTTP body  = AES-256-CBC ciphertext of: [int32 LE json length][UTF-8 JSON][raw binary data]
#   Headers    = X-SL-V (protocol version), X-SL-Dir (req|resp), X-SL-TS (unix seconds),
#                X-SL-Nonce (random, 32 hex), X-SL-Ref (request nonce echoed in response, '-' in request),
#                X-SL-IV (base64 IV), X-SL-MAC (base64 HMAC-SHA256 over headers + IV + ciphertext)
#   Keys       = derived from the shared token with HMAC-SHA256 (separate encryption and MAC keys).
# The token itself never travels over the network. Encrypt-then-MAC, constant-time MAC compare,
# timestamp window + nonce cache (agent side) stop replays, direction + ref binding stop reflection.

Set-StrictMode -Version 2.0

$script:SLProtocolVersion = '1'
$script:SLAgentVersion    = '1.1.1'

function Test-SLWindows {
    return ($env:OS -eq 'Windows_NT')
}

function Get-SLUnixTime {
    return [int64][Math]::Floor(([DateTime]::UtcNow - [DateTime]'1970-01-01').TotalSeconds)
}

function New-SLRandomBytes {
    param([int]$Count)
    $b = New-Object byte[] $Count
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($b) } finally { $rng.Dispose() }
    return ,$b
}

function ConvertTo-SLHex {
    param([byte[]]$Bytes)
    return ([System.BitConverter]::ToString($Bytes) -replace '-', '').ToLowerInvariant()
}

function New-SLToken {
    # 32 random bytes, base64url, 43 chars
    $b = New-SLRandomBytes -Count 32
    return ([Convert]::ToBase64String($b).TrimEnd('=').Replace('+', '-').Replace('/', '_'))
}

function Test-SLTokenFormat {
    param([string]$Token)
    if ([string]::IsNullOrWhiteSpace($Token)) { return $false }
    if ($Token.Length -lt 32 -or $Token.Length -gt 256) { return $false }
    return ($Token -match '^[A-Za-z0-9_\-]+$')
}

function Get-SLHmac {
    param([byte[]]$Key, [byte[]]$Data)
    $h = New-Object System.Security.Cryptography.HMACSHA256 (,$Key)
    try { return ,$h.ComputeHash($Data) } finally { $h.Dispose() }
}

function New-SLKeys {
    param([Parameter(Mandatory)][string]$Token)
    if (-not (Test-SLTokenFormat $Token)) { throw 'Token format invalid (32-256 chars, A-Z a-z 0-9 _ -).' }
    $tk = [Text.Encoding]::UTF8.GetBytes($Token)
    return @{
        Enc = (Get-SLHmac -Key $tk -Data ([Text.Encoding]::UTF8.GetBytes('simlink-enc-v1')))
        Mac = (Get-SLHmac -Key $tk -Data ([Text.Encoding]::UTF8.GetBytes('simlink-mac-v1')))
    }
}

function Test-SLBytesEqual {
    # constant-time compare
    param([byte[]]$A, [byte[]]$B)
    if ($null -eq $A -or $null -eq $B) { return $false }
    if ($A.Length -ne $B.Length) { return $false }
    $diff = 0
    for ($i = 0; $i -lt $A.Length; $i++) { $diff = $diff -bor ($A[$i] -bxor $B[$i]) }
    return ($diff -eq 0)
}

function Invoke-SLAes {
    param([byte[]]$Key, [byte[]]$IV, [byte[]]$Data, [switch]$Decrypt)
    $aes = [System.Security.Cryptography.Aes]::Create()
    try {
        $aes.KeySize = 256
        $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
        $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
        $aes.Key = $Key
        $aes.IV = $IV
        if ($Decrypt) { $t = $aes.CreateDecryptor() } else { $t = $aes.CreateEncryptor() }
        try { return ,$t.TransformFinalBlock($Data, 0, $Data.Length) } finally { $t.Dispose() }
    } finally { $aes.Dispose() }
}

function Get-SLMacInput {
    param([string]$Dir, [string]$TS, [string]$Nonce, [string]$Ref, [byte[]]$IV, [byte[]]$CT)
    $hdr = [Text.Encoding]::UTF8.GetBytes("SL$($script:SLProtocolVersion)|$Dir|$TS|$Nonce|$Ref|")
    $buf = New-Object byte[] ($hdr.Length + $IV.Length + $CT.Length)
    [Array]::Copy($hdr, 0, $buf, 0, $hdr.Length)
    [Array]::Copy($IV, 0, $buf, $hdr.Length, $IV.Length)
    [Array]::Copy($CT, 0, $buf, $hdr.Length + $IV.Length, $CT.Length)
    return ,$buf
}

function New-SLFrame {
    # Returns @{ Headers = @{...}; Body = byte[]; Nonce = string }
    param(
        [Parameter(Mandatory)][hashtable]$Keys,
        [Parameter(Mandatory)][ValidateSet('req', 'resp')][string]$Dir,
        [string]$Ref = '-',
        [Parameter(Mandatory)]$Object,
        [byte[]]$Data
    )
    $json = $Object | ConvertTo-Json -Depth 12 -Compress
    $jb = [Text.Encoding]::UTF8.GetBytes($json)
    $dataLen = 0
    if ($null -ne $Data) { $dataLen = $Data.Length }
    $plain = New-Object byte[] (4 + $jb.Length + $dataLen)
    [Array]::Copy([BitConverter]::GetBytes([int32]$jb.Length), 0, $plain, 0, 4)
    [Array]::Copy($jb, 0, $plain, 4, $jb.Length)
    if ($dataLen -gt 0) { [Array]::Copy($Data, 0, $plain, 4 + $jb.Length, $dataLen) }

    $iv = New-SLRandomBytes -Count 16
    $ct = Invoke-SLAes -Key $Keys.Enc -IV $iv -Data $plain
    $ts = [string](Get-SLUnixTime)
    $nonce = ConvertTo-SLHex (New-SLRandomBytes -Count 16)
    $mac = Get-SLHmac -Key $Keys.Mac -Data (Get-SLMacInput -Dir $Dir -TS $ts -Nonce $nonce -Ref $Ref -IV $iv -CT $ct)
    return @{
        Nonce   = $nonce
        Body    = $ct
        Headers = @{
            'X-SL-V'     = $script:SLProtocolVersion
            'X-SL-Dir'   = $Dir
            'X-SL-TS'    = $ts
            'X-SL-Nonce' = $nonce
            'X-SL-Ref'   = $Ref
            'X-SL-IV'    = [Convert]::ToBase64String($iv)
            'X-SL-MAC'   = [Convert]::ToBase64String($mac)
        }
    }
}

function Read-SLFrame {
    # Verifies MAC + timestamp + direction (+ ref), decrypts, returns @{ Object; Data; Nonce; TS }
    # Throws 'AUTH:...' on any authentication failure.
    param(
        [Parameter(Mandatory)][hashtable]$Keys,
        [Parameter(Mandatory)][ValidateSet('req', 'resp')][string]$ExpectDir,
        [Parameter(Mandatory)][hashtable]$Headers,
        [byte[]]$Body,
        [string]$ExpectRef,
        [int]$WindowSec = 120
    )
    foreach ($h in 'X-SL-V', 'X-SL-Dir', 'X-SL-TS', 'X-SL-Nonce', 'X-SL-Ref', 'X-SL-IV', 'X-SL-MAC') {
        if ([string]::IsNullOrEmpty([string]$Headers[$h])) { throw "AUTH:missing header $h" }
    }
    if ($Headers['X-SL-V'] -ne $script:SLProtocolVersion) { throw 'AUTH:protocol version mismatch' }
    if ($Headers['X-SL-Dir'] -ne $ExpectDir) { throw 'AUTH:wrong direction' }
    $tsStr = [string]$Headers['X-SL-TS']
    if ($tsStr -notmatch '^\d{1,12}$') { throw 'AUTH:bad timestamp' }
    $nonce = [string]$Headers['X-SL-Nonce']
    if ($nonce -notmatch '^[0-9a-f]{32}$') { throw 'AUTH:bad nonce' }
    $ref = [string]$Headers['X-SL-Ref']
    if ($ref -ne '-' -and $ref -notmatch '^[0-9a-f]{32}$') { throw 'AUTH:bad ref' }
    if ($PSBoundParameters.ContainsKey('ExpectRef') -and $ref -ne $ExpectRef) { throw 'AUTH:response does not match request' }
    if ($null -eq $Body -or $Body.Length -lt 16 -or ($Body.Length % 16) -ne 0) { throw 'AUTH:bad body length' }
    try {
        $iv = [Convert]::FromBase64String([string]$Headers['X-SL-IV'])
        $mac = [Convert]::FromBase64String([string]$Headers['X-SL-MAC'])
    } catch { throw 'AUTH:bad encoding' }
    if ($iv.Length -ne 16 -or $mac.Length -ne 32) { throw 'AUTH:bad iv/mac length' }

    $expected = Get-SLHmac -Key $Keys.Mac -Data (Get-SLMacInput -Dir $ExpectDir -TS $tsStr -Nonce $nonce -Ref $ref -IV $iv -CT $Body)
    if (-not (Test-SLBytesEqual $expected $mac)) { throw 'AUTH:bad signature (wrong token?)' }

    $ts = [int64]$tsStr
    $now = Get-SLUnixTime
    if ([Math]::Abs($now - $ts) -gt $WindowSec) { throw "AUTH:timestamp outside window (check both PC clocks; diff=$($now - $ts)s)" }

    $plain = Invoke-SLAes -Key $Keys.Enc -IV $iv -Data $Body -Decrypt
    if ($plain.Length -lt 4) { throw 'AUTH:bad payload' }
    $jlen = [BitConverter]::ToInt32($plain, 0)
    if ($jlen -lt 2 -or $jlen -gt ($plain.Length - 4)) { throw 'AUTH:bad payload length' }
    $json = [Text.Encoding]::UTF8.GetString($plain, 4, $jlen)
    $obj = $json | ConvertFrom-Json
    $dlen = $plain.Length - 4 - $jlen
    $data = New-Object byte[] $dlen
    if ($dlen -gt 0) { [Array]::Copy($plain, 4 + $jlen, $data, 0, $dlen) }
    return @{ Object = $obj; Data = $data; Nonce = $nonce; TS = $ts }
}

# ---------- secret storage ----------
# Windows: DPAPI (LocalMachine scope for the agent file, CurrentUser scope for the controller file),
# file ACLs are applied by the installers. Non-Windows (used only for automated testing): plain text.

function Protect-SLSecret {
    param([Parameter(Mandatory)][string]$Secret, [ValidateSet('CurrentUser', 'LocalMachine')][string]$Scope = 'CurrentUser')
    if (Test-SLWindows) {
        Add-Type -AssemblyName System.Security
        $entropy = [Text.Encoding]::UTF8.GetBytes('SimLink-v1')
        $sc = [System.Security.Cryptography.DataProtectionScope]::$Scope
        $enc = [System.Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($Secret), $entropy, $sc)
        return 'dpapi:' + [Convert]::ToBase64String($enc)
    }
    return 'plain:' + $Secret
}

function Unprotect-SLSecret {
    param([Parameter(Mandatory)][string]$Stored, [ValidateSet('CurrentUser', 'LocalMachine')][string]$Scope = 'CurrentUser')
    $Stored = $Stored.Trim()
    if ($Stored.StartsWith('dpapi:')) {
        Add-Type -AssemblyName System.Security
        $entropy = [Text.Encoding]::UTF8.GetBytes('SimLink-v1')
        $sc = [System.Security.Cryptography.DataProtectionScope]::$Scope
        $dec = [System.Security.Cryptography.ProtectedData]::Unprotect([Convert]::FromBase64String($Stored.Substring(6)), $entropy, $sc)
        return [Text.Encoding]::UTF8.GetString($dec)
    }
    if ($Stored.StartsWith('plain:')) {
        if (Test-SLWindows) { throw 'Refusing plain-text token on Windows. Re-run setup/installer.' }
        return $Stored.Substring(6)
    }
    throw 'Unknown token storage format.'
}

function Get-SLStreamBytes {
    param([System.IO.Stream]$Stream, [int64]$MaxBytes)
    $ms = New-Object System.IO.MemoryStream
    try {
        $buf = New-Object byte[] 65536
        $total = 0
        while (($n = $Stream.Read($buf, 0, $buf.Length)) -gt 0) {
            $total += $n
            if ($MaxBytes -gt 0 -and $total -gt $MaxBytes) { throw 'Body too large' }
            $ms.Write($buf, 0, $n)
        }
        return ,$ms.ToArray()
    } finally { $ms.Dispose() }
}
