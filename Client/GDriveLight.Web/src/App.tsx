import { Cloud, Home } from 'lucide-react';
import styles from './App.module.css';
export default function App() {
  return <div className={styles.shell}><aside className={styles.sidebar}><a className={styles.brand} href="/"><Cloud size={32} /><span>GDriveLight</span></a><nav aria-label="Main navigation"><a className={styles.navItem} href="/"><Home size={20} />Home</a></nav></aside><main className={styles.surface}><h1>Home</h1><div className={styles.empty}><Cloud size={48} /><h2>Your cloud, in one place</h2><p>Keep your files organized and close at hand.</p></div></main></div>;
}
