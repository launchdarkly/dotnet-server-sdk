#!/bin/bash

set -eu

# CircleCI Mac hosts do have Homebrew installed, but installing the AWS CLI via Homebrew is
# somewehat inefficient and has been unreliable in the past, so we'll run the AWS installer
# as described at https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2-mac.html#cliv2-mac-install-cmd

INSTALLER_PACKAGE="${LD_RELEASE_TEMP_DIR}/AWSCLIV2.pkg"

curl --fail "https://awscli.amazonaws.com/AWSCLIV2.pkg" -o "${INSTALLER_PACKAGE}"
sudo installer -pkg "${INSTALLER_PACKAGE}" -target /
