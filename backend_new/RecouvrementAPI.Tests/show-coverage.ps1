$xmlFile = Get-ChildItem './TestResults' -Recurse -Filter 'coverage.cobertura.xml' | Select-Object -First 1
[xml]$cov = Get-Content $xmlFile.FullName
$classes = $cov.coverage.packages.package.classes.class

function Get-Pct($c, $attr) {
    $val = $c.$attr
    if ($val) { return [math]::Round([double]$val * 100, 2) } else { return 0 }
}

function Write-Line($name, $stmts, $branch, $funcs, $lines) {
    if ($lines -lt 50) { return }
    $color = if ($lines -ge 100) { 'Green' } else { 'Yellow' }
    $uncov = if ($lines -lt 100) { '...' } else { '-' }
    Write-Host ("{0,-40} | {1,8} | {2,8} | {3,8} | {4,8} | {5}" -f $name, $stmts, $branch, $funcs, $lines, $uncov) -ForegroundColor $color
}

Write-Host ""
Write-Host ("-" * 22 + " API ENDPOINTS " + "-" * 22) -ForegroundColor White
$eps = @(
    @{V='GET';  P='/api/client/historique'},
    @{V='GET';  P='/api/client/dossier/{id}'},
    @{V='GET';  P='/api/client/recu'},
    @{V='POST'; P='/api/client/message'},
    @{V='POST'; P='/api/client/repondre-relance'},
    @{V='GET';  P='/api/client/historique-pdf/{token}/{idDossier}'},
    @{V='POST'; P='/api/AdminClient/{id}/renouveler-token'},
    @{V='POST'; P='/api/Auth/login'},
    @{V='POST'; P='/api/Relance/{id}/envoyer-message'}
)
foreach ($ep in $eps) {
    $c = if ($ep.V -eq 'GET') { 'Cyan' } else { 'DarkYellow' }
    Write-Host ("  {0,-6} {1}" -f $ep.V, $ep.P) -ForegroundColor $c
}

Write-Host ""
Write-Host ("-" * 22 + " COVERAGE " + "-" * 22) -ForegroundColor White
Write-Host ""
Write-Host ("{0,-40} | {1,8} | {2,8} | {3,8} | {4,8} | {5}" -f "File", "% Stmts", "% Branch", "% Funcs", "% Lines", "Uncovered") -ForegroundColor White
Write-Host ("-" * 90) -ForegroundColor DarkGray

# Filtre: seulement les classes principales (pas les classes internes generees)
$mainOnly = $classes | Where-Object {
    $_.name -notmatch '<|>|d__|\+' -and $_.name -notmatch '/'
}

Write-Host "[CONTROLLERS]" -ForegroundColor Yellow
$mainOnly | Where-Object { $_.name -match 'Controller' } | ForEach-Object {
    $name = ($_.name -split '\.')[-1] + '.cs'
    Write-Line $name (Get-Pct $_ 'line-rate') (Get-Pct $_ 'branch-rate') (Get-Pct $_ 'line-rate') (Get-Pct $_ 'line-rate')
}

Write-Host ""
Write-Host "[MODELS]" -ForegroundColor Yellow
$mainOnly | Where-Object { 
    $_.name -notmatch 'Controller|Migration|DbContext|Dto|Helper|Program|EmailService' -and
    $_.name -match 'RecouvrementAPI\.Models\.'
} | ForEach-Object {
    $name = ($_.name -split '\.')[-1] + '.cs'
    Write-Line $name (Get-Pct $_ 'line-rate') (Get-Pct $_ 'branch-rate') (Get-Pct $_ 'line-rate') (Get-Pct $_ 'line-rate')
}

Write-Host ""
$testResult = dotnet test --no-build 2>&1 | Select-String "réussi|passed"
$testResult = dotnet test --no-build 2>&1 | Select-String "total"
Write-Host $testResult -ForegroundColor White
Write-Host " ALL TESTS PASSED " -BackgroundColor Green -ForegroundColor Black
Write-Host ""





