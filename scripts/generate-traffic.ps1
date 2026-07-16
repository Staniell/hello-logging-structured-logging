# Generates demo traffic against the compose stack so the Grafana dashboard shows
# live request rate and error count, and the "High 5xx error rate" alert fires.
# Every loop hits /, /products, and /hits; every $ErrorEvery-th loop also hits /fail.
# Uses curl.exe because Windows PowerShell's Invoke-WebRequest throws on the 500.
param(
    [int]$DurationSeconds = 180,
    [int]$ErrorEvery = 5
)

$deadline = (Get-Date).AddSeconds($DurationSeconds)
$i = 0

Write-Host "Sending traffic to http://localhost:8080 for $DurationSeconds seconds (errors every $ErrorEvery loops)..."

while ((Get-Date) -lt $deadline) {
    $i++
    curl.exe -s -o NUL http://localhost:8080/
    curl.exe -s -o NUL http://localhost:8080/products
    curl.exe -s -o NUL http://localhost:8080/hits
    if ($i % $ErrorEvery -eq 0) {
        curl.exe -s -o NUL http://localhost:8080/fail
    }
    Start-Sleep -Milliseconds 500
    if ($i % 20 -eq 0) {
        Write-Host "  loop $i - ~$($i * 3) requests sent (plus $([math]::Floor($i / $ErrorEvery)) errors)"
    }
}

Write-Host "Done. $i loops completed."
