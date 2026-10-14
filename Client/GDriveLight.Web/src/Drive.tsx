import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronRight, RefreshCw, Search, X, Cloud, ArrowUp, ArrowDown, LoaderCircle, Plus, Upload, FolderPlus, Download, Pencil, FolderInput, Trash2, MoreVertical } from 'lucide-react';
import { Profile, Shell } from './Layout';
import { FileIcon } from './FileIcon';
import { folderUrl, formatSize, loadItems, pathQuery, sortItems, typesQuery, type SortKey } from './drive-api';
import styles from './Drive.module.css';
import * as Menu from '@radix-ui/react-dropdown-menu';
import { OperationDialog, type Operation } from './OperationDialog';
import { downloadItem } from './operations-api';
import { useUploads } from './useUploads';
import { UploadQueue } from './UploadQueue';
import type { DriveItem } from './drive-api';
export default function Drive() {
  const { folderId: routeId } = useParams();
  const folderId = routeId || null;
  const [params, setParams] = useSearchParams();
  const search = params.get('q')?.trim() || '';
  const type = params.get('type') || '';
  const [sort, setSort] = useState<SortKey>('name');
  const [descending, setDescending] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const client = useQueryClient();
  const [operation, setOperation] = useState<Operation | null>(null);
  const [notice, setNotice] = useState('');
  const [actionError, setActionError] = useState('');
  const [downloading, setDownloading] = useState(false);
  const downloadController = useRef<AbortController | null>(null);
  const input = useRef<HTMLInputElement>(null);
  const uploads = useUploads();
  useEffect(() => { setSelectedId(null); }, [folderId, search]);
  useEffect(() => () => downloadController.current?.abort(), []);
  async function download(item: DriveItem) {
    if (downloading) return;
    setActionError(''); setDownloading(true);
    downloadController.current = new AbortController();
    try { await downloadItem(item, downloadController.current.signal); }
    catch (error) { if (!(error instanceof Error && error.name === 'AbortError')) setActionError(error instanceof Error ? error.message : 'Download failed.'); }
    finally { setDownloading(false); }
  }
  const path = useQuery(pathQuery(folderId));
  const types = useQuery(typesQuery);
  const content = useQuery({ queryKey: ['drive', 'content', folderId, search], queryFn: ({ signal }) => loadItems(folderId, search, signal) });
  const items = sortItems(content.data || [], sort, descending, type);
  const selected = items.find(item => item.id === selectedId);
  const loading = content.isPending || path.isPending;
  const error = content.error || path.error;
  function submitSearch(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const value = String(new FormData(event.currentTarget).get('search')).trim(); setParams(previous => { if (value) previous.set('q', value); else previous.delete('q'); return previous; }); }
  function changeSort(key: SortKey) { if (sort === key) setDescending(!descending); else { setSort(key); setDescending(false); } }
  const sortHeader = (key: SortKey, text: string) => <button onClick={() => changeSort(key)}>{text}{sort === key && (descending ? <ArrowDown size={14} /> : <ArrowUp size={14} />)}</button>;
  const currentName = path.data?.at(-1)?.name || 'Home';
  const rowMenu = (item: DriveItem) => <Menu.Root><Menu.Trigger className="iconButton" aria-label={`Actions for ${item.name}`}><MoreVertical size={18} /></Menu.Trigger><Menu.Portal><Menu.Content className="menu" align="end">{item.kind === 'file' && <Menu.Item className="menuItem" disabled={downloading} onSelect={() => void download(item)}><Download size={16} />Download</Menu.Item>}<Menu.Item className="menuItem" onSelect={() => setOperation({ mode: 'rename', item })}><Pencil size={16} />Rename</Menu.Item><Menu.Item className="menuItem" onSelect={() => setOperation({ mode: 'move', item })}><FolderInput size={16} />Move</Menu.Item><Menu.Separator className="separator" /><Menu.Item className="menuItem" onSelect={() => setOperation({ mode: 'delete', item })}><Trash2 size={16} />Delete</Menu.Item></Menu.Content></Menu.Portal></Menu.Root>;
  const actions = <><Menu.Root><Menu.Trigger className="newButton" disabled={!!error || path.isPending}><Plus size={24} />New</Menu.Trigger><Menu.Portal><Menu.Content className="menu" sideOffset={8} align="start"><Menu.Item className="menuItem" onSelect={() => setOperation({ mode: 'create', parentId: folderId })}><FolderPlus size={18} />New folder</Menu.Item><Menu.Item className="menuItem" onSelect={() => input.current?.click()}><Upload size={18} />Upload files</Menu.Item></Menu.Content></Menu.Portal></Menu.Root><input ref={input} type="file" multiple hidden aria-label="Upload files" onChange={event => { uploads.enqueue(Array.from(event.target.files || []), folderId, currentName); event.target.value = ''; }} /></>;
  return <Shell actions={actions}><header className={styles.header}><nav aria-label="Folder path" className={styles.breadcrumb}><Link to="/">Home</Link>{path.data?.map(folder => <span key={folder.id}><ChevronRight size={18} /><Link to={folderUrl(folder.id)} aria-current={folder.id === folderId ? 'page' : undefined}>{folder.name}</Link></span>)}</nav><div className={styles.headerActions}><button className="iconButton" aria-label="Refresh" title="Refresh" onClick={() => void client.invalidateQueries({ queryKey: ['drive'] })}><RefreshCw size={20} className={content.isFetching ? 'spin' : ''} /></button><Profile /></div></header><form key={`${folderId}:${search}`} onSubmit={submitSearch} className={styles.search}><button aria-label="Search" className="iconButton"><Search size={20} /></button><input name="search" aria-label="Search files and folders" placeholder="Search files and folders" defaultValue={search} />{search && <button type="button" aria-label="Clear search" className="iconButton" onClick={() => setParams(previous => { previous.delete('q'); return previous; })}><X size={18} /></button>}</form><div className={styles.toolbar}><label className={styles.filter}>Type:<select aria-label="File type" value={type} onChange={event => setParams(previous => { if (event.target.value) previous.set('type', event.target.value); else previous.delete('type'); return previous; })}><option value="">All types</option>{types.data?.map(fileType => <option key={fileType.id} value={fileType.id}>{fileType.extension}</option>)}</select></label><span className={styles.count}>{search ? `Results for “${search}” across your cloud` : `${items.length} items`}</span>{selected && <div className="selectionActions" aria-label="Selected item actions">{selected.kind === 'file' && <button className="iconButton" title="Download" aria-label="Download selected file" disabled={downloading} onClick={() => void download(selected)}><Download size={18} /></button>}<button className="iconButton" title="Rename" aria-label="Rename selected item" onClick={() => setOperation({ mode: 'rename', item: selected })}><Pencil size={18} /></button><button className="iconButton" title="Move" aria-label="Move selected item" onClick={() => setOperation({ mode: 'move', item: selected })}><FolderInput size={18} /></button><button className="iconButton" title="Delete" aria-label="Delete selected item" onClick={() => setOperation({ mode: 'delete', item: selected })}><Trash2 size={18} /></button><button className="iconButton" aria-label="Clear selection" onClick={() => setSelectedId(null)}><X size={18} /></button></div>}</div>{notice && <div className="notice" role="status">{notice}<button className="iconButton" aria-label="Dismiss notification" onClick={() => setNotice('')}><X size={16} /></button></div>}{actionError && <div className="error" role="alert">{actionError}<button className="textButton" onClick={() => setActionError('')}>Dismiss</button></div>}{downloading && <div className="notice" role="status">Preparing download…<button className="textButton" onClick={() => downloadController.current?.abort()}>Cancel</button></div>}{types.error && <div className="error" role="alert">File type filter unavailable. <button className="textButton" onClick={() => void types.refetch()}>Retry</button></div>}{error ? <div className="error" role="alert">{error.message}<button className="textButton" onClick={() => void client.invalidateQueries({ queryKey: ['drive'] })}>Try again</button><Link to="/">Go to Home</Link></div> : loading ? <div className={styles.empty} role="status"><LoaderCircle className="spin" size={32} /><p>Loading your files…</p></div> : items.length === 0 ? <div className={styles.empty}><Cloud size={52} /><h1>{search || type ? 'No matching items' : 'This folder is empty'}</h1><p>{search || type ? 'Try a different search or file type.' : 'Choose New to create a folder or upload files.'}</p></div> : <div className={styles.tableScroll}><table className={styles.table} aria-label="Files and folders"><thead><tr><th className={styles.checkColumn}><span className="srOnly">Select</span></th><th aria-sort={sort === 'name' ? descending ? 'descending' : 'ascending' : 'none'}>{sortHeader('name', 'Name')}</th><th className={styles.ownerColumn}>Owner</th><th className={styles.dateColumn} aria-sort={sort === 'modified' ? descending ? 'descending' : 'ascending' : 'none'}>{sortHeader('modified', 'Date modified')}</th><th aria-sort={sort === 'size' ? descending ? 'descending' : 'ascending' : 'none'}>{sortHeader('size', 'File size')}</th><th className="rowActions"><span className="srOnly">Actions</span></th></tr></thead><tbody>{items.map(item => <tr key={item.id} className={selected?.id === item.id ? styles.selected : ''} onClick={() => setSelectedId(item.id)}><td><input type="checkbox" aria-label={`Select ${item.name}`} checked={selected?.id === item.id} onClick={event => event.stopPropagation()} onChange={() => setSelectedId(selected?.id === item.id ? null : item.id)} /></td><td><div className={styles.name}><FileIcon name={item.name} folder={item.kind === 'folder'} />{item.kind === 'folder' ? <Link to={folderUrl(item.id)} title={item.name}>{item.name}</Link> : <button className="downloadName" title={`Download ${item.name}`} onClick={() => void download(item)} disabled={downloading}>{item.name}</button>}</div></td><td className={styles.ownerColumn}>me</td><td className={styles.dateColumn}>{item.kind === 'file' ? new Date(item.editedAtUtc || item.uploadedAtUtc).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—'}</td><td>{item.kind === 'file' ? formatSize(item.sizeBytes) : '—'}</td><td className="rowActions">{rowMenu(item)}</td></tr>)}</tbody></table></div>}<UploadQueue {...uploads} />{operation && <OperationDialog operation={operation} close={() => setOperation(null)} completed={message => { setNotice(message); setSelectedId(null); }} />}</Shell>;
}
