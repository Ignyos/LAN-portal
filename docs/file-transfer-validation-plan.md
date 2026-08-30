# File Transfer Validation Plan

## Purpose

Validate and harden the core LAN Portal file-transfer experience before the `v1.0.0.0` general release. Upload and download behavior must be tested through the actual Host-managed API/Web workflow, not only through isolated unit tests.

The current known risk is that uploads can fail without a documented cause. Downloads have not yet received an equivalent end-to-end validation pass and must be treated as unverified until this plan is completed.

## Test Setup

Use two devices on the same network:

- **Device A:** install and run the latest dev build as the Host.
- **Device B:** connect to Device A using the normal LAN Portal client workflow.

Before testing:

- confirm Device A is running the intended installed dev build, not a separately running API/Web process;
- record the installed version, Host address, network type, and storage location;
- complete the access-request and approval flow before beginning transfer cases;
- use an empty or uniquely named test folder on the Host;
- prepare repeatable source files with known SHA-256 hashes;
- use `< 1 MiB` for the small-file boundary and `> 1 MiB` for the large-file boundary;
- keep large test files below `1 GiB` for this baseline pass;
- include at least one filename containing spaces and one nested directory when the product workflow supports them.

## Baseline Upload Pass

Run these cases independently:

| Case | Selection |
| --- | --- |
| U1 | One file smaller than 1 MiB |
| U2 | One file larger than 1 MiB and smaller than 1 GiB |
| U3 | Multiple files, each smaller than 1 MiB |
| U4 | Multiple files, each larger than 1 MiB and smaller than 1 GiB |

For every case, record:

- access/request status before transfer;
- file names, extensions, and individual sizes;
- total batch size and number of files;
- whether the UI reports success or failure;
- whether every expected file appears on the Host;
- resulting file sizes and SHA-256 hashes;
- elapsed transfer time;
- browser, WebView, API, and Host errors;
- whether retrying creates duplicates, partial files, or zero-byte files.

## Baseline Download Pass

After a successful upload, download the same files back to Device B using the normal application workflow. Run the equivalent four cases:

| Case | Selection |
| --- | --- |
| D1 | One file smaller than 1 MiB |
| D2 | One file larger than 1 MiB and smaller than 1 GiB |
| D3 | Multiple files, each smaller than 1 MiB |
| D4 | Multiple files, each larger than 1 MiB and smaller than 1 GiB |

For every case, verify:

- the download completes and the UI reports the correct result;
- the downloaded file count, names, sizes, and SHA-256 hashes match the source;
- directory structure is preserved when applicable;
- range or streaming behavior does not corrupt the result;
- interrupted downloads do not leave misleading complete files;
- retry behavior is clear and does not create unintended duplicates.

## Failure And Recovery Coverage

The baseline pass must reproduce and document any failure in enough detail for another developer to repeat it. After the four upload and four download cases, add targeted coverage for:

- rejected uploads;
- cancelled transfers;
- interrupted network connections;
- retry after failure;
- duplicate file names;
- missing files;
- permission failures;
- insufficient storage;
- large-file and boundary-size behavior;
- partial or zero-byte artifacts.

For each failure, capture:

- exact reproduction steps;
- source and destination device;
- file size, count, and names;
- visible UI message;
- relevant API/Host log entries;
- whether cleanup occurred;
- user/operator recovery steps;
- whether the issue is fixed, deferred, or release-blocking.

## Success Criteria

The file-transfer release gate is complete only when:

- all baseline upload cases transfer every file intact;
- all baseline download cases return the expected content;
- source and destination hashes match;
- no unexplained upload or download failures remain;
- failed or interrupted transfers do not leave misleading partial results;
- retries do not create unintended duplicates;
- users receive clear success and failure feedback;
- known errors, causes, fixes, and recovery steps are documented;
- the results are recorded for the actual installed Host-managed workflow;
- automated coverage protects the defects and edge cases found during manual validation.

This plan is a baseline, not a substitute for later stress, cancellation, concurrency, or sustained-throughput testing if the baseline reveals risks in those areas.