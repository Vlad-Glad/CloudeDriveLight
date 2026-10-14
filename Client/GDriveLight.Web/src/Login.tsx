import { useState, type FormEvent } from 'react';
import { Cloud, ArrowRight, LoaderCircle } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import { useLocation, useNavigate } from 'react-router-dom';
import { authenticate } from './api';
import { setSession } from './session';
import styles from './Auth.module.css';
export default function Login() {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(''); setPending(true);
    const form = new FormData(event.currentTarget);
    try {
      const result = await authenticate(mode, { email: String(form.get('email')).trim(), password: String(form.get('password')), ...(mode === 'register' ? { firstName: String(form.get('firstName')).trim() } : {}) });
      queryClient.clear(); setSession(result.token);
      const destination = location.state?.from;
      navigate(typeof destination === 'string' && destination.startsWith('/') && !destination.startsWith('//') ? destination : '/', { replace: true });
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not sign in.'); }
    finally { setPending(false); }
  }
  return <main className={styles.page}><div className={styles.card}><div className={styles.intro}><div className={styles.brand}><Cloud size={32} />GDriveLight</div><h1>{mode === 'login' ? 'Welcome to GDriveLight' : 'Create your account'}</h1><p>Your files. Your space.<br />Everything you need, wherever you are.</p></div><form onSubmit={submit} className={styles.form}><h2>{mode === 'login' ? 'Sign in' : 'Get started'}</h2><label>Email address<input name="email" type="email" autoComplete="username" required disabled={pending} /></label>{mode === 'register' && <label>First name<input name="firstName" autoComplete="given-name" required maxLength={100} disabled={pending} /></label>}<label>Password<input name="password" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'} required disabled={pending} /></label>{error && <div className="error" role="alert">{error}</div>}<button className="primary" disabled={pending}>{pending ? <LoaderCircle size={18} className="spin" /> : <ArrowRight size={18} />}{pending ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}</button><button className="textButton" type="button" disabled={pending} onClick={() => { setMode(mode === 'login' ? 'register' : 'login'); setError(''); }}>{mode === 'login' ? 'Create an account' : 'Already have an account? Sign in'}</button></form></div></main>;
}
