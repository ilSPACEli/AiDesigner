# Наповнення тестової БД (гілка Neon perf-lr9) для вимірювання JMeter (ЛР №9).
# Запускати ЛИШЕ проти тестової БД: застосунок має працювати з .env, що вказує на perf-lr9,
# і з Gallery__SavesPerMinute, більшим за кількість робіт (інакше 429).
# Запуск з кореня репозиторію: powershell -ExecutionPolicy Bypass -File docs/quality/jmeter/seed-perf-db.ps1
param(
    [string]$BaseUrl = "http://localhost:5245",
    [int]$Total = 60,
    [string]$Image = "wwwroot/images/catalog/sofa-05.png",
    [string]$Item = "sofa-05"
)

$ErrorActionPreference = "Stop"
$styles = @("SCANDINAVIAN", "MINIMALISM", "MODERN", "CLASSIC", "BOHO")
$colors = '[{"hex":"#D8C3A5","hue":36,"share":61.2},{"hex":"#322D28","hue":30,"share":30.6},{"hex":"#9A8F80","hue":35,"share":8.2}]'
$tmp = New-TemporaryFile
$saved = 0; $loft = 0; $basic = 0

for ($i = 1; $i -le $Total; $i++) {
    if ($i % 3 -eq 0) {
        $analysis = '{"mode":"CLOUD_AI","lighting":"MODERATE","colors":' + $colors + ',"style":"LOFT","confidence":0.82}'
        $kind = "loft"
    }
    elseif ($i % 7 -eq 0) {
        $analysis = '{"mode":"BASIC","lighting":"MODERATE","colors":' + $colors + ',"style":null,"confidence":null}'
        $kind = "basic"
    }
    else {
        $style = $styles[$i % $styles.Count]
        $analysis = '{"mode":"CLOUD_AI","lighting":"MODERATE","colors":' + $colors + ',"style":"' + $style + '","confidence":0.75}'
        $kind = "other"
    }

    Set-Content -Path $tmp -Value $analysis -Encoding ascii -NoNewline
    $code = curl.exe -s -o NUL -w "%{http_code}" `
        -F "image=@$Image;type=image/png" -F "catalogItemId=$Item" -F "analysis=<$tmp" "$BaseUrl/api/results"

    if ($code -eq "201") {
        $saved++
        if ($kind -eq "loft") { $loft++ } elseif ($kind -eq "basic") { $basic++ }
    }
    else {
        Write-Warning "Робота ${i}: HTTP $code"
    }
}

Remove-Item $tmp
Write-Host "Збережено $saved з $Total (LOFT: $loft, без ШІ: $basic, інші стилі: $($saved - $loft - $basic))."
