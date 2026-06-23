import { apiFetch } from './client';
import type { NotificationRecipient, NotificationRule } from '$lib/types';

export const getRecipients = (applicationId: string) =>
	apiFetch<NotificationRecipient[]>(`/api/admin/applications/${applicationId}/recipients`);

export const updateRecipient = (id: string, data: { name: string; email?: string; webhookUrl?: string; webhookSecret?: string }) =>
	apiFetch<NotificationRecipient>(`/api/admin/recipients/${id}`, {
		method: 'PUT',
		body: JSON.stringify(data)
	});

export const deleteRecipient = (id: string) =>
	apiFetch<void>(`/api/admin/recipients/${id}`, { method: 'DELETE' });

export const getNotificationRules = (applicationId: string) =>
	apiFetch<NotificationRule[]>(`/api/admin/applications/${applicationId}/notification-rules`);

export const updateNotificationRule = (id: string, data: { environment?: string; name: string; eventType: string; severityMinimum: string; thresholdCount?: number; thresholdWindowMinutes?: number; cooldownMinutes: number; digestIntervalMinutes?: number }) =>
	apiFetch<NotificationRule>(`/api/admin/notification-rules/${id}`, {
		method: 'PUT',
		body: JSON.stringify(data)
	});

export const deleteNotificationRule = (id: string) =>
	apiFetch<void>(`/api/admin/notification-rules/${id}`, { method: 'DELETE' });

export const addRecipient = (data: {
	applicationId: string;
	type: 'Email' | 'Webhook';
	name: string;
	email?: string;
	webhookUrl?: string;
	webhookSecret?: string;
}) =>
	apiFetch('/api/admin/recipients', {
		method: 'POST',
		body: JSON.stringify(data)
	});

export const createNotificationRule = (data: {
	applicationId: string;
	environment?: string;
	name: string;
	eventType: string;
	severityMinimum: string;
	thresholdCount?: number;
	thresholdWindowMinutes?: number;
	cooldownMinutes: number;
	digestIntervalMinutes?: number;
}) =>
	apiFetch('/api/admin/notification-rules', {
		method: 'POST',
		body: JSON.stringify(data)
	});
