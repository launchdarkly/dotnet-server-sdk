#!/bin/bash

set -eu

# Create directories that Releaser has set paths for in environment variables
mkdir -p "${LD_RELEASE_TEMP_DIR}"
mkdir -p "${LD_RELEASE_SECRETS_DIR}"
mkdir -p "${LD_RELEASE_ARTIFACTS_DIR}"
mkdir -p  "${LD_RELEASE_DOCS_DIR}"

# Download all AWS secrets that are listed in either .ldrelease/secrets.properties or
# .ldrelease/circleci/template/secrets.properties
secrets_list_file="${LD_RELEASE_TEMP_DIR}/all-secrets.properties"
touch "${secrets_list_file}"
for file in "./.ldrelease/secrets.properties" "./.ldrelease/circleci/template/secrets.properties"; do
  if [[ -f "$file" ]]; then
    # get only the lines that have name-value pairs
    grep "^[a-zA-Z].*=.*$" < "$file" >> "${secrets_list_file}" || true
  fi
done
if [[ -s "${secrets_list_file}" ]]; then
  AWS=aws
  if [[ -z "$(which aws || true)" ]]; then
    curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "${LD_RELEASE_TEMP_DIR}/awscliv2.zip"
    if [[ -z "$(which unzip || true)" ]]; then
      apt install -y unzip
    fi
    unzip -q "${LD_RELEASE_TEMP_DIR}/awscliv2.zip" -d "${LD_RELEASE_TEMP_DIR}"
    AWS="${LD_RELEASE_TEMP_DIR}/aws/dist/aws"
  fi
  cat "${secrets_list_file}" | while IFS= read -r line; do
    if [[ "$line" =~ ^[a-zA-Z].*= ]]; then
      name="${line%%=*}"
      url="${line#*=}"
      dest="${LD_RELEASE_SECRETS_DIR}/${name}"
      echo "getting ${url}"
      key="${url#*:}"
      if [[ "$url" =~ ^blob: ]]; then
        $AWS s3 cp "s3://${LD_RELEASE_FILE_BUCKET}${key}" "${dest}"  # LD_RELEASE_FILE_BUCKET is set by SecretsManager
      elif [[ "$url" =~ ^param: ]]; then
        $AWS ssm get-parameter --name "${key}" --with-decryption --query "Parameter.Value" --output text > "${dest}"
      fi
    fi
  done
fi
