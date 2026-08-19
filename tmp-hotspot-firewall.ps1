$exe = "C:\Users\RicardoAlvaro\AndroidStudioProjects\YaPasakay\backend\YaPasakay.Api\bin\Debug\net9.0\YaPasakay.Api.exe"
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source

New-NetFirewallRule -DisplayName "YaPasakay API exe" -Direction Inbound -Action Allow -Program $exe -Profile Any -ErrorAction SilentlyContinue | Out-Null
Get-NetFirewallRule -DisplayName "YaPasakay API exe" -ErrorAction SilentlyContinue | Set-NetFirewallRule -Enabled True -Action Allow -Profile Any -Program $exe

if ($dotnet) {
  New-NetFirewallRule -DisplayName "YaPasakay dotnet exe" -Direction Inbound -Action Allow -Program $dotnet -Profile Any -ErrorAction SilentlyContinue | Out-Null
}

New-NetFirewallRule -DisplayName "YaPasakay API 5088 hotspot" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5088 -Profile Any -InterfaceAlias "Local Area Connection* 10" -ErrorAction SilentlyContinue | Out-Null
Get-NetFirewallRule -DisplayName "YaPasakay API 5088" -ErrorAction SilentlyContinue | Set-NetFirewallRule -Enabled True -Action Allow -Profile Any

try {
  Set-NetConnectionProfile -InterfaceAlias "Local Area Connection* 10" -NetworkCategory Private -ErrorAction Stop
} catch {
  Write-Host "Adapter profile: $($_.Exception.Message)"
}

Write-Host "Firewall updated for hotspot clients."
Write-Host "Phone URL: http://192.168.137.1:5088"
Start-Sleep -Seconds 4
