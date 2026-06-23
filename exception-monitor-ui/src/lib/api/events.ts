import { apiFetch } from './client';
import type { ErrorGroup, ErrorGroupDetail, ExceptionEvent } from '$lib/types';

export const getErrorGroups = (
	params: {
		applicationId?: string;
		clientId?: string;
		environment?: string;
		status?: string;
		q?: string;
	} = {}
) => {
	const qs = new URLSearchParams();
	Object.entries(params).forEach(([k, v]) => v && qs.set(k, v));
	const query = qs.toString();
	return apiFetch<ErrorGroup[]>(`/api/error-groups${query ? '?' + query : ''}`);
};

export const getErrorGroup = (id: string) =>
	apiFetch<ErrorGroupDetail>(`/api/error-groups/${id}`);

export const updateGroupStatus = (id: string, status: string) =>
	apiFetch<void>(`/api/error-groups/${id}/status?status=${encodeURIComponent(status)}`, {
		method: 'POST'
	});

export const getEvents = (
	params: {
		applicationId?: string;
		environment?: string;
		severity?: string;
		q?: string;
	} = {}
) => {
	const qs = new URLSearchParams();
	Object.entries(params).forEach(([k, v]) => v && qs.set(k, v));
	const query = qs.toString();
	return apiFetch<ExceptionEvent[]>(`/api/events${query ? '?' + query : ''}`);
};

export const getEvent = (id: string) => apiFetch<ExceptionEvent>(`/api/events/${id}`);
