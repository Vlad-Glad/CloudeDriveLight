import { expect, it } from "vitest";
import { selectItem, type SelectionState } from "./useFileSelection";
const order = ["a", "b", "c", "d", "e"];
const single: SelectionState = { ids: ["b"], anchor: "b" };
it("Shift extends and shrinks a visible range around the same anchor", () => {
  const expanded = selectItem(single, "e", order, { shiftKey: true });
  expect(expanded.ids).toEqual(["b", "c", "d", "e"]);
  expect(selectItem(expanded, "c", order, { shiftKey: true })).toEqual({
    ids: ["b", "c"],
    anchor: "b",
  });
});
it("Ctrl toggles individual items and Ctrl+Shift adds a range", () => {
  const added = selectItem(single, "d", order, { ctrlKey: true });
  expect(added.ids).toEqual(["b", "d"]);
  expect(selectItem(added, "d", order, { ctrlKey: true }).ids).toEqual(["b"]);
  expect(
    selectItem(added, "e", order, { ctrlKey: true, shiftKey: true }).ids,
  ).toEqual(["b", "d", "e"]);
});
it("ranges follow the current sort and omit filtered-out items", () => {
  expect(
    selectItem(single, "d", ["e", "d", "b", "a"], { shiftKey: true }).ids,
  ).toEqual(["d", "b"]);
  expect(selectItem(single, "d", ["a", "c", "d"], { shiftKey: true })).toEqual({
    ids: ["d"],
    anchor: "d",
  });
});
it("a normal click replaces a group selection", () => {
  expect(
    selectItem({ ids: ["a", "b", "c"], anchor: "a" }, "e", order, {}),
  ).toEqual({ ids: ["e"], anchor: "e" });
});
