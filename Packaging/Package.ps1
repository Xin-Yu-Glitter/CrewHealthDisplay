param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'))

$ErrorActionPreference = 'Stop'
$modDir = Split-Path -Parent $PSScriptRoot
[xml]$manifest = Get-Content -LiteralPath (Join-Path $modDir 'filelist.xml') -Raw -Encoding UTF8
[xml]$pluginInfo = Get-Content -LiteralPath (Join-Path $modDir 'PluginInfo.xml') -Raw -Encoding UTF8
$version = [version]$manifest.contentpackage.modversion
$gameVersion = [version]$manifest.contentpackage.gameversion
$project = Join-Path $PSScriptRoot 'CrewHealthDisplayClient.csproj'

$buildRoot = Join-Path ([IO.Path]::GetTempPath()) ('CrewHealthDisplay-' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $buildRoot $manifest.contentpackage.name

try {
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

    foreach ($file in @('filelist.xml', 'PluginInfo.xml', 'ModConfig.xml', 'image.png')) {
        Copy-Item -LiteralPath (Join-Path $modDir $file) -Destination $stage
    }

    foreach ($assembly in $pluginInfo.PluginInfos.MainAssemblyInfo) {
        $platform = [string]$assembly.platforms
        $relativePath = $assembly.assemblypath.Replace('%ModDir%/', '')
        $destination = Join-Path $stage $relativePath
        $buildOutput = Join-Path $buildRoot "build/$platform"
        dotnet build $project --nologo -c Release "-p:PlatformName=$platform" "-p:GameReferenceVersion=$gameVersion" `
            "-p:Version=$version" "-p:OutputPath=$buildOutput/bin/" "-p:BaseIntermediateOutputPath=$buildOutput/obj/"
        if ($LASTEXITCODE -ne 0) { throw "The $platform client build failed." }

        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path "$buildOutput/bin" ([IO.Path]::GetFileName($destination))) -Destination $destination
    }

    $archive = Join-Path $OutputDirectory "CrewHealthDisplay-$version.zip"
    Compress-Archive -LiteralPath $stage -DestinationPath $archive -Force
}
finally {
    if (Test-Path -LiteralPath $buildRoot -PathType Container) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force -ErrorAction Continue
    }
}

Write-Output "Package: $archive"
