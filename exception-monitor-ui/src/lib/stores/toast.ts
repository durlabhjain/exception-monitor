import { writable } from 'svelte/store';

export interface Toast {
	id: number;
	type: 'success' | 'error';
	message: string;
}

const { subscribe, update } = writable<Toast[]>([]);
let nextId = 0;

function add(type: Toast['type'], message: string, duration = type === 'success' ? 3000 : 5000) {
	const id = nextId++;
	update((t) => [...t, { id, type, message }]);
	setTimeout(() => update((t) => t.filter((x) => x.id !== id)), duration);
}

export const toasts = {
	subscribe,
	success: (message: string) => add('success', message),
	error: (message: string) => add('error', message)
};
