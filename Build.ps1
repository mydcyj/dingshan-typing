param([string]$Destination = (Join-Path $PSScriptRoot 'DingshanTyping.exe'))

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:windir 'Microsoft.NET/Framework64/v4.0.30319'
$references = @(
    'System.Windows.Forms.dll'
    'System.Web.Extensions.dll'
    'System.Xaml.dll'
    'WPF/WindowsBase.dll'
    'WPF/PresentationCore.dll'
    'WPF/PresentationFramework.dll'
    'WPF/System.Speech.dll'
) | ForEach-Object { '/reference:' + (Join-Path $framework $_) }

& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:anycpu /codepage:65001 /optimize+ `
    "/out:$Destination" $references `
    ("/win32icon:{0}" -f (Join-Path $PSScriptRoot 'TypingIcon.ico')) `
    ("/resource:{0},TypingIcon.ico" -f (Join-Path $PSScriptRoot 'TypingIcon.ico')) `
    ("/resource:{0},Window.xaml" -f (Join-Path $PSScriptRoot 'Window.xaml')) `
    ("/resource:{0},Passages.json" -f (Join-Path $PSScriptRoot 'Passages.json')) `
    ("/resource:{0},Cet4.tsv" -f (Join-Path $PSScriptRoot 'Cet4.tsv')) `
    ("/resource:{0},Cet6.tsv" -f (Join-Path $PSScriptRoot 'Cet6.tsv')) `
    ("/resource:{0},Cet4-LICENSE.txt" -f (Join-Path $PSScriptRoot 'Cet4-LICENSE.txt')) `
    ("/resource:{0},LICENSE" -f (Join-Path $PSScriptRoot 'LICENSE')) `
    (Join-Path $PSScriptRoot 'Program.cs') (Join-Path $PSScriptRoot 'PracticeSettings.cs') `
    (Join-Path $PSScriptRoot 'StartupExperience.cs') `
    (Join-Path $PSScriptRoot 'InteractionMotion.cs') (Join-Path $PSScriptRoot 'SnakeBot.cs') `
    (Join-Path $PSScriptRoot 'TypingRound.cs') (Join-Path $PSScriptRoot 'SnakeTutorial.cs') `
    (Join-Path $PSScriptRoot 'TypingEffects.cs') (Join-Path $PSScriptRoot 'SnakeModes.cs') `
    (Join-Path $PSScriptRoot 'SnakeRace.cs') (Join-Path $PSScriptRoot 'SnakePower.cs') `
    (Join-Path $PSScriptRoot 'SnakeHotspot.cs') (Join-Path $PSScriptRoot 'SnakeRelay.cs') `
    (Join-Path $PSScriptRoot 'SnakeWordVoice.cs') `
    (Join-Path $PSScriptRoot 'SnakeTeams.cs') (Join-Path $PSScriptRoot 'RoomMusic.cs') `
    (Join-Path $PSScriptRoot 'MusicPlayback.cs') (Join-Path $PSScriptRoot 'SnakeGame.cs') (Join-Path $PSScriptRoot 'SnakeWords.cs') `
    (Join-Path $PSScriptRoot 'SnakePractice.cs') (Join-Path $PSScriptRoot 'SnakeMatch.cs') `
    (Join-Path $PSScriptRoot 'LanRoom.cs') (Join-Path $PSScriptRoot 'LanDiscovery.cs') (Join-Path $PSScriptRoot 'LanLobby.cs')
if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($LASTEXITCODE)." }
