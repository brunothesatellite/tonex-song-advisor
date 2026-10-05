# Extrait toutes les chaines candidates a la localisation (Phase 0 du plan
# INTERNATIONALISATION.md) et écrit un CSV brut : Fichier; Ligne; Nature; Valeur.
#
#   & tools\inventaire-i18n.ps1
#   & tools\inventaire-i18n.ps1 -Out C:\temp\inv.csv
#
# Natures produites ici : xaml | invite | compose | texte | exception.
# Le fichier est ensuite curaté (colonnes Nature exacte + Clé_proposée).
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Out = ""
)
$ErrorActionPreference = 'Stop'
if (-not $Out) { $Out = Join-Path $Root 'inventaire-i18n-raw.csv' }

$rows = New-Object System.Collections.Generic.List[object]
function Add-Row([string]$f, [int]$l, [string]$n, [string]$v) {
    $script:rows.Add([pscustomobject]@{ Fichier = $f; Ligne = $l; Nature = $n; Valeur = $v })
}

# --- 1. XAML : attributs porteurs de texte utilisateur --------------------------
$attrRe = '\s(?:Text|Header|Content|ToolTip\.Tip|Watermark|PlaceholderText)="([^"{][^"]*)"'
$axams = Get-ChildItem -Path (Join-Path $Root 'src') -Recurse -Filter '*.axaml' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
foreach ($f in $axams) {
    $rel = $f.FullName.Substring($Root.Length + 1)
    $n = 0
    foreach ($line in Get-Content -Path $f.FullName) {
        $n++
        foreach ($m in [regex]::Matches($line, $attrRe)) {
            $val = $m.Groups[1].Value.Trim()
            if ($val -match '[A-Za-zÀ-ÿ]') { Add-Row $rel $n 'xaml' $val }
        }
    }
}

# --- 2. C# : literales dont la ligne sent le francais --------------------------
$frRe = '[éèêàçùôîï«»]|Aucun|Aucune|Echec|Échec|\bcl[ée]s?\b|dossier|catég|régl|Conseil|conseil|arbitre|voix|lecture|Recherche|recherche|Réinitialiser|gratuit|payant|SUGGESTION|VERDICT|BAFFLE|CONSEIL LIBRE|joint|extraits|touche|Installation|Désinstallation|charg|\bVous\b|\bvotre\b|\bVotre\b|fichier|Délai|indisponible|Annuler|annuler|preset\(s\)'
$csFiles = Get-ChildItem -Path (Join-Path $Root 'src') -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
foreach ($f in $csFiles) {
    $rel = $f.FullName.Substring($Root.Length + 1)
    $n = 0
    foreach ($line in Get-Content -Path $f.FullName) {
        $n++
        if ($line -match '^\s*(//|/\*|\*)') { continue }   # commentaire pur
        if ($line -match 'Localizer\.|CoreTexts\.') { continue }   # références de clés, pas des chaînes d'affichage
        if ($line -notmatch $frRe) { continue }
        foreach ($m in [regex]::Matches($line, '"([^"\\]*(?:\\.[^"\\]*)*)"')) {
            $val = $m.Groups[1].Value
            if ($val.Length -lt 2) { continue }
            $nature = 'texte'
            if ($rel -like '*Prompt*.cs') { $nature = 'invite' }
            elseif ($line -match '\$"') { $nature = 'compose' }
            elseif ($line -match 'throw new') { $nature = 'exception' }
            Add-Row $rel $n $nature $val
        }
    }
}

$rows | Export-Csv -Path $Out -NoTypeInformation -Encoding UTF8
'écrit : {0}' -f $Out
'total : {0} candidats (xaml {1}, invite {2}, compose {3}, texte {4}, exception {5})' -f `
    $rows.Count,
    ($rows | Where-Object Nature -eq 'xaml').Count,
    ($rows | Where-Object Nature -eq 'invite').Count,
    ($rows | Where-Object Nature -eq 'compose').Count,
    ($rows | Where-Object Nature -eq 'texte').Count,
    ($rows | Where-Object Nature -eq 'exception').Count
