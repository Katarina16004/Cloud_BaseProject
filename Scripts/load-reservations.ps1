Connect-ServiceFabricCluster localhost:19000 | Out-Null

$api = "http://localhost:8939/api"
$bookId = 1
$service = "fabric:/MiniProjekat/LibraryService"

$successful = 0

foreach ($i in 1..200) {
    if ($i -in 50, 120) {
        Write-Host "Premestanje primarnih replika..."

        Get-ServiceFabricPartition `
            -ServiceName $service |
        ForEach-Object {
            Move-ServiceFabricPrimaryReplica `
                -PartitionId $_.PartitionId `
                -ServiceName $service |
            Out-Null
        }

        Start-Sleep -Seconds 2
    }

    try {
        Invoke-RestMethod `
            -Method Post `
            -Uri "$api/books/$bookId/reservations" `
            -ContentType "application/json" `
            -Body '{"quantity":1}' `
            -TimeoutSec 5 |
        Out-Null

        $successful++

        Write-Host "Zahtev $i uspesan"
    }
    catch {
        Write-Warning `
            "Zahtev $i nije uspeo: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Uspesnih odgovora: $successful"
Write-Host ""
Write-Host "Konzistentnost knjige:"

Invoke-RestMethod `
    -Uri "$api/books/$bookId/consistency" |
ConvertTo-Json -Depth 5