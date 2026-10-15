import { useEffect, useRef, useState, type DragEvent } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { pathQuery, type DriveItem } from "./drive-api";
import { moveItem } from "./operations-api";
import { getSession } from "./session";

export const cloudItemDragType = "application/x-gdrivelight-item";
const parentOf = (item: DriveItem) =>
  item.kind === "folder" ? item.parentFolderId : item.folderId;

export function useCloudMove(
  enabled: boolean,
  completed: (item: DriveItem) => void,
  failed: (message: string) => void,
) {
  const [dragged, setDragged] = useState<DriveItem | null>(null);
  const [hovered, setHovered] = useState<string | null>(null);
  const [moving, setMoving] = useState(false);
  const active = useRef<DriveItem | null>(null);
  const busy = useRef(false);
  const mounted = useRef(false);
  const client = useQueryClient();
  const clear = () => {
    active.current = null;
    setDragged(null);
    setHovered(null);
  };
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      active.current = null;
    };
  }, []);
  useEffect(() => {
    window.addEventListener("dragend", clear);
    window.addEventListener("drop", clear);
    window.addEventListener("blur", clear);
    return () => {
      window.removeEventListener("dragend", clear);
      window.removeEventListener("drop", clear);
      window.removeEventListener("blur", clear);
    };
  }, []);
  useEffect(() => {
    if (!enabled) clear();
  }, [enabled]);
  const ownDrag = (event: DragEvent) =>
    !!active.current &&
    Array.from(event.dataTransfer.types).includes(cloudItemDragType);
  const allowed = (item: DriveItem, destination: string | null) =>
    enabled &&
    !busy.current &&
    destination !== parentOf(item) &&
    !(item.kind === "folder" && destination === item.id);
  async function move(item: DriveItem, destination: string | null) {
    if (!allowed(item, destination)) return;
    const token = getSession()?.token;
    if (!token) return;
    busy.current = true;
    setMoving(true);
    try {
      if (item.kind === "folder" && destination) {
        const ancestors = await client.fetchQuery({
          ...pathQuery(destination),
          staleTime: 0,
        });
        if (ancestors.some((folder) => folder.id === item.id))
          throw new Error(
            "Cannot move a folder into itself or its descendant.",
          );
      }
      if (getSession()?.token !== token) return;
      await moveItem(item, destination);
      if (mounted.current && getSession()?.token === token) completed(item);
    } catch (error) {
      if (mounted.current && getSession()?.token === token)
        failed(
          error instanceof Error ? error.message : "Could not move the item.",
        );
    } finally {
      busy.current = false;
      if (mounted.current) {
        setMoving(false);
        if (getSession()?.token === token)
          void client.invalidateQueries({ queryKey: ["drive"] });
      }
    }
  }
  return {
    dragged,
    moving,
    sourceProps: (item: DriveItem) => ({
      draggable: enabled && !moving,
      onDragStart: (event: DragEvent) => {
        if (
          !enabled ||
          busy.current ||
          (event.target instanceof Element &&
            event.target.closest('input, button, [role="menuitem"]'))
        ) {
          event.preventDefault();
          return;
        }
        active.current = item;
        setDragged(item);
        event.dataTransfer.clearData();
        event.dataTransfer.setData(cloudItemDragType, item.id);
        event.dataTransfer.effectAllowed = "move";
      },
      onDragEnd: clear,
    }),
    targetProps: (destination: string | null) => ({
      "data-move-target": destination || "home",
      "data-move-active":
        hovered === (destination || "home") ? "true" : undefined,
      onDragEnter: (event: DragEvent) => {
        if (!ownDrag(event)) return;
        event.preventDefault();
        event.stopPropagation();
        setHovered(
          allowed(active.current!, destination) ? destination || "home" : null,
        );
      },
      onDragOver: (event: DragEvent) => {
        if (!ownDrag(event)) return;
        event.preventDefault();
        event.stopPropagation();
        const valid = allowed(active.current!, destination);
        event.dataTransfer.dropEffect = valid ? "move" : "none";
        setHovered(valid ? destination || "home" : null);
      },
      onDragLeave: (event: DragEvent) => {
        if (!ownDrag(event)) return;
        event.stopPropagation();
        if (!event.currentTarget.contains(event.relatedTarget as Node | null))
          setHovered(null);
      },
      onDrop: (event: DragEvent) => {
        if (!ownDrag(event)) return;
        event.preventDefault();
        event.stopPropagation();
        const item = active.current!;
        clear();
        void move(item, destination);
      },
    }),
  };
}
