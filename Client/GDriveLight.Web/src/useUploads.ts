import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { uploadFile } from './operations-api';
import { getSession } from './session';
export interface UploadJob { id: string; file: File; folderId: string | null; folderName: string; status: 'queued' | 'uploading' | 'done' | 'error'; error?: string }
export function useUploads() {
  const [jobs, setJobs] = useState<UploadJob[]>([]);
  const active = useRef<AbortController | null>(null);
  const client = useQueryClient();
  useEffect(() => () => active.current?.abort(), []);
  useEffect(() => {
    if (active.current) return;
    const job = jobs.find(job => job.status === 'queued');
    if (!job) return;
    const controller = new AbortController(); active.current = controller;
    const token = getSession()?.token;
    setJobs(current => current.map(entry => entry.id === job.id ? { ...entry, status: 'uploading' } : entry));
    uploadFile(job.file, job.folderId, controller.signal).then(() => {
      if (getSession()?.token === token) void client.invalidateQueries({ queryKey: ['drive'] });
      setJobs(current => current.map(entry => entry.id === job.id ? { ...entry, status: 'done' } : entry));
    }).catch((error: unknown) => {
      setJobs(current => current.map(entry => entry.id === job.id ? { ...entry, status: 'error', error: error instanceof Error ? error.message : 'Upload failed.' } : entry));
    }).finally(() => { active.current = null; setJobs(current => [...current]); });
  }, [jobs, client]);
  return { jobs, enqueue: (files: File[], folderId: string | null, folderName: string) => setJobs(current => [...current, ...files.map(file => ({ id: crypto.randomUUID(), file, folderId, folderName, status: 'queued' as const }))]), retry: (id: string) => setJobs(current => current.map(job => job.id === id ? { ...job, status: 'queued', error: undefined } : job)), dismiss: () => setJobs(current => current.filter(job => job.status === 'queued' || job.status === 'uploading')) };
}
