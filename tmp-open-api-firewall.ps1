# Allow phone on same Wi-Fi to reach YaPasakay API (port 5088).
# Wi-Fi is currently classified Public, so the rule must include Public.

New-NetFirewallRule -DisplayName "YaPasakay API 5088" -Direction Inbound -Protocol TCP -LocalPort 5088 -Action Allow -Profile Any -ErrorAction SilentlyContinue
Get-NetFirewallRule -DisplayName "YaPasakay API 5088" -ErrorAction SilentlyContinue | Set-NetFirewallRule -Enabled True -Action Allow -Profile Any

# Prefer Private so Windows is less strict on this LAN.
Get-NetConnectionProfile | Where-Object { $_.InterfaceAlias -eq 'Wi-Fi' } | Set-NetConnectionProfile -NetworkCategory Private -ErrorAction SilentlyContinue

Write-Host "Done. Phone API URL: http://192.168.254.185:5088"
Write-Host "Press any key to close..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
