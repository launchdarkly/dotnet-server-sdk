
$ErrorActionPreference = "Stop"

# Create directories that Releaser has set paths for in environment variables
new-item -path "${env:LD_RELEASE_TEMP_DIR}" -itemtype "directory" -force | out-null
new-item -path "${env:LD_RELEASE_SECRETS_DIR}" -itemtype "directory" -force | out-null
new-item -path "${env:LD_RELEASE_ARTIFACTS_DIR}" -itemtype "directory" -force | out-null
new-item -path "${env:LD_RELEASE_DOCS_DIR}" -itemtype "directory" -force | out-null

# Download all AWS secrets that are listed in either .ldrelease/secrets.properties or
# .ldrelease/circleci/template/secrets.properties
$secretsConfig = ""
if (test-path "./.ldrelease/secrets.properties") {
    $secretsConfig += get-content -path "./.ldrelease/secrets.properties" -raw
}
if (test-path "./.ldrelease/circleci/template/secrets.properties") {
    $secretsConfig += get-content -path "./.ldrelease/circleci/template/secrets.properties" -raw
}
if ($secretsConfig -ne "") {
    $lines = $secretsConfig.Split([string[]]"`r`n", [StringSplitOptions]::None)
    foreach ($line in $lines) {
        if ($line -match "^([a-zA-z][^= ]*) *= *(.*)$") {
            $name = $matches[1]
            $url = $matches[2]
            $dest = "${env:LD_RELEASE_SECRETS_DIR}/${name}"
            write-host "getting ${url}"
            if ($url -match "^blob:") {
                $key = $url.substring(5)
                & aws s3 cp "s3://${env:LD_RELEASE_FILE_BUCKET}${key}" $dest | out-null
            } elseif ($url -match "^param:") {
                $key = $url.substring(6)
                & aws ssm get-parameter --name $key --with-decryption --query "Parameter.Value" --output text `
                    | out-file $dest -encoding utf8 -nonewline
            }
            if ($lastexitcode -ne 0) {
                throw "Failed to get AWS resource; if this is a permission error, you may not have set a CircleCI context"
            }
        }
    }
}
