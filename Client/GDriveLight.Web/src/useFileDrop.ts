import { useEffect, useRef, useState, type DragEvent } from "react";
export function filesFromDrop(data: DataTransfer) {
  const files: File[] = [];
  let foldersSkipped = false;
  if (data.items?.length) {
    for (const item of Array.from(data.items)) {
      if (item.kind !== "file") continue;
      const entry = item.webkitGetAsEntry?.();
      if (entry?.isDirectory) {
        foldersSkipped = true;
        continue;
      }
      const file = item.getAsFile();
      if (file) files.push(file);
    }
  } else files.push(...Array.from(data.files));
  return { files, foldersSkipped };
}
const isFileDrag = (data: DataTransfer) =>
  Array.from(data.types).includes("Files");
export function useFileDrop(
  enabled: boolean,
  dropped: (files: File[], foldersSkipped: boolean) => void,
) {
  const [dragging, setDragging] = useState(false);
  const depth = useRef(0);
  const reset = () => {
    depth.current = 0;
    setDragging(false);
  };
  useEffect(() => {
    // Dropping outside the upload surface must never navigate away from the app.
    const prevent = (event: globalThis.DragEvent) => {
      if (event.dataTransfer && isFileDrag(event.dataTransfer)) {
        event.preventDefault();
        if (event.type === "drop") reset();
      }
    };
    window.addEventListener("dragover", prevent);
    window.addEventListener("drop", prevent);
    window.addEventListener("dragend", reset);
    window.addEventListener("blur", reset);
    return () => {
      window.removeEventListener("dragover", prevent);
      window.removeEventListener("drop", prevent);
      window.removeEventListener("dragend", reset);
      window.removeEventListener("blur", reset);
    };
  }, []);
  useEffect(() => {
    if (!enabled) reset();
  }, [enabled]);
  return {
    dragging: dragging && enabled,
    handlers: {
      onDragEnter: (event: DragEvent) => {
        if (!isFileDrag(event.dataTransfer)) return;
        event.preventDefault();
        depth.current++;
        if (enabled) setDragging(true);
      },
      onDragOver: (event: DragEvent) => {
        if (!isFileDrag(event.dataTransfer)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = enabled ? "copy" : "none";
      },
      onDragLeave: (event: DragEvent) => {
        if (!isFileDrag(event.dataTransfer)) return;
        event.preventDefault();
        depth.current = Math.max(0, depth.current - 1);
        if (!depth.current) setDragging(false);
      },
      onDrop: (event: DragEvent) => {
        if (!isFileDrag(event.dataTransfer)) return;
        event.preventDefault();
        reset();
        if (!enabled) return;
        const result = filesFromDrop(event.dataTransfer);
        dropped(result.files, result.foldersSkipped);
      },
    },
  };
}
