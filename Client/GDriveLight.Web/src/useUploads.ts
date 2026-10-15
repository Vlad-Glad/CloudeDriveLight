import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { uploadWithProgress } from "./upload-transport";
import { getSession } from "./session";
import { typesQuery } from "./drive-api";
import { validateUploadType } from "./upload-validation";

export interface UploadJob {
  id: string;
  file: File;
  folderId: string | null;
  folderName: string;
  status: "queued" | "uploading" | "done" | "error" | "cancelled" | "rejected";
  progress: number;
  error?: string;
}
export function useUploads() {
  const [jobs, setJobs] = useState<UploadJob[]>([]);
  const active = useRef<{ id: string; controller: AbortController } | null>(
    null,
  );
  const mounted = useRef(false);
  const client = useQueryClient();
  const types = useQuery(typesQuery);
  const busy = jobs.some(
    (job) => job.status === "queued" || job.status === "uploading",
  );
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      active.current?.controller.abort();
    };
  }, []);
  useEffect(() => {
    if (!busy) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [busy]);
  useEffect(() => {
    if (active.current || !getSession()) return;
    const job = jobs.find((job) => job.status === "queued");
    if (!job) return;
    if (types.isPending || types.isFetching) return;
    const validationError =
      types.isError || !types.data
        ? "Could not verify supported file types. Retry when the file type catalog is available."
        : validateUploadType(job.file.name, types.data);
    if (validationError) {
      setJobs((current) =>
        current.map((entry) =>
          entry.id === job.id
            ? {
                ...entry,
                status: types.isError || !types.data ? "error" : "rejected",
                error: validationError,
              }
            : entry,
        ),
      );
      return;
    }
    const controller = new AbortController();
    active.current = { id: job.id, controller };
    const token = getSession()?.token;
    const update = (patch: Partial<UploadJob>) => {
      if (mounted.current)
        setJobs((current) =>
          current.map((entry) =>
            entry.id === job.id ? { ...entry, ...patch } : entry,
          ),
        );
    };
    update({ status: "uploading", progress: 0 });
    uploadWithProgress(job.file, job.folderId, controller.signal, (progress) =>
      update({ progress }),
    )
      .then(() => update({ status: "done", progress: 100 }))
      .catch((error: unknown) =>
        update({
          status:
            error instanceof Error && error.name === "AbortError"
              ? "cancelled"
              : "error",
          error:
            error instanceof Error && error.name === "AbortError"
              ? "Cancelled locally. Check the destination folder before retrying."
              : error instanceof Error
                ? error.message
                : "Upload failed.",
        }),
      )
      .finally(() => {
        active.current = null;
        if (!mounted.current) return;
        if (getSession()?.token === token)
          void client.invalidateQueries({ queryKey: ["drive"] });
        setJobs((current) => [...current]);
      });
  }, [
    jobs,
    client,
    types.data,
    types.isError,
    types.isPending,
    types.isFetching,
  ]);
  return {
    jobs,
    enqueue: (files: File[], folderId: string | null, folderName: string) =>
      setJobs((current) => [
        ...current,
        ...files.map((file) => {
          const error =
            types.data && !types.isError && !types.isFetching
              ? validateUploadType(file.name, types.data)
              : null;
          return {
            id: crypto.randomUUID(),
            file,
            folderId,
            folderName,
            status: error ? ("rejected" as const) : ("queued" as const),
            error: error || undefined,
            progress: 0,
          };
        }),
      ]),
    cancel: (id: string) => {
      if (active.current?.id === id) active.current.controller.abort();
      else
        setJobs((current) =>
          current.map((job) =>
            job.id === id && job.status === "queued"
              ? { ...job, status: "cancelled", error: undefined }
              : job,
          ),
        );
    },
    retry: (id: string) => {
      if (types.isError) void types.refetch();
      setJobs((current) =>
        current.map((job) =>
          job.id === id &&
          (job.status === "error" || job.status === "cancelled")
            ? { ...job, status: "queued", progress: 0, error: undefined }
            : job,
        ),
      );
    },
    dismiss: () =>
      setJobs((current) =>
        current.filter(
          (job) => job.status === "queued" || job.status === "uploading",
        ),
      ),
  };
}
