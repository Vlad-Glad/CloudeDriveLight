import type { FileType } from "./drive-api";

// Match the API's last-extension lookup, including names without an extension.
export function validateUploadType(
  name: string,
  types: FileType[],
): string | null {
  const dot = name.lastIndexOf(".");
  const extension =
    dot >= 0 && dot < name.length - 1 ? name.slice(dot).toLowerCase() : "";
  if (types.some((type) => type.extension.toLowerCase() === extension))
    return null;
  return extension
    ? `File type '${extension}' is not supported.`
    : "Files without an extension are not supported.";
}
