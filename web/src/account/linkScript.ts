// 提取抽卡链接的 PowerShell 命令（用户自己运行，本站不执行任何本地操作）。
// 规则与 src/Core/Services/GachaLogLocator.cs、Infrastructure/ReadLines.cs 一致：
// 读取 Wuthering Waves Game\Client\Saved\Logs\Client.log（共享读，游戏运行中也能读），逐字节按最低位异或 0xA5 / 0xEF 解密，
// 取最后一条含 aki/gacha/index.html#/record? 的 "url" 值复制到剪贴板。旧版未加密的日志直接按明文匹配。
export const linkScript = [
  `$p=@(Get-PSDrive -PSProvider FileSystem|%{$r=$_.Root;"\${r}Wuthering Waves\\Wuthering Waves Game";"\${r}Program Files\\Wuthering Waves\\Wuthering Waves Game";"\${r}Games\\Wuthering Waves\\Wuthering Waves Game"})|?{Test-Path "$_\\Client\\Saved\\Logs\\Client.log"}|Select -First 1;`,
  `if(!$p){$p=Read-Host 'Wuthering Waves Game path'};`,
  `$s=[IO.File]::Open("$p\\Client\\Saved\\Logs\\Client.log",'Open','Read','ReadWrite');$b=New-Object byte[] $s.Length;[void]$s.Read($b,0,$b.Length);$s.Close();`,
  `$x='"url"\\s*:\\s*"([^"]*aki/gacha/index\\.html#/record\\?[^"]*)"';$m=[regex]::Matches([Text.Encoding]::UTF8.GetString($b),$x);`,
  `if(!$m.Count){for($i=0;$i -lt $b.Length;$i++){$b[$i]=$b[$i] -bxor $(if($b[$i] -band 1){0xA5}else{0xEF})};$m=[regex]::Matches([Text.Encoding]::UTF8.GetString($b),$x)};`,
  `if($m.Count){Set-Clipboard $m[$m.Count-1].Groups[1].Value;'OK'}else{'NOT FOUND'}`,
].join('');
