import * as Dialog from "@radix-ui/react-dialog";
import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronRight, Folder, Home, X, LoaderCircle } from "lucide-react";
import { foldersQuery, pathQuery, type DriveItem } from "./drive-api";
import {
  createFolder,
  renameItem,
  deleteItem,
  moveItem,
} from "./operations-api";
import styles from "./Operations.module.css";
export type Operation =
  | { mode: "create"; parentId: string | null }
  | { mode: "rename" | "delete" | "move"; item: DriveItem };
export function OperationDialog({
  operation,
  close,
  completed,
  restoreFocus,
}: {
  restoreFocus: () => void;
  operation: Operation;
  close: () => void;
  completed: (message: string) => void;
}) {
  const client = useQueryClient();
  const [name, setName] = useState(
    operation.mode === "rename" ? operation.item.name : "",
  );
  const [destination, setDestination] = useState<string | null>(null);
  const [localError, setLocalError] = useState("");
  const move = operation.mode === "move";
  const folders = useQuery({ ...foldersQuery(destination), enabled: move });
  const path = useQuery({ ...pathQuery(destination), enabled: move });
  const mutation = useMutation({
    mutationFn: async () => {
      if (operation.mode === "create")
        return createFolder(name.trim(), operation.parentId);
      if (operation.mode === "rename")
        return renameItem(operation.item, name.trim());
      if (operation.mode === "delete") return deleteItem(operation.item);
      return moveItem(operation.item, destination);
    },
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ["drive"] });
      completed(
        operation.mode === "create"
          ? "Folder created."
          : operation.mode === "rename"
            ? "Item renamed."
            : operation.mode === "delete"
              ? "Item deleted."
              : "Item moved.",
      );
      close();
    },
  });
  function submit(event: FormEvent) {
    event.preventDefault();
    setLocalError("");
    if (
      (operation.mode === "create" || operation.mode === "rename") &&
      (!name.trim() ||
        /[\\/\u0000-\u001f]/.test(name) ||
        name.trim() === "." ||
        name.trim() === "..")
    ) {
      setLocalError("Enter a name without slashes or control characters.");
      return;
    }
    mutation.mutate();
  }
  const sameParent =
    move &&
    destination ===
      (operation.item.kind === "folder"
        ? operation.item.parentFolderId
        : operation.item.folderId);
  const invalidDestination =
    move &&
    operation.item.kind === "folder" &&
    (destination === operation.item.id ||
      path.data?.some((folder) => folder.id === operation.item.id));
  const title =
    operation.mode === "create"
      ? "New folder"
      : operation.mode === "rename"
        ? "Rename"
        : operation.mode === "delete"
          ? "Delete permanently?"
          : "Move to";
  return (
    <Dialog.Root
      open
      onOpenChange={(open) => {
        if (!open && !mutation.isPending) close();
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay className={styles.overlay} />
        <Dialog.Content
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            restoreFocus();
          }}
          className={styles.dialog}
          onEscapeKeyDown={(event) => {
            if (mutation.isPending) event.preventDefault();
          }}
        >
          <div className={styles.dialogHeader}>
            <Dialog.Title>{title}</Dialog.Title>
            <Dialog.Close
              className="iconButton"
              aria-label="Close dialog"
              disabled={mutation.isPending}
            >
              <X size={20} />
            </Dialog.Close>
          </div>
          <Dialog.Description className={styles.description}>
            {operation.mode === "create"
              ? "Keep related files together in a folder."
              : operation.mode === "delete"
                ? `“${operation.item.name}”${operation.item.kind === "folder" ? " and all files and subfolders inside it" : ""} will be permanently deleted. This cannot be undone.`
                : operation.mode === "rename"
                  ? `Choose a new name for “${operation.item.name}”.`
                  : `Choose a destination for “${operation.item.name}”.`}
          </Dialog.Description>
          <form onSubmit={submit}>
            <fieldset disabled={mutation.isPending} className={styles.fields}>
              {(operation.mode === "create" || operation.mode === "rename") && (
                <label className={styles.nameField}>
                  Name
                  <input
                    autoFocus
                    value={name}
                    onFocus={(event) => event.target.select()}
                    onChange={(event) => setName(event.target.value)}
                    maxLength={255}
                    required
                  />
                </label>
              )}
              {move && (
                <>
                  <nav
                    aria-label="Destination path"
                    className={styles.destinationPath}
                  >
                    <button
                      type="button"
                      className="textButton"
                      onClick={() => setDestination(null)}
                    >
                      <Home size={16} />
                      Home
                    </button>
                    {path.data?.map((folder) => (
                      <button
                        type="button"
                        className="textButton"
                        key={folder.id}
                        onClick={() => setDestination(folder.id)}
                      >
                        <ChevronRight size={14} />
                        {folder.name}
                      </button>
                    ))}
                  </nav>
                  <div className={styles.folderPicker}>
                    {folders.isPending || path.isPending ? (
                      <p role="status">Loading folders…</p>
                    ) : folders.error || path.error ? (
                      <div className="error" role="alert">
                        Could not load destination folders.
                        <button
                          type="button"
                          className="textButton"
                          onClick={() => {
                            void folders.refetch();
                            void path.refetch();
                          }}
                        >
                          Retry
                        </button>
                      </div>
                    ) : (
                      <>
                        {folders.data
                          ?.filter(
                            (folder) =>
                              operation.item.kind !== "folder" ||
                              folder.id !== operation.item.id,
                          )
                          .map((folder) => (
                            <button
                              type="button"
                              key={folder.id}
                              onClick={() => setDestination(folder.id)}
                            >
                              <Folder size={20} />
                              {folder.name}
                              <ChevronRight size={16} />
                            </button>
                          ))}
                        {!folders.data?.some(
                          (folder) =>
                            operation.item.kind !== "folder" ||
                            folder.id !== operation.item.id,
                        ) && (
                          <p>
                            No subfolders here. You can move the item into this
                            folder.
                          </p>
                        )}
                      </>
                    )}
                  </div>
                </>
              )}
              {(localError || mutation.error) && (
                <div className="error" role="alert">
                  {localError || mutation.error?.message}
                </div>
              )}
              <div className={styles.dialogActions}>
                <button type="button" className="secondary" onClick={close}>
                  Cancel
                </button>
                <button
                  className={
                    operation.mode === "delete" ? "primary danger" : "primary"
                  }
                  disabled={
                    mutation.isPending ||
                    (move &&
                      (folders.isPending ||
                        path.isPending ||
                        !!folders.error ||
                        !!path.error ||
                        sameParent ||
                        !!invalidDestination))
                  }
                >
                  {mutation.isPending && (
                    <LoaderCircle size={16} className="spin" />
                  )}
                  {operation.mode === "delete"
                    ? "Delete permanently"
                    : operation.mode === "move"
                      ? "Move here"
                      : operation.mode === "create"
                        ? "Create"
                        : "Save"}
                </button>
              </div>
            </fieldset>
          </form>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
