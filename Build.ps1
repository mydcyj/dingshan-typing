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

$fontResources = [IO.Path]::GetFullPath($Destination) + '.' + [Guid]::NewGuid().ToString('N') + '.resources'
try {
    $writer = New-Object System.Resources.ResourceWriter($fontResources)
    try {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'fonts') -File) {
            $stream = $file.OpenRead()
            $writer.AddResource('fonts/' + $file.Name.ToLowerInvariant(), $stream, $true)
        }
    } finally {
        $writer.Dispose()
    }

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
        ("/resource:{0},{1}.g.resources" -f $fontResources, [IO.Path]::GetFileNameWithoutExtension($Destination)) `
        (Join-Path $PSScriptRoot 'Program.cs') (Join-Path $PSScriptRoot 'PracticeSettings.cs') `
        (Join-Path $PSScriptRoot 'PracticeLeaderboard.cs') `
        (Join-Path $PSScriptRoot 'ThemePalette.cs') `
        (Join-Path $PSScriptRoot 'ThemeLayout.cs') (Join-Path $PSScriptRoot 'ThemeControlDesign.cs') `
        (Join-Path $PSScriptRoot 'ThemeWorldMotion.cs') (Join-Path $PSScriptRoot 'KeyboardMappingPractice.cs') `
        (Join-Path $PSScriptRoot 'DenseCityArtwork.cs') `
        (Join-Path $PSScriptRoot 'StartupExperience.cs') `
        (Join-Path $PSScriptRoot 'StartupThemeEffects.cs') `
        (Join-Path $PSScriptRoot 'InteractionMotion.cs') (Join-Path $PSScriptRoot 'SnakeBot.cs') `
        (Join-Path $PSScriptRoot 'TypingRound.cs') (Join-Path $PSScriptRoot 'SnakeTutorial.cs') `
        (Join-Path $PSScriptRoot 'TypingEffects.cs') (Join-Path $PSScriptRoot 'SnakeModes.cs') `
        (Join-Path $PSScriptRoot 'ThemedTypingAudio.cs') (Join-Path $PSScriptRoot 'TypingEffectVisuals.cs') `
        (Join-Path $PSScriptRoot 'SnakeRace.cs') (Join-Path $PSScriptRoot 'SnakePower.cs') `
        (Join-Path $PSScriptRoot 'SnakeHotspot.cs') (Join-Path $PSScriptRoot 'SnakeRelay.cs') `
        (Join-Path $PSScriptRoot 'SnakeWordVoice.cs') `
        (Join-Path $PSScriptRoot 'SnakeTeams.cs') (Join-Path $PSScriptRoot 'RoomMusic.cs') `
        (Join-Path $PSScriptRoot 'MusicPlayback.cs') (Join-Path $PSScriptRoot 'SnakeGame.cs') (Join-Path $PSScriptRoot 'SnakeWords.cs') `
        (Join-Path $PSScriptRoot 'SnakePractice.cs') (Join-Path $PSScriptRoot 'SnakeMatch.cs') `
        (Join-Path $PSScriptRoot 'LanRoom.cs') (Join-Path $PSScriptRoot 'LanDiscovery.cs') (Join-Path $PSScriptRoot 'LanLobby.cs')
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($LASTEXITCODE)." }
} finally {
    if (Test-Path -LiteralPath $fontResources) {
        Remove-Item -LiteralPath $fontResources
    }
}
