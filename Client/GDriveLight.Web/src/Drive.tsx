import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ChevronRight,
  RotateCw,
  Search,
  X,
  Cloud,
  ArrowUp,
  ArrowDown,
  LoaderCircle,
  Plus,
  Upload,
  FolderPlus,
  Download,
  Pencil,
  FolderInput,
  Trash2,
  MoreVertical,
  Eye,
} from "lucide-react";
import { Profile, Shell } from "./Layout";
import { FileIcon } from "./FileIcon";
import {
  folderUrl,
  formatSize,
  loadItems,
  pathQuery,
  sortItems,
  typesQuery,
  type SortKey,
} from "./drive-api";
import styles from "./Drive.module.css";
import * as Menu from "@radix-ui/react-dropdown-menu";
import { OperationDialog, type Operation } from "./OperationDialog";
import { downloadItem } from "./operations-api";
import { useUploads } from "./useUploads";
import { UploadQueue } from "./UploadQueue";
import { ActivityPopups } from "./ActivityPopups";
import { PreviewDialog } from "./PreviewDialog";
import { useFileDrop } from "./useFileDrop";
import { useCloudMove } from "./useCloudMove";
import type { DriveFile, DriveItem } from "./drive-api";
import { useFileSelection } from "./useFileSelection";
import { useMarquee } from "./useMarquee";
import { batchError, runItemBatch } from "./batch";
import {
  ColumnsMenu,
  useVisibleColumns,
  type VisibleColumns,
} from "./ColumnsMenu";
export default function Drive() {
  const { folderId: routeId } = useParams();
  const navigate = useNavigate();
  const folderId = routeId || null;
  const [params, setParams] = useSearchParams();
  const search = params.get("q")?.trim() || "";
  const type = params.get("type") || "";
  const [sort, setSort] = useState<SortKey>("name");
  const [descending, setDescending] = useState(false);
  const [visibleColumns, setVisibleColumns] = useVisibleColumns();
  function changeColumns(next: VisibleColumns) {
    setVisibleColumns(next);
    if (
      (sort === "created" && !next.created) ||
      (sort === "modified" && !next.modified) ||
      (sort === "size" && !next.size)
    ) {
      setSort("name");
      setDescending(false);
    }
  }
  const [actionMenuId, setActionMenuId] = useState<string | null>(null);
  const rowRefs = useRef(new Map<string, HTMLTableRowElement>());
  const client = useQueryClient();
  const [operation, setOperation] = useState<Operation | null>(null);
  const [preview, setPreview] = useState<{
    files: DriveFile[];
    initialId: string;
  } | null>(null);
  const previewFocus = useRef<HTMLElement | null>(null);
  const [notice, setNotice] = useState("");
  const [actionError, setActionError] = useState("");
  const [downloading, setDownloading] = useState(false);
  const downloadController = useRef<AbortController | null>(null);
  const input = useRef<HTMLInputElement>(null);
  const pickerDestination = useRef<{
    folderId: string | null;
    folderName: string;
  } | null>(null);
  const uploads = useUploads();
  const newButton = useRef<HTMLButtonElement>(null);
  const menuTrigger = useRef<HTMLElement | null>(null);
  const restoreTarget = useRef<HTMLElement | null>(null);
  function openOperation(next: Operation) {
    const focused = document.activeElement as HTMLElement | null;
    restoreTarget.current =
      focused?.getAttribute("role") === "menuitem"
        ? menuTrigger.current
        : focused;
    setOperation(next);
  }
  function restoreFocus() {
    (restoreTarget.current?.isConnected
      ? restoreTarget.current
      : newButton.current
    )?.focus();
  }
  useEffect(() => () => downloadController.current?.abort(), []);
  async function download(input: DriveItem | DriveItem[]) {
    if (downloading) return;
    setActionError("");
    setDownloading(true);
    downloadController.current = new AbortController();
    try {
      const result = await runItemBatch(
        (Array.isArray(input) ? input : [input]).filter(
          (item) => item.kind === "file",
        ),
        (item) => downloadItem(item, downloadController.current!.signal),
      );
      if (result.failed.length && !downloadController.current.signal.aborted)
        setActionError(batchError(result));
    } catch (error) {
      if (!(error instanceof Error && error.name === "AbortError"))
        setActionError(
          error instanceof Error ? error.message : "Download failed.",
        );
    } finally {
      setDownloading(false);
    }
  }
  const path = useQuery(pathQuery(folderId));
  const types = useQuery(typesQuery);
  const content = useQuery({
    queryKey: ["drive", "content", folderId, search],
    queryFn: ({ signal }) => loadItems(folderId, search, signal),
  });
  const items = sortItems(content.data || [], sort, descending, type);
  const selection = useFileSelection(items);
  const selected =
    selection.items.length === 1 ? selection.items[0] : undefined;
  const marquee = useMarquee(selection.ids, selection.replace);
  useEffect(() => {
    selection.clear();
    setActionMenuId(null);
    setPreview(null);
  }, [folderId, search, selection.clear]);
  function openPreview(item: DriveFile) {
    const focused = document.activeElement as HTMLElement | null;
    previewFocus.current =
      focused?.getAttribute("role") === "menuitem"
        ? menuTrigger.current
        : focused;
    setPreview({
      files: items.filter(
        (item): item is DriveFile & { kind: "file" } => item.kind === "file",
      ),
      initialId: item.id,
    });
  }
  function selectedOperation(
    mode: "move" | "delete",
    targets = selection.items,
  ) {
    if (targets.length)
      openOperation({ mode, item: targets[0], items: targets });
  }
  function keyDown(event: React.KeyboardEvent<HTMLElement>) {
    if (
      event.target instanceof Element &&
      event.target.closest(
        'input, textarea, select, button, a, [role="menuitem"]',
      )
    )
      return;
    if (event.key === "a" && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      selection.selectAll();
      return;
    }
    if (event.key === "Escape") {
      event.preventDefault();
      selection.clear();
      return;
    }
    if (event.key === "Delete" && selection.items.length) {
      event.preventDefault();
      selectedOperation("delete");
      return;
    }
    if (event.key === "F2" && selected) {
      event.preventDefault();
      openOperation({ mode: "rename", item: selected });
      return;
    }
    const current =
      (event.target as HTMLElement).closest<HTMLElement>("[data-item-id]")
        ?.dataset.itemId || selection.anchor;
    if (event.key === "Enter") {
      const item = items.find((item) => item.id === current);
      if (item?.kind === "folder") {
        event.preventDefault();
        navigate(folderUrl(item.id));
      } else if (item?.kind === "file") {
        event.preventDefault();
        openPreview(item);
      }
      return;
    }
    if (
      event.key === "ContextMenu" ||
      (event.key === "F10" && event.shiftKey)
    ) {
      if (current) {
        event.preventDefault();
        setActionMenuId(current);
      }
      return;
    }
    if (event.key === " " && current) {
      event.preventDefault();
      selection.select(current, event);
      return;
    }
    const index = items.findIndex((item) => item.id === current);
    const next =
      event.key === "ArrowDown"
        ? Math.min(items.length - 1, index + 1)
        : event.key === "ArrowUp"
          ? Math.max(0, index - 1)
          : event.key === "Home"
            ? 0
            : event.key === "End"
              ? items.length - 1
              : -1;
    if (next >= 0 && items[next]) {
      event.preventDefault();
      if (event.shiftKey || (!event.ctrlKey && !event.metaKey))
        selection.select(items[next].id, {
          shiftKey: event.shiftKey,
          ctrlKey: event.shiftKey && (event.ctrlKey || event.metaKey),
        });
      rowRefs.current.get(items[next].id)?.focus();
    }
  }
  const loading = content.isPending || path.isPending;
  const error = content.error || path.error;
  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = String(
      new FormData(event.currentTarget).get("search"),
    ).trim();
    setParams((previous) => {
      if (value) previous.set("q", value);
      else previous.delete("q");
      return previous;
    });
  }
  function changeSort(key: SortKey) {
    if (sort === key) setDescending(!descending);
    else {
      setSort(key);
      setDescending(false);
    }
  }
  const sortHeader = (key: SortKey, text: string) => (
    <button onClick={() => changeSort(key)}>
      {text}
      {sort === key &&
        (descending ? <ArrowDown size={14} /> : <ArrowUp size={14} />)}
    </button>
  );
  const cloudMove = useCloudMove(
    !error && !loading && !operation && !preview,
    (moved) => {
      setNotice(
        moved.length === 1
          ? `“${moved[0].name}” moved.`
          : `${moved.length} items moved.`,
      );
      selection.clear();
      setActionError("");
    },
    (message, remaining) => {
      selection.replace(remaining.map((item) => item.id));
      setActionError(message);
      setNotice("");
    },
  );
  const currentDrop = cloudMove.targetProps(folderId);
  const currentName = path.data?.at(-1)?.name || "Home";
  const drop = useFileDrop(
    !error && !loading && !operation && !preview,
    (files, foldersSkipped) => {
      if (files.length) uploads.enqueue(files, folderId, currentName);
      if (foldersSkipped)
        setActionError(
          "Folder uploads are not supported. Open the local folder and select its files.",
        );
    },
  );
  const rowMenu = (item: DriveItem) => {
    const targets = selection.ids.has(item.id) ? selection.items : [item];
    return (
      <Menu.Root
        modal={false}
        open={actionMenuId === item.id}
        onOpenChange={(open) => {
          if (open && !selection.ids.has(item.id)) selection.select(item.id);
          setActionMenuId(open ? item.id : null);
        }}
      >
        <Menu.Trigger
          onPointerDown={(event) => event.preventDefault()}
          onClick={(event) => {
            event.currentTarget.focus();
            if (!selection.ids.has(item.id)) selection.select(item.id);
            setActionMenuId(actionMenuId === item.id ? null : item.id);
          }}
          onFocus={(event) => {
            menuTrigger.current = event.currentTarget;
          }}
          className="iconButton"
          aria-label={`Actions for ${item.name}`}
        >
          <MoreVertical size={18} />
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Content
            onCloseAutoFocus={(event) => {
              if (operation || preview) event.preventDefault();
            }}
            className="menu"
            align="end"
          >
            {targets.length === 1 && item.kind === "file" && (
              <Menu.Item
                className="menuItem"
                onSelect={() => openPreview(item)}
              >
                <Eye size={16} />
                Preview
              </Menu.Item>
            )}
            {targets.every((item) => item.kind === "file") && (
              <Menu.Item
                className="menuItem"
                disabled={downloading}
                onSelect={() => void download(targets)}
              >
                <Download size={16} />
                Download
              </Menu.Item>
            )}
            {targets.length === 1 && (
              <Menu.Item
                className="menuItem"
                onSelect={() => openOperation({ mode: "rename", item })}
              >
                <Pencil size={16} />
                Rename
              </Menu.Item>
            )}
            <Menu.Item
              className="menuItem"
              disabled={cloudMove.moving}
              onSelect={() => selectedOperation("move", targets)}
            >
              <FolderInput size={16} />
              Move
            </Menu.Item>
            <Menu.Separator className="separator" />
            <Menu.Item
              className="menuItem"
              onSelect={() => selectedOperation("delete", targets)}
            >
              <Trash2 size={16} />
              Delete
            </Menu.Item>
          </Menu.Content>
        </Menu.Portal>
      </Menu.Root>
    );
  };
  const actions = (
    <>
      <Menu.Root>
        <Menu.Trigger
          ref={newButton}
          onFocus={(event) => {
            menuTrigger.current = event.currentTarget;
          }}
          className="newButton"
          disabled={!!error || path.isPending}
        >
          <Plus size={24} />
          New
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Content
            onCloseAutoFocus={(event) => {
              if (operation) event.preventDefault();
            }}
            className="menu"
            sideOffset={8}
            align="start"
          >
            <Menu.Item
              className="menuItem"
              onSelect={() =>
                openOperation({ mode: "create", parentId: folderId })
              }
            >
              <FolderPlus size={18} />
              New folder
            </Menu.Item>
            <Menu.Item
              className="menuItem"
              onSelect={() => {
                pickerDestination.current = {
                  folderId,
                  folderName: currentName,
                };
                input.current?.click();
              }}
            >
              <Upload size={18} />
              Upload files
            </Menu.Item>
          </Menu.Content>
        </Menu.Portal>
      </Menu.Root>
      <input
        ref={input}
        type="file"
        multiple
        hidden
        disabled={loading || !!error}
        aria-label="Upload files"
        onChange={(event) => {
          const destination = pickerDestination.current || {
            folderId,
            folderName: currentName,
          };
          uploads.enqueue(
            Array.from(event.target.files || []),
            destination.folderId,
            destination.folderName,
          );
          pickerDestination.current = null;
          event.target.value = "";
        }}
      />
    </>
  );
  return (
    <Shell actions={actions} homeDropProps={cloudMove.targetProps(null)}>
      <div
        data-testid="upload-surface"
        className={styles.dropSurface}
        onClick={(event) => {
          if (event.target === event.currentTarget) selection.clear();
        }}
        onDragEnter={(event) => {
          currentDrop.onDragEnter(event);
          drop.handlers.onDragEnter(event);
        }}
        onDragOver={(event) => {
          currentDrop.onDragOver(event);
          drop.handlers.onDragOver(event);
        }}
        onDragLeave={(event) => {
          currentDrop.onDragLeave(event);
          drop.handlers.onDragLeave(event);
        }}
        onDrop={(event) => {
          currentDrop.onDrop(event);
          if (!event.defaultPrevented) drop.handlers.onDrop(event);
        }}
      >
        {drop.dragging && (
          <div className={styles.dropOverlay} role="status">
            <Upload size={48} />
            <strong>Drop files to upload to {currentName}</strong>
            <span>Files will be added to this cloud folder.</span>
          </div>
        )}
        <header className={styles.header}>
          <nav aria-label="Folder path" className={styles.breadcrumb}>
            <Link to="/" {...cloudMove.targetProps(null)}>
              Home
            </Link>
            {path.data?.map((folder) => (
              <span key={folder.id}>
                <ChevronRight size={18} />
                <Link
                  {...cloudMove.targetProps(folder.id)}
                  to={folderUrl(folder.id)}
                  aria-current={folder.id === folderId ? "page" : undefined}
                >
                  {folder.name}
                </Link>
              </span>
            ))}
          </nav>
          <div className={styles.headerActions}>
            <button
              className="iconButton"
              aria-label="Refresh"
              title="Refresh"
              onClick={() =>
                void client.invalidateQueries({ queryKey: ["drive"] })
              }
            >
              <RotateCw
                size={20}
                className={content.isFetching ? "spin" : ""}
              />
            </button>
            <Profile />
          </div>
        </header>
        <form
          key={`${folderId}:${search}`}
          onSubmit={submitSearch}
          className={styles.search}
        >
          <button aria-label="Search" className="iconButton">
            <Search size={20} />
          </button>
          <input
            name="search"
            aria-label="Search files and folders"
            placeholder="Search files and folders"
            defaultValue={search}
          />
          {search && (
            <button
              type="button"
              aria-label="Clear search"
              className="iconButton"
              onClick={() =>
                setParams((previous) => {
                  previous.delete("q");
                  return previous;
                })
              }
            >
              <X size={18} />
            </button>
          )}
        </form>
        <div className={styles.toolbar}>
          <label className={styles.filter}>
            Type:
            <select
              aria-label="File type"
              value={type}
              onChange={(event) =>
                setParams((previous) => {
                  if (event.target.value)
                    previous.set("type", event.target.value);
                  else previous.delete("type");
                  return previous;
                })
              }
            >
              <option value="">All types</option>
              {types.data?.map((fileType) => (
                <option key={fileType.id} value={fileType.id}>
                  {fileType.extension}
                </option>
              ))}
            </select>
          </label>
          {search && (
            <span className={styles.count}>
              Results for “{search}” across your cloud
            </span>
          )}
          {!!selection.items.length && (
            <div
              className="selectionActions"
              aria-label="Selected item actions"
            >
              <span className={styles.count}>
                {selection.items.length} selected
              </span>
              {selection.items.every((item) => item.kind === "file") && (
                <button
                  className="iconButton"
                  title="Download"
                  aria-label={
                    selection.items.length === 1
                      ? "Download selected file"
                      : "Download selected files"
                  }
                  disabled={downloading}
                  onClick={() => void download(selection.items)}
                >
                  <Download size={18} />
                </button>
              )}
              {selected && (
                <button
                  className="iconButton"
                  title="Rename"
                  aria-label="Rename selected item"
                  onClick={() =>
                    openOperation({ mode: "rename", item: selected })
                  }
                >
                  <Pencil size={18} />
                </button>
              )}
              <button
                className="iconButton"
                title="Move"
                aria-label={
                  selection.items.length === 1
                    ? "Move selected item"
                    : "Move selected items"
                }
                disabled={cloudMove.moving}
                onClick={() => selectedOperation("move")}
              >
                <FolderInput size={18} />
              </button>
              <button
                className="iconButton"
                title="Delete"
                aria-label={
                  selection.items.length === 1
                    ? "Delete selected item"
                    : "Delete selected items"
                }
                onClick={() => selectedOperation("delete")}
              >
                <Trash2 size={18} />
              </button>
              <button
                className="iconButton"
                aria-label="Clear selection"
                onClick={selection.clear}
              >
                <X size={18} />
              </button>
            </div>
          )}
          <ColumnsMenu
            visible={visibleColumns}
            onChange={changeColumns}
            className={styles.columnsButton}
          />
        </div>
        {types.error && (
          <div className="error" role="alert">
            File type filter unavailable.{" "}
            <button className="textButton" onClick={() => void types.refetch()}>
              Retry
            </button>
          </div>
        )}
        {error ? (
          <div className="error" role="alert">
            {error.message}
            <button
              className="textButton"
              onClick={() =>
                void client.invalidateQueries({ queryKey: ["drive"] })
              }
            >
              Try again
            </button>
            <Link to="/">Go to Home</Link>
          </div>
        ) : loading ? (
          <div className={styles.empty} role="status">
            <LoaderCircle className="spin" size={32} />
            <p>Loading your files…</p>
          </div>
        ) : items.length === 0 ? (
          <div className={styles.empty}>
            <Cloud size={52} />
            <h1>
              {search || type ? "No matching items" : "This folder is empty"}
            </h1>
            <p>
              {search || type
                ? "Try a different search or file type."
                : "Choose New to create a folder or upload files."}
            </p>
          </div>
        ) : (
          <div
            className={styles.tableScroll}
            data-testid="file-selection-area"
            tabIndex={0}
            onKeyDown={keyDown}
            {...marquee.handlers}
          >
            {marquee.rectangle && (
              <div className={styles.marquee} style={marquee.rectangle} />
            )}
            <table
              className={styles.table}
              aria-label="Files and folders"
              style={{
                minWidth:
                  240 +
                  (visibleColumns.created ? 160 : 0) +
                  (visibleColumns.modified ? 160 : 0) +
                  (visibleColumns.size ? 110 : 0),
              }}
            >
              <thead>
                <tr>
                  <th
                    aria-sort={
                      sort === "name"
                        ? descending
                          ? "descending"
                          : "ascending"
                        : "none"
                    }
                  >
                    {sortHeader("name", "Name")}
                  </th>
                  {visibleColumns.modified && (
                    <th
                      className={styles.dateColumn}
                      aria-sort={
                        sort === "modified"
                          ? descending
                            ? "descending"
                            : "ascending"
                          : "none"
                      }
                    >
                      {sortHeader("modified", "Date modified")}
                    </th>
                  )}
                  {visibleColumns.created && (
                    <th
                      className={styles.dateColumn}
                      aria-sort={
                        sort === "created"
                          ? descending
                            ? "descending"
                            : "ascending"
                          : "none"
                      }
                    >
                      {sortHeader("created", "Date created")}
                    </th>
                  )}
                  {visibleColumns.size && (
                    <th
                      className={styles.sizeColumn}
                      aria-sort={
                        sort === "size"
                          ? descending
                            ? "descending"
                            : "ascending"
                          : "none"
                      }
                    >
                      {sortHeader("size", "File size")}
                    </th>
                  )}
                  <th className="rowActions">
                    <span className="srOnly">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr
                    key={item.id}
                    {...cloudMove.sourceProps(item, selection.items, () =>
                      selection.select(item.id),
                    )}
                    {...(item.kind === "folder"
                      ? cloudMove.targetProps(item.id)
                      : {})}
                    data-item-id={item.id}
                    data-item-kind={item.kind}
                    className={[
                      selection.ids.has(item.id) ? styles.selected : "",
                      item.kind === "folder" ? styles.folderRow : "",
                    ].join(" ")}
                    aria-selected={selection.ids.has(item.id)}
                    tabIndex={
                      item.id === (selection.items[0]?.id || items[0]?.id)
                        ? 0
                        : -1
                    }
                    ref={(element) => {
                      if (element) rowRefs.current.set(item.id, element);
                      else rowRefs.current.delete(item.id);
                    }}
                    onPointerDown={(event) => {
                      if (event.shiftKey) event.preventDefault();
                    }}
                    onContextMenu={(event) => {
                      event.preventDefault();
                      if (!selection.ids.has(item.id))
                        selection.select(item.id);
                      menuTrigger.current =
                        event.currentTarget.querySelector("button");
                      setActionMenuId(item.id);
                    }}
                    onDoubleClick={(event) => {
                      if (
                        item.kind !== "file" ||
                        event.shiftKey ||
                        event.ctrlKey ||
                        event.metaKey ||
                        (event.target instanceof Element &&
                          event.target.closest(
                            "a, button, input, select, textarea",
                          ))
                      )
                        return;
                      window.getSelection()?.removeAllRanges();
                      openPreview(item);
                    }}
                    onClick={(event) => {
                      if (
                        event.target instanceof Element &&
                        event.target.closest(
                          'a, button, input, select, textarea, [role="menuitem"]',
                        )
                      )
                        return;
                      event.currentTarget.focus();
                      if (
                        item.kind === "folder" &&
                        !event.shiftKey &&
                        !event.ctrlKey &&
                        !event.metaKey
                      )
                        navigate(folderUrl(item.id));
                      else selection.select(item.id, event);
                    }}
                  >
                    <td>
                      <div className={styles.name}>
                        <FileIcon
                          name={item.name}
                          folder={item.kind === "folder"}
                        />
                        {item.kind === "folder" ? (
                          <Link
                            draggable={false}
                            onClick={(event) => {
                              if (
                                event.shiftKey ||
                                event.ctrlKey ||
                                event.metaKey
                              ) {
                                event.preventDefault();
                                selection.select(item.id, event);
                                rowRefs.current.get(item.id)?.focus();
                              }
                            }}
                            to={folderUrl(item.id)}
                            title={item.name}
                          >
                            {item.name}
                          </Link>
                        ) : (
                          <span title={item.name}>{item.name}</span>
                        )}
                      </div>
                    </td>
                    {visibleColumns.modified && (
                      <td className={styles.dateColumn}>
                        {item.kind === "file"
                          ? new Date(
                              item.editedAtUtc || item.uploadedAtUtc,
                            ).toLocaleDateString(undefined, {
                              year: "numeric",
                              month: "short",
                              day: "numeric",
                            })
                          : "—"}
                      </td>
                    )}
                    {visibleColumns.created && (
                      <td className={styles.dateColumn}>
                        {item.kind === "file" && item.uploadedAtUtc
                          ? new Date(item.uploadedAtUtc).toLocaleDateString(
                              undefined,
                              {
                                year: "numeric",
                                month: "short",
                                day: "numeric",
                              },
                            )
                          : "—"}
                      </td>
                    )}
                    {visibleColumns.size && (
                      <td className={styles.sizeColumn}>
                        {item.kind === "file" ? formatSize(item.sizeBytes) : "—"}
                      </td>
                    )}
                    <td className="rowActions">{rowMenu(item)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {operation && (
          <OperationDialog
            restoreFocus={restoreFocus}
            operation={operation}
            close={() => setOperation(null)}
            completed={(message) => {
              setNotice(message);
              selection.clear();
            }}
          />
        )}
      </div>
      {preview && (
        <PreviewDialog
          {...preview}
          close={() => setPreview(null)}
          restoreFocus={() => {
            const target = previewFocus.current?.isConnected
              ? previewFocus.current
              : rowRefs.current.get(preview.initialId) || newButton.current;
            target?.focus({ preventScroll: true });
          }}
        />
      )}
      <ActivityPopups
        notice={notice}
        error={actionError}
        moving={cloudMove.moving}
        downloading={downloading}
        dismissNotice={() => setNotice("")}
        dismissError={() => setActionError("")}
        cancelDownload={() => downloadController.current?.abort()}
      >
        <UploadQueue {...uploads} />
      </ActivityPopups>
    </Shell>
  );
}
