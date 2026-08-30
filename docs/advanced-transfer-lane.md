# Advanced Transfer Lane

## Purpose

Define a possible future transfer lane for advanced users who need interoperability with established file-transfer tools or capabilities beyond the basic browser workflow.

This is a possible product direction, not an approved implementation. The core LAN Portal experience remains browser-based HTTP file sharing for users who need a simple, understandable workflow.

## Product Positioning

LAN Portal can serve two different usage levels:

- **Basic user lane:** simple browser-based file sharing with minimal setup, clear access requests, and no requirement to understand transfer protocols.
- **Advanced user lane:** optional interoperability with a secure protocol such as SFTP or FTPS for users who need an existing desktop client, resumable transfers, automation, or specialized transfer tooling.

An advanced transfer lane could become one possible basis for a freemium model, but that is a business hypothesis rather than a product commitment. Before using it in sales or marketing, validate that advanced transfer capabilities solve a meaningful problem for a definable customer group and justify ongoing support and security costs.

## Protocol Assessment

### Plain FTP

Do not use plain FTP for a product feature. It exposes credentials and file contents without encryption and would create avoidable security risk.

### FTPS

FTPS could provide compatibility with traditional FTP clients while protecting credentials and file contents with TLS. It would require certificate management, passive-mode port configuration, firewall rules, client compatibility testing, and a separate authentication integration.

### SFTP

SFTP is technically distinct from FTP and runs over SSH. It offers encrypted transport and a strong interoperability story, but would require an SSH server, key or credential management, user authorization mapping, host-key handling, and client support.

## Architecture Impact

Adding an advanced transfer lane would be more than a transport substitution. It would introduce another server and client protocol alongside the existing HTTP workflow.

Required design areas include:

- Host-managed service lifecycle and shutdown behavior;
- storage-root and permission mapping;
- access-request and session authorization integration;
- temporary credentials, certificates, keys, or token bridging;
- concurrent transfers, resume, cancellation, partial-file cleanup, and duplicate handling;
- audit logging consistent with the existing application log and access-history boundaries;
- firewall configuration and passive/data-port management where applicable;
- installer, upgrade, and rollback behavior;
- clear separation between basic and advanced user configuration.

The existing HTTP transfer path should remain the source of truth for permissions and storage behavior if a second protocol is added. The two paths must not silently diverge in authorization, file visibility, naming, overwrite rules, or audit behavior.

## Security Requirements

Any future implementation must:

- reject plain FTP;
- use encrypted transport and authenticated connections;
- avoid exposing JWTs, refresh tokens, signing keys, or passwords through protocol configuration;
- define credential expiry, revocation, rotation, and recovery behavior;
- restrict access to the configured storage root;
- enforce the same user and role permissions as the HTTP path;
- record security-relevant connection and transfer events without storing secrets;
- document LAN exposure, firewall requirements, and threat assumptions.

## User Experience Considerations

A protocol-based transfer lane would not naturally fit the current browser UI. The likely experience would be:

- basic users continue using the existing portal;
- advanced users receive optional connection details or configuration guidance;
- supported desktop clients or automation tools perform the transfer;
- the Host remains responsible for authorization, storage, and audit behavior.

This must not make the basic workflow appear incomplete or require every user to configure an FTP/SFTP client.

## Suggested Evaluation Sequence

Before implementation:

1. Finish and harden the existing HTTP upload and download workflow.
2. Identify the concrete advanced-user problem, such as NAS interoperability, automation, or resumable multi-gigabyte transfers.
3. Compare SFTP and FTPS against that problem, including library/server maturity and Windows deployment support.
4. Prototype the smallest secure connection flow without exposing it in the default UI.
5. Prove authorization parity and storage isolation against the HTTP path.
6. Test interruptions, resume, concurrency, large files, revocation, and upgrade/rollback behavior.
7. Estimate certificate/key support, firewall support, documentation, and operational burden.
8. Run product validation before treating the lane as a paid-tier or marketing differentiator.

## Decision Gate

Do not add this lane to the core product roadmap until there is:

- a confirmed advanced-user need;
- a selected secure protocol;
- a defined authentication and authorization model;
- an implementation and support cost estimate;
- evidence that the feature can coexist with simple browser sharing;
- a deliberate product and business decision about free versus paid availability.
