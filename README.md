# GDriveLight

A lightweight cloud file manager with a Windows desktop client, a web client, and a shared REST API.

## Features

- User registration, sign-in, and personal file storage.
- File upload, download, deletion, renaming, and moving.
- Folder management, breadcrumbs, search, and file type filtering.
- Configurable file metadata columns.
- Image and source code previews, including `.jpg` and `.cs`.
- PDF and DOCX previews in the web client.
- Manual two-way synchronization between a local folder and a device’s cloud folder.
- Drag-and-drop uploads in both clients and downloads to Windows Explorer from the desktop client.
- Local disk or Amazon S3 file storage, with per-user storage quotas.

## Technology

| Component | Stack                                                     |
| --------- | --------------------------------------------------------- |
| Backend   | .NET 10, ASP.NET Core, EF Core, PostgreSQL, Identity, JWT |
| Desktop   | WPF, MVVM, SQLite for local synchronization state         |
| Web       | React, TypeScript, Vite, TanStack Query                   |
| Testing   | xUnit, Moq, Vitest, Testing Library, Playwright           |

The backend follows Clean Architecture with CQRS and MediatR. Both clients communicate with the same REST API.

## Project structure
```arduino
Server/
  GDriveLight.Api/              HTTP endpoints and configuration
  GDriveLight.Application/      Use cases and service abstractions
  GDriveLight.Domain/           Domain models
  GDriveLight.Infrastructure/   Persistence, authentication, and storage
Client/
  GDriveLight.Desktop/          Windows desktop application
  GDriveLight.Web/              Browser application
Tests/
  GDriveLight.Tests/            Backend tests
  GDriveLight.Desktop.Tests/    Desktop tests
UML_Diagrams/                  UML diagrams and design documentation
```

## UML documentation

Explore the [UML diagrams](UML_Diagrams/README.md) for use cases, class relationships, workflows, object interactions, synchronization states, and the system architecture.
