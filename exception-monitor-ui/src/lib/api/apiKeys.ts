import { apiFetch } from './client';
import type { ApiKey, CreatedApiKey } from '$lib/types';

export const getApiKeys = (applicationId: string) =>
	apiFetch<ApiKey[]>(`/api/admin/applications/${applicationId}/api-keys`);

export const createApiKey = (data: {
	applicationId: string;
	name: string;
	retentionDays?: number;
	rateLimitPerMinute?: number;
	allowAllIps: boolean;
	ipAllowlist?: string[];
}) =>
	apiFetch<CreatedApiKey>('/api/admin/api-keys', {
		method: 'POST',
		body: JSON.stringify(data)
	});

export const revokeApiKey = (id: string) =>
	apiFetch<void>(`/api/admin/api-keys/${id}/revoke`, { method: 'POST' });
