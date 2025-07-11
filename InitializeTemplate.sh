#!/bin/bash
# Copyright (c) Eitan. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Exit on error
set -e

# Ensure script is executable
if [[ ! -x "$0" ]]; then
  chmod +x "$0"
fi

# Collect user input
read -p "Set Author name: (i.e. your GitHub username) " InputAuthor
ProjectAuthor="ProjectAuthor"
read -p "Enter a name for your new project " InputName
ProjectName="ProjectName"
read -p "Enter a scope for your new project (optional) " InputScope

# Validate inputs
if [ -z "$InputAuthor" ]; then
  echo "Error: Author name cannot be empty"
  exit 1
fi

if [ -z "$InputName" ]; then
  echo "Error: Project name cannot be empty"
  exit 1
fi

# Handle optional scope
if [ -n "$InputScope" ]; then
  InputScope="$InputScope."
fi

ProjectScope="ProjectScope."

# Function to convert string to lowercase (compatible with both BSD and GNU)
to_lower() {
  echo "$1" | tr '[:upper:]' '[:lower:]'
}

# Function to convert string to uppercase
to_upper() {
  echo "$1" | tr '[:lower:]' '[:upper:]'
}

echo "Your new com.$(to_lower "$InputScope")$(to_lower "$InputName") project is being created..."

# Check if files and directories exist before operating on them
if [ -f "./README.md" ]; then
  rm -f "./README.md"
fi

if [ -d "./$ProjectScope$ProjectName/Assets/Samples" ]; then
  rm -rf "./$ProjectScope$ProjectName/Assets/Samples"
fi

oldPackageRoot="./$ProjectScope$ProjectName/Packages/com.$(to_lower "$ProjectScope")$(to_lower "$ProjectName")"

# Check if the package root exists
if [ ! -d "$oldPackageRoot" ]; then
  echo "Error: Package root directory not found: $oldPackageRoot"
  exit 1
fi

# Check if source files exist before moving them
if [ -f "$oldPackageRoot/Documentation~/README.md" ]; then
  cp "$oldPackageRoot/Documentation~/README.md" "./README.md"
else
  echo "Warning: README.md not found in Documentation~ folder"
fi

# Move asmdef files with error handling
if [ -f "$oldPackageRoot/Runtime/$ProjectScope$ProjectName.asmdef" ]; then
  mv "$oldPackageRoot/Runtime/$ProjectScope$ProjectName.asmdef" "$oldPackageRoot/Runtime/$InputScope$InputName.asmdef"
else
  echo "Warning: Runtime asmdef file not found"
fi

if [ -f "$oldPackageRoot/Editor/$ProjectScope$ProjectName.Editor.asmdef" ]; then
  mv "$oldPackageRoot/Editor/$ProjectScope$ProjectName.Editor.asmdef" "$oldPackageRoot/Editor/$InputScope$InputName.Editor.asmdef"
else
  echo "Warning: Editor asmdef file not found"
fi

if [ -f "$oldPackageRoot/Tests/$ProjectScope$ProjectName.Tests.asmdef" ]; then
  mv "$oldPackageRoot/Tests/$ProjectScope$ProjectName.Tests.asmdef" "$oldPackageRoot/Tests/$InputScope$InputName.Tests.asmdef"
else
  echo "Warning: Tests asmdef file not found"
fi

# Check if Samples directory exists - FIXED to match PowerShell script
if [ -d "$oldPackageRoot/Samples~/Demo" ] && [ -f "$oldPackageRoot/Samples~/Demo/$ProjectScope$ProjectName.Demo.asmdef" ]; then
  mv "$oldPackageRoot/Samples~/Demo/$ProjectScope$ProjectName.Demo.asmdef" "$oldPackageRoot/Samples~/Demo/$InputScope$InputName.Demo.asmdef"
else
  echo "Warning: Samples Demo asmdef file not found"
fi

# Move package directory
mv "$oldPackageRoot" "./$ProjectScope$ProjectName/Packages/com.$(to_lower "$InputScope")$(to_lower "$InputName")"
mv "./$ProjectScope$ProjectName" "./$InputScope$InputName"

# Process files to replace content
excludes=("*Library*" "*Obj*" "*InitializeTemplate*" "*.git*" "*Temp*" "*Logs*")

