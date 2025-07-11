# Copyright (c) Eitan. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

$InputAuthor = Read-Host "Set Author name: (i.e. your GitHub username)"
$ProjectAuthor = "ProjectAuthor"
$InputName = Read-Host "Enter a name for your new project"
$ProjectName = "ProjectName"
$InputScope = Read-Host "Enter a scope for your new project (optional)"

if (-not [String]::IsNullOrWhiteSpace($InputScope)) {
  $InputScope = "$InputScope."
}

$ProjectScope = "ProjectScope."

Write-Host "Your new com.$($InputScope.ToLower())$($InputName.ToLower()) project is being created..."

# Remove README.md if it exists
if (Test-Path -Path ".\README.md") {
  Remove-Item -Path ".\README.md" -Force
}

# Remove Samples directory if it exists
if (Test-Path -Path ".\$ProjectScope$ProjectName\Assets\Samples") {
  Remove-Item -Path ".\$ProjectScope$ProjectName\Assets\Samples" -Recurse -Force
}

$oldPackageRoot = ".\$ProjectScope$ProjectName\Packages\com.$($ProjectScope.ToLower())$($ProjectName.ToLower())"

# Copy README file
if (Test-Path -Path "$oldPackageRoot\Documentation~\README.md") {
  Copy-Item -Path "$oldPackageRoot\Documentation~\README.md" -Destination ".\README.md" -Force
}
else {
  Write-Host "Warning: README.md not found in Documentation~ folder"
}

# Rename asmdef files
if (Test-Path -Path "$oldPackageRoot\Runtime\$ProjectScope$ProjectName.asmdef") {
  Rename-Item -Path "$oldPackageRoot\Runtime\$ProjectScope$ProjectName.asmdef" -NewName "$InputScope$InputName.asmdef" -Force
}
else {
  Write-Host "Warning: Runtime asmdef file not found"
}

if (Test-Path -Path "$oldPackageRoot\Editor\$ProjectScope$ProjectName.Editor.asmdef") {
  Rename-Item -Path "$oldPackageRoot\Editor\$ProjectScope$ProjectName.Editor.asmdef" -NewName "$InputScope$InputName.Editor.asmdef" -Force
}
else {
  Write-Host "Warning: Editor asmdef file not found"
}

if (Test-Path -Path "$oldPackageRoot\Tests\$ProjectScope$ProjectName.Tests.asmdef") {
  Rename-Item -Path "$oldPackageRoot\Tests\$ProjectScope$ProjectName.Tests.asmdef" -NewName "$InputScope$InputName.Tests.asmdef" -Force
}
else {
  Write-Host "Warning: Tests asmdef file not found"
}

if (Test-Path -Path "$oldPackageRoot\Samples~\Demo\$ProjectScope$ProjectName.Demo.asmdef") {
  Rename-Item -Path "$oldPackageRoot\Samples~\Demo\$ProjectScope$ProjectName.Demo.asmdef" -NewName "$InputScope$InputName.Demo.asmdef" -Force
}
else {
  Write-Host "Warning: Samples Demo asmdef file not found"
}

# Move and rename package directory
Rename-Item -Path "$oldPackageRoot" -NewName "com.$($InputScope.ToLower())$($InputName.ToLower())" -Force
Rename-Item -Path ".\$ProjectScope$ProjectName" -NewName ".\$InputScope$InputName" -Force

# Process asmdef files specifically to update name and rootNamespace
$asmdefFiles = Get-ChildItem -Path ".\$InputScope$InputName" -Include "*.asmdef" -Recurse -File
foreach ($asmdefFile in $asmdefFiles) {
  Write-Host "Processing asmdef file: $($asmdefFile.FullName)"
  $asmdefContent = Get-Content $asmdefFile.FullName -Raw
  
  # Update name field
  $asmdefContent = $asmdefContent -replace "`"name`":\s*`"$ProjectScope$ProjectName`"", "`"name`": `"$InputScope$InputName`""
  $asmdefContent = $asmdefContent -replace "`"name`":\s*`"$ProjectScope$ProjectName\.Editor`"", "`"name`": `"$InputScope$InputName.Editor`""
  $asmdefContent = $asmdefContent -replace "`"name`":\s*`"$ProjectScope$ProjectName\.Tests`"", "`"name`": `"$InputScope$InputName.Tests`""
  $asmdefContent = $asmdefContent -replace "`"name`":\s*`"$ProjectScope$ProjectName\.Demo`"", "`"name`": `"$InputScope$InputName.Demo`""
  
  # Update rootNamespace field
  $asmdefContent = $asmdefContent -replace "`"rootNamespace`":\s*`"$ProjectScope$ProjectName`"", "`"rootNamespace`": `"$InputScope$InputName`""
  $asmdefContent = $asmdefContent -replace "`"rootNamespace`":\s*`"$ProjectScope$ProjectName\.Editor`"", "`"rootNamespace`": `"$InputScope$InputName.Editor`""
  $asmdefContent = $asmdefContent -replace "`"rootNamespace`":\s*`"$ProjectScope$ProjectName\.Tests`"", "`"rootNamespace`": `"$InputScope$InputName.Tests`""
  $asmdefContent = $asmdefContent -replace "`"rootNamespace`":\s*`"$ProjectScope$ProjectName\.Demo`"", "`"rootNamespace`": `"$InputScope$InputName.Demo`""
  
  # Save changes
  Set-Content -Path $asmdefFile.FullName -Value $asmdefContent -NoNewline
}

