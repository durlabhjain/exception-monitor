<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { getEvent } from '$lib/api/events';
	import Badge from '$lib/components/Badge.svelte';
	import StackTrace from '$lib/components/StackTrace.svelte';
	import { toasts } from '$lib/stores/toast';
	import { ArrowLeft, Globe, Tag, Database, Code } from '@lucide/svelte';
	import type { ExceptionEvent } from '$lib/types';

	let appId = $derived($page.params.id);
	let groupId = $derived($page.params.gid);
	let eventId = $derived($page.params.eid);

	let event = $state<ExceptionEvent | null>(null);
	let loading = $state(true);
	let showRaw = $state(false);

	onMount(async () => {
		try {
			const raw = await getEvent(eventId);
			event = {
				...raw,
				tags: typeof raw.tags === 'string' ? JSON.parse(raw.tags) : (raw.tags ?? {}),
				metadata: typeof raw.metadata === 'string' ? JSON.parse(raw.metadata) : (raw.metadata ?? {}),
				rawPayload: typeof raw.rawPayload === 'string' ? JSON.parse(raw.rawPayload) : raw.rawPayload
			};
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load event');
		} finally {
			loading = false;
		}
	});

	function fmt(val: unknown) {
		if (val === null || val === undefined) return '—';
		return String(val);
	}
</script>

<svelte:head><title>Event Detail — Exception Monitor</title></svelte:head>

