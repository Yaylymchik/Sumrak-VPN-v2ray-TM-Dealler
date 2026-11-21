$p = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
$v = Get-ItemProperty -Path $p
"ProxyEnable  : $($v.ProxyEnable)"
"ProxyServer  : $($v.ProxyServer)"
"ProxyOverride: $($v.ProxyOverride)"
"AutoConfigURL: $($v.AutoConfigURL)"
"port 10809   : " + (Test-NetConnection -ComputerName 127.0.0.1 -Port 10809 -InformationLevel Quiet -WarningAction SilentlyContinue)
"port 10808   : " + (Test-NetConnection -ComputerName 127.0.0.1 -Port 10808 -InformationLevel Quiet -WarningAction SilentlyContinue)
