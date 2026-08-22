# LAN Portal Add-in API Design

## Purpose

This document records the proposed foundation for third-party add-ins. The core application should support authorization and isolated add-in data without introducing a core user database or requiring the core File Explorer to understand every add-in permission.

## Core Principles

- The core application remains session and device based for now.
- The core File Explorer exposes only its own five user-facing actions: Upload, Download, Rename, Delete, and New Folder.
- Add-in permissions are declared, named, and managed by each add-in.
- The core application enforces authorization boundaries but does not interpret every add-in capability.
- Add-in data is isolated by add-in identity and cannot be accessed through another add-in's namespace.
- The add-in API should remain stable if a future account system replaces the current session-based identity.

## Access Subject

The core should expose an abstract access subject rather than exposing the current session implementation directly:

```csharp
public sealed record AccessSubject(
    string SubjectKey,
    string DisplayName,
    string DeviceName,
    Guid SessionId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> CorePermissions);
```

`SubjectKey` is opaque to add-ins. Initially it may represent an approved name and device or another session-derived identity. It must not be treated as a permanent user-account identifier. A future account model can change how the key is created without changing the add-in API.

## Add-in Contract

An add-in should identify itself and declare its capabilities through a manifest:

```csharp
public interface ILanPortalAddIn
{
    string Id { get; }
    string Name { get; }
    AddInManifest Manifest { get; }

    void Configure(AddInContext context);
}
```

The context provides the current subject, authorization, isolated data, and logging:

```csharp
public interface IAddInContext
{
    IAccessSubjectAccessor CurrentSubject { get; }
    IAddInDataStore Data { get; }
    IAddInAuthorization Authorization { get; }
    ILogger Logger { get; }
}
```

Example usage:

```csharp
var subject = context.CurrentSubject.GetRequired();

if (!context.Authorization.HasPermission(subject, "calendar.view"))
{
    return Forbid();
}

var preferences = await context.Data
    .ForSubject(subject.SubjectKey)
    .GetAsync<CalendarPreferences>("preferences");
```

## Permission Namespaces

Permission keys should be namespaced so add-ins cannot collide with each other or with the core application:

```text
file:upload
file:download
file:rename
file:delete
file:new-folder

acme.calendar:view
acme.calendar:create
acme.photos:view
acme.photos:organize
```

The exact separator convention should be finalized before the SDK is published. The important requirement is that the add-in ID owns the namespace.

Each add-in declares its permissions and friendly descriptions:

```json
{
  "id": "acme.calendar",
  "name": "Calendar",
  "permissions": [
    {
      "key": "acme.calendar:view",
      "displayName": "View calendar",
      "description": "View calendar information"
    },
    {
      "key": "acme.calendar:create",
      "displayName": "Create events",
      "description": "Create calendar events"
    }
  ]
}
```

Unknown add-in permissions should not appear in the core File Explorer. They belong in the add-in's own UI or a future central permissions view.

## Roles and Permissions

Roles are useful bundles, but API authorization should ultimately evaluate permissions:

```text
Standard User
  file:read
  file:upload
  file:download
  file:rename
  file:delete
  file:new-folder

Administrator
  core permissions and administration permissions

Calendar User
  acme.calendar:view
  acme.calendar:create
```

The current JWT can continue carrying effective permission claims. Later, the host can resolve permissions from the current access grant, roles, add-in approvals, and other policy data.

## Isolated Add-in Data

Add-ins should not receive unrestricted access to the core SQLite database. The host should provide an add-in-scoped store:

```csharp
public interface IAddInDataStore
{
    ISubjectDataStore ForSubject(string subjectKey);
    ISharedDataStore Shared { get; }
}
```

A first implementation could use tables such as:

```text
AddInData
- AddInId
- SubjectKey
- DataKey
- DataJson
- UpdatedAtUtc
```

The host must supply `AddInId` from the active add-in context. An add-in should not be able to select another add-in's namespace.

If multiple add-ins from one publisher need shared data, that capability should be explicitly declared and separately permissioned. Publisher identity may group packages, signing, updates, and shared services, but add-in data remains isolated by default.

## Authorization Flow

```text
Access Session
    |
    +-- Core permissions
    |     +-- file:upload
    |     +-- file:download
    |     +-- file:rename
    |     +-- file:delete
    |     +-- file:new-folder
    |
    +-- Add-in grants
          +-- acme.calendar:view
          +-- acme.calendar:create
```

The host should:

1. Discover and verify an add-in package.
2. Read its manifest.
3. Register its namespaced permissions.
4. Let the host approve or enable the add-in.
5. Let the add-in provide detailed permission management.
6. Include only approved permissions in the add-in authorization context.
7. Enforce add-in data isolation and permission checks server-side.

## Identity Transition

The current product does not need a user database for the core File Explorer. The add-in API should still be designed around an abstract subject so it can evolve:

```text
Current:
  approved name + device -> access session -> JWT

Future:
  user account + device -> access grant -> access session -> JWT
```

Add-in data should follow the opaque subject contract, not a raw session ID. The initial subject implementation may be session/device based, but the SDK should not promise that it is permanent until a durable account identity exists.

## Open Decisions

- Final permission key syntax and validation rules.
- How add-ins are packaged, signed, discovered, and updated.
- Whether the host approves an add-in once or approves each permission.
- Whether add-ins can expose server endpoints, UI components, background jobs, or all three.
- Whether access subjects should initially follow an approved name, a device, or a name/device pair.
- How existing JWT permission claims are invalidated when add-in grants change.
- Whether shared publisher services need a separate trusted boundary.
