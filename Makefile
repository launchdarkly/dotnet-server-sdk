
build:
	dotnet build

test:
	dotnet test

clean:
	dotnet clean

TEMP_TEST_OUTPUT=/tmp/sdk-contract-test-service.log
BUILDFRAMEWORKS ?= netcoreapp2.1
TESTFRAMEWORK ?= netcoreapp2.1

# temporary skips for contract tests that can't pass till more U2C work is done
TEST_HARNESS_PARAMS := $(TEST_HARNESS_PARAMS) \
	-skip 'big segments/evaluation/context kind' \
	-skip 'big segments/membership caching/context kind' \
	-skip 'evaluation/bucketing/bucket by non-key' \
	-skip 'evaluation/bucketing/secondary/a non-empty string/ignored in experiments' \
	-skip 'evaluation/bucketing/secondary/an empty string/affects' \
	-skip 'evaluation/bucketing/selection of context' \
	-skip 'evaluation/parameterized/bad attribute reference errors' \
	-skip 'evaluation/parameterized/clause kind matching' \
	-skip 'evaluation/parameterized/prerequisites' \
	-skip 'evaluation/parameterized/segment recursion' \
	-skip 'evaluation/parameterized/target match/user targets.*multi-kind' \
	-skip 'evaluation/parameterized/target match/context targets' \
	-skip 'events/context properties/multi-kind' \
	-skip 'events/custom' \
	-skip 'events/feature events/full feature/with reason/multi-kind' \
	-skip 'events/feature events/full feature/without reason/multi-kind' \
	-skip 'events/index events/only one index event per evaluation context' \
	-skip 'events/requests/method and headers' \
	-skip 'events/summary events/contextKinds'

build-contract-tests:
	@cd contract-tests && dotnet build TestService.csproj

start-contract-test-service:
	@cd contract-tests && dotnet bin/Debug/${TESTFRAMEWORK}/ContractTestService.dll

start-contract-test-service-bg:
	@echo "Test service output will be captured in $(TEMP_TEST_OUTPUT)"
	@make start-contract-test-service >$(TEMP_TEST_OUTPUT) 2>&1 &

run-contract-tests:
	@curl -s https://raw.githubusercontent.com/launchdarkly/sdk-test-harness/main/downloader/run.sh \
      | VERSION=v2 PARAMS="-url http://localhost:8000 -debug -stop-service-at-end $(TEST_HARNESS_PARAMS)" sh

contract-tests: build-contract-tests start-contract-test-service-bg run-contract-tests

.PHONY: build test clean build-contract-tests start-contract-test-service run-contract-tests contract-tests
