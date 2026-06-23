import { apiFetch } from './client';
import type { User } from '$lib/types';

export const getUsers = () => apiFetch<User[]>('/api/admin/users');

export const createUser = (data: { email: string; displayName?: string }) =>
	apiFetch<User>('/api/admin/users', {
		method: 'POST',
		body: JSON.stringify(data)
	});

export const grantClientAccess = (data: {
	userId: string;
	clientId: string;
	role: string;
	allApplications: boolean;
}) =>
	apiFetch('/api/admin/users/client-access', {
		method: 'POST',
		body: JSON.stringify(data)
	});

export const grantApplicationAccess = (data: {
	userId: string;
	applicationId: string;
	role: string;
}) =>
	apiFetch('/api/admin/users/application-access', {
		method: 'POST',
		body: JSON.stringify(data)
	});
