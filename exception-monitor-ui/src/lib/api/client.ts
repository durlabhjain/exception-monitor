import { get } from 'svelte/store';
import { settings } from '$lib/stores/settings';
import { authToken } from '$lib/stores/auth';

export class ApiError extends Error {
	constructor(
		public status: number,
		message: string
	) {
		super(message);
		this.name = 'ApiError';
	}
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
	const { apiBaseUrl, adminApiKey } = get(settings);
	const token = get(authToken);

	const authHeader = token
		? { Authorization: `Bearer ${token}` }
		: adminApiKey
			? { 'X-Admin-Api-Key': adminApiKey }
			: {};

	const response = await fetch(`${apiBaseUrl}${path}`, {
		...init,
		headers: {
			'Content-Type': 'application/json',
			...authHeader,
			...(init?.headers ?? {})
		}
	});

	if (response.status === 401) {
		authToken.set(null);
		if (typeof window !== 'undefined') window.location.href = '/login';
		throw new ApiError(401, 'Session expired. Please log in again.');
	}

	if (!response.ok) {
		let message: string;
		try {
			const ct = response.headers.get('content-type') ?? '';
			if (ct.includes('application/json')) {
				const json = await response.json();
				message = json.message ?? json.title ?? JSON.stringify(json);
			} else {
				message = await response.text();
			}
		} catch {
			message = `HTTP ${response.status} ${response.statusText}`;
		}
		throw new ApiError(response.status, message || `HTTP ${response.status}`);
	}

	if (response.status === 204) return undefined as T;

	const ct = response.headers.get('content-type') ?? '';
	if (!ct.includes('application/json')) return undefined as T;

	return response.json();
}
