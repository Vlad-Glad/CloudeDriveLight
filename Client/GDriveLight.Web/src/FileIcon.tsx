import {
  File,
  FileText,
  FileImage,
  FileVideo,
  Sheet,
  Folder,
} from "lucide-react";
export function FileIcon({
  name,
  folder = false,
}: {
  name: string;
  folder?: boolean;
}) {
  if (folder)
    return <Folder size={22} fill="var(--icon-folder)" color="var(--icon-folder)" strokeWidth={1} />;
  const extension = name.split(".").pop()?.toLowerCase();
  if (["png", "jpg", "jpeg", "gif", "svg", "webp"].includes(extension || ""))
    return <FileImage size={22} color="var(--icon-image)" />;
  if (["mp4", "mov", "webm", "avi"].includes(extension || ""))
    return <FileVideo size={22} color="var(--icon-video)" />;
  if (["xlsx", "xls", "csv"].includes(extension || ""))
    return <Sheet size={22} color="var(--icon-excel)" />;
  if (extension === "pdf") return <FileText size={22} color="var(--icon-pdf)" />;
  if (["doc", "docx"].includes(extension || ""))
    return <FileText size={22} color="var(--icon-word)" />;
  return <File size={22} color="var(--muted)" />;
}