# Function to perform sed replacement that works on both macOS and Linux
safe_sed() {
  local pattern="$1"
  local replacement="$2"
  local file="$3"
  
  # Escape special characters in pattern for sed
  pattern=$(echo "$pattern" | sed 's/[\/&]/\\&/g')
  replacement=$(echo "$replacement" | sed 's/[\/&]/\\&/g')
  
  # Try macOS/BSD sed syntax first, fall back to GNU sed if it fails
  sed -i '' "s/$pattern/$replacement/g" "$file" 2>/dev/null || sed -i "s/$pattern/$replacement/g" "$file"
}

# Function to generate a UUID
generate_uuid() {
  # Try multiple methods to generate UUID
  if command -v uuidgen &>/dev/null; then
    uuidgen
  elif command -v python &>/dev/null; then
    python -c 'import uuid; print(uuid.uuid4())'
  elif command -v python3 &>/dev/null; then
    python3 -c 'import uuid; print(uuid.uuid4())'
  else
    # Fallback method if no tools are available
    od -x /dev/urandom | head -1 | awk '{OFS="-"; print $2$3,$4,$5,$6,$7$8$9}'
  fi
}

# Process asmdef files first specifically for the name and rootNamespace fields
echo "Processing asmdef files..."
find "./$InputScope$InputName" -name "*.asmdef" | while read -r asmdefFile; do
  echo "Processing asmdef file: $asmdefFile"
  
  # Main assembly
  if grep -q "\"name\": \"$ProjectScope$ProjectName\"" "$asmdefFile"; then
    safe_sed "\"name\": \"$ProjectScope$ProjectName\"" "\"name\": \"$InputScope$InputName\"" "$asmdefFile"
    echo "Updated name in $asmdefFile"
  fi
  
  # Editor assembly
  if grep -q "\"name\": \"$ProjectScope$ProjectName.Editor\"" "$asmdefFile"; then
    safe_sed "\"name\": \"$ProjectScope$ProjectName.Editor\"" "\"name\": \"$InputScope$InputName.Editor\"" "$asmdefFile"
    echo "Updated name in $asmdefFile"
  fi
  
  # Tests assembly
  if grep -q "\"name\": \"$ProjectScope$ProjectName.Tests\"" "$asmdefFile"; then
    safe_sed "\"name\": \"$ProjectScope$ProjectName.Tests\"" "\"name\": \"$InputScope$InputName.Tests\"" "$asmdefFile"
    echo "Updated name in $asmdefFile"
  fi
  
  # Demo assembly
  if grep -q "\"name\": \"$ProjectScope$ProjectName.Demo\"" "$asmdefFile"; then
    safe_sed "\"name\": \"$ProjectScope$ProjectName.Demo\"" "\"name\": \"$InputScope$InputName.Demo\"" "$asmdefFile"
    echo "Updated name in $asmdefFile"
  fi
  
  # Main rootNamespace
  if grep -q "\"rootNamespace\": \"$ProjectScope$ProjectName\"" "$asmdefFile"; then
    safe_sed "\"rootNamespace\": \"$ProjectScope$ProjectName\"" "\"rootNamespace\": \"$InputScope$InputName\"" "$asmdefFile"
    echo "Updated rootNamespace in $asmdefFile"
  fi
  
  # Editor rootNamespace
  if grep -q "\"rootNamespace\": \"$ProjectScope$ProjectName.Editor\"" "$asmdefFile"; then
    safe_sed "\"rootNamespace\": \"$ProjectScope$ProjectName.Editor\"" "\"rootNamespace\": \"$InputScope$InputName.Editor\"" "$asmdefFile"
    echo "Updated rootNamespace in $asmdefFile"
  fi
  
  # Tests rootNamespace
  if grep -q "\"rootNamespace\": \"$ProjectScope$ProjectName.Tests\"" "$asmdefFile"; then
    safe_sed "\"rootNamespace\": \"$ProjectScope$ProjectName.Tests\"" "\"rootNamespace\": \"$InputScope$InputName.Tests\"" "$asmdefFile"
    echo "Updated rootNamespace in $asmdefFile"
  fi
  
  # Demo rootNamespace
  if grep -q "\"rootNamespace\": \"$ProjectScope$ProjectName.Demo\"" "$asmdefFile"; then
    safe_sed "\"rootNamespace\": \"$ProjectScope$ProjectName.Demo\"" "\"rootNamespace\": \"$InputScope$InputName.Demo\"" "$asmdefFile"
    echo "Updated rootNamespace in $asmdefFile"
  fi
done

