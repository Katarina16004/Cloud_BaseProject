$api = "http://localhost:8939/api/BackendSF"

Write-Host "Dodavanje knjiga..."

foreach ($i in 1..20) {
    $book = @{
        bookId = "$i"
        title = "Knjiga $i"
        price = 10 + $i
        stock = if ($i -eq 1) { 1000 } else { 50 }
    }

    Invoke-RestMethod `
        -Method Post `
        -Uri "$api/books" `
        -ContentType "application/json" `
        -Body ($book | ConvertTo-Json)

    Write-Host "Dodata knjiga: Knjiga $i"
}

Write-Host "Dodavanje korisnickih naloga..."

foreach ($i in 1..10) {
    $account = @{
        userId = "user$i"
        name = "Klijent $i"
        balance = 1000
    }

    Invoke-RestMethod `
        -Method Post `
        -Uri "$api/accounts" `
        -ContentType "application/json" `
        -Body ($account | ConvertTo-Json)

    Write-Host "Dodat nalog: user$i"
}

$poorAccount = @{
    userId = "user0"
    name = "Klijent bez novca"
    balance = 5
}

Invoke-RestMethod `
    -Method Post `
    -Uri "$api/accounts" `
    -ContentType "application/json" `
    -Body ($poorAccount | ConvertTo-Json)

Write-Host "Dodat nalog: user0"
Write-Host "Seed je uspesno zavrsen."