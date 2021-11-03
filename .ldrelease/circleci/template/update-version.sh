#!/bin/bash

# See: https://github.com/launchdarkly/project-releaser/blob/master/docs/templates/dotnet-windows.md

set -ue

# Standard update-version.sh for .NET projects built on Windows
# (This script runs on the Releaser host, not on the CircleCI Windows host)

PROJECT_FILES=$(find ./src -name "*.csproj")
for project_file in ${PROJECT_FILES}; do
  echo "Setting version in ${project_file} to ${LD_RELEASE_VERSION}"
  temp_file="${project_file}.tmp"
  sed "s#^\( *\)<Version>[^<]*</Version>#\1<Version>${LD_RELEASE_VERSION}</Version>#g" "${project_file}" > "${temp_file}"
  mv "${temp_file}" "${project_file}"
done

# Besides updating the version, we also need to tell Releaser if we'll want it to download any
# key files for strong-naming. We inspect the project files to see if any keys were specified.

for project_file in ${PROJECT_FILES}; do
  key_file=$(sed <"${project_file}" -n -e 's#.*<AssemblyOriginatorKeyFile> *[./\\]*\([^ <]*\) *</AssemblyOriginatorKeyFile>.*#\1#p')
  if [[ -n "${key_file}" ]]; then
    echo "Adding ${key_file} to required secrets"
    touch .ldrelease/secrets.properties
    echo "${key_file}=blob:/ci/dotnet/${key_file}" >> .ldrelease/secrets.properties
  fi
done
