import { useEffect, useRef, useState, type DragEvent } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { pathQuery, type DriveItem } from "./drive-api";
import { moveItems } from "./operations-api";
import { parentOf, batchError } from "./batch";
import { getSession } from "./session";
export const cloudItemDragType = "application/x-gdrivelight-item";
export function useCloudMove(
  enabled: boolean,
  completed: (items: DriveItem[]) => void,
  failed: (message: string, remaining: DriveItem[]) => void,
) {
  const [moving, setMoving] = useState(false);
  const [hovered, setHovered] = useState<string | null>(null);
  const active = useRef<DriveItem[] | null>(null);
  const busy = useRef(false),
    mounted = useRef(false);
  const client = useQueryClient();
  const clear = () => {
    active.current = null;
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
  const allowed = (items: DriveItem[], destination: string | null) =>
    enabled &&
    !busy.current &&
    items.some((item) => parentOf(item) !== destination) &&
    !items.some((item) => item.kind === "folder" && item.id === destination);
  async function move(items: DriveItem[], destination: string | null) {
    if (!allowed(items, destination)) return;
    const token = getSession()?.token;
    if (!token) return;
    busy.current = true;
    setMoving(true);
    try {
      const result = await moveItems(items, destination, (id) =>
        client.fetchQuery({ ...pathQuery(id), staleTime: 0 }),
      );
      if (!mounted.current || getSession()?.token !== token) return;
      if (result.failed.length)
        failed(
          batchError(result),
          result.failed.map((failure) => failure.item),
        );
      else completed(result.succeeded);
    } catch (error) {
      if (mounted.current && getSession()?.token === token)
        failed(
          error instanceof Error ? error.message : "Could not move the items.",
          items,
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
    moving,
    sourceProps: (
      item: DriveItem,
      selection: DriveItem[],
      selectSource: () => void,
    ) => ({
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
        const items = selection.some((selected) => selected.id === item.id)
          ? [...selection]
          : [item];
        if (
          items.length === 1 &&
          !selection.some((selected) => selected.id === item.id)
        )
          selectSource();
        active.current = items;
        event.dataTransfer.clearData();
        event.dataTransfer.setData(
          cloudItemDragType,
          JSON.stringify(items.map((item) => item.id)),
        );
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
        const items = active.current!;
        clear();
        void move(items, destination);
      },
    }),
  };
}
