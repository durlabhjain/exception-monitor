import { apiFetch } from './client';
import type { Client } from '$lib/types';

export const getClients = () => apiFetch<Client[]>('/api/admin/clients');

export const createClient = (data: { name: string; slug?: string }) =>
	apiFetch<Client>('/api/admin/clients', {
		method: 'POST',
		body: JSON.stringify(data)
	});
