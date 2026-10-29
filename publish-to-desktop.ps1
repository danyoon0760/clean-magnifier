# 단일 EXE로 배포한 뒤 바탕화면에 "깔끔한 돋보기.exe" 로 복사한다.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# 실행 중이면 덮어쓸 수 없으므로 종료한다(publish 폴더의 exe 를 실행 중이면 publish 자체가 실패하므로 그 전에).
# 먼저 종료 신호로 정상 종료(설정 저장)를 시도하고, 5초 안에 끝나지 않을 때만 강제로 끝낸다.
$running = @(Get-Process CleanMagnifier, '깔끔한 돋보기' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $exitEvent = $null
    if ([Threading.EventWaitHandle]::TryOpenExisting('Local\CleanMagnifier.Exit', [ref]$exitEvent)) {
        [void]$exitEvent.Set()
        $exitEvent.Dispose()
    }
    $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
    $running | Where-Object { -not $_.HasExited } | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}

dotnet publish "$root\CleanMagnifier" -p:PublishProfile=Desktop
if ($LASTEXITCODE -ne 0) { throw "publish 실패" }

$desktop = [Environment]::GetFolderPath('Desktop')
$target = Join-Path $desktop '깔끔한 돋보기.exe'
Copy-Item "$root\publish\CleanMagnifier.exe" $target -Force
Write-Host "복사 완료: $target"
