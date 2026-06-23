export interface Client {
	id: string;
	name: string;
	slug: string;
	isActive: boolean;
	createdAt: string;
}

export interface Application {
	id: string;
	clientId: string;
	clientName: string;
	name: string;
	slug: string;
	defaultEnvironment: string;
	defaultRetentionDays: number;
	isActive: boolean;
	createdAt: string;
}

export interface ApiKey {
	id: string;
	name: string;
	keyPrefix: string;
	rateLimitPerMinute: number;
	retentionDays: number;
	isActive: boolean;
	lastUsedAt: string | null;
	createdAt: string;
	revokedAt: string | null;
}

export interface CreatedApiKey extends ApiKey {
	plaintextKey: string;
}

export interface ErrorGroup {
	id: string;
	clientId: string;
	applicationId: string;
	applicationName: string;
	environment: string;
	fingerprint: string;
	exceptionType: string | null;
	message: string;
	severity: string;
	status: string;
	firstSeenAt: string;
	lastSeenAt: string;
	totalCount: number;
}

export interface ExceptionEvent {
	id: string;
	groupId: string;
	clientId: string;
	applicationId: string;
	environment: string;
	severity: string;
	exceptionType: string | null;
	message: string;
	stackTrace: string;
	fingerprint: string;
	occurredAt: string;
	receivedAt: string;
	source: string | null;
	release: string | null;
	correlationId: string | null;
	traceId: string | null;
	spanId: string | null;
	userHash: string | null;
	requestMethod: string | null;
	requestUrl: string | null;
	requestRoute: string | null;
	requestReferrer: string | null;
	requestStatusCode: number | null;
	remoteIp: string | null;
	userAgent: string | null;
	tags: Record<string, string> | string;
	metadata: unknown;
	rawPayload: unknown;
}

export interface ErrorGroupDetail extends ErrorGroup {
	events: ExceptionEvent[];
}

export interface User {
	id: string;
	email: string;
	displayName: string | null;
	isActive: boolean;
}

export interface NotificationRecipient {
	id: string;
	applicationId: string;
	type: 'Email' | 'Webhook';
	name: string;
	email: string | null;
	webhookUrl: string | null;
	isActive: boolean;
	createdAt: string;
}

export interface NotificationRule {
	id: string;
	applicationId: string;
	name: string;
	environment: string | null;
	eventType: string;
	severityMinimum: string;
	cooldownMinutes: number;
	thresholdCount: number | null;
	thresholdWindowMinutes: number | null;
	digestIntervalMinutes: number | null;
	isActive: boolean;
	createdAt: string;
}