# General content replacement in all files
$excludes = @('*Library*', '*Obj*', '*InitializeTemplate*', '*.git*', '*Temp*', '*Logs*')
Get-ChildItem -Path "*" -File -Recurse | Where-Object {
  $isValid = $true
  $path = Split-Path -Path $_.FullName -Parent
  
  foreach ($exclude in $excludes) {
    if ($path -like $exclude) {
      $isValid = $false
      break
    }
  }
  
  $isValid
} | ForEach-Object -Process {
  $updated = $false;
  $filePath = $_.FullName
  
  try {
    # Skip binary files
    if ((Get-Item $filePath).Length -gt 0) {
      $isBinary = $false
      try {
        [System.IO.File]::ReadAllText($filePath) | Out-Null
      }
      catch {
        $isBinary = $true
      }
      
      if (-not $isBinary) {
        $fileContent = Get-Content $filePath -Raw -ErrorAction SilentlyContinue
        
        if ($null -ne $fileContent) {
          # Rename all PascalCase instances
          if ($fileContent -cmatch $ProjectName) {
            $fileContent = $fileContent -creplace $ProjectName, $InputName
            $updated = $true
          }
          
          if ($fileContent -cmatch $ProjectScope) {
            $fileContent = $fileContent -creplace $ProjectScope, $InputScope
            $updated = $true
          }
          
          if ($fileContent -cmatch $ProjectAuthor) {
            $fileContent = $fileContent -creplace $ProjectAuthor, $InputAuthor
            $updated = $true
          }
          
          $Eitan = "Eitan"
          if ($fileContent -cmatch $Eitan) {
            $fileContent = $fileContent -creplace $Eitan, $InputAuthor
            $updated = $true
          }
          
          # Rename all lowercase instances
          if ($fileContent -cmatch $ProjectName.ToLower()) {
            $fileContent = $fileContent -creplace $ProjectName.ToLower(), $InputName.ToLower()
            $updated = $true
          }
          
          if ($fileContent -cmatch $ProjectScope.ToLower()) {
            $fileContent = $fileContent -creplace $ProjectScope.ToLower(), $InputScope.ToLower()
            $updated = $true
          }
          
          # Rename all UPPERCASE instances
          if ($fileContent -cmatch $ProjectName.ToUpper()) {
            $fileContent = $fileContent -creplace $ProjectName.ToUpper(), $InputName.ToUpper()
            $updated = $true
          }
          
          if ($fileContent -cmatch $ProjectScope.ToUpper()) {
            $fileContent = $fileContent -creplace $ProjectScope.ToUpper(), $InputScope.ToUpper()
            $updated = $true
          }
          
          # Update guids
          if ($fileContent -match "#INSERT_GUID_HERE#") {
            $fileContent = $fileContent -replace "#INSERT_GUID_HERE#", [guid]::NewGuid()
            $updated = $true
          }
          
          # Update year
          if ($fileContent -match "#CURRENT_YEAR#") {
            $fileContent = $fileContent -replace "#CURRENT_YEAR#", (Get-Date).year
            $updated = $true
          }
          
          # Save changes if any were made
          if ($updated) {
            Set-Content -Path $filePath -Value $fileContent -NoNewline -ErrorAction SilentlyContinue
            Write-Host "Updated: $($_.Name)"
          }
        }
      }
    }
  }
  catch {
    Write-Host "Warning: Couldn't process file $($_.Name): $_"
  }
  
  # Rename files
  if ($_.Name -match $ProjectName) {
    try {
      $newName = $_.Name -replace $ProjectName, $InputName
      Rename-Item -LiteralPath $_.FullName -NewName $newName -ErrorAction Stop
      Write-Host "Renamed: $($_.Name) to $newName"
    }
    catch {
      Write-Host "Warning: Couldn't rename file $($_.Name): $_"
    }
  }
}

# Copy Samples instead of creating a symbolic link
Write-Host "Setting up Samples folder..."
$samplesTargetDir = ".\$InputScope$InputName\Assets\Samples"
$samplesSourceDir = ".\$InputScope$InputName\Packages\com.$($InputScope.ToLower())$($InputName.ToLower())\Samples~"

# Create the target directory if it doesn't exist
if (-not (Test-Path -Path $samplesTargetDir)) {
  New-Item -Path $samplesTargetDir -ItemType Directory -Force | Out-Null
}

# Copy samples if the source directory exists
if (Test-Path -Path $samplesSourceDir) {
  # Remove existing contents if any
  if (Test-Path -Path $samplesTargetDir) {
    Remove-Item -Path "$samplesTargetDir\*" -Recurse -Force -ErrorAction SilentlyContinue
  }
  
  # Copy the samples
  Copy-Item -Path "$samplesSourceDir\*" -Destination $samplesTargetDir -Recurse -Force
  Write-Host "Samples copied successfully"
}
else {
  Write-Host "Warning: Samples~ directory not found, skipping setup"
}

Write-Host "Template initialization completed successfully!"
Write-Host "You can now open the project in Unity"

# Clean up initialization scripts
Remove-Item -Path "InitializeTemplate.ps1" -Force
Remove-Item -Path "InitializeTemplate.sh" -Force
