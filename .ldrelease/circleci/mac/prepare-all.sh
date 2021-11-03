#!/bin/bash

set -eu

# Create directories that Releaser has set paths for in environment variables
mkdir -p "${LD_RELEASE_TEMP_DIR}"
mkdir -p "${LD_RELEASE_SECRETS_DIR}"
mkdir -p "${LD_RELEASE_ARTIFACTS_DIR}"
mkdir -p  "${LD_RELEASE_DOCS_DIR}"

# Download all AWS secrets that are listed in either .ldrelease/secrets.properties or
# .ldrelease/circleci/template/secrets.properties. This requires the AWS CLI.
secrets_list_file="${LD_RELEASE_TEMP_DIR}/all-secrets.properties"
touch "${secrets_list_file}"
for file in "./.ldrelease/secrets.properties" "./.ldrelease/circleci/template/secrets.properties"; do
  if [[ -f "$file" ]]; then
    # get only the lines that have name-value pairs
    grep "^[a-zA-Z].*=.*$" < "$file" >> "${secrets_list_file}" || true
  fi
done
if [[ -s "${secrets_list_file}" ]]; then
  $(dirname $0)/install-aws-cli.sh
  cat "${secrets_list_file}" | while IFS= read -r line; do
    if [[ "$line" =~ ^[a-zA-Z].*= ]]; then
      name="${line%%=*}"
      url="${line#*=}"
      dest="${LD_RELEASE_SECRETS_DIR}/${name}"
      echo "getting ${url}"
      key="${url#*:}"
      failed=
      if [[ "$url" =~ ^blob: ]]; then
        aws s3 cp "s3://${LD_RELEASE_FILE_BUCKET}${key}" "${dest}" || failed=1  # LD_RELEASE_FILE_BUCKET is set by SecretsManager
      elif [[ "$url" =~ ^param: ]]; then
        aws ssm get-parameter --name "${key}" --with-decryption --query "Parameter.Value" --output text > "${dest}" || failed=1
      fi
      if [[ -n "$failed" ]]; then
        echo "Failed to get AWS resource; if this is a permission error, you may not have set a CircleCI context" >&2
        exit 1
      fi
    fi
  done
fi