<div class="p-6">
	<a href="/apps/{appId}/errors/{groupId}" class="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-4">
		<ArrowLeft size={14} /> Error Group
	</a>

	{#if loading}
		<div class="flex items-center gap-2 text-gray-500 text-sm">
			<div class="animate-spin h-4 w-4 border-2 border-indigo-600 border-t-transparent rounded-full"></div>
			Loading...
		</div>
	{:else if !event}
		<p class="text-gray-500">Event not found.</p>
	{:else}
		<div class="flex items-center gap-2 mb-4 flex-wrap">
			<Badge value={event.severity} />
			<span class="text-xs text-gray-400 font-mono">{event.id}</span>
		</div>

		<h1 class="text-xl font-bold text-gray-900 break-words mb-1">{event.exceptionType ?? 'Exception'}</h1>
		<p class="text-gray-500 text-sm mb-6 break-words">{event.message}</p>

		<div class="grid grid-cols-1 xl:grid-cols-2 gap-6">
			<!-- Left column -->
			<div class="space-y-6">
				<!-- Stack Trace -->
				<div class="bg-white rounded-xl border border-gray-200 p-5">
					<h3 class="font-semibold text-gray-800 mb-3 flex items-center gap-2"><Code size={16} /> Stack Trace</h3>
					<StackTrace value={event.stackTrace} />
				</div>

				<!-- Request Info -->
				{#if event.requestUrl || event.requestMethod}
					<div class="bg-white rounded-xl border border-gray-200 p-5">
						<h3 class="font-semibold text-gray-800 mb-3 flex items-center gap-2"><Globe size={16} /> Request</h3>
						<dl class="space-y-2 text-sm">
							{#if event.requestMethod || event.requestUrl}
								<div class="flex gap-2">
									{#if event.requestMethod}<span class="font-medium text-xs bg-gray-100 px-1.5 py-0.5 rounded shrink-0">{event.requestMethod}</span>{/if}
									<span class="text-gray-600 break-all">{event.requestUrl ?? ''}</span>
								</div>
							{/if}
							{#if event.requestRoute}<div class="flex justify-between"><dt class="text-gray-400">Route</dt><dd class="font-mono text-xs">{event.requestRoute}</dd></div>{/if}
							{#if event.requestStatusCode}<div class="flex justify-between"><dt class="text-gray-400">Status Code</dt><dd class="font-medium">{event.requestStatusCode}</dd></div>{/if}
							{#if event.requestReferrer}<div class="flex justify-between"><dt class="text-gray-400">Referrer</dt><dd class="text-xs break-all">{event.requestReferrer}</dd></div>{/if}
							{#if event.remoteIp}<div class="flex justify-between"><dt class="text-gray-400">IP</dt><dd class="font-mono text-xs">{event.remoteIp}</dd></div>{/if}
						</dl>
					</div>
				{/if}

				<!-- Tags -->
				{#if event.tags && Object.keys(event.tags).length > 0}
					<div class="bg-white rounded-xl border border-gray-200 p-5">
						<h3 class="font-semibold text-gray-800 mb-3 flex items-center gap-2"><Tag size={16} /> Tags</h3>
						<table class="w-full text-sm">
							<tbody class="divide-y divide-gray-50">
								{#each Object.entries(event.tags) as [key, value]}
									<tr>
										<td class="py-1.5 text-gray-500 font-medium w-1/3">{key}</td>
										<td class="py-1.5 text-gray-700 font-mono text-xs break-all">{value}</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>
				{/if}
			</div>

			<!-- Right column -->
			<div class="space-y-6">
				<!-- Event Details -->
				<div class="bg-white rounded-xl border border-gray-200 p-5">
					<h3 class="font-semibold text-gray-800 mb-3">Event Details</h3>
					<dl class="space-y-2.5 text-sm">
						<div class="flex justify-between"><dt class="text-gray-400">Environment</dt><dd class="font-medium">{event.environment}</dd></div>
						<div class="flex justify-between"><dt class="text-gray-400">Occurred At</dt><dd class="text-xs">{new Date(event.occurredAt).toLocaleString()}</dd></div>
						<div class="flex justify-between"><dt class="text-gray-400">Received At</dt><dd class="text-xs">{new Date(event.receivedAt).toLocaleString()}</dd></div>
						{#if event.source}<div class="flex justify-between"><dt class="text-gray-400">Source</dt><dd class="text-xs">{event.source}</dd></div>{/if}
						{#if event.release}<div class="flex justify-between"><dt class="text-gray-400">Release</dt><dd class="font-mono text-xs">{event.release}</dd></div>{/if}
						{#if event.correlationId}<div class="flex justify-between"><dt class="text-gray-400">Correlation ID</dt><dd class="font-mono text-xs break-all">{event.correlationId}</dd></div>{/if}
						{#if event.traceId}<div class="flex justify-between"><dt class="text-gray-400">Trace ID</dt><dd class="font-mono text-xs break-all">{event.traceId}</dd></div>{/if}
						{#if event.spanId}<div class="flex justify-between"><dt class="text-gray-400">Span ID</dt><dd class="font-mono text-xs">{event.spanId}</dd></div>{/if}
						{#if event.userHash}<div class="flex justify-between"><dt class="text-gray-400">User Hash</dt><dd class="font-mono text-xs">{event.userHash}</dd></div>{/if}
						{#if event.userAgent}<div class="flex justify-between"><dt class="text-gray-400">User Agent</dt><dd class="text-xs break-all">{event.userAgent}</dd></div>{/if}
					</dl>
				</div>

				<!-- Metadata -->
				{#if event.metadata && JSON.stringify(event.metadata) !== '{}'}
					<div class="bg-white rounded-xl border border-gray-200 p-5">
						<h3 class="font-semibold text-gray-800 mb-3 flex items-center gap-2"><Database size={16} /> Metadata</h3>
						<pre class="bg-gray-50 text-gray-700 text-xs p-3 rounded-lg overflow-x-auto max-h-64 font-mono">{JSON.stringify(event.metadata, null, 2)}</pre>
					</div>
				{/if}

				<!-- Raw Payload -->
				{#if event.rawPayload}
					<div class="bg-white rounded-xl border border-gray-200 p-5">
						<button
							onclick={() => (showRaw = !showRaw)}
							class="flex items-center justify-between w-full text-sm font-semibold text-gray-800"
						>
							Raw Payload
							<span class="text-gray-400 text-xs">{showRaw ? 'hide' : 'show'}</span>
						</button>
						{#if showRaw}
							<pre class="mt-3 bg-gray-50 text-gray-700 text-xs p-3 rounded-lg overflow-x-auto max-h-64 font-mono">{JSON.stringify(event.rawPayload, null, 2)}</pre>
						{/if}
					</div>
				{/if}
			</div>
		</div>
	{/if}
</div>
