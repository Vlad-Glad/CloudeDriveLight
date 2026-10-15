import { useEffect, useState } from "react";
import * as Menu from "@radix-ui/react-dropdown-menu";
import { Check, Columns3 } from "lucide-react";

const storageKey = "gdrivelight.columns.v2";
const columns = [
  { key: "created", label: "Date created" },
  { key: "modified", label: "Date modified" },
  { key: "size", label: "File size" },
] as const;
export type VisibleColumns = Record<(typeof columns)[number]["key"], boolean>;
const allVisible: VisibleColumns = { created: true, modified: true, size: true };
const allHidden: VisibleColumns = { created: false, modified: false, size: false };

function restoreColumns(): VisibleColumns {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey) || "null");
    return Object.fromEntries(
      columns.map(({ key }) => [
        key,
        typeof saved?.[key] === "boolean" ? saved[key] : true,
      ]),
    ) as VisibleColumns;
  } catch {
    return allVisible;
  }
}

export function useVisibleColumns() {
  const [visibleColumns, setVisibleColumns] = useState(restoreColumns);
  useEffect(() => {
    try {
      localStorage.setItem(storageKey, JSON.stringify(visibleColumns));
    } catch {
      // Keep controls usable when storage is unavailable.
    }
  }, [visibleColumns]);
  return [visibleColumns, setVisibleColumns] as const;
}

export function ColumnsMenu({
  visible,
  onChange,
  className,
}: {
  visible: VisibleColumns;
  onChange: (next: VisibleColumns) => void;
  className?: string;
}) {
  return (
    <Menu.Root>
      <Menu.Trigger className={className} aria-label="Show or hide columns">
        <Columns3 size={16} aria-hidden="true" />
        Columns
      </Menu.Trigger>
      <Menu.Portal>
        <Menu.Content
          className="menu"
          align="end"
          sideOffset={6}
          aria-label="Columns"
        >
          {columns.map(({ key, label }) => (
            <Menu.CheckboxItem
              key={key}
              className="menuItem"
              checked={visible[key]}
              onCheckedChange={(checked) =>
                onChange({ ...visible, [key]: checked === true })
              }
              onSelect={(event) => event.preventDefault()}
            >
              <span style={{ width: 16, height: 16 }} aria-hidden="true">
                <Menu.ItemIndicator>
                  <Check size={16} />
                </Menu.ItemIndicator>
              </span>
              {label}
            </Menu.CheckboxItem>
          ))}
          <Menu.Separator className="separator" />
          <Menu.Item className="menuItem" onSelect={() => onChange(allVisible)}>
            Show all columns
          </Menu.Item>
          <Menu.Item className="menuItem" onSelect={() => onChange(allHidden)}>
            Hide all columns
          </Menu.Item>
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}
