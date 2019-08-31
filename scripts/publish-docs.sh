#!/bin/bash

set -ue

# Publishes HTML content built by build-docs.ps1 to Github Pages. If the gh-pages branch
# doesn't already exist, we will create it. It takes a single parameter: the release version.

# This logic is copied from the publish-github-pages.sh script in Releaser. Once we are able
# to build docs in CI, this step can just be done by Releaser.

if [ ! -d ./docs/build/html ]; then
  echo "Docs have not been built"
  exit 1
fi

LD_RELEASE_VERSION=$1
if [ -z "${LD_RELEASE_VERSION}" ]; then
  echo "Must specify release version"
  exit 1
fi

CONTENT_PATH=$(pwd)/docs/build/html
COMMIT_MESSAGE="Updating documentation to version ${LD_RELEASE_VERSION}"

GH_PAGES_BRANCH=gh-pages

LD_RELEASE_PROJECT_DIR=$(pwd)
LD_RELEASE_TEMP_DIR=$(mktemp -d)
trap "rm -rf ${LD_RELEASE_TEMP_DIR}" EXIT

# Check for a prerelease version string like "2.0.0-beta.1"
if [[ "${LD_RELEASE_VERSION}" =~ '-' ]]; then
  echo "Not publishing documentation because this is not a production release"
  exit 0
fi

echo "Publishing to Github Pages"

cd "${LD_RELEASE_PROJECT_DIR}"
GIT_URL="$(git remote get-url origin)"
GH_PAGES_CHECKOUT_DIR="${LD_RELEASE_TEMP_DIR}/gh-pages-checkout"

rm -rf "${GH_PAGES_CHECKOUT_DIR}"
if git clone -b "${GH_PAGES_BRANCH}" --single-branch "${GIT_URL}" "${GH_PAGES_CHECKOUT_DIR}"; then
  cd "${GH_PAGES_CHECKOUT_DIR}"
  git rm -qr ./* || true
else
  echo "Can't find ${GH_PAGES_BRANCH} branch; creating one from default branch"
  git clone "${GIT_URL}" "${GH_PAGES_CHECKOUT_DIR}"
  cd "${GH_PAGES_CHECKOUT_DIR}"
  # branch off of the very first commit, so the history of the new branch will be simple
  first_commit=$(git log --reverse --format=%H | head -n 1)
  git checkout "${first_commit}"
  git checkout -b "${GH_PAGES_BRANCH}"
  git rm -qr ./* || true
  git commit -m "clearing Github Pages branch" || true
fi

touch .nojekyll  # this turns off unneeded preprocessing by GH Pages which can break our docs
git add .nojekyll
cp -r "${CONTENT_PATH}"/* .
git add ./*
git commit -m "${COMMIT_MESSAGE}" || true  # possibly there are no changes
git push origin "${GH_PAGES_BRANCH}" || { echo "push to gh-pages failed" >&2; exit 1; }
