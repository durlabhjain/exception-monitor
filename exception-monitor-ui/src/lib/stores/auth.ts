import { writable, derived } from 'svelte/store';
import { browser } from '$app/environment';

interface AuthUser {
	id: string;
	email: string;
	displayName: string;
	role: string;
}

function parseJwt(token: string): AuthUser | null {
	try {
		const payload = JSON.parse(atob(token.split('.')[1]));
		return {
			id: payload.sub,
			email: payload.email,
			displayName: payload.displayName ?? payload.email,
			role: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload.role ?? 'Viewer'
		};
	} catch {
		return null;
	}
}

function isExpired(token: string): boolean {
	try {
		const payload = JSON.parse(atob(token.split('.')[1]));
		return payload.exp * 1000 < Date.now();
	} catch {
		return true;
	}
}

const stored = browser ? localStorage.getItem('auth_token') : null;
const validStored = stored && !isExpired(stored) ? stored : null;

export const authToken = writable<string | null>(validStored);

export const authUser = derived(authToken, ($token) => ($token ? parseJwt($token) : null));

export const isAuthenticated = derived(authToken, ($token) => !!$token && !isExpired($token));

authToken.subscribe((token) => {
	if (!browser) return;
	if (token) localStorage.setItem('auth_token', token);
	else localStorage.removeItem('auth_token');
});

export function logout() {
	authToken.set(null);
}
