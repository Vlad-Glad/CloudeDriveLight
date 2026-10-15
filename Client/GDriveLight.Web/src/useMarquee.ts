import { useRef, useState, type PointerEvent } from "react";
export function useMarquee(
  selected: Set<string>,
  replace: (ids: string[]) => void,
) {
  const [rectangle, setRectangle] = useState<{
    left: number;
    top: number;
    width: number;
    height: number;
  } | null>(null);
  const active = useRef<{
    x: number;
    y: number;
    base: string[];
    moved: boolean;
  } | null>(null);
  function update(event: PointerEvent<HTMLDivElement>) {
    const drag = active.current;
    if (!drag) return;
    if (
      !drag.moved &&
      Math.hypot(event.clientX - drag.x, event.clientY - drag.y) < 4
    )
      return;
    drag.moved = true;
    const bounds = event.currentTarget.getBoundingClientRect();
    const left = Math.min(drag.x, event.clientX),
      top = Math.min(drag.y, event.clientY);
    const right = Math.max(drag.x, event.clientX),
      bottom = Math.max(drag.y, event.clientY);
    setRectangle({
      left: left - bounds.left + event.currentTarget.scrollLeft,
      top: top - bounds.top + event.currentTarget.scrollTop,
      width: right - left,
      height: bottom - top,
    });
    const found = Array.from(
      event.currentTarget.querySelectorAll<HTMLElement>("[data-item-id]"),
    )
      .filter((row) => {
        const rect = row.getBoundingClientRect();
        return (
          rect.right >= left &&
          rect.left <= right &&
          rect.bottom >= top &&
          rect.top <= bottom
        );
      })
      .map((row) => row.dataset.itemId!);
    replace(Array.from(new Set([...drag.base, ...found])));
  }
  return {
    rectangle,
    handlers: {
      onPointerDown: (event: PointerEvent<HTMLDivElement>) => {
        if (
          event.button !== 0 ||
          event.pointerType !== "mouse" ||
          event.target !== event.currentTarget
        )
          return;
        event.preventDefault();
        event.currentTarget.focus({ preventScroll: true });
        active.current = {
          x: event.clientX,
          y: event.clientY,
          base: event.ctrlKey || event.metaKey ? [...selected] : [],
          moved: false,
        };
        event.currentTarget.setPointerCapture(event.pointerId);
      },
      onPointerMove: update,
      onPointerUp: (event: PointerEvent<HTMLDivElement>) => {
        if (!active.current) return;
        update(event);
        if (!active.current.moved) replace(active.current.base);
        active.current = null;
        setRectangle(null);
        event.currentTarget.releasePointerCapture(event.pointerId);
      },
      onPointerCancel: () => {
        active.current = null;
        setRectangle(null);
      },
      onLostPointerCapture: () => {
        active.current = null;
        setRectangle(null);
      },
    },
  };
}
