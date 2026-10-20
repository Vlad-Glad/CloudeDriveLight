# GDriveLight UML

The UML specification of GDriveLight: nine diagrams covering user workflows, the domain model, object interactions, synchronization, and deployment.

[Project overview](../README.md) · [Diagram files](diagrams/)

> **Design snapshot**
>
> These diagrams capture the initial design. The implementation has since evolved: local synchronization state is stored in the desktop client's SQLite database as `LocalItemState`, while the server maintains a change log. References to `SyncFileState` describe the earlier design. Service names in the diagrams represent responsibilities now implemented through CQRS handlers and infrastructure services.

## Diagram index

| Diagram | What it describes |
| --- | --- |
| [Use cases](diagrams/01-use-case.png) | User roles and file management capabilities |
| [Domain classes](diagrams/02-domain-classes.png) | Entities, relationships, and synchronization concepts |
| [View file VOPC](diagrams/03-view-file-vopc.png) | Classes participating in the file viewing use case |
| [Upload activity](diagrams/04-upload-activity.png) | File selection, validation, storage, and outcomes |
| [Delete file sequence](diagrams/05-delete-file-sequence.png) | Ordered calls between the UI, API, storage, and database |
| [Create folder communication](diagrams/06-create-folder-communication.png) | Collaborating objects and numbered messages |
| [Synchronization states](diagrams/07-sync-state.png) | Local changes, remote changes, conflicts, and retries |
| [Components](diagrams/08-components.png) | Client applications, backend layers, and dependencies |
| [Deployment](diagrams/09-deployment.png) | Client devices, the hosted API, database, and file storage |

## User workflows

Expand a diagram to view it inline. Select an image to inspect the full-resolution file.

<details open>
<summary><strong>01 · Use cases</strong> — what users can do</summary>

The system boundary and user roles, including authentication, browsing, file operations, filtering, sorting, and synchronization.

[![Use case diagram showing user roles and file management operations](diagrams/01-use-case.png)](diagrams/01-use-case.png)

</details>

<details>
<summary><strong>04 · Upload activity</strong> — from file selection to storage</summary>

The upload workflow, including validation, saving content and metadata, and handling success or failure.

[![Activity diagram of the file upload workflow](diagrams/04-upload-activity.png)](diagrams/04-upload-activity.png)

</details>

<details>
<summary><strong>07 · Synchronization states</strong> — changes and recovery</summary>

The conceptual synchronization lifecycle, with transitions for local and remote edits, deletions, conflicts, and retrying failed synchronization.

[![State diagram of the synchronization lifecycle](diagrams/07-sync-state.png)](diagrams/07-sync-state.png)

</details>

## Structure and architecture

<details>
<summary><strong>02 · Domain classes</strong> — the core data model</summary>

Users, files, folders, file types, devices, and their relationships, together with the original synchronization model.

[![Class diagram of the domain entities and their relationships](diagrams/02-domain-classes.png)](diagrams/02-domain-classes.png)

</details>

<details>
<summary><strong>03 · View file VOPC</strong> — participating classes</summary>

A View of Participating Classes for viewing a file: the API boundary, coordinating service, repository, domain entities, and response DTO.

[![VOPC diagram of the classes participating in viewing a file](diagrams/03-view-file-vopc.png)](diagrams/03-view-file-vopc.png)

</details>

<details>
<summary><strong>08 · Components</strong> — application boundaries</summary>

The desktop and web clients, the backend's architectural layers, and its persistence and file storage dependencies.

[![Component diagram of the clients and Clean Architecture backend layers](diagrams/08-components.png)](diagrams/08-components.png)

</details>

<details>
<summary><strong>09 · Deployment</strong> — where the system runs</summary>

The client device, local filesystem, hosted ASP.NET Core API, PostgreSQL database, and remote file storage, connected through their respective protocols.

[![Deployment diagram of client devices and hosted backend services](diagrams/09-deployment.png)](diagrams/09-deployment.png)

</details>

## Object interactions

<details>
<summary><strong>05 · Delete file sequence</strong> — calls in order</summary>

The sequence of interactions required to locate a file, remove its stored content and metadata, and return the result to the user interface.

[![Sequence diagram of deleting a file](diagrams/05-delete-file-sequence.png)](diagrams/05-delete-file-sequence.png)

</details>

<details>
<summary><strong>06 · Create folder communication</strong> — collaborating objects</summary>

Numbered messages between the user interface, controller, coordinating service, repository, and database during folder creation.

[![Communication diagram of creating a folder](diagrams/06-create-folder-communication.png)](diagrams/06-create-folder-communication.png)

</details>
