import { apiFetch } from './client';

export const setEnvironment = (data: {
	applicationId: string;
	name: string;
	notificationsEnabled: boolean;
}) =>
	apiFetch('/api/admin/environments', {
		method: 'POST',
		body: JSON.stringify(data)
	});
