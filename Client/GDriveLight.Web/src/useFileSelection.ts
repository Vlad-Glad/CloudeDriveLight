import { useCallback, useEffect, useState } from "react";
import type { DriveItem } from "./drive-api";
export interface SelectionState {
  ids: string[];
  anchor: string | null;
}
export interface SelectionModifiers {
  shiftKey?: boolean;
  ctrlKey?: boolean;
  metaKey?: boolean;
}
export function selectItem(
  state: SelectionState,
  id: string,
  order: string[],
  modifiers: SelectionModifiers,
): SelectionState {
  const additive = modifiers.ctrlKey || modifiers.metaKey;
  if (modifiers.shiftKey) {
    const anchor =
      state.anchor && order.includes(state.anchor) ? state.anchor : id;
    const start = order.indexOf(anchor),
      end = order.indexOf(id);
    const range = order.slice(Math.min(start, end), Math.max(start, end) + 1);
    return {
      ids: additive ? Array.from(new Set([...state.ids, ...range])) : range,
      anchor,
    };
  }
  return {
    ids: additive
      ? state.ids.includes(id)
        ? state.ids.filter((value) => value !== id)
        : [...state.ids, id]
      : [id],
    anchor: id,
  };
}
export function useFileSelection(items: DriveItem[]) {
  const [state, setState] = useState<SelectionState>({ ids: [], anchor: null });
  const order = items.map((item) => item.id);
  const orderKey = JSON.stringify(order);
  useEffect(() => {
    const visible = new Set<string>(JSON.parse(orderKey));
    setState((previous) => {
      const ids = previous.ids.filter((id) => visible.has(id));
      const anchor =
        previous.anchor && visible.has(previous.anchor)
          ? previous.anchor
          : null;
      return ids.length === previous.ids.length && anchor === previous.anchor
        ? previous
        : { ids, anchor };
    });
  }, [orderKey]);
  const clear = useCallback(() => setState({ ids: [], anchor: null }), []);
  const replace = useCallback(
    (ids: string[], anchor: string | null = ids[0] || null) =>
      setState({ ids, anchor }),
    [],
  );
  return {
    ids: new Set(state.ids),
    anchor: state.anchor,
    items: items.filter((item) => state.ids.includes(item.id)),
    clear,
    replace,
    select: (id: string, modifiers: SelectionModifiers = {}) =>
      setState((previous) => selectItem(previous, id, order, modifiers)),
    selectAll: () => replace(order),
  };
}