echo "Processing all other files..."
find . -type f | grep -v "\.git/" | grep -v "/Library/" | grep -v "/Temp/" | grep -v "/Logs/" | while read -r file; do
  # Check if file should be excluded
  valid=true
  for exclude in "${excludes[@]}"; do
    if [[ $(dirname "$file") == $exclude || $(dirname "$file") =~ $exclude ]]; then
      valid=false
      break
    fi
  done

  # Skip binary files
  if file "$file" | grep -q "binary"; then
    valid=false
  fi

  if [ "$valid" = true ]; then
    updated=false
    filename=$(basename "$file")
    
    # Standard text replacements for all file types
    # Rename all PascalCase instances
    if grep -q "$ProjectName" "$file" 2>/dev/null; then
      safe_sed "$ProjectName" "$InputName" "$file"
      updated=true
    fi

    if grep -q "$ProjectScope" "$file" 2>/dev/null; then
      safe_sed "$ProjectScope" "$InputScope" "$file"
      updated=true
    fi

    if grep -q "$ProjectAuthor" "$file" 2>/dev/null; then
      safe_sed "$ProjectAuthor" "$InputAuthor" "$file"
      updated=true
    fi

    Eitan="Eitan"
    if grep -q "$Eitan" "$file" 2>/dev/null; then
      safe_sed "$Eitan" "$InputAuthor" "$file"
      updated=true
    fi

    # Rename all lowercase instances
    ProjectNameLower=$(to_lower "$ProjectName")
    InputNameLower=$(to_lower "$InputName")
    if grep -q "$ProjectNameLower" "$file" 2>/dev/null; then
      safe_sed "$ProjectNameLower" "$InputNameLower" "$file"
      updated=true
    fi

    ProjectScopeLower=$(to_lower "$ProjectScope")
    InputScopeLower=$(to_lower "$InputScope")
    if grep -q "$ProjectScopeLower" "$file" 2>/dev/null; then
      safe_sed "$ProjectScopeLower" "$InputScopeLower" "$file"
      updated=true
    fi

    # Rename all UPPERCASE instances
    ProjectNameUpper=$(to_upper "$ProjectName")
    InputNameUpper=$(to_upper "$InputName")
    if grep -q "$ProjectNameUpper" "$file" 2>/dev/null; then
      safe_sed "$ProjectNameUpper" "$InputNameUpper" "$file"
      updated=true
    fi

    ProjectScopeUpper=$(to_upper "$ProjectScope")
    InputScopeUpper=$(to_upper "$InputScope")
    if grep -q "$ProjectScopeUpper" "$file" 2>/dev/null; then
      safe_sed "$ProjectScopeUpper" "$InputScopeUpper" "$file"
      updated=true
    fi

    # Update guids
    if grep -q "#INSERT_GUID_HERE#" "$file" 2>/dev/null; then
      uuid=$(generate_uuid)
      safe_sed "#INSERT_GUID_HERE#" "$uuid" "$file"
      updated=true
    fi

    # Update year
    current_year=$(date +"%Y")
    if grep -q "#CURRENT_YEAR#" "$file" 2>/dev/null; then
      safe_sed "#CURRENT_YEAR#" "$current_year" "$file"
      updated=true
    fi

    # Rename files
    if [[ "$filename" == *"$ProjectName"* ]]; then
      newname="${filename//$ProjectName/$InputName}"
      mv "$file" "$(dirname "$file")/$newname"
      updated=true
    fi

    if [ "$updated" = true ]; then
      echo "Updated: $filename"
    fi
  fi
done

# Setup Samples folder by copying files instead of using symbolic links
echo "Setting up Samples folder..."
samplesTargetDir="./$InputScope$InputName/Assets/Samples"
samplesSourceDir="./$InputScope$InputName/Packages/com.$(to_lower "$InputScope")$(to_lower "$InputName")/Samples~"

# Create the target directory if it doesn't exist
mkdir -p "$samplesTargetDir"

# Copy samples if the source directory exists
if [ -d "$samplesSourceDir" ]; then
  # Remove existing contents if any
  rm -rf "$samplesTargetDir/"*
  
  # Copy the samples
  if [ "$(ls -A "$samplesSourceDir")" ]; then
    cp -r "$samplesSourceDir/"* "$samplesTargetDir/"
    echo "Samples copied successfully"
  else
    echo "Samples~ directory is empty, nothing to copy"
  fi
else
  echo "Warning: Samples~ directory not found at $samplesSourceDir, skipping setup"
fi

echo "Template initialization completed successfully!"
echo "You can now open the project in Unity"

# Make the script executable before removing it
chmod +x InitializeTemplate.sh

# Clean up initialization scripts
rm -f "InitializeTemplate.ps1"
rm -f "InitializeTemplate.sh" 