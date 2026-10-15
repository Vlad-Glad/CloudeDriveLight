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
    return <Folder size={22} fill="#5f6368" color="#5f6368" strokeWidth={1} />;
  const extension = name.split(".").pop()?.toLowerCase();
  if (["png", "jpg", "jpeg", "gif", "svg", "webp"].includes(extension || ""))
    return <FileImage size={22} color="#a142f4" />;
  if (["mp4", "mov", "webm", "avi"].includes(extension || ""))
    return <FileVideo size={22} color="#ea4335" />;
  if (["xlsx", "xls", "csv"].includes(extension || ""))
    return <Sheet size={22} color="#107c41" />;
  if (extension === "pdf") return <FileText size={22} color="#ea4335" />;
  if (["doc", "docx"].includes(extension || ""))
    return <FileText size={22} color="#185abd" />;
  return <File size={22} color="#5f6368" />;
}
