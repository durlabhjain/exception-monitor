import { apiFetch } from './client';
import type { Application } from '$lib/types';

export const getApplications = (clientId?: string) => {
	const qs = clientId ? `?clientId=${clientId}` : '';
	return apiFetch<Application[]>(`/api/admin/applications${qs}`);
};

export const createApplication = (data: {
	clientId: string;
	name: string;
	slug?: string;
	defaultEnvironment?: string;
	defaultRetentionDays?: number;
}) =>
	apiFetch<Application>('/api/admin/applications', {
		method: 'POST',
		body: JSON.stringify(data)
	});
