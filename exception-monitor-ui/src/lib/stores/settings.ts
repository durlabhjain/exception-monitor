import { writable } from 'svelte/store';
import { browser } from '$app/environment';

interface Settings {
	adminApiKey: string;
	apiBaseUrl: string;
}

const STORAGE_KEY = 'exception_monitor_settings';

const defaults: Settings = {
	adminApiKey: 'dev-admin-key',
	apiBaseUrl: import.meta.env.VITE_BACKEND_URL || 'http://localhost:5180'
};

function createSettings() {
	let initial = defaults;
	if (browser) {
		try {
			const stored = localStorage.getItem(STORAGE_KEY);
			if (stored) initial = { ...defaults, ...JSON.parse(stored) };
		} catch {
			// ignore
		}
	}

	const { subscribe, set, update } = writable<Settings>(initial);

	return {
		subscribe,
		set(value: Settings) {
			if (browser) localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
			set(value);
		},
		update(fn: (s: Settings) => Settings) {
			update((s) => {
				const next = fn(s);
				if (browser) localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
				return next;
			});
		}
	};
}

export const settings = createSettings();
